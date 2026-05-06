// File: AlarmEngine.cs  Module: Alarm Engine  Author: IndustrialDAQ Team
using System.Collections.Concurrent;
using IndustrialDAQ.Core.Models;
using IndustrialDAQ.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IndustrialDAQ.Alarm;

/// <summary>
/// 报警引擎 — 订阅实时数据库变更流，根据报警规则对测点值进行阈值判断，
/// 管理报警生命周期（触发→确认→清除），通过 <see cref="AlarmChannel"/> 发布报警事件。
/// 支持回滞防抖和冷却时间，防止报警风暴。
/// 作为 <see cref="IHostedService"/> 运行。
/// </summary>
public sealed class AlarmEngine : IHostedService
{
    private readonly RealTimeStore _store;
    private readonly AlarmChannel _alarmChannel;
    private readonly ILogger<AlarmEngine> _logger;

    /// <summary>所有报警规则（线程安全字典）。</summary>
    private readonly ConcurrentDictionary<string, AlarmRule> _rules = new();

    /// <summary>规则 → 当前活跃的报警ID（如果有值则表示处于报警状态）。</summary>
    private readonly ConcurrentDictionary<string, string?> _activeAlarmIds = new();

    /// <summary>规则 → 上次触发时间（用于冷却时间控制）。</summary>
    private readonly ConcurrentDictionary<string, DateTime> _lastTriggeredAt = new();

    /// <summary>报警 ID 计数器。</summary>
    private long _alarmIdCounter;

    private CancellationTokenSource? _cts;
    private Task? _consumeTask;

    /// <summary>
    /// 初始化报警引擎。
    /// </summary>
    public AlarmEngine(RealTimeStore store, AlarmChannel alarmChannel, ILogger<AlarmEngine> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _alarmChannel = alarmChannel ?? throw new ArgumentNullException(nameof(alarmChannel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 注册报警规则。
    /// </summary>
    public void RegisterRule(AlarmRule rule)
    {
        _rules[rule.RuleId] = rule;
        _activeAlarmIds[rule.RuleId] = null;
        _logger.LogInformation("已注册报警规则: {RuleId} [{TagName} {Operator} {Threshold}] {Title}",
            rule.RuleId, rule.TagName, rule.Operator, rule.Threshold, rule.Title);
    }

    /// <summary>
    /// 批量注册报警规则。
    /// </summary>
    public void RegisterRules(IEnumerable<AlarmRule> rules)
    {
        foreach (var rule in rules) RegisterRule(rule);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        _consumeTask = Task.Run(() => ConsumeAsync(_cts.Token), _cts.Token);
        _logger.LogInformation("报警引擎已启动 ({Count} 条规则)", _rules.Count);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("报警引擎正在停止...");
        _cts?.Cancel();

        if (_consumeTask is not null)
        {
            try { await _consumeTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        _alarmChannel.Writer.Complete();
        _cts?.Dispose();
        _logger.LogInformation("报警引擎已停止");
    }

    /// <summary>
    /// 主消费循环 — 监听实时数据变更，触发相关规则评估。
    /// </summary>
    private async Task ConsumeAsync(CancellationToken ct)
    {
        try
        {
            await foreach (TagValue value in _store.ChangeStream.ReadAllAsync(ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();

                // 跳过质量不良的数据
                if (value.Quality == Quality.Bad) continue;

                // 查找关联此测点的报警规则
                var matchedRules = _rules.Values
                    .Where(r => r.Enabled && r.TagId == value.TagId);

                foreach (AlarmRule rule in matchedRules)
                {
                    EvaluateRule(rule, value);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 正常退出
        }
    }

    /// <summary>
    /// 评估单条报警规则，处理状态转换（触发 / 清除）。
    /// </summary>
    private void EvaluateRule(AlarmRule rule, TagValue value)
    {
        try
        {
            if (!TryConvertToDouble(value.Value, out double currentValue))
                return;

            bool conditionMet = EvaluateCondition(currentValue, rule.Threshold, rule.Operator);
            bool isCurrentlyAlarming = _activeAlarmIds.TryGetValue(rule.RuleId, out string? activeId) && activeId != null;
            var now = DateTime.UtcNow;

            if (conditionMet && !isCurrentlyAlarming)
            {
                // 状态转换: 正常 → 报警
                // 检查冷却时间
                if (_lastTriggeredAt.TryGetValue(rule.RuleId, out DateTime lastTrigger))
                {
                    double elapsed = (now - lastTrigger).TotalSeconds;
                    if (elapsed < rule.CooldownSeconds)
                        return; // 冷却中，跳过
                }

                string alarmId = $"ALM-{Interlocked.Increment(ref _alarmIdCounter):D6}";
                _activeAlarmIds[rule.RuleId] = alarmId;
                _lastTriggeredAt[rule.RuleId] = now;
                var alarmRecord = new AlarmRecord
                {
                    Id = alarmId,
                    RuleId = rule.RuleId,
                    Severity = rule.Severity,
                    Source = rule.Source,
                    Title = rule.Title,
                    Message = BuildMessage(rule, value, currentValue),
                    TagId = rule.TagId,
                    TagName = rule.TagName,
                    TriggerValue = currentValue,
                    Threshold = rule.Threshold,
                    OccurredAt = now,
                    Status = AlarmStatus.Active
                };

                // 发布到报警管道
                _alarmChannel.Writer.TryWrite(alarmRecord);

                string severityLabel = rule.Severity switch
                {
                    AlarmSeverity.Critical => "严重",
                    AlarmSeverity.Warning => "警告",
                    _ => "信息"
                };

                _logger.LogWarning("[{Severity}] {AlarmId}: {Title} — {TagName}={Value}, 阈值 {Operator}{Threshold}",
                    severityLabel, alarmId, rule.Title, rule.TagName, currentValue, rule.Operator, rule.Threshold);
            }
            else if (!conditionMet && isCurrentlyAlarming)
            {
                // 状态转换: 报警 → 正常（考虑回滞）
                double returnThreshold = rule.Operator switch
                {
                    ">" or ">=" => rule.Threshold - rule.Hysteresis,
                    "<" or "<=" => rule.Threshold + rule.Hysteresis,
                    _ => rule.Threshold
                };

                bool shouldClear = rule.Operator switch
                {
                    ">" or ">=" => currentValue <= returnThreshold,
                    "<" or "<=" => currentValue >= returnThreshold,
                    "==" => Math.Abs(currentValue - rule.Threshold) > rule.Hysteresis,
                    "!=" => Math.Abs(currentValue - rule.Threshold) <= rule.Hysteresis,
                    _ => false
                };

                if (shouldClear)
                {
                    string clearedId = activeId ?? $"ALM-{Interlocked.Increment(ref _alarmIdCounter):D6}";
                    _activeAlarmIds[rule.RuleId] = null;
                    
                    var clearRecord = new AlarmRecord
                    {
                        Id = clearedId,
                        RuleId = rule.RuleId,
                        Severity = rule.Severity,
                        Source = rule.Source,
                        Title = rule.Title,
                        Message = $"[已恢复] {BuildMessage(rule, value, currentValue)}",
                        TagId = rule.TagId,
                        TagName = rule.TagName,
                        TriggerValue = currentValue,
                        Threshold = rule.Threshold,
                        OccurredAt = now,
                        Status = AlarmStatus.Cleared,
                        ClearedAt = now
                    };
                    
                    _alarmChannel.Writer.TryWrite(clearRecord);
                    
                    _logger.LogInformation("报警已自动清除: {RuleId} [{TagName}={Value}]",
                        rule.RuleId, rule.TagName, currentValue);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "评估报警规则 {RuleId} 时发生异常", rule.RuleId);
        }
    }

    /// <summary>
    /// 根据运算符比较当前值与阈值。
    /// </summary>
    private static bool EvaluateCondition(double value, double threshold, string op) => op switch
    {
        ">" => value > threshold,
        "<" => value < threshold,
        ">=" => value >= threshold,
        "<=" => value <= threshold,
        "==" => Math.Abs(value - threshold) < 0.0001,
        "!=" => Math.Abs(value - threshold) >= 0.0001,
        _ => false
    };

    /// <summary>
    /// 构建报警消息，替换消息模板中的占位符。
    /// </summary>
    private static string BuildMessage(AlarmRule rule, TagValue value, double currentValue)
    {
        if (string.IsNullOrWhiteSpace(rule.MessageTemplate))
        {
            return $"{rule.TagName} 当前值: {currentValue}, 阈值: {rule.Operator}{rule.Threshold}";
        }

        return rule.MessageTemplate
            .Replace("{TagName}", rule.TagName)
            .Replace("{Value}", currentValue.ToString("F2"))
            .Replace("{Threshold}", rule.Threshold.ToString("F2"))
            .Replace("{Operator}", rule.Operator);
    }

    /// <summary>
    /// 安全尝试将 object 值转为 double。
    /// </summary>
    private static bool TryConvertToDouble(object? value, out double result)
    {
        switch (value)
        {
            case double d: result = d; return true;
            case float f: result = f; return true;
            case int i: result = i; return true;
            case long l: result = l; return true;
            case short s: result = s; return true;
            case byte b: result = b; return true;
            case ushort us: result = us; return true;
            case uint ui: result = ui; return true;
            default:
                result = 0;
                return false;
        }
    }
}

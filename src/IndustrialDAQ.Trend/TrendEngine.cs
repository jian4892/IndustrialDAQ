// File: TrendEngine.cs  Module: Trend  Author: IndustrialDAQ Team
using System.Collections.Concurrent;
using IndustrialDAQ.Alarm;
using IndustrialDAQ.Core.Models;
using IndustrialDAQ.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IndustrialDAQ.Trend;

/// <summary>
/// 趋势引擎 — 订阅实时数据和报警事件，管理趋势缓存，
/// 提供 UI 刷新通知和报警线数据。
/// 作为 <see cref="IHostedService"/> 运行。
/// </summary>
public sealed class TrendEngine : IHostedService
{
    private readonly RealTimeStore _store;
    private readonly AlarmManager _alarmManager;
    private readonly TrendDataStore _dataStore;
    private readonly ILogger<TrendEngine> _logger;

    private CancellationTokenSource? _cts;
    private Task? _consumeTask;
    private Task? _refreshTask;

    /// <summary>已注册的 Tag 模板。</summary>
    private readonly ConcurrentDictionary<string, TrendTemplate> _registeredTags = new();

    /// <summary>UI 刷新间隔（毫秒），默认 500ms。</summary>
    public int RefreshIntervalMs { get; set; } = 500;

    /// <summary>趋势数据存储。</summary>
    public TrendDataStore DataStore => _dataStore;

    /// <summary>报警线集合（UI 绑定）。</summary>
    public List<TrendAlarmLine> AlarmLines { get; } = [];

    /// <summary>报警点集合（时间, tagId, value）。</summary>
    public List<(DateTime Time, string TagId, double Value, AlarmSeverity Severity)> AlarmPoints { get; } = [];

    /// <summary>数据刷新事件 — 通知 UI 更新图表。</summary>
    public event Action? DataRefreshed;

    /// <summary>报警点触发事件。</summary>
    public event Action<string, DateTime, double, AlarmSeverity>? AlarmPointTriggered;

    /// <summary>
    /// 初始化趋势引擎。
    /// </summary>
    public TrendEngine(RealTimeStore store, AlarmManager alarmManager,
        TrendDataStore dataStore, ILogger<TrendEngine> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _alarmManager = alarmManager ?? throw new ArgumentNullException(nameof(alarmManager));
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 注册 Tag 的趋势跟踪。
    /// </summary>
    /// <param name="tagId">Tag ID。</param>
    /// <param name="template">趋势模板（可选）。</param>
    public void RegisterTag(string tagId, TrendTemplate? template = null)
    {
        _dataStore.RegisterTag(tagId, template);
        if (template is not null)
            _registeredTags[tagId] = template;
        _logger.LogInformation("已注册趋势跟踪: {TagId}", tagId);
    }

    /// <summary>
    /// 注册多个 Tag。
    /// </summary>
    public void RegisterTags(IEnumerable<string> tagIds, TrendTemplate? template = null)
    {
        foreach (var tagId in tagIds)
            RegisterTag(tagId, template);
    }

    /// <summary>
    /// 添加报警线。
    /// </summary>
    public void AddAlarmLine(TrendAlarmLine line)
    {
        AlarmLines.Add(line);
    }

    /// <summary>
    /// 根据 AlarmRule 批量添加报警线。
    /// </summary>
    public void AddAlarmLinesFromRules(IEnumerable<AlarmRule> rules)
    {
        foreach (var rule in rules)
        {
            if (rule.AlarmType == AlarmType.Bool) continue;

            string color = rule.Severity switch
            {
                AlarmSeverity.Critical => "#EF4444",
                AlarmSeverity.Warning => "#F59E0B",
                _ => "#3B82F6"
            };

            // Threshold 用于 High/Low 类型
            if (rule.Threshold > 0 && rule.AlarmType is AlarmType.High or AlarmType.Low)
                AlarmLines.Add(new TrendAlarmLine
                {
                    TagId = rule.TagId, TagName = rule.TagName,
                    Value = rule.Threshold, Severity = rule.Severity,
                    Color = color, Label = $"{rule.TagName} {rule.AlarmType}",
                    AlarmType = rule.AlarmType
                });

            if (rule.HighHighThreshold > 0)
                AlarmLines.Add(new TrendAlarmLine
                {
                    TagId = rule.TagId, TagName = rule.TagName,
                    Value = rule.HighHighThreshold, Severity = AlarmSeverity.Critical,
                    Color = "#EF4444", Label = $"{rule.TagName} HH",
                    AlarmType = AlarmType.HighHigh
                });

            if (rule.LowLowThreshold > 0)
                AlarmLines.Add(new TrendAlarmLine
                {
                    TagId = rule.TagId, TagName = rule.TagName,
                    Value = rule.LowLowThreshold, Severity = AlarmSeverity.Critical,
                    Color = "#EF4444", Label = $"{rule.TagName} LL",
                    AlarmType = AlarmType.LowLow
                });
        }
    }

    /// <summary>
    /// 获取指定 Tag 的报警线。
    /// </summary>
    public IReadOnlyList<TrendAlarmLine> GetAlarmLines(string tagId)
    {
        return AlarmLines.Where(l => l.TagId == tagId).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        _consumeTask = Task.Run(() => ConsumeAsync(_cts.Token), _cts.Token);
        _refreshTask = Task.Run(() => RefreshLoopAsync(_cts.Token), _cts.Token);

        // 订阅报警事件
        _alarmManager.AlarmTriggered += OnAlarmTriggered;

        _logger.LogInformation("趋势引擎已启动 (刷新间隔: {Interval}ms)", RefreshIntervalMs);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("趋势引擎正在停止...");
        _alarmManager.AlarmTriggered -= OnAlarmTriggered;
        _cts?.Cancel();

        if (_consumeTask is not null)
        {
            try { await _consumeTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        if (_refreshTask is not null)
        {
            try { await _refreshTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        _cts?.Dispose();
        _logger.LogInformation("趋势引擎已停止");
    }

    /// <summary>
    /// 消费 RealTimeStore 数据流，写入趋势缓存。
    /// </summary>
    private async Task ConsumeAsync(CancellationToken ct)
    {
        _logger.LogDebug("趋势数据消费循环已启动");
        try
        {
            var reader = _store.Subscribe();
            await foreach (TagValue value in reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();

                // 只处理已注册的 Tag
                if (!_dataStore.TrackedTagIds.Contains(value.TagId))
                    continue;

                // 跳过质量不良的数据
                if (value.Quality == Quality.Bad)
                    continue;

                // 尝试转换为 double
                if (!TryConvertToDouble(value.Value, out double doubleValue))
                    continue;

                var point = new TrendPoint(value.Timestamp.UtcDateTime, doubleValue, value.Quality);
                _dataStore.Add(value.TagId, point);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 正常退出
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "趋势数据消费循环异常");
        }
    }

    /// <summary>
    /// 定时刷新循环 — 通知 UI 更新图表。
    /// </summary>
    private async Task RefreshLoopAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(RefreshIntervalMs));
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                DataRefreshed?.Invoke();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 正常退出
        }
    }

    /// <summary>
    /// 处理报警触发事件 — 记录报警点。
    /// </summary>
    private void OnAlarmTriggered(object? sender, AlarmEventArgs e)
    {
        var point = (e.Record.OccurredAt, e.Record.TagId, e.Record.TriggerValue, e.Record.Severity);
        AlarmPoints.Add(point);

        // 限制报警点数量
        while (AlarmPoints.Count > 1000)
            AlarmPoints.RemoveAt(0);

        AlarmPointTriggered?.Invoke(e.Record.TagId, e.Record.OccurredAt,
            e.Record.TriggerValue, e.Record.Severity);
    }

    /// <summary>
    /// 安全转换 object 为 double。
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
            case bool b: result = b ? 1.0 : 0.0; return true;
            default:
                result = 0;
                return false;
        }
    }
}

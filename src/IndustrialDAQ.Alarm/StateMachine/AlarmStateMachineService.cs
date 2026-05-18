using System.Collections.Concurrent;
using IndustrialDAQ.Alarm.RuleEngine;
using IndustrialDAQ.Core.Models;
using Microsoft.Extensions.Logging;

namespace IndustrialDAQ.Alarm.StateMachine;

/// <summary>
/// 工业报警状态机运行时服务。
/// 消费来自 RuleEngineService 的报警规则信号（AlarmRuleSignal），
/// 并管理报警的完整生命周期：Normal, Pending, Active, Cleared, Acknowledged 和 Suppressed。
/// </summary>
public sealed class AlarmStateMachineService : IAlarmStateMachineService
{
    private readonly IAlarmRuleSignalBus _signalBus;
    private readonly IAlarmStateTransitionBus _transitionBus;
    private readonly ILogger<AlarmStateMachineService> _logger;
    private readonly ConcurrentDictionary<string, AlarmStateContext> _contexts = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _cts;
    private Task? _consumeTask;

    /// <summary>
    /// 初始化报警状态机服务的新实例。
    /// </summary>
    public AlarmStateMachineService(
        IAlarmRuleSignalBus signalBus,
        IAlarmStateTransitionBus transitionBus,
        ILogger<AlarmStateMachineService> logger)
    {
        _signalBus = signalBus ?? throw new ArgumentNullException(nameof(signalBus));
        _transitionBus = transitionBus ?? throw new ArgumentNullException(nameof(transitionBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary> 启动报警状态机消费任务 </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumeTask = Task.Run(() => ConsumeAsync(_cts.Token), _cts.Token);
        _logger.LogInformation("报警状态机服务已启动。");
        return Task.CompletedTask;
    }

    /// <summary> 停止报警状态机服务 </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("报警状态机服务正在停止...");
        _cts?.Cancel();

        if (_consumeTask is not null)
        {
            try
            {
                await _consumeTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts?.Dispose();
        _logger.LogInformation("报警状态机服务已停止。");
    }

    /// <summary> 获取所有报警的实时状态 </summary>
    public IReadOnlyCollection<AlarmRuntimeState> GetStates()
    {
        return _contexts.Values.Select(static context => context.ToSnapshot()).ToArray();
    }

    /// <summary> 获取指定报警的实时状态 </summary>
    public AlarmRuntimeState? GetState(string ruleId)
    {
        return _contexts.TryGetValue(ruleId, out var context) ? context.ToSnapshot() : null;
    }

    /// <summary> 执行确认操作，触发状态迁移 </summary>
    public async ValueTask<bool> AcknowledgeAsync(
        string ruleId,
        string operatorId,
        CancellationToken cancellationToken = default)
    {
        if (!_contexts.TryGetValue(ruleId, out var context))
        {
            return false;
        }

        AlarmStateTransition? transition;
        lock (context)
        {
            transition = context.State switch
            {
                // Active 状态下确认，迁移至 Acknowledged
                AlarmState.Active => Transition(context, AlarmState.Acknowledged, null, operatorId),
                // Cleared 状态（条件已消失但未确认）下确认，迁移至 Normal（关闭报警）
                AlarmState.Cleared => Transition(context, AlarmState.Normal, null, operatorId),
                _ => null
            };
        }

        if (transition is null)
        {
            return false;
        }

        await _transitionBus.PublishAsync(transition, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary> 消费报警信号异步循环 </summary>
    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var signal in _signalBus.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ProcessSignalAsync(signal, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "报警状态机消费循环异常。");
        }
    }

    /// <summary> 处理接收到的规则信号，驱动状态机 </summary>
    private async ValueTask ProcessSignalAsync(
        AlarmRuleSignal signal,
        CancellationToken cancellationToken)
    {
        var context = _contexts.GetOrAdd(
            signal.RuleId,
            _ => new AlarmStateContext(signal.Definition));

        context.Definition = signal.Definition;

        var transitions = new List<AlarmStateTransition>();
        lock (context)
        {
            context.LastValue = signal.Value;

            if (signal.IsSuppressed)
            {
                // 收到抑制信号，直接转换到抑制状态
                AddTransitionIfNeeded(transitions, context, AlarmState.Suppressed, signal);
            }
            else
            {
                // 如果当前处于抑制状态且收到了非抑制信号，先恢复到 Normal
                if (context.State == AlarmState.Suppressed)
                {
                    AddTransitionIfNeeded(transitions, context, AlarmState.Normal, signal);
                }

                // 根据当前状态处理信号
                switch (context.State)
                {
                    case AlarmState.Normal:
                        HandleNormal(context, signal, transitions);
                        break;

                    case AlarmState.Pending:
                        HandlePending(context, signal, transitions);
                        break;

                    case AlarmState.Active:
                    case AlarmState.Acknowledged:
                        HandleActiveOrAcknowledged(context, signal, transitions);
                        break;

                    case AlarmState.Cleared:
                        HandleCleared(context, signal, transitions);
                        break;
                }
            }
        }

        // 发布所有发生的状态变更
        foreach (var transition in transitions)
        {
            await _transitionBus.PublishAsync(transition, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary> 处理 Normal 状态下的信号 </summary>
    private static void HandleNormal(
        AlarmStateContext context,
        AlarmRuleSignal signal,
        List<AlarmStateTransition> transitions)
    {
        if (!signal.IsTriggered || IsInCooldown(context, signal.Timestamp))
        {
            return;
        }

        // 如果没有延迟设置，直接进入 Active
        if (context.Definition.EffectiveDelay <= TimeSpan.Zero)
        {
            AddTransitionIfNeeded(transitions, context, AlarmState.Active, signal);
            return;
        }

        // 否则进入 Pending 待定状态（防抖）
        AddTransitionIfNeeded(transitions, context, AlarmState.Pending, signal);
    }

    /// <summary> 处理 Pending 状态下的信号（防抖计时） </summary>
    private static void HandlePending(
        AlarmStateContext context,
        AlarmRuleSignal signal,
        List<AlarmStateTransition> transitions)
    {
        if (!signal.IsTriggered || signal.IsCleared)
        {
            AddTransitionIfNeeded(transitions, context, AlarmState.Normal, signal);
            return;
        }

        var pendingSince = context.PendingSince ?? signal.Timestamp;
        if (signal.Timestamp - pendingSince >= context.Definition.EffectiveDelay)
        {
            // 防抖时间已到，正式触发报警
            AddTransitionIfNeeded(transitions, context, AlarmState.Active, signal);
        }
    }

    /// <summary> 处理活跃或已确认状态下的信号（判断条件是否消失） </summary>
    private static void HandleActiveOrAcknowledged(
        AlarmStateContext context,
        AlarmRuleSignal signal,
        List<AlarmStateTransition> transitions)
    {
        if (!signal.IsCleared)
        {
            return;
        }

        // 如果已经确认过，或者配置不需要确认，则直接恢复正常
        if (context.State == AlarmState.Acknowledged || !context.Definition.IsAckRequired)
        {
            AddTransitionIfNeeded(transitions, context, AlarmState.Normal, signal);
            return;
        }

        // 否则进入 Cleared 状态，等待操作员确认后才彻底关闭
        AddTransitionIfNeeded(transitions, context, AlarmState.Cleared, signal);
    }

    /// <summary> 处理 Cleared 状态下的信号（等待确认，或条件重新触发） </summary>
    private static void HandleCleared(
        AlarmStateContext context,
        AlarmRuleSignal signal,
        List<AlarmStateTransition> transitions)
    {
        if (signal.IsTriggered)
        {
            // 条件在确认前重新触发，回跳到 Active
            AddTransitionIfNeeded(transitions, context, AlarmState.Active, signal);
        }
    }

    /// <summary> 检查是否处于冷却时间内（防止报警风暴） </summary>
    private static bool IsInCooldown(AlarmStateContext context, DateTimeOffset timestamp)
    {
        if (context.ClearedAt is null || context.Definition.CooldownSeconds <= 0)
        {
            return false;
        }

        return timestamp - context.ClearedAt.Value < TimeSpan.FromSeconds(context.Definition.CooldownSeconds);
    }

    private static void AddTransitionIfNeeded(
        List<AlarmStateTransition> transitions,
        AlarmStateContext context,
        AlarmState targetState,
        AlarmRuleSignal signal)
    {
        var transition = Transition(context, targetState, signal, null);
        if (transition is not null)
        {
            transitions.Add(transition);
        }
    }

    /// <summary> 执行状态转换逻辑并记录上下文 </summary>
    private static AlarmStateTransition? Transition(
        AlarmStateContext context,
        AlarmState targetState,
        AlarmRuleSignal? signal,
        string? operatorId)
    {
        if (context.State == targetState)
        {
            return null;
        }

        var oldState = context.State;
        var now = signal?.Timestamp ?? DateTimeOffset.UtcNow;
        var value = signal?.Value ?? context.LastValue;

        // 生成报警发生标识
        if (string.IsNullOrWhiteSpace(context.OccurrenceId) &&
            (targetState == AlarmState.Pending || targetState == AlarmState.Active))
        {
            context.OccurrenceId = CreateOccurrenceId(context.Definition.RuleId, now);
        }

        context.State = targetState;
        context.LastValue = value;

        // 维护上下文的时间戳
        switch (targetState)
        {
            case AlarmState.Pending:
                context.PendingSince = now;
                break;
            case AlarmState.Active:
                context.ActivatedAt = now;
                context.PendingSince = null;
                context.ClearedAt = null;
                context.AcknowledgedAt = null;
                context.OccurrenceId ??= CreateOccurrenceId(context.Definition.RuleId, now);
                break;
            case AlarmState.Acknowledged:
                context.AcknowledgedAt = now;
                break;
            case AlarmState.Cleared:
                context.ClearedAt = now;
                break;
            case AlarmState.Normal:
                context.PendingSince = null;
                context.ClearedAt ??= now;
                if (oldState == AlarmState.Cleared || oldState == AlarmState.Acknowledged)
                {
                    context.OccurrenceId = null;
                }
                break;
            case AlarmState.Suppressed:
                context.PendingSince = null;
                break;
        }

        return new AlarmStateTransition
        {
            OccurrenceId = context.OccurrenceId ?? CreateOccurrenceId(context.Definition.RuleId, now),
            RuleId = context.Definition.RuleId,
            AlarmCode = context.Definition.AlarmCode,
            Definition = context.Definition,
            FromState = oldState,
            ToState = targetState,
            Value = value,
            OperatorId = operatorId,
            OccurredAt = now,
            Signal = signal
        };
    }

    private static string CreateOccurrenceId(string ruleId, DateTimeOffset timestamp)
    {
        var safeRuleId = string.Concat(ruleId.Select(static ch => char.IsLetterOrDigit(ch) ? ch : '-'));
        return $"ALM-{safeRuleId}-{timestamp.UtcDateTime:yyyyMMddHHmmssfff}";
    }
}

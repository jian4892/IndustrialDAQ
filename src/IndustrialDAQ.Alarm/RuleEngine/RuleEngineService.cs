using System.Threading.Channels;
using IndustrialDAQ.Alarm.RuleBuilder;
using IndustrialDAQ.Core.Models;
using IndustrialDAQ.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IndustrialDAQ.Alarm.RuleEngine;

/// <summary>
/// 托管的运行时规则引擎服务。
/// 加载 AlarmDefinition 配置，通过 RuleBuilder 构建可执行工作流，
/// 消费来自 RealTimeStore 的 TagValue 事件，并通过异步 Channel 发布报警规则信号。
/// </summary>
public sealed class RuleEngineService : IHostedService, IRuleEngineService
{
    private readonly RealTimeStore _realTimeStore;
    private readonly IAlarmDefinitionService _definitionService;
    private readonly IAlarmRuleBuilder _ruleBuilder;
    private readonly IAlarmRuleSignalBus _signalBus;
    private readonly ILogger<RuleEngineService> _logger;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);

    private volatile AlarmRuleWorkflowSnapshot _current = AlarmRuleWorkflowSnapshot.Empty;
    private CancellationTokenSource? _cts;
    private Task? _consumeTask;
    private ChannelReader<TagValue>? _subscription;

    /// <summary>
    /// 初始化规则引擎服务的新实例。
    /// </summary>
    public RuleEngineService(
        RealTimeStore realTimeStore,
        IAlarmDefinitionService definitionService,
        IAlarmRuleBuilder ruleBuilder,
        IAlarmRuleSignalBus signalBus,
        ILogger<RuleEngineService> logger)
    {
        _realTimeStore = realTimeStore ?? throw new ArgumentNullException(nameof(realTimeStore));
        _definitionService = definitionService ?? throw new ArgumentNullException(nameof(definitionService));
        _ruleBuilder = ruleBuilder ?? throw new ArgumentNullException(nameof(ruleBuilder));
        _signalBus = signalBus ?? throw new ArgumentNullException(nameof(signalBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取当前的报警规则工作流运行时快照。
    /// </summary>
    public AlarmRuleWorkflowSnapshot Current => _current;

    /// <summary>
    /// 启动服务。
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await ReloadAsync(cancellationToken).ConfigureAwait(false);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _subscription = _realTimeStore.Subscribe();
        _consumeTask = Task.Run(() => ConsumeAsync(_subscription, _cts.Token), _cts.Token);

        _logger.LogInformation(
            "规则引擎服务已启动，共编译了 {Count} 条报警工作流。版本={Version}",
            _current.Workflows.Count,
            _current.Version);
    }

    /// <summary>
    /// 停止服务。
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("规则引擎服务正在停止...");

        _cts?.Cancel();

        if (_subscription is not null)
        {
            _realTimeStore.Unsubscribe(_subscription);
        }

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
        _logger.LogInformation("规则引擎服务已停止。");
    }

    /// <summary>
    /// 热重载：重新加载报警配置并重建规则引擎工作流。
    /// </summary>
    public async Task<AlarmRuleWorkflowSnapshot> ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _reloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var definitionSnapshot = await _definitionService.ReloadAsync(cancellationToken).ConfigureAwait(false);
            var next = _ruleBuilder.BuildSnapshot(definitionSnapshot.Definitions);
            _current = next;

            _logger.LogInformation(
                "规则引擎服务已重载 {Count} 条工作流。定义版本={DefinitionVersion}, 工作流版本={WorkflowVersion}",
                next.Workflows.Count,
                definitionSnapshot.Version,
                next.Version);

            return next;
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    /// <summary> 消费标签数据异步循环 </summary>
    private async Task ConsumeAsync(ChannelReader<TagValue> reader, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var value in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ProcessTagValueAsync(value, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "规则引擎服务消费循环异常。");
        }
    }

    /// <summary> 评估标签数据并发布报警信号 </summary>
    private async ValueTask ProcessTagValueAsync(TagValue value, CancellationToken cancellationToken)
    {
        if (value.Quality == Quality.Bad)
        {
            _logger.LogTrace("跳过坏质量的标签值。TagId={TagId}", value.TagId);
            return;
        }

        var snapshot = Current;
        var workflows = snapshot.FindByTagId(value.TagId);
        if (workflows.Count == 0)
        {
            return;
        }

        foreach (var workflow in workflows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // 使用编译后的工作流对标签值进行评估
                var result = await workflow.EvaluateAsync(value, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                // 规则运行时日志：记录每次评估结果
                _logger.LogDebug(
                    "规则评估: RuleId={RuleId}, TagId={TagId}, Value={Value}, 触发={IsTriggered}, 清除={IsCleared}, 抑制={IsSuppressed}",
                    workflow.Definition.RuleId, value.TagId, value.Value,
                    result.IsTriggered, result.IsCleared, result.IsSuppressed);

                // 将评估结果包装成信号发布
                var signal = AlarmRuleSignal.FromEvaluation(workflow, result, value);
                await _signalBus.PublishAsync(signal, cancellationToken).ConfigureAwait(false);

                if (result.Errors.Count > 0)
                {
                    _logger.LogWarning(
                        "报警工作流评估返回错误。RuleId={RuleId}, 错误={Errors}",
                        workflow.Definition.RuleId,
                        string.Join("; ", result.Errors));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "评估报警工作流失败。RuleId={RuleId}, TagId={TagId}",
                    workflow.Definition.RuleId,
                    value.TagId);
            }
        }
    }
}

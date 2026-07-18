using IndustrialDAQ.Alarm.RuleBuilder;
using Microsoft.Extensions.Hosting;

namespace IndustrialDAQ.Alarm.RuleEngine;

/// <summary>
/// 运行时规则引擎边界接口。
/// 该服务消费标签事件，评估编译后的工作流，并为状态机发布报警规则信号（AlarmRuleSignal）。
/// </summary>
public interface IRuleEngineService : IHostedService
{
    /// <summary>
    /// 获取当前的报警规则工作流运行时快照。
    /// </summary>
    AlarmRuleWorkflowSnapshot Current { get; }

    /// <summary>
    /// 从持久化存储中重载报警定义，重新构建工作流并发布新快照。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>新发布的快照。</returns>
    Task<AlarmRuleWorkflowSnapshot> ReloadAsync(CancellationToken cancellationToken = default);
}

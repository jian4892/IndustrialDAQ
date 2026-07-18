using IndustrialDAQ.Core.Models;

namespace IndustrialDAQ.Alarm.RuleBuilder;

/// <summary>
/// 报警规则构建器接口。
/// 负责将数据库中的报警定义（AlarmDefinition）转换为可执行的报警规则工作流。
/// </summary>
public interface IAlarmRuleBuilder
{
    /// <summary>
    /// 将单个报警定义构建为可执行的工作流。
    /// </summary>
    /// <param name="definition">报警定义配置。</param>
    /// <returns>可执行的报警规则工作流。</returns>
    AlarmRuleWorkflow Build(AlarmDefinition definition);

    /// <summary>
    /// 将报警定义集合构建为不可变的运行时工作流快照。
    /// </summary>
    /// <param name="definitions">报警定义集合。</param>
    /// <returns>报警规则工作流快照。</returns>
    AlarmRuleWorkflowSnapshot BuildSnapshot(IEnumerable<AlarmDefinition> definitions);
}

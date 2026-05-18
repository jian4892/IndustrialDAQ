using Microsoft.Extensions.Hosting;

namespace IndustrialDAQ.Alarm.StateMachine;

/// <summary>
/// 报警状态机服务接口。
/// 负责维护所有活跃报警的生命周期状态。
/// </summary>
public interface IAlarmStateMachineService : IHostedService
{
    /// <summary>
    /// 获取当前全系统的报警运行时状态集合。
    /// </summary>
    IReadOnlyCollection<AlarmRuntimeState> GetStates();

    /// <summary>
    /// 获取指定报警规则的运行时状态。
    /// </summary>
    /// <param name="ruleId">报警规则 ID。</param>
    /// <returns>运行时状态快照，如果不存在则返回 null。</returns>
    AlarmRuntimeState? GetState(string ruleId);

    /// <summary>
    /// 操作员确认报警。
    /// </summary>
    /// <param name="ruleId">报警规则 ID。</param>
    /// <param name="operatorId">执行操作的操作员 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>如果确认操作成功且导致状态转换，则返回 true。</returns>
    ValueTask<bool> AcknowledgeAsync(
        string ruleId,
        string operatorId,
        CancellationToken cancellationToken = default);
}

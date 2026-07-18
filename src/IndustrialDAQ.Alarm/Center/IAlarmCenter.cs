using IndustrialDAQ.Core.Models;
using Microsoft.Extensions.Hosting;

namespace IndustrialDAQ.Alarm.Center;

/// <summary>
/// 报警中心接口。
/// 作为全系统报警状态的权威来源，汇总所有报警事件并提供实时视图。
/// </summary>
public interface IAlarmCenter : IHostedService
{
    /// <summary>
    /// 获取当前所有活跃的报警记录列表。
    /// </summary>
    IReadOnlyList<AlarmRecord> GetCurrentAlarms();

    /// <summary>
    /// 根据唯一发生标识（OccurrenceId）查找当前活跃的报警记录。
    /// </summary>
    /// <param name="occurrenceId">报警发生唯一标识。</param>
    /// <returns>报警记录，如果未找到或已关闭则返回 null。</returns>
    AlarmRecord? FindCurrentAlarm(string occurrenceId);

    /// <summary>
    /// 操作员确认指定的报警。
    /// </summary>
    /// <param name="occurrenceId">报警发生唯一标识。</param>
    /// <param name="operatorId">操作员 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>如果确认操作成功且导致状态转换，则返回 true。</returns>
    Task<bool> AcknowledgeAsync(
        string occurrenceId,
        string operatorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 操作员确认当前所有活跃的报警。
    /// </summary>
    /// <param name="operatorId">操作员 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功确认的报警数量。</returns>
    Task<int> AcknowledgeAllAsync(
        string operatorId,
        CancellationToken cancellationToken = default);
}

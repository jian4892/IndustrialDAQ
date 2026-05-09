// File: AlarmHistoryRepository.cs  Module: Storage  Author: IndustrialDAQ Team
using IndustrialDAQ.Core.Models;
using IndustrialDAQ.Infrastructure;
using IndustrialDAQ.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IndustrialDAQ.Storage;

/// <summary>
/// 报警历史仓储 — 使用 EF Core 将报警记录持久化到 SQLite。
/// 支持批量写入和查询优化。
/// </summary>
public sealed class AlarmHistoryRepository
{
    private readonly IDbContextFactory<DaqDbContext> _contextFactory;
    private readonly ILogger<AlarmHistoryRepository> _logger;

    /// <summary>
    /// 初始化报警历史仓储。
    /// </summary>
    public AlarmHistoryRepository(IDbContextFactory<DaqDbContext> contextFactory,
        ILogger<AlarmHistoryRepository> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 保存报警记录到数据库。
    /// </summary>
    /// <param name="record">报警记录。</param>
    /// <param name="alarmType">报警类型。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task SaveAsync(AlarmRecord record, AlarmType alarmType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var entity = AlarmHistoryEntity.FromDomain(record, alarmType);
            entity.CreatedAt = DateTime.UtcNow;

            context.AlarmHistories.Add(entity);
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("报警记录已保存: {AlarmId}", record.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存报警记录失败: {AlarmId}", record.Id);
            throw;
        }
    }

    /// <summary>
    /// 更新报警记录状态。
    /// </summary>
    /// <param name="alarmId">报警 ID。</param>
    /// <param name="status">新状态。</param>
    /// <param name="acknowledgedAt">确认时间（可选）。</param>
    /// <param name="clearedAt">清除时间（可选）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task UpdateStatusAsync(string alarmId, AlarmStatus status,
        DateTime? acknowledgedAt = null, DateTime? clearedAt = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var entity = await context.AlarmHistories
                .FirstOrDefaultAsync(e => e.AlarmId == alarmId, cancellationToken);

            if (entity is null)
            {
                _logger.LogWarning("报警记录不存在: {AlarmId}", alarmId);
                return;
            }

            entity.Status = status;
            if (acknowledgedAt.HasValue)
                entity.AcknowledgedAt = acknowledgedAt.Value;
            if (clearedAt.HasValue)
                entity.ClearedAt = clearedAt.Value;

            await context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("报警记录状态已更新: {AlarmId} -> {Status}", alarmId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新报警记录状态失败: {AlarmId}", alarmId);
            throw;
        }
    }

    /// <summary>
    /// 获取报警历史记录（分页）。
    /// </summary>
    /// <param name="pageNumber">页码（从 1 开始）。</param>
    /// <param name="pageSize">每页大小。</param>
    /// <param name="status">状态筛选（可选）。</param>
    /// <param name="severity">严重程度筛选（可选）。</param>
    /// <param name="tagId">测点 ID 筛选（可选）。</param>
    /// <param name="startTime">开始时间（可选）。</param>
    /// <param name="endTime">结束时间（可选）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>报警记录列表和总数。</returns>
    public async Task<(IReadOnlyList<AlarmRecord> Records, int TotalCount)> GetHistoryAsync(
        int pageNumber = 1, int pageSize = 50,
        AlarmStatus? status = null, AlarmSeverity? severity = null,
        string? tagId = null, DateTime? startTime = null, DateTime? endTime = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var query = context.AlarmHistories.AsQueryable();

            // 应用筛选条件
            if (status.HasValue)
                query = query.Where(e => e.Status == status.Value);
            if (severity.HasValue)
                query = query.Where(e => e.Severity == severity.Value);
            if (!string.IsNullOrEmpty(tagId))
                query = query.Where(e => e.TagId == tagId);
            if (startTime.HasValue)
                query = query.Where(e => e.OccurredAt >= startTime.Value);
            if (endTime.HasValue)
                query = query.Where(e => e.OccurredAt <= endTime.Value);

            // 获取总数
            int totalCount = await query.CountAsync(cancellationToken);

            // 分页查询
            var records = await query
                .OrderByDescending(e => e.OccurredAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(e => e.ToDomain())
                .ToListAsync(cancellationToken);

            return (records, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询报警历史失败");
            throw;
        }
    }

    /// <summary>
    /// 获取活跃报警列表。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>活跃报警记录列表。</returns>
    public async Task<IReadOnlyList<AlarmRecord>> GetActiveAlarmsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var records = await context.AlarmHistories
                .Where(e => e.Status == AlarmStatus.Active || e.Status == AlarmStatus.Acknowledged)
                .OrderByDescending(e => e.OccurredAt)
                .Select(e => e.ToDomain())
                .ToListAsync(cancellationToken);

            return records;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询活跃报警失败");
            throw;
        }
    }

    /// <summary>
    /// 获取报警统计信息。
    /// </summary>
    /// <param name="startTime">开始时间。</param>
    /// <param name="endTime">结束时间。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>报警统计信息。</returns>
    public async Task<AlarmStatistics> GetStatisticsAsync(
        DateTime startTime, DateTime endTime,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var query = context.AlarmHistories
                .Where(e => e.OccurredAt >= startTime && e.OccurredAt <= endTime);

            var statistics = new AlarmStatistics
            {
                TotalCount = await query.CountAsync(cancellationToken),
                ActiveCount = await query.CountAsync(e => e.Status == AlarmStatus.Active, cancellationToken),
                AcknowledgedCount = await query.CountAsync(e => e.Status == AlarmStatus.Acknowledged, cancellationToken),
                ClearedCount = await query.CountAsync(e => e.Status == AlarmStatus.Cleared, cancellationToken),
                CriticalCount = await query.CountAsync(e => e.Severity == AlarmSeverity.Critical, cancellationToken),
                WarningCount = await query.CountAsync(e => e.Severity == AlarmSeverity.Warning, cancellationToken),
                InfoCount = await query.CountAsync(e => e.Severity == AlarmSeverity.Info, cancellationToken)
            };

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取报警统计失败");
            throw;
        }
    }

    /// <summary>
    /// 清理过期的报警历史记录。
    /// </summary>
    /// <param name="retentionDays">保留天数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>删除的记录数。</returns>
    public async Task<int> CleanupAsync(int retentionDays = 90,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

            var expiredRecords = await context.AlarmHistories
                .Where(e => e.ClearedAt.HasValue && e.ClearedAt.Value < cutoffDate)
                .ToListAsync(cancellationToken);

            int count = expiredRecords.Count;
            if (count > 0)
            {
                context.AlarmHistories.RemoveRange(expiredRecords);
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("已清理 {Count} 条过期报警记录", count);
            }

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理过期报警记录失败");
            throw;
        }
    }
}

/// <summary>
/// 报警统计信息。
/// </summary>
public sealed class AlarmStatistics
{
    /// <summary>总数。</summary>
    public int TotalCount { get; set; }

    /// <summary>活跃报警数。</summary>
    public int ActiveCount { get; set; }

    /// <summary>已确认报警数。</summary>
    public int AcknowledgedCount { get; set; }

    /// <summary>已清除报警数。</summary>
    public int ClearedCount { get; set; }

    /// <summary>严重报警数。</summary>
    public int CriticalCount { get; set; }

    /// <summary>警告报警数。</summary>
    public int WarningCount { get; set; }

    /// <summary>信息报警数。</summary>
    public int InfoCount { get; set; }
}

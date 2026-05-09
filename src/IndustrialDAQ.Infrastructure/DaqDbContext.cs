// File: DaqDbContext.cs  Module: Infrastructure  Author: IndustrialDAQ Team
using IndustrialDAQ.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndustrialDAQ.Infrastructure;

/// <summary>
/// 工业数据采集数据库上下文 — SQLite 单机版。
/// 使用连接池和 WAL 模式以支持并发读写。
/// </summary>
public sealed class DaqDbContext : DbContext
{
    /// <summary>历史记录表。</summary>
    public DbSet<HistoricalRecord> HistoricalRecords => Set<HistoricalRecord>();

    /// <summary>报警历史表。</summary>
    public DbSet<AlarmHistoryEntity> AlarmHistories => Set<AlarmHistoryEntity>();

    /// <summary>
    /// 使用选项配置（由 DI 注入连接字符串）。
    /// </summary>
    public DaqDbContext(DbContextOptions<DaqDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HistoricalRecord>(entity =>
        {
            entity.HasIndex(e => e.TagId);
            entity.HasIndex(e => e.Timestamp);
            // 复合索引：按测点 + 时间范围查询是最高频场景
            entity.HasIndex(e => new { e.TagId, e.Timestamp });
        });

        modelBuilder.Entity<AlarmHistoryEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AlarmId);
            entity.HasIndex(e => e.RuleId);
            entity.HasIndex(e => e.TagId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Severity);
            entity.HasIndex(e => e.OccurredAt);
            // 复合索引：按状态 + 时间查询是最高频场景
            entity.HasIndex(e => new { e.Status, e.OccurredAt });
        });
    }
}

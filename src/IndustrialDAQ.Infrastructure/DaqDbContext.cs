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
    }
}

// File: DeviceTemplateRepository.cs  Module: Storage  Author: IndustrialDAQ Team
using System.Text.Json;
using IndustrialDAQ.Core.Models;
using IndustrialDAQ.Infrastructure;
using IndustrialDAQ.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IndustrialDAQ.Storage;

/// <summary>
/// 设备模板仓储 — 使用 EF Core 将设备模板、报警模板、趋势模板持久化到 SQLite。
/// 支持内置模板初始化和用户自定义模板的增删改查。
/// </summary>
public sealed class DeviceTemplateRepository
{
    private readonly IDbContextFactory<DaqDbContext> _contextFactory;
    private readonly ILogger<DeviceTemplateRepository> _logger;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>趋势曲线默认颜色池（循环分配）。</summary>
    private static readonly string[] s_trendColors =
    [
        "#3B82F6", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6",
        "#EC4899", "#06B6D4", "#F97316", "#84CC16", "#6366F1"
    ];

    public DeviceTemplateRepository(IDbContextFactory<DaqDbContext> contextFactory,
        ILogger<DeviceTemplateRepository> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region 初始化内置模板

    /// <summary>
    /// 初始化内置模板 — 首次运行时从工厂写入数据库。
    /// </summary>
    public async Task InitializeBuiltInTemplatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            // 初始化内置报警模板
            await InitializeAlarmTemplatesAsync(context, cancellationToken);

            // 初始化内置趋势模板
            await InitializeTrendTemplatesAsync(context, cancellationToken);

            // 初始化内置设备模板
            await InitializeDeviceTemplatesAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化内置模板失败");
            throw;
        }
    }

    private async Task InitializeAlarmTemplatesAsync(DaqDbContext context, CancellationToken ct)
    {
        int count = await context.AlarmTemplates.CountAsync(e => e.IsBuiltIn, ct);
        if (count > 0) return;

        foreach (var (id, template) in AlarmTemplateFactory.All)
        {
            var entity = new AlarmTemplateEntity
            {
                TemplateId = template.TemplateId,
                Name = template.Name,
                ApplicableDataType = (byte)template.ApplicableDataType,
                Unit = template.Unit,
                HighThreshold = template.HighThreshold,
                HighHighThreshold = template.HighHighThreshold,
                LowThreshold = template.LowThreshold,
                LowLowThreshold = template.LowLowThreshold,
                Hysteresis = template.Hysteresis,
                Severity = (byte)template.Severity,
                CooldownSeconds = template.CooldownSeconds,
                SupportedAlarmTypesJson = JsonSerializer.Serialize(
                    template.SupportedAlarmTypes.Select(t => (int)t), s_jsonOptions),
                IsBuiltIn = true,
                CreatedAt = DateTime.UtcNow
            };
            context.AlarmTemplates.Add(entity);
        }
        await context.SaveChangesAsync(ct);
        _logger.LogInformation("已初始化 {Count} 个内置报警模板", AlarmTemplateFactory.All.Count);
    }

    private async Task InitializeTrendTemplatesAsync(DaqDbContext context, CancellationToken ct)
    {
        int count = await context.TrendTemplates.CountAsync(e => e.IsBuiltIn, ct);
        if (count > 0) return;

        // 为内置设备模板的数据点创建趋势模板
        int colorIndex = 0;
        foreach (var (_, deviceTemplate) in DeviceTemplateFactory.All)
        {
            foreach (var dp in deviceTemplate.DataPoints)
            {
                if (dp.TrendTemplate is null) continue;
                var tt = dp.TrendTemplate;
                var entity = new TrendTemplateEntity
                {
                    TemplateId = tt.TemplateId,
                    Name = tt.Name,
                    Unit = tt.Unit,
                    YMin = double.IsNaN(tt.YMin) ? null : tt.YMin,
                    YMax = double.IsNaN(tt.YMax) ? null : tt.YMax,
                    BufferCapacity = tt.BufferCapacity,
                    WindowSeconds = tt.WindowSeconds,
                    LineColor = tt.LineColor,
                    ShowAlarmLines = tt.ShowAlarmLines,
                    StrokeThickness = tt.StrokeThickness,
                    ShowGeometry = tt.ShowGeometry,
                    IsBuiltIn = true,
                    CreatedAt = DateTime.UtcNow
                };
                context.TrendTemplates.Add(entity);
                colorIndex++;
            }
        }
        await context.SaveChangesAsync(ct);
        _logger.LogInformation("已初始化内置趋势模板");
    }

    private async Task InitializeDeviceTemplatesAsync(DaqDbContext context, CancellationToken ct)
    {
        int count = await context.DeviceTemplates.CountAsync(e => e.IsBuiltIn, ct);
        if (count > 0) return;

        foreach (var (_, template) in DeviceTemplateFactory.All)
        {
            var entity = new DeviceTemplateEntity
            {
                TemplateId = template.TemplateId,
                Name = template.Name,
                DriverType = template.DriverType,
                IsBuiltIn = true,
                CreatedAt = DateTime.UtcNow
            };
            context.DeviceTemplates.Add(entity);
            await context.SaveChangesAsync(ct);

            foreach (var dp in template.DataPoints)
            {
                var dpEntity = new DataPointTemplateEntity
                {
                    DeviceTemplateId = entity.Id,
                    TemplateId = dp.TemplateId,
                    Name = dp.Name,
                    DataType = (byte)dp.DataType,
                    Unit = dp.Unit,
                    AlarmTemplateId = dp.AlarmTemplate?.TemplateId,
                    TrendTemplateId = dp.TrendTemplate?.TemplateId
                };
                context.DataPointTemplates.Add(dpEntity);
            }
            await context.SaveChangesAsync(ct);
        }
        _logger.LogInformation("已初始化 {Count} 个内置设备模板", DeviceTemplateFactory.All.Count);
    }

    #endregion

    #region 设备模板 CRUD

    public async Task<List<DeviceTemplate>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var deviceEntities = await context.DeviceTemplates
            .OrderBy(e => e.IsBuiltIn ? 0 : 1).ThenBy(e => e.Name)
            .ToListAsync(cancellationToken);

        var dpEntities = await context.DataPointTemplates.ToListAsync(cancellationToken);
        var dpGrouped = dpEntities.GroupBy(e => e.DeviceTemplateId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 预加载报警模板和趋势模板
        var alarmEntities = await context.AlarmTemplates.ToListAsync(cancellationToken);
        var trendEntities = await context.TrendTemplates.ToListAsync(cancellationToken);
        var alarmDict = alarmEntities.ToDictionary(e => e.TemplateId, e => e);
        var trendDict = trendEntities.ToDictionary(e => e.TemplateId, e => e);

        var templates = new List<DeviceTemplate>();
        foreach (var dev in deviceEntities)
        {
            var dataPoints = dpGrouped.GetValueOrDefault(dev.Id, [])
                .Select(dp => ConvertToDataPointTemplate(dp, alarmDict, trendDict))
                .ToList();

            templates.Add(new DeviceTemplate
            {
                TemplateId = dev.TemplateId,
                Name = dev.Name,
                DriverType = dev.DriverType,
                DataPoints = dataPoints
            });
        }
        return templates;
    }

    public async Task SaveAsync(DeviceTemplate template, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = new DeviceTemplateEntity
        {
            TemplateId = template.TemplateId,
            Name = template.Name,
            DriverType = template.DriverType,
            IsBuiltIn = false,
            CreatedAt = DateTime.UtcNow
        };
        context.DeviceTemplates.Add(entity);
        await context.SaveChangesAsync(ct);

        int colorIndex = 0;
        foreach (var dp in template.DataPoints)
        {
            // 保存报警模板（如果不在数据库中）
            if (dp.AlarmTemplate is not null)
            {
                var existing = await context.AlarmTemplates
                    .FirstOrDefaultAsync(e => e.TemplateId == dp.AlarmTemplate.TemplateId, ct);
                if (existing is null)
                {
                    context.AlarmTemplates.Add(ConvertToAlarmEntity(dp.AlarmTemplate, false));
                    await context.SaveChangesAsync(ct);
                }
            }

            // 保存趋势模板
            string? trendTemplateId = null;
            if (dp.TrendTemplate is not null)
            {
                trendTemplateId = dp.TrendTemplate.TemplateId;
                var existing = await context.TrendTemplates
                    .FirstOrDefaultAsync(e => e.TemplateId == trendTemplateId, ct);
                if (existing is null)
                {
                    context.TrendTemplates.Add(ConvertToTrendEntity(dp.TrendTemplate, false));
                    await context.SaveChangesAsync(ct);
                }
            }

            var dpEntity = new DataPointTemplateEntity
            {
                DeviceTemplateId = entity.Id,
                TemplateId = dp.TemplateId,
                Name = dp.Name,
                DataType = (byte)dp.DataType,
                Unit = dp.Unit,
                AlarmTemplateId = dp.AlarmTemplate?.TemplateId,
                TrendTemplateId = trendTemplateId
            };
            context.DataPointTemplates.Add(dpEntity);
            colorIndex++;
        }

        await context.SaveChangesAsync(ct);
        _logger.LogInformation("已保存设备模板: {Name}", template.Name);
    }

    public async Task UpdateAsync(DeviceTemplate template, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.DeviceTemplates
            .FirstOrDefaultAsync(e => e.TemplateId == template.TemplateId, ct);
        if (entity is null) throw new InvalidOperationException($"模板不存在: {template.TemplateId}");

        entity.Name = template.Name;
        entity.DriverType = template.DriverType;

        // 删除旧的数据点
        var oldDps = await context.DataPointTemplates
            .Where(e => e.DeviceTemplateId == entity.Id).ToListAsync(ct);
        context.DataPointTemplates.RemoveRange(oldDps);

        // 重新添加数据点
        foreach (var dp in template.DataPoints)
        {
            context.DataPointTemplates.Add(new DataPointTemplateEntity
            {
                DeviceTemplateId = entity.Id,
                TemplateId = dp.TemplateId,
                Name = dp.Name,
                DataType = (byte)dp.DataType,
                Unit = dp.Unit,
                AlarmTemplateId = dp.AlarmTemplate?.TemplateId,
                TrendTemplateId = dp.TrendTemplate?.TemplateId
            });
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string templateId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.DeviceTemplates
            .FirstOrDefaultAsync(e => e.TemplateId == templateId, ct);
        if (entity is null) return;
        if (entity.IsBuiltIn) throw new InvalidOperationException("内置模板不可删除");

        var dps = await context.DataPointTemplates
            .Where(e => e.DeviceTemplateId == entity.Id).ToListAsync(ct);
        context.DataPointTemplates.RemoveRange(dps);
        context.DeviceTemplates.Remove(entity);
        await context.SaveChangesAsync(ct);
    }

    #endregion

    #region 报警模板 CRUD

    public async Task<List<AlarmTemplate>> LoadAllAlarmTemplatesAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entities = await context.AlarmTemplates.OrderBy(e => e.Name).ToListAsync(ct);
        return entities.Select(ConvertToAlarmTemplate).ToList();
    }

    public async Task SaveAlarmTemplateAsync(AlarmTemplate template, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var existing = await context.AlarmTemplates
            .FirstOrDefaultAsync(e => e.TemplateId == template.TemplateId, ct);
        if (existing is not null)
        {
            UpdateAlarmEntity(existing, template);
        }
        else
        {
            context.AlarmTemplates.Add(ConvertToAlarmEntity(template, false));
        }
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAlarmTemplateAsync(string templateId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.AlarmTemplates
            .FirstOrDefaultAsync(e => e.TemplateId == templateId, ct);
        if (entity is null) return;
        if (entity.IsBuiltIn) throw new InvalidOperationException("内置报警模板不可删除");
        context.AlarmTemplates.Remove(entity);
        await context.SaveChangesAsync(ct);
    }

    #endregion

    #region 趋势模板 CRUD

    public async Task<List<TrendTemplate>> LoadAllTrendTemplatesAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entities = await context.TrendTemplates.OrderBy(e => e.Name).ToListAsync(ct);
        return entities.Select(ConvertToTrendTemplate).ToList();
    }

    public async Task SaveTrendTemplateAsync(TrendTemplate template, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var existing = await context.TrendTemplates
            .FirstOrDefaultAsync(e => e.TemplateId == template.TemplateId, ct);
        if (existing is not null)
        {
            UpdateTrendEntity(existing, template);
        }
        else
        {
            context.TrendTemplates.Add(ConvertToTrendEntity(template, false));
        }
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteTrendTemplateAsync(string templateId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entity = await context.TrendTemplates
            .FirstOrDefaultAsync(e => e.TemplateId == templateId, ct);
        if (entity is null) return;
        if (entity.IsBuiltIn) throw new InvalidOperationException("内置趋势模板不可删除");
        context.TrendTemplates.Remove(entity);
        await context.SaveChangesAsync(ct);
    }

    #endregion

    #region 转换方法

    private static DataPointTemplate ConvertToDataPointTemplate(DataPointTemplateEntity entity,
        Dictionary<string, AlarmTemplateEntity> alarmDict,
        Dictionary<string, TrendTemplateEntity> trendDict)
    {
        AlarmTemplate? alarm = null;
        if (!string.IsNullOrEmpty(entity.AlarmTemplateId) &&
            alarmDict.TryGetValue(entity.AlarmTemplateId, out var alarmEntity))
        {
            alarm = ConvertToAlarmTemplate(alarmEntity);
        }

        TrendTemplate? trend = null;
        if (!string.IsNullOrEmpty(entity.TrendTemplateId) &&
            trendDict.TryGetValue(entity.TrendTemplateId, out var trendEntity))
        {
            trend = ConvertToTrendTemplate(trendEntity);
        }

        return new DataPointTemplate
        {
            TemplateId = entity.TemplateId,
            Name = entity.Name,
            DataType = (TagDataType)entity.DataType,
            Unit = entity.Unit,
            AlarmTemplate = alarm,
            TrendTemplate = trend
        };
    }

    private static AlarmTemplate ConvertToAlarmTemplate(AlarmTemplateEntity e) => new()
    {
        TemplateId = e.TemplateId,
        Name = e.Name,
        ApplicableDataType = (TagDataType)e.ApplicableDataType,
        Unit = e.Unit,
        HighThreshold = e.HighThreshold,
        HighHighThreshold = e.HighHighThreshold,
        LowThreshold = e.LowThreshold,
        LowLowThreshold = e.LowLowThreshold,
        Hysteresis = e.Hysteresis,
        Severity = (AlarmSeverity)e.Severity,
        CooldownSeconds = e.CooldownSeconds,
        SupportedAlarmTypes = JsonSerializer.Deserialize<int[]>(e.SupportedAlarmTypesJson, s_jsonOptions)
            ?.Select(i => (AlarmType)i).ToArray() ?? []
    };

    private static AlarmTemplateEntity ConvertToAlarmEntity(AlarmTemplate t, bool isBuiltIn) => new()
    {
        TemplateId = t.TemplateId,
        Name = t.Name,
        ApplicableDataType = (byte)t.ApplicableDataType,
        Unit = t.Unit,
        HighThreshold = t.HighThreshold,
        HighHighThreshold = t.HighHighThreshold,
        LowThreshold = t.LowThreshold,
        LowLowThreshold = t.LowLowThreshold,
        Hysteresis = t.Hysteresis,
        Severity = (byte)t.Severity,
        CooldownSeconds = t.CooldownSeconds,
        SupportedAlarmTypesJson = JsonSerializer.Serialize(t.SupportedAlarmTypes.Select(x => (int)x), s_jsonOptions),
        IsBuiltIn = isBuiltIn,
        CreatedAt = DateTime.UtcNow
    };

    private static void UpdateAlarmEntity(AlarmTemplateEntity e, AlarmTemplate t)
    {
        e.Name = t.Name;
        e.ApplicableDataType = (byte)t.ApplicableDataType;
        e.Unit = t.Unit;
        e.HighThreshold = t.HighThreshold;
        e.HighHighThreshold = t.HighHighThreshold;
        e.LowThreshold = t.LowThreshold;
        e.LowLowThreshold = t.LowLowThreshold;
        e.Hysteresis = t.Hysteresis;
        e.Severity = (byte)t.Severity;
        e.CooldownSeconds = t.CooldownSeconds;
        e.SupportedAlarmTypesJson = JsonSerializer.Serialize(t.SupportedAlarmTypes.Select(x => (int)x), s_jsonOptions);
    }

    private static TrendTemplate ConvertToTrendTemplate(TrendTemplateEntity e) => new()
    {
        TemplateId = e.TemplateId,
        Name = e.Name,
        Unit = e.Unit,
        YMin = e.YMin ?? double.NaN,
        YMax = e.YMax ?? double.NaN,
        BufferCapacity = e.BufferCapacity,
        WindowSeconds = e.WindowSeconds,
        LineColor = e.LineColor,
        ShowAlarmLines = e.ShowAlarmLines,
        StrokeThickness = e.StrokeThickness,
        ShowGeometry = e.ShowGeometry
    };

    private static TrendTemplateEntity ConvertToTrendEntity(TrendTemplate t, bool isBuiltIn) => new()
    {
        TemplateId = t.TemplateId,
        Name = t.Name,
        Unit = t.Unit,
        YMin = double.IsNaN(t.YMin) ? null : t.YMin,
        YMax = double.IsNaN(t.YMax) ? null : t.YMax,
        BufferCapacity = t.BufferCapacity,
        WindowSeconds = t.WindowSeconds,
        LineColor = t.LineColor,
        ShowAlarmLines = t.ShowAlarmLines,
        StrokeThickness = t.StrokeThickness,
        ShowGeometry = t.ShowGeometry,
        IsBuiltIn = isBuiltIn,
        CreatedAt = DateTime.UtcNow
    };

    private static void UpdateTrendEntity(TrendTemplateEntity e, TrendTemplate t)
    {
        e.Name = t.Name;
        e.Unit = t.Unit;
        e.YMin = double.IsNaN(t.YMin) ? null : t.YMin;
        e.YMax = double.IsNaN(t.YMax) ? null : t.YMax;
        e.BufferCapacity = t.BufferCapacity;
        e.WindowSeconds = t.WindowSeconds;
        e.LineColor = t.LineColor;
        e.ShowAlarmLines = t.ShowAlarmLines;
        e.StrokeThickness = t.StrokeThickness;
        e.ShowGeometry = t.ShowGeometry;
    }

    #endregion
}

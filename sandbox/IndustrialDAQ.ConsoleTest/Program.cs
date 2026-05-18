// File: Program.cs  Module: Console Demo  Author: IndustrialDAQ Team
using IndustrialDAQ.Acquisition;
using IndustrialDAQ.Acquisition.Mocks;
using IndustrialDAQ.Alarm;
using IndustrialDAQ.Core;
using IndustrialDAQ.Core.Configuration;
using IndustrialDAQ.Core.Interfaces;
using IndustrialDAQ.Core.Models;
using IndustrialDAQ.Infrastructure;
using IndustrialDAQ.Processing;
using IndustrialDAQ.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

// ──────────── 0. 命令行参数解析 ────────────
string configFile = args.Length > 0 ? args[0] : "config/modbus-test.json";
string driverOverride = args.Length > 1 ? args[1] : "";  // 可覆盖 JSON 中的驱动类型

// 自动查找配置文件：依次搜索 CWD、项目目录、解决方案根目录
if (!File.Exists(configFile))
{
    string? resolved = ResolveConfigPath(configFile);
    if (resolved is not null)
        configFile = resolved;
    else
    {
        Console.WriteLine($"错误: 配置文件不存在: {configFile}");
        Console.WriteLine("用法: dotnet run -- [config-file] [driver-type]");
        Console.WriteLine("示例: dotnet run -- config/production-line.json OpcUA");
        Console.WriteLine("      dotnet run -- config/production-line.json Modbus");
        Console.WriteLine("      dotnet run -- config/production-line.json S7");
        return 1;
    }
}

// 向上查找项目根目录（行走至找到 .slnx 或 config/ 目录）
static string? ResolveConfigPath(string relativePath)
{
    var dir = AppContext.BaseDirectory;
    for (int i = 0; i < 8; i++)
    {
        string candidate = Path.Combine(dir, relativePath);
        if (File.Exists(candidate)) return candidate;
        dir = Path.GetDirectoryName(dir);
        if (dir is null) break;
    }
    return null;
}

// ──────────── 1. Serilog 配置 ────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    // ──────────── 2. 加载 JSON 配置 ────────────
    List<DeviceConfig> deviceConfigs = await DeviceConfigurationLoader.LoadFromFileAsync(configFile);

    if (!string.IsNullOrEmpty(driverOverride))
    {
        foreach (var cfg in deviceConfigs)
            cfg.GetType().GetProperty("DriverType")?.SetValue(cfg, driverOverride);
        Log.Information("驱动类型已覆盖为: {Driver}", driverOverride);
    }

    Log.Information("已加载 {Count} 台设备配置", deviceConfigs.Count);
    foreach (DeviceConfig dc in deviceConfigs)
    {
        int rCount = dc.Tags.Count(t => t.Access == TagAccess.Read);
        int wCount = dc.Tags.Count(t => t.Access == TagAccess.Write);
        Log.Information("  {Name} [{DriverType}] — {TagCount} 个测点 (读:{ReadCount}, 写:{WriteCount})",
            dc.Name, dc.DriverType, dc.Tags.Count, rCount, wCount);
    }

    // ──────────── 3. DI 容器 & 托管服务 ────────────
    IHost host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((ctx, services) =>
        {
            services.AddSingleton<AcquisitionChannel>();
            services.AddSingleton<IDriverFactory, DriverFactory>();
            services.AddSingleton<RealTimeStore>();
            services.AddSingleton<AlarmEventBus>();
            services.AddSingleton<DataProcessor>();
            services.AddSingleton<AlarmEngine>();
            services.AddSingleton<AlarmHistoryRepository>();

            services.AddDbContextFactory<DaqDbContext>(options =>
                options.UseSqlite("Data Source=industrialdaq.db"));

            services.AddSingleton<AcquisitionHost>();
            services.AddHostedService(sp => sp.GetRequiredService<AcquisitionHost>());

            services.AddSingleton<HistoryWriter>();
            services.AddHostedService(sp => sp.GetRequiredService<HistoryWriter>());

            services.AddHostedService(sp => sp.GetRequiredService<DataProcessor>());
            services.AddHostedService(sp => sp.GetRequiredService<AlarmEngine>());
        })
        .Build();

    // ──────────── 4. 初始化数据库 ────────────
    {
        var dbFactory = host.Services.GetRequiredService<IDbContextFactory<DaqDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
    }

    // ──────────── 5. 注册所有真实驱动 ────────────
    IDriverFactory driverFactory = host.Services.GetRequiredService<IDriverFactory>();

    // Modbus TCP 驱动
    driverFactory.RegisterDriver("Modbus", (config, ct) =>
    {
        var driver = new Drivers.Modbus.ModbusTcpDriver(config);
        return Task.FromResult<IProtocolDriver>(driver);
    });

    // OPC UA 驱动
    driverFactory.RegisterDriver("OpcUA", (config, ct) =>
    {
        var driver = new Drivers.OpcUA.OpcUaDriver(config);
        return Task.FromResult<IProtocolDriver>(driver);
    });

    // Siemens S7 驱动
    driverFactory.RegisterDriver("S7", (config, ct) =>
    {
        var driver = new IndustrialDAQ.Drivers.S7.S7Driver(config);
        return Task.FromResult<IProtocolDriver>(driver);
    });

    // Mock 驱动 (回退)
    driverFactory.RegisterDriver("Mock", (config, ct) =>
    {
        var driver = new MockProtocolDriver();
        return Task.FromResult<IProtocolDriver>(driver);
    });

    Log.Information("已注册 {Count} 种协议驱动", driverFactory.RegisteredDriverTypes.Count);
    foreach (string dt in driverFactory.RegisteredDriverTypes)
        Log.Information("  - {DriverType}", dt);

    // ──────────── 6. 启动宿主 ────────────
    await host.StartAsync();

    AcquisitionHost acquisitionHost = host.Services.GetRequiredService<AcquisitionHost>();
    HistoryWriter historyWriter = host.Services.GetRequiredService<HistoryWriter>();
    RealTimeStore realTimeStore = host.Services.GetRequiredService<RealTimeStore>();
    DataProcessor dataProcessor = host.Services.GetRequiredService<DataProcessor>();
    AlarmEngine alarmEngine = host.Services.GetRequiredService<AlarmEngine>();

    // ──────────── 7. 配置计算规则 ────────────
    dataProcessor.RegisterRules(new[]
    {
        new CalculationRule
        {
            RuleId = "calc-001",
            Expression = "Filling_ActualLevel / Filling_SetLevel * 100",
            InputTagNames = new List<string> { "Filling.ActualLevel", "Filling.SetLevel" },
            TargetTagId = "calc-fill-percent",
            TargetTagName = "Filling.FillPercent",
            TargetDataType = TagDataType.Float64
        }
    });

    // ──────────── 8. 配置报警规则 ────────────
    alarmEngine.RegisterRules(new[]
    {
        // 灌装液位高限报警
        new AlarmDefinition
        {
            RuleId = "alm-fill-high",
            AlarmCode = "FILL_LEVEL_HIGH",
            TagId = "tag-filling-actuallevel",
            TagName = "Filling.ActualLevel",
            AlarmType = AlarmType.High,
            ConditionExpression = "Value >= 700",
            Hysteresis = 30.0,
            Severity = AlarmSeverity.Warning,
            Title = "灌装液位偏高",
            MessageTemplate = "灌装液位 {Value} mL 超过 {Threshold} mL",
            Source = "灌装产线 S7-1500",
            CooldownSeconds = 15
        },
        // 灌装液位高高限报警
        new AlarmDefinition
        {
            RuleId = "alm-fill-highhigh",
            AlarmCode = "FILL_LEVEL_HIGH_HIGH",
            TagId = "tag-filling-actuallevel",
            TagName = "Filling.ActualLevel",
            AlarmType = AlarmType.HighHigh,
            ConditionExpression = "Value >= 800",
            Hysteresis = 30.0,
            Severity = AlarmSeverity.Critical,
            Title = "灌装液位超高（溢出风险）",
            MessageTemplate = "灌装液位 {Value} mL 超过高高限 {Threshold} mL！",
            Source = "灌装产线 S7-1500",
            CooldownSeconds = 10
        },
        // 传送速度高限报警
        new AlarmDefinition
        {
            RuleId = "alm-speed-high",
            AlarmCode = "CONVEYOR_SPEED_HIGH",
            TagId = "tag-conveyor-actualspeed",
            TagName = "Conveyor.ActualSpeed",
            AlarmType = AlarmType.High,
            ConditionExpression = "Value > 25",
            Hysteresis = 2.0,
            Severity = AlarmSeverity.Warning,
            Title = "传送速度偏高",
            MessageTemplate = "传送速度 {Value} m/min 超过 {Threshold} m/min",
            Source = "灌装产线 S7-1500",
            CooldownSeconds = 15
        }
    });

    // ──────────── 9. 启动设备采集 ────────────
    foreach (DeviceConfig config in deviceConfigs)
    {
        // 注册测点到 HistoryWriter
        var readableTags = config.Tags.Where(t => t.Access != TagAccess.Write).ToList();
        historyWriter.RegisterTags(readableTags);

        try
        {
            await acquisitionHost.StartDeviceAsync(config);
            Log.Information("设备 {Name} 采集已启动", config.Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "设备 {Name} 启动失败 (驱动: {Driver}, IP: {IP})",
                config.Name, config.DriverType, config.IpAddress);
            Log.Warning("提示: 如无真实 PLC，可使用 Mock 驱动测试:");
            Log.Warning("  dotnet run -- {Config} Mock", configFile);
        }
    }

    // ──────────── 10. 实时数据观察者 ────────────
    var observerCts = new CancellationTokenSource();
    _ = Task.Run(async () =>
    {
        try
        {
            var reader = realTimeStore.Subscribe();
            await foreach (TagValue value in reader.ReadAllAsync(observerCts.Token))
            {
                string access = value.TagName.Contains(".Set") || value.TagName.Contains(".Start")
                    || value.TagName.Contains(".Stop") || value.TagName.Contains(".AutoMode")
                    || value.TagName.Contains(".EStop") ? "[写入]" : "[读取]";
                Log.Information("{Access} {TagName} = {Value} [{Quality}]",
                    access, value.TagName, value.Value, value.Quality);
            }
        }
        catch (OperationCanceledException) { }
    });

    // ──────────── 11. 报警观察者 ────────────
    var alarmObserverCts = new CancellationTokenSource();
    _ = Task.Run(async () =>
    {
        var alarmEventBus = host.Services.GetRequiredService<AlarmEventBus>();
        try
        {
            await foreach (AlarmEvent alarmEvent in alarmEventBus.Subscribe(alarmObserverCts.Token))
            {
                string sev = alarmEvent.Rule.Severity switch
                {
                    AlarmSeverity.Critical => "严重",
                    AlarmSeverity.Warning => "警告",
                    _ => "信息"
                };
                string state = alarmEvent.State switch
                {
                    AlarmState.Active => "触发",
                    AlarmState.Acknowledged => "已确认",
                    AlarmState.Normal => "已恢复",
                    _ => "未知"
                };
                Console.ForegroundColor = alarmEvent.Rule.Severity == AlarmSeverity.Critical
                    ? ConsoleColor.Red : ConsoleColor.Yellow;
                Console.WriteLine($"[报警] [{sev}] [{state}] {alarmEvent.Rule.Title} — 值={alarmEvent.TriggerValue:F2}");
                Console.ResetColor();
            }
        }
        catch (OperationCanceledException) { }
    });

    // ──────────── 12. 交互式写入菜单 ────────────
    Log.Information("═══════════════════════════════════════════");
    Log.Information("  灌装产线数据采集系统");
    Log.Information("  输入命令控制产线 (输入 'help' 查看帮助)");
    Log.Information("═══════════════════════════════════════════");

    var writeCts = new CancellationTokenSource();
    _ = Task.Run(async () =>
    {
        var writeTargets = deviceConfigs
            .SelectMany(dc => dc.Tags.Where(t => t.Access != TagAccess.Read))
            .ToList();

        await Task.Yield();

        while (!writeCts.Token.IsCancellationRequested)
        {
            Console.Write("\n> ");
            string? line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLowerInvariant();

            try
            {
                switch (cmd)
                {
                    case "help":
                        Console.WriteLine("═══ 写标签列表 ═══");
                        foreach (var t in writeTargets)
                            Console.WriteLine($"  {t.Name} — {t.Description} (类型: {t.DataType})");
                        Console.WriteLine();
                        Console.WriteLine("═══ 命令 ═══");
                        Console.WriteLine("  write <TagName> <Value>  — 写入标签值");
                        Console.WriteLine("  read                     — 立即读取所有标签");
                        Console.WriteLine("  status                   — 显示当前所有测点值");
                        Console.WriteLine("  exit / quit              — 退出程序");
                        Console.WriteLine();
                        Console.WriteLine("═══ 示例 ═══");
                        Console.WriteLine("  write Line.Start true");
                        Console.WriteLine("  write Filling.SetLevel 500.0");
                        Console.WriteLine("  write Conveyor.SetSpeed 15.5");
                        Console.WriteLine("  write Line.AutoMode true");
                        Console.WriteLine("  write Line.Stop true");
                        break;

                    case "write":
                        if (parts.Length < 3)
                        {
                            Console.WriteLine("用法: write <TagName> <Value>");
                            break;
                        }
                        string tagName = parts[1];
                        string valueStr = parts[2];

                        var target = writeTargets.FirstOrDefault(t =>
                            t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
                        if (target is null)
                        {
                            Console.WriteLine($"错误: 找不到可写标签 '{tagName}'");
                            break;
                        }

                        object writeValue = target.DataType switch
                        {
                            TagDataType.Bool => bool.Parse(valueStr),
                            TagDataType.Int16 => short.Parse(valueStr),
                            TagDataType.Int32 => int.Parse(valueStr),
                            TagDataType.Float32 => float.Parse(valueStr),
                            TagDataType.Float64 => double.Parse(valueStr),
                            TagDataType.UInt16 => ushort.Parse(valueStr),
                            TagDataType.UInt32 => uint.Parse(valueStr),
                            TagDataType.String => valueStr,
                            _ => valueStr
                        };

                        var parentDevice = deviceConfigs.FirstOrDefault(dc =>
                            dc.Tags.Any(t => t.Id == target.Id));
                        if (parentDevice is null)
                        {
                            Console.WriteLine("错误: 找不到标签所属设备");
                            break;
                        }

                        var driver = acquisitionHost.GetDriver(parentDevice.Id);
                        if (driver is not null)
                        {
                            await driver.WriteTagAsync(target, writeValue, CancellationToken.None);
                            Console.WriteLine($"已写入 {target.Name} = {writeValue}");
                        }
                        else
                        {
                            Console.WriteLine($"错误: 设备 {parentDevice.Name} 未启动或未连接");
                        }
                        break;

                    case "read":
                    case "status":
                        Console.WriteLine("═══ 当前测点值 ═══");
                        Console.WriteLine($"测点总数: {realTimeStore.Count}");
                        foreach (TagValue tv in realTimeStore.GetAll().OrderBy(x => x.TagName))
                        {
                            Console.WriteLine($"  {tv.TagName} = {tv.Value} [{tv.Quality}]");
                        }
                        Console.WriteLine("══════════════");
                        break;

                    case "exit":
                    case "quit":
                        writeCts.Cancel();
                        break;

                    default:
                        Console.WriteLine($"未知命令: {cmd}，输入 'help' 查看帮助");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"命令执行失败: {ex.Message}");
            }
        }
    });

    // ──────────── 13. 等待退出信号 ────────────
    try
    {
        await Task.Delay(Timeout.Infinite, writeCts.Token);
    }
    catch (OperationCanceledException) { }

    // ──────────── 14. 统计与优雅停止 ────────────
    Log.Information("═══════════════════════════════════════════");
    Log.Information("  运行统计");
    Log.Information("  实时库测点数: {Count}", realTimeStore.Count);
    foreach (TagValue tv in realTimeStore.GetAll().OrderBy(x => x.TagName))
        Log.Information("    {TagName} = {Value}", tv.TagName, tv.Value);
    {
        var dbFactory = host.Services.GetRequiredService<IDbContextFactory<DaqDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        int historyCount = await db.HistoricalRecords.CountAsync();
        Log.Information("  历史库记录数: {Count}", historyCount);
    }
    Log.Information("═══════════════════════════════════════════");

    Log.Information("演示结束，优雅关闭...");
    observerCts.Cancel();
    alarmObserverCts.Cancel();
    await host.StopAsync();

    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "演示程序异常终止");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

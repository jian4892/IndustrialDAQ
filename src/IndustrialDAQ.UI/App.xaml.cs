// File: App.xaml.cs  Module: UI (Composition Root)  Author: IndustrialDAQ Team
using IndustrialDAQ.Acquisition;
using IndustrialDAQ.Acquisition.Mocks;
using IndustrialDAQ.Alarm;
using IndustrialDAQ.Core;
using IndustrialDAQ.Core.Configuration;
using IndustrialDAQ.Core.Interfaces;
using IndustrialDAQ.Core.Models;
using IndustrialDAQ.Infrastructure;
using IndustrialDAQ.Storage;
using IndustrialDAQ.UI.ViewModels;
using IndustrialDAQ.UI.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Navigation.Regions;
using Serilog;
using Serilog.Events;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace IndustrialDAQ.UI;

public partial class App : PrismApplication
{
    private FileSystemWatcher? _configWatcher;
    private CancellationTokenSource? _debounceCts;
    protected override Window CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<AcquisitionChannel>();
        containerRegistry.RegisterSingleton<IDriverFactory, DriverFactory>();
        containerRegistry.RegisterSingleton<RealTimeStore>();
        containerRegistry.RegisterSingleton<AcquisitionHost>();
        containerRegistry.RegisterSingleton<HistoryWriter>();
        containerRegistry.RegisterSingleton<MainWindowViewModel>();

        // 报警系统服务
        containerRegistry.RegisterSingleton<AlarmEventBus>();
        containerRegistry.RegisterSingleton<AlarmEngine>();
        containerRegistry.RegisterSingleton<AlarmHistoryRepository>();
        containerRegistry.RegisterSingleton<AlarmManager>();

        // ViewModel 注册（支持构造函数注入）
        containerRegistry.Register<AlarmRecordViewModel>();

        containerRegistry.RegisterForNavigation<DashboardView>();
        containerRegistry.RegisterForNavigation<ProductionMonitorView>();
        containerRegistry.RegisterForNavigation<DeviceDetailView>();
        containerRegistry.RegisterForNavigation<AlarmRecordView>();
        containerRegistry.RegisterForNavigation<SystemSettingsView>();

        containerRegistry.RegisterDialogWindow<FramelessDialogWindow>();
        containerRegistry.RegisterDialog<WriteTagDialog, WriteTagDialogViewModel>();
    }

    protected override IContainerExtension CreateContainerExtension()
    {
        var extension = new DryIocContainerExtension();
        var services = new ServiceCollection();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()  // 全局 Information
            .MinimumLevel.Override("IndustrialDAQ", LogEventLevel.Debug)  // 项目代码保持 Debug
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Query", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.ChangeTracking", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Update", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Migrations", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Model", LogEventLevel.Warning)
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        services.AddLogging(builder => {
            builder.AddSerilog();
        });

        services.AddDbContextFactory<DaqDbContext>(options =>
            options.UseSqlite("Data Source=industrialdaq.db"));

        extension.Populate(services);

        return extension;
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        // ── 初始化数据库 ──
        var dbFactory = Container.Resolve<IDbContextFactory<DaqDbContext>>();
        using (var db = dbFactory.CreateDbContext())
        {
            db.Database.EnsureCreated();           
        }

        // ── 注册所有协议驱动 ──
        var driverFactory = Container.Resolve<IDriverFactory>();

        driverFactory.RegisterDriver("Modbus", (config, ct) =>
        {
            var driver = new global::Drivers.Modbus.ModbusTcpDriver(config);
            return Task.FromResult<IProtocolDriver>(driver);
        });

        driverFactory.RegisterDriver("OpcUA", (config, ct) =>
        {
            var driver = new global::Drivers.OpcUA.OpcUaDriver(config);
            return Task.FromResult<IProtocolDriver>(driver);
        });

        driverFactory.RegisterDriver("S7", (config, ct) =>
        {
            var driver = new IndustrialDAQ.Drivers.S7.S7Driver(config);
            return Task.FromResult<IProtocolDriver>(driver);
        });

        driverFactory.RegisterDriver("Mock", (config, ct) =>
        {
            var driver = new MockProtocolDriver();
            return Task.FromResult<IProtocolDriver>(driver);
        });

        // ── 启动采集宿主和历史写入器 ──
        var acquisitionHost = Container.Resolve<AcquisitionHost>();
        var historyWriter = Container.Resolve<HistoryWriter>();

        _ = acquisitionHost.StartAsync(CancellationToken.None);
        _ = historyWriter.StartAsync(CancellationToken.None);

        // ── 启动报警系统 ──
        var alarmEngine = Container.Resolve<AlarmEngine>();
        var alarmManager = Container.Resolve<AlarmManager>();
        _ = alarmEngine.StartAsync(CancellationToken.None);
        _ = alarmManager.StartAsync(CancellationToken.None);

        // ── 注册测试报警规则 ──
        RegisterTestAlarmRules(alarmManager);

        // ── 加载 JSON 配置并启动设备 ──
        _ = LoadAndStartDevicesAsync(acquisitionHost, historyWriter);

        // ── 导航到仪表板 ──
        var regionManager = Container.Resolve<IRegionManager>();
        regionManager.RequestNavigate("MainRegion", nameof(DashboardView));
    }

    /// <summary>
    /// 注册测试报警规则 — 用于开发调试。
    /// TagId 匹配 production-line.json 中的实际配置。
    /// </summary>
    private void RegisterTestAlarmRules(AlarmManager alarmManager)
    {
        var rules = new[]
        {
            // ═══ S7-1500 PLC (OpcUA) 报警规则 ═══

            // 高限报警 — 灌装液位达到或超过 700 mL
            new AlarmRule
            {
                RuleId = "alm-fill-high",
                TagId = "tag-filling-actuallevel",
                TagName = "Filling.ActualLevel",
                AlarmType = AlarmType.High,
                Threshold = 699.0,  // >= 700 报警（使用 699 配合 > 判断）
                Hysteresis = 30.0,  // 报警后需降到 670 以下才恢复
                Severity = AlarmSeverity.Warning,
                Title = "灌装液位偏高",
                MessageTemplate = "灌装液位 {Value} mL 达到警戒线",
                Source = "灌装产线 S7-1500",
                CooldownSeconds = 60  // 60秒冷却，防止重复报警
            },
            // 高高限报警 — 灌装液位达到或超过 800 mL（溢出风险）
            new AlarmRule
            {
                RuleId = "alm-fill-highhigh",
                TagId = "tag-filling-actuallevel",
                TagName = "Filling.ActualLevel",
                AlarmType = AlarmType.HighHigh,
                Threshold = 799.0,  // >= 800 报警
                HighHighThreshold = 799.0,
                Hysteresis = 30.0,  // 报警后需降到 770 以下才恢复
                Severity = AlarmSeverity.Critical,
                Title = "灌装液位超高（溢出风险）",
                MessageTemplate = "灌装液位 {Value} mL 超过高高限，有溢出风险！",
                Source = "灌装产线 S7-1500",
                CooldownSeconds = 60  // 60秒冷却，防止重复报警
            },
            // 高限报警 — 传送速度超过 25 m/min
            new AlarmRule
            {
                RuleId = "alm-speed-high",
                TagId = "tag-conveyor-actualspeed",
                TagName = "Conveyor.ActualSpeed",
                AlarmType = AlarmType.High,
                Threshold = 25.0,
                Hysteresis = 2.0,  // >25 报警, <23 恢复
                Severity = AlarmSeverity.Warning,
                Title = "传送速度偏高",
                MessageTemplate = "传送速度 {Value} m/min 超过 {Threshold} m/min",
                Source = "灌装产线 S7-1500",
                CooldownSeconds = 15
            },
            // 低限报警 — 传送速度低于 5 m/min
            new AlarmRule
            {
                RuleId = "alm-speed-low",
                TagId = "tag-conveyor-actualspeed",
                TagName = "Conveyor.ActualSpeed",
                AlarmType = AlarmType.Low,
                Threshold = 5.0,
                Hysteresis = 2.0,  // <5 报警, >7 恢复
                Severity = AlarmSeverity.Warning,
                Title = "传送速度偏低",
                MessageTemplate = "传送速度 {Value} m/min 低于 {Threshold} m/min",
                Source = "灌装产线 S7-1500",
                CooldownSeconds = 15
            },
            // 布尔报警 — 急停按钮触发
            new AlarmRule
            {
                RuleId = "alm-estop",
                TagId = "tag-line-estop",
                TagName = "Line.EStop",
                AlarmType = AlarmType.Bool,
                Severity = AlarmSeverity.Critical,
                Title = "急停按钮已触发",
                MessageTemplate = "产线急停按钮已按下，请立即检查！",
                Source = "灌装产线 S7-1500",
                CooldownSeconds = 5
            }
        };

        alarmManager.RegisterRules(rules);
        Log.Information("已注册 {Count} 条测试报警规则", rules.Length);
    }

    /// <summary>
    /// 从 JSON 配置文件加载设备并启动采集。
    /// </summary>
    private async Task LoadAndStartDevicesAsync(AcquisitionHost host, HistoryWriter writer)
    {
        try
        {
            string configPath = FindConfigFile("config/production-line.json");
            if (!File.Exists(configPath))
            {
                Log.Warning("配置文件 {Path} 不存在，使用 Mock 设备", configPath);
                StartMockDevices(host, writer);
                return;
            }

            var deviceConfigs = await DeviceConfigurationLoader.LoadFromFileAsync(configPath);
            foreach (var config in deviceConfigs)
            {
                var readableTags = config.Tags.ToList();
                writer.RegisterTags(readableTags);

                try
                {
                    await host.StartDeviceAsync(config);
                    Log.Information("设备 {Name} [{DriverType}] 采集已启动", config.Name, config.DriverType);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "设备 {Name} [{DriverType}] 启动失败: {Message}",
                        config.Name, config.DriverType, ex.Message);
                    Log.Warning("提示: 如无真实 PLC 设备，请检查 IP 地址和网络连接");
                }
            }

            // 启动文件监听
            StartConfigurationWatcher(configPath, host, writer);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载配置文件失败，使用 Mock 设备");
            StartMockDevices(host, writer);
        }
    }

    /// <summary>
    /// 回退方案：创建 2 台模拟设备用于 UI 调试。
    /// </summary>
    private void StartMockDevices(AcquisitionHost host, HistoryWriter writer)
    {
        var device1 = new DeviceConfig
        {
            Id = "device-001",
            Name = "反应釜 #1",
            DriverType = "Mock",
            IpAddress = "192.168.1.101",
            Port = 502,
            CycleTimeMs = 500,
            TimeoutMs = 3000,
            RetryCount = 3,
            Tags = new List<TagPoint>
            {
                new() { Id = "tag-001", Name = "Temp_Reactor_01",  Address = "40001", DataType = TagDataType.Float32 },
                new() { Id = "tag-002", Name = "Pressure_Reactor_01", Address = "40003", DataType = TagDataType.Float32 },
                new() { Id = "tag-003", Name = "Valve_Status_01",   Address = "00001", DataType = TagDataType.Bool }
            }
        };

        var device2 = new DeviceConfig
        {
            Id = "device-002",
            Name = "锅炉 #3",
            DriverType = "Mock",
            IpAddress = "192.168.1.102",
            Port = 502,
            CycleTimeMs = 1000,
            TimeoutMs = 3000,
            RetryCount = 3,
            Tags = new List<TagPoint>
            {
                new() { Id = "tag-004", Name = "Temp_Boiler_03", Address = "40001", DataType = TagDataType.Float32 },
                new() { Id = "tag-005", Name = "Flow_Boiler_03",  Address = "40003", DataType = TagDataType.Float32 }
            }
        };

        writer.RegisterTags(device1.Tags);
        writer.RegisterTags(device2.Tags);

        _ = host.StartDeviceAsync(device1);
        _ = host.StartDeviceAsync(device2);
    }

    /// <summary>
    /// 向上查找配置文件（从 AppContext.BaseDirectory 开始）。
    /// </summary>
    private static string FindConfigFile(string relativePath)
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            string candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
            if (dir is null) break;
        }
        return relativePath; // 返回原始路径，由调用方检查是否存在
    }

    private void StartConfigurationWatcher(string configPath, AcquisitionHost host, HistoryWriter writer)
    {
        var directory = Path.GetDirectoryName(configPath);
        var fileName = Path.GetFileName(configPath);

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return;

        // 监听整个目录的 *.json，防止 IDE (如 VS/VSCode) 使用“安全保存”（先写临时文件后重命名替换）导致丢失事件
        _configWatcher = new FileSystemWatcher(directory, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        FileSystemEventHandler handler = (s, e) =>
        {
            if (string.Equals(e.Name, fileName, StringComparison.OrdinalIgnoreCase))
            {
                OnConfigurationChanged(configPath, host, writer);
            }
        };

        RenamedEventHandler renamedHandler = (s, e) =>
        {
            if (string.Equals(e.Name, fileName, StringComparison.OrdinalIgnoreCase))
            {
                OnConfigurationChanged(configPath, host, writer);
            }
        };

        _configWatcher.Changed += handler;
        _configWatcher.Created += handler;
        _configWatcher.Renamed += renamedHandler;
    }

    private void OnConfigurationChanged(string configPath, AcquisitionHost host, HistoryWriter writer)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, token); // 防抖 500ms
                if (!token.IsCancellationRequested)
                {
                    Log.Information("检测到配置文件变更，正在重新加载...");
                    await ReloadConfigurationAsync(configPath, host, writer);
                }
            }
            catch (TaskCanceledException) { /* 预期内取消 */ }
        }, token);
    }

    private async Task ReloadConfigurationAsync(string configPath, AcquisitionHost host, HistoryWriter writer)
    {
        try
        {
            if (!File.Exists(configPath)) return;

            var newConfigs = await DeviceConfigurationLoader.LoadFromFileAsync(configPath);
            var currentDevices = host.GetDevices();
            var newIds = newConfigs.Select(c => c.Id).ToHashSet();
            var currentIds = currentDevices.Select(c => c.Id).ToHashSet();

            // 1. 停止被删除的设备
            var toStop = currentIds.Except(newIds);
            foreach (var id in toStop)
            {
                var oldConfig = currentDevices.First(c => c.Id == id);
                writer.UnregisterTags(oldConfig.Tags.Select(t => t.Id));
                await host.StopDeviceAsync(id);
                Log.Information("动态移除并停止设备 {Name}", oldConfig.Name);
            }

            // 2. 启动新设备
            var toStart = newIds.Except(currentIds);
            foreach (var id in toStart)
            {
                var newConfig = newConfigs.First(c => c.Id == id);
                writer.RegisterTags(newConfig.Tags);
                await host.StartDeviceAsync(newConfig);
                Log.Information("动态添加并启动设备 {Name}", newConfig.Name);
            }

            // 3. 重载已存在且配置发生变化的设备
            var toCheck = currentIds.Intersect(newIds);
            foreach (var id in toCheck)
            {
                var newConfig = newConfigs.First(c => c.Id == id);
                var oldConfig = currentDevices.First(c => c.Id == id);

                // 使用 JSON 序列化简单对比配置是否改变
                var newJson = JsonSerializer.Serialize(newConfig);
                var oldJson = JsonSerializer.Serialize(oldConfig);

                if (newJson != oldJson)
                {
                    writer.UnregisterTags(oldConfig.Tags.Select(t => t.Id));
                    writer.RegisterTags(newConfig.Tags);
                    await host.ReloadDeviceAsync(newConfig);
                    Log.Information("检测到设备 {Name} 配置变更，已重载", newConfig.Name);
                }
            }
            
            // 通知 UI 刷新设备列表
            var eventAggregator = Container.Resolve<IEventAggregator>();
            eventAggregator.GetEvent<IndustrialDAQ.UI.Events.ConfigurationReloadedEvent>().Publish();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "动态重载配置失败");
        }
    }
}

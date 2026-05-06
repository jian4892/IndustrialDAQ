# IndustrialDAQ

IndustrialDAQ 是一款现代化的工业级数据采集与监控终端 (SCADA/HMI) 系统。本项目基于 .NET 8 WPF 和 Prism MVVM 架构开发，致力于提供高性能、可扩展且美观的数据采集解决方案。

## 🌟 核心特性

- **现代 UI 架构**：采用深色/浅色全局主题热切换，基于 LiveCharts2 提供高帧率的生产过程趋势可视化。
- **协议解耦设计**：采用插件式驱动架构（支持 Modbus TCP、OPC UA 扩展等），实现软硬件彻底解耦。
- **高效采集引擎**：内置高性能后台采集轮询引擎，支持数据防抖、死区压缩和断线自动重连。
- **配置热加载**：支持 `FileSystemWatcher`，当工业现场的设备或测点配置 (JSON) 变更时，可实现采集通道的无缝热重载（无需重启）。
- **生产监控与分析**：
  - **实时数据看板**：仪表盘（Gauge）与动态折线图（LineSeries）组件，且支持本地状态缓存。
  - **参数下发 (写入)**：无边框弹窗组件，可直接针对具备 Write 权限的测点进行快速指令下发，自带类型推断转换。
  - **警报拦截**：实时拦截异常数据点并做记录展示。

## 💻 技术栈

- **框架**: .NET 8.0, WPF
- **架构**: MVVM, Prism Library (DI/EventAggregator/DialogService)
- **图表**: LiveChartsCore.SkiaSharpView.WPF (使用高性能 Skia 渲染引擎)
- **日志记录**: Serilog
- **通讯驱动**: NModbus (Modbus TCP)

## 📁 项目结构

- **`IndustrialDAQ.UI`**: WPF 用户界面主程序，负责视图、主题和用户交互。
- **`IndustrialDAQ.Core`**: 领域核心，定义实体模型（DeviceConfig, TagPoint, TagValue）、接口和通用契约。
- **`IndustrialDAQ.Acquisition`**: 采集与调度引擎，负责后台数据轮询与存储队列调度。
- **`IndustrialDAQ.Storage`**: 数据存储层，包含基于 Channel 的实时流和历史数据持久化（SQLite/时序库预留）。
- **`Drivers.Modbus`**: Modbus 通讯驱动插件实现。
- **`config/`**: JSON 设备配置文件存放目录。

## 🚀 快速启动

### 1. 运行模拟设备 (Python)
为方便测试，根目录提供了一个简易的 Modbus Slave 模拟器（需要安装 Python 和 Pymodbus 库）：
```bash
pip install pymodbus
python config/python_modbus_slave.py
```
*注：该模拟器默认使用 CDAB 字节序（Word: Little, Byte: Big）以完美匹配 C# 浮点数解析。*

### 2. 编译并运行主程序
使用 Visual Studio 2022 或 JetBrains Rider 打开 `IndustrialDAQ.sln`，设置 `IndustrialDAQ.UI` 为启动项并运行。

## 🎨 主题与界面外观
本项目设计抛弃了传统的灰色原生控件，提供了精调的 `DarkTheme.xaml` 和 `LightTheme.xaml`。内置：
- 亚克力风格的测点状态展示
- 动态路由切换
- 自定义无边框沉浸式窗口

## 📜 许可证

MIT License.

# TrendEngine Runtime

这是一个工业 SCADA Runtime 平台。

技术栈：

- .NET 8
- WPF
- MVVM
- DDD
- DI
- OPC UA
- Modbus Tcp
- MQTT
- SQLite/PostgreSQL

架构原则：

1. 配置驱动
2. 禁止写死资源
3. 所有功能模块化
4. 所有资源树结构化
5. 所有权限动态化
6. 所有规则热更新
7. 所有服务异步化
8. 所有数据事件化

核心架构：

TagManager
 ↓
EventBus
 ↓
RuleEngine
 ↓
AlarmStateMachine
 ↓
AlarmCenter
 ↓
AuthorizationService

禁止：

- if else 报警
- 写死 permissionId
- 写死菜单
- 写死 Tag
- 写死设备树

要求：

所有功能必须支持：

- 热更新
- 动态配置
- Runtime加载
- 权限继承
- ResourcePath

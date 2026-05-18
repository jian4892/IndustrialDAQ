using IndustrialDAQ.Core.ResourceTree;

namespace IndustrialDAQ.Core.Models;

/// <summary>
/// 工业报警定义。
/// 该模型属于配置而非执行逻辑：它描述了报警信息、所属资源、触发/清除/抑制表达式以及操作员处理策略。
/// RuleBuilder 在下一层将该定义转换为运行时的 Rule 工作流。
/// </summary>
public sealed class AlarmDefinition
{
    /// <summary>
    /// 获取报警定义的持久化唯一标识符。
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 运行时规则标识符。为了与现有报警引擎保持兼容，工业配置应将其作为外部可见的规则主键。
    /// </summary>
    public string RuleId { get; init; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>
    /// 工程报警代码，例如 TEMP_HIGH 或 PLC_COMM_LOST。
    /// 它与 RuleId 分开，以便同一个报警代码可以重新生成为不同的工作流版本，而不改变面向操作员的含义。
    /// </summary>
    public string AlarmCode { get; init; } = string.Empty;

    /// <summary>
    /// 该报警定义的资源路径。
    /// 例如：Factory/LineA/PLC1/Temp1/Alarm/TEMP_HIGH。
    /// </summary>
    public ResourcePath? ResourcePath { get; init; }

    /// <summary>
    /// 该报警监控的主标签资源路径。
    /// 例如：Factory/LineA/PLC1/Temp1。
    /// </summary>
    public ResourcePath? TargetResourcePath { get; init; }

    /// <summary>
    /// 当前采集/报警引擎使用的旧版标签 ID。
    /// 未来的 TagManager 集成应从 TargetResourcePath 中解析它。
    /// </summary>
    public string TagId { get; init; } = string.Empty;

    /// <summary>
    /// 在日志和操作员消息中使用的易于阅读的标签名称。
    /// </summary>
    public string TagName { get; init; } = string.Empty;

    /// <summary>
    /// 报警类型，用于显示和默认清除逻辑的生成。
    /// </summary>
    public AlarmType AlarmType { get; init; } = AlarmType.High;

    /// <summary>
    /// 进入报警条件的表达式，例如 Value &gt; 80。
    /// 这是配置文本；它由 RuleBuilder 编译，不由此定义直接计算。
    /// </summary>
    public string ConditionExpression { get; init; } = string.Empty;

    /// <summary>
    /// 可选的结构化条件片段。存在多个片段时，RuleBuilder 使用
    /// ExpressionJoin 将它们组合为触发 Workflow。
    /// ConditionExpression 用于旧版和单条件报警。
    /// </summary>
    public IReadOnlyList<AlarmConditionDefinition> Conditions { get; init; } = [];

    /// <summary>
    /// 可选的显式清除报警表达式。
    /// 如果缺失，RuleBuilder 可能会从触发表达式和迟滞(Hysteresis)中安全地推导出清除条件。
    /// </summary>
    public string? ClearExpression { get; init; }

    /// <summary>
    /// 可选的抑制评估表达式，例如维护模式、设备禁用、过程未运行或联锁激活。
    /// </summary>
    public string? SuppressionExpression { get; init; }

    /// <summary>
    /// 当该定义在元数据或未来的子条件表中包含多个表达式片段时使用的逻辑运算符。
    /// </summary>
    public AlarmExpressionJoin ExpressionJoin { get; init; } = AlarmExpressionJoin.And;

    /// <summary>
    /// 去抖延迟（毫秒）。报警首先进入 Pending 状态，只有在条件保持为 true 达到此时长后才变为 Active。
    /// </summary>
    public int DelayMs { get; init; }

    /// <summary>
    /// 现有报警运行时使用的旧版秒级延迟。
    /// </summary>
    public int DelaySeconds { get; init; }

    /// <summary>
    /// 死区 / 迟滞。RuleBuilder 在推导清除逻辑时使用。
    /// </summary>
    public double Hysteresis { get; init; }

    /// <summary>
    /// 向操作员显示并随报警事件持久化的严重程度。
    /// </summary>
    public AlarmSeverity Severity { get; init; } = AlarmSeverity.Warning;

    /// <summary>
    /// 面向操作员的报警标题。
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 消息模板。支持的占位符包括 {TagName}, {Value}, {Expression}, {AlarmCode}, {ResourcePath} 和 {Delay}。
    /// </summary>
    public string MessageTemplate { get; init; } = string.Empty;

    /// <summary>
    /// 面向操作员的数据源文本。ResourcePath 仍然是权威的来源标识；这仅用于显示。
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// 该报警定义是否在运行时启用。
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 对应工业命名习惯的启用标志。
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// 在最终关闭报警前是否需要操作员确认。
    /// </summary>
    public bool RequireAck { get; init; } = true;

    /// <summary>
    /// 工业报警确认策略。
    /// </summary>
    public AlarmAckPolicy AckPolicy { get; init; } = AlarmAckPolicy.Required;

    /// <summary>
    /// 状态机使用的清除行为策略。
    /// </summary>
    public AlarmClearPolicy ClearPolicy { get; init; } = AlarmClearPolicy.AutoClearWhenConditionFalse;

    /// <summary>
    /// 清除后重新激活前的最小间隔。这是为了防止报警风暴，而不是为了替代延迟。
    /// </summary>
    public int CooldownSeconds { get; init; } = 60;

    /// <summary>
    /// 配置请求的工作流类型。实际的可执行工作流由 RuleBuilder 单独构建和版本化。
    /// </summary>
    public AlarmWorkflowType WorkflowType { get; init; } = AlarmWorkflowType.Expression;

    /// <summary>
    /// 用于选择 RuleBuilder 策略的可选工作流键。
    /// </summary>
    public string WorkflowKey { get; init; } = "default";

    /// <summary>
    /// 用于工程元数据的自由格式 JSON，例如 ISA-18.2 类别、搁置限制、通知路由或供应商特定详细信息。
    /// </summary>
    public string? MetadataJson { get; init; }

    /// <summary>
    /// 乐观运行时配置版本号。
    /// </summary>
    public long Version { get; init; } = 1;

    /// <summary> 创建时间（UTC）。 </summary>
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary> 最后更新时间（UTC）。 </summary>
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 新运行时代码使用的有效延迟时长。
    /// </summary>
    public TimeSpan EffectiveDelay =>
        DelayMs > 0 ? TimeSpan.FromMilliseconds(DelayMs) : TimeSpan.FromSeconds(DelaySeconds);

    /// <summary>
    /// 旧版和新版代码使用的有效确认要求。
    /// </summary>
    public bool IsAckRequired => AckPolicy switch
    {
        AlarmAckPolicy.NotRequired => false,
        AlarmAckPolicy.Required => true,
        AlarmAckPolicy.RequiredBeforeClear => true,
        _ => RequireAck
    };

    /// <summary>
    /// 有效启用标志。现有代码使用 Enabled；新配置使用 IsEnabled。两者都必须为 true。
    /// </summary>
    public bool IsRuntimeEnabled => Enabled && IsEnabled;

    /// <summary>
    /// 在报警定义被接受进运行时快照前进行验证。
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RuleId))
        {
            throw new InvalidOperationException("AlarmDefinition 必须包含 RuleId。");
        }

        if (string.IsNullOrWhiteSpace(AlarmCode))
        {
            throw new InvalidOperationException($"报警定义 '{RuleId}' 必须包含 AlarmCode。");
        }

        if (ResourcePath is null && string.IsNullOrWhiteSpace(TagId))
        {
            throw new InvalidOperationException(
                $"报警定义 '{RuleId}' 必须包含 ResourcePath 或旧版 TagId。");
        }

        if (string.IsNullOrWhiteSpace(ConditionExpression) &&
            !Conditions.Any(static condition => condition.IsEnabled && !string.IsNullOrWhiteSpace(condition.Expression)))
        {
            throw new InvalidOperationException($"报警定义 '{RuleId}' 必须包含 ConditionExpression 或 Conditions。");
        }

        if (DelayMs < 0 || DelaySeconds < 0)
        {
            throw new InvalidOperationException($"报警定义 '{RuleId}' 包含负数延迟。");
        }

        if (CooldownSeconds < 0)
        {
            throw new InvalidOperationException($"报警定义 '{RuleId}' 包含负数冷却时间。");
        }
    }
}

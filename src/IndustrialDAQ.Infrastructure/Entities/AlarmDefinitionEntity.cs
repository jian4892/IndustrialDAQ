using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using IndustrialDAQ.Core.Models;
using IndustrialDAQ.Core.ResourceTree;

namespace IndustrialDAQ.Infrastructure.Entities;

/// <summary>
/// 报警定义配置的持久化实体。
/// 可执行的工作流故意不存储在此处；RuleBuilder 负责工作流的构建和版本控制。
/// </summary>
[Table("alarm_definitions")]
public sealed class AlarmDefinitionEntity
{
    /// <summary> 获取或设置主键 ID。 </summary>
    [Key]
    [MaxLength(64)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary> 获取或设置报警规则 ID。 </summary>
    [Required]
    [MaxLength(128)]
    public string RuleId { get; set; } = string.Empty;

    /// <summary> 获取或设置工程报警代码。 </summary>
    [Required]
    [MaxLength(128)]
    public string AlarmCode { get; set; } = string.Empty;

    /// <summary> 获取或设置报警自身资源路径。 </summary>
    [MaxLength(512)]
    public string? ResourcePath { get; set; }

    /// <summary> 获取或设置监控的目标资源路径。 </summary>
    [MaxLength(512)]
    public string? TargetResourcePath { get; set; }

    /// <summary> 获取或设置运行时解析后的标签 ID。 </summary>
    [MaxLength(128)]
    public string TagId { get; set; } = string.Empty;

    /// <summary> 获取或设置标签名称。 </summary>
    [MaxLength(256)]
    public string TagName { get; set; } = string.Empty;

    /// <summary> 获取或设置报警类型字符串。 </summary>
    [MaxLength(32)]
    public string AlarmType { get; set; } = nameof(Core.Models.AlarmType.High);

    /// <summary> 获取或设置比较运算符。 </summary>
    [MaxLength(32)]
    public string Operator { get; set; } = nameof(AlarmOperator.GreaterThan);

    /// <summary> 获取或设置报警阈值。 </summary>
    public double Threshold { get; set; }

    /// <summary> 获取或设置死区值。 </summary>
    public double Deadband { get; set; }

    /// <summary> 获取或设置报警触发条件表达式。 </summary>
    [Required]
    public string ConditionExpression { get; set; } = string.Empty;

    public string ConditionsJson { get; set; } = "[]";

    /// <summary> 获取或设置报警清除条件表达式。 </summary>
    public string? ClearExpression { get; set; }

    /// <summary> 获取或设置抑制条件表达式。 </summary>
    public string? SuppressionExpression { get; set; }

    /// <summary> 获取或设置多条件连接运算符。 </summary>
    [MaxLength(16)]
    public string ExpressionJoin { get; set; } = nameof(AlarmExpressionJoin.And);

    /// <summary> 获取或设置延迟时长（毫秒）。 </summary>
    public int DelayMs { get; set; }

    /// <summary> 获取或设置迟滞。 </summary>
    public double Hysteresis { get; set; }

    /// <summary> 获取或设置严重程度。 </summary>
    [MaxLength(32)]
    public string Severity { get; set; } = nameof(AlarmSeverity.Warning);

    /// <summary> 获取或设置报警标题。 </summary>
    [MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    /// <summary> 获取或设置报警消息模板。 </summary>
    public string MessageTemplate { get; set; } = string.Empty;

    /// <summary> 获取或设置报警来源说明。 </summary>
    [MaxLength(256)]
    public string Source { get; set; } = string.Empty;

    /// <summary> 获取或设置启用状态。 </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary> 获取或设置确认策略。 </summary>
    [MaxLength(32)]
    public string AckPolicy { get; set; } = nameof(AlarmAckPolicy.Required);

    /// <summary> 获取或设置清除策略。 </summary>
    [MaxLength(32)]
    public string ClearPolicy { get; set; } = nameof(AlarmClearPolicy.AutoClearWhenConditionFalse);

    /// <summary> 获取或设置冷却时长（秒）。 </summary>
    public int CooldownSeconds { get; set; } = 60;

    /// <summary> 获取或设置工作流类型。 </summary>
    [MaxLength(32)]
    public string WorkflowType { get; set; } = nameof(AlarmWorkflowType.Expression);

    /// <summary> 获取或设置工作流选择键。 </summary>
    [MaxLength(128)]
    public string WorkflowKey { get; set; } = "default";

    /// <summary> 获取或设置元数据 JSON。 </summary>
    public string? MetadataJson { get; set; }

    /// <summary> 获取或设置版本号。 </summary>
    public long Version { get; set; } = 1;

    /// <summary> 获取或设置创建时间（UTC）。 </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary> 获取或设置更新时间（UTC）。 </summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 将实体转换为领域模型。
    /// </summary>
    public AlarmDefinition ToDomain()
    {
        var ackPolicy = ParseEnum(AckPolicy, AlarmAckPolicy.Required);

        return new AlarmDefinition
        {
            Id = Id,
            RuleId = RuleId,
            AlarmCode = AlarmCode,
            ResourcePath = ToPath(ResourcePath),
            TargetResourcePath = ToPath(TargetResourcePath),
            TagId = TagId,
            TagName = TagName,
            AlarmType = ParseEnum(AlarmType, Core.Models.AlarmType.High),
            Operator = ParseEnum(Operator, AlarmOperator.GreaterThan),
            Threshold = Threshold,
            Deadband = Deadband,
            ConditionExpression = ConditionExpression,
            Conditions = DeserializeConditions(ConditionsJson),
            ClearExpression = ClearExpression,
            SuppressionExpression = SuppressionExpression,
            ExpressionJoin = ParseEnum(ExpressionJoin, AlarmExpressionJoin.And),
            DelayMs = DelayMs,
            Hysteresis = Hysteresis,
            Severity = ParseEnum(Severity, AlarmSeverity.Warning),
            Title = Title,
            MessageTemplate = MessageTemplate,
            Source = Source,
            IsEnabled = IsEnabled,
            AckPolicy = ackPolicy,
            ClearPolicy = ParseEnum(ClearPolicy, AlarmClearPolicy.AutoClearWhenConditionFalse),
            CooldownSeconds = CooldownSeconds,
            WorkflowType = ParseEnum(WorkflowType, AlarmWorkflowType.Expression),
            WorkflowKey = WorkflowKey,
            MetadataJson = MetadataJson,
            Version = Version,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = UpdatedAtUtc
        };
    }

    /// <summary>
    /// 从领域模型创建实体实例。
    /// </summary>
    public static AlarmDefinitionEntity FromDomain(AlarmDefinition definition)
    {
        var entity = new AlarmDefinitionEntity
        {
            Id = string.IsNullOrWhiteSpace(definition.Id) ? Guid.NewGuid().ToString("N") : definition.Id,
            CreatedAtUtc = definition.CreatedAtUtc == default ? DateTime.UtcNow : definition.CreatedAtUtc
        };

        Apply(definition, entity);
        return entity;
    }

    /// <summary>
    /// 将领域模型的状态应用到现有实体。
    /// </summary>
    public static void Apply(AlarmDefinition definition, AlarmDefinitionEntity entity)
    {
        definition.Validate();

        entity.RuleId = definition.RuleId;
        entity.AlarmCode = definition.AlarmCode;
        entity.ResourcePath = definition.ResourcePath?.Value;
        entity.TargetResourcePath = definition.TargetResourcePath?.Value;
        entity.TagId = definition.TagId;
        entity.TagName = definition.TagName;
        entity.AlarmType = definition.AlarmType.ToString();
        entity.Operator = definition.Operator.ToString();
        entity.Threshold = definition.Threshold;
        entity.Deadband = definition.Deadband;
        entity.ConditionExpression = definition.GetEffectiveConditionExpression();
        entity.ConditionsJson = JsonSerializer.Serialize(definition.Conditions, JsonOptions);
        entity.ClearExpression = definition.ClearExpression;
        entity.SuppressionExpression = definition.SuppressionExpression;
        entity.ExpressionJoin = definition.ExpressionJoin.ToString();
        entity.DelayMs = definition.DelayMs;
        entity.Hysteresis = definition.Hysteresis;
        entity.Severity = definition.Severity.ToString();
        entity.Title = definition.Title;
        entity.MessageTemplate = definition.MessageTemplate;
        entity.Source = definition.Source;
        entity.IsEnabled = definition.IsEnabled;
        entity.AckPolicy = definition.AckPolicy.ToString();
        entity.ClearPolicy = definition.ClearPolicy.ToString();
        entity.CooldownSeconds = definition.CooldownSeconds;
        entity.WorkflowType = definition.WorkflowType.ToString();
        entity.WorkflowKey = definition.WorkflowKey;
        entity.MetadataJson = definition.MetadataJson;
        entity.Version = Math.Max(1, definition.Version);
        entity.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static ResourcePath? ToPath(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : new ResourcePath(value);
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
    }

    private static IReadOnlyList<AlarmConditionDefinition> DeserializeConditions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<AlarmConditionDefinition[]>(json, JsonOptions) ?? [];
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
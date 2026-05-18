using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using IndustrialDAQ.Core.Models;
using RulesEngine.Models;

namespace IndustrialDAQ.Alarm.RuleBuilder;

/// <summary>
/// 默认的工业报警规则构建器。
/// 将 AlarmDefinition 配置转换为确定性的可执行工作流。
/// 构建的工作流与定义分离，以便运行时热重载能够安全地验证并切换工作流。
/// </summary>
public sealed partial class AlarmRuleBuilder : IAlarmRuleBuilder
{
    private static readonly Regex s_simpleComparisonRegex = SimpleComparisonRegex();
    /// <summary> 禁止在表达式中使用的敏感/不安全令牌，防止代码注入 </summary>
    private static readonly string[] s_blockedExpressionTokens =
    [
        "System.",
        "Microsoft.",
        "Environment",
        "Process",
        "File.",
        "Directory.",
        "Reflection",
        "Activator",
        "new ",
        "typeof",
        "GetType",
        "Thread",
        "Task.",
        "Console."
    ];

    /// <summary>
    /// 构建可执行的报警规则工作流。
    /// </summary>
    public AlarmRuleWorkflow Build(AlarmDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();

        var triggerExpression = BuildTriggerExpression(definition);
        var clearExpression = BuildClearExpression(definition, triggerExpression);
        var suppressionExpression = BuildSuppressionExpression(definition);

        ValidateExpression(definition, "trigger", triggerExpression);
        ValidateExpression(definition, "clear", clearExpression);
        ValidateExpression(definition, "suppression", suppressionExpression);

        var workflow = new Workflow
        {
            WorkflowName = CreateWorkflowName(definition),
            Rules =
            [
                new Rule
                {
                    RuleName = AlarmRuleWorkflow.SuppressionRuleName,
                    Expression = suppressionExpression,
                    RuleExpressionType = RuleExpressionType.LambdaExpression
                },
                new Rule
                {
                    RuleName = AlarmRuleWorkflow.TriggerRuleName,
                    Expression = triggerExpression,
                    RuleExpressionType = RuleExpressionType.LambdaExpression
                },
                new Rule
                {
                    RuleName = AlarmRuleWorkflow.ClearRuleName,
                    Expression = clearExpression,
                    RuleExpressionType = RuleExpressionType.LambdaExpression
                }
            ]
        };

        var compiledHash = ComputeHash(definition, triggerExpression, clearExpression, suppressionExpression);

        return new AlarmRuleWorkflow(
            definition,
            workflow,
            triggerExpression,
            clearExpression,
            suppressionExpression,
            compiledHash);
    }

    /// <summary>
    /// 构建工作流快照。
    /// </summary>
    public AlarmRuleWorkflowSnapshot BuildSnapshot(IEnumerable<AlarmDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var workflows = definitions
            .Where(static definition => definition.IsRuntimeEnabled)
            .Select(Build)
            .ToArray();

        return AlarmRuleWorkflowSnapshot.Build(workflows);
    }

    /// <summary> 构建触发表达式 </summary>
    private static string BuildTriggerExpression(AlarmDefinition definition)
    {
        // 如果定义了 Conditions (子条件)，则组合它们
        var fragments = definition.Conditions
            .Where(static condition => condition.IsEnabled && !string.IsNullOrWhiteSpace(condition.Expression))
            .OrderBy(static condition => condition.SortOrder)
            .Select(static condition => $"({condition.Expression.Trim()})")
            .ToArray();

        if (fragments.Length == 0)
        {
            return definition.ConditionExpression.Trim();
        }

        var op = definition.ExpressionJoin == AlarmExpressionJoin.Or ? " || " : " && ";
        return string.Join(op, fragments);
    }

    /// <summary> 构建清除表达式 </summary>
    private static string BuildClearExpression(AlarmDefinition definition, string triggerExpression)
    {
        if (!string.IsNullOrWhiteSpace(definition.ClearExpression))
        {
            return definition.ClearExpression.Trim();
        }

        // 尝试根据迟滞(Hysteresis)推导清除条件
        var derived = TryDeriveClearExpression(definition, triggerExpression);
        if (!string.IsNullOrWhiteSpace(derived))
        {
            return derived;
        }

        // 复杂表达式的安全回退：仅当触发条件不再满足时清除
        // Builder 保持该表达式显式，以便状态机不需要理解表达式语法
        return $"!({triggerExpression})";
    }

    /// <summary> 构建抑制表达式 </summary>
    private static string BuildSuppressionExpression(AlarmDefinition definition)
    {
        return string.IsNullOrWhiteSpace(definition.SuppressionExpression)
            ? "false"
            : definition.SuppressionExpression.Trim();
    }

    /// <summary> 尝试根据迟滞自动推导清除条件 </summary>
    private static string? TryDeriveClearExpression(AlarmDefinition definition, string triggerExpression)
    {
        if (definition.Hysteresis <= 0)
        {
            return null;
        }

        var match = s_simpleComparisonRegex.Match(triggerExpression.Trim());
        if (!match.Success)
        {
            return null;
        }

        var variable = match.Groups["left"].Value;
        var op = match.Groups["op"].Value;
        var threshold = double.Parse(match.Groups["right"].Value, System.Globalization.CultureInfo.InvariantCulture);
        var hysteresis = definition.Hysteresis;

        return op switch
        {
            ">" or ">=" => $"{variable} < {FormatNumber(threshold - hysteresis)}",
            "<" or "<=" => $"{variable} > {FormatNumber(threshold + hysteresis)}",
            "==" when definition.AlarmType == AlarmType.Bool => $"{variable} == false",
            _ => null
        };
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string CreateWorkflowName(AlarmDefinition definition)
    {
        var safeRuleId = Regex.Replace(definition.RuleId, "[^A-Za-z0-9_]", "_");
        return $"Alarm_{safeRuleId}_{definition.Version}";
    }

    /// <summary> 验证表达式的安全性及语法完整性 </summary>
    private static void ValidateExpression(AlarmDefinition definition, string expressionRole, string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new AlarmRuleBuilderException(
                $"报警 '{definition.RuleId}' 的 {expressionRole} 表达式为空。");
        }

        foreach (var token in s_blockedExpressionTokens)
        {
            if (expression.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                throw new AlarmRuleBuilderException(
                    $"报警 '{definition.RuleId}' 的 {expressionRole} 表达式包含禁用令牌 '{token}'。");
            }
        }

        var balance = 0;
        foreach (var ch in expression)
        {
            if (ch == '(') balance++;
            if (ch == ')') balance--;
            if (balance < 0)
            {
                throw new AlarmRuleBuilderException(
                    $"报警 '{definition.RuleId}' 的 {expressionRole} 表达式括号不匹配。");
            }
        }

        if (balance != 0)
        {
            throw new AlarmRuleBuilderException(
                $"报警 '{definition.RuleId}' 的 {expressionRole} 表达式括号不匹配。");
        }
    }

    /// <summary> 计算编译哈希值，用于检测配置是否发生实质性变更 </summary>
    private static string ComputeHash(
        AlarmDefinition definition,
        string triggerExpression,
        string clearExpression,
        string suppressionExpression)
    {
        var text = string.Join('\n',
            definition.RuleId,
            definition.AlarmCode,
            definition.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            definition.WorkflowType,
            definition.WorkflowKey,
            triggerExpression,
            clearExpression,
            suppressionExpression,
            definition.DelayMs,
            definition.AckPolicy,
            definition.ClearPolicy);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }

    [GeneratedRegex(@"^(?<left>[A-Za-z_][A-Za-z0-9_]*)\s*(?<op>>=|<=|>|<|==)\s*(?<right>-?\d+(\.\d+)?)$", RegexOptions.Compiled)]
    private static partial Regex SimpleComparisonRegex();
}

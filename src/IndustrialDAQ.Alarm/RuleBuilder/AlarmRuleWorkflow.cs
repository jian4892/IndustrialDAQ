using IndustrialDAQ.Core.Models;
using RulesEngine.Models;

namespace IndustrialDAQ.Alarm.RuleBuilder;

/// <summary>
/// Compiled runtime workflow generated from an AlarmDefinition.
/// The definition remains configuration; this object owns the executable
/// RulesEngine workflow and runtime evaluation boundary.
/// </summary>
public sealed class AlarmRuleWorkflow
{
    public const string TriggerRuleName = "Trigger";
    public const string ClearRuleName = "Clear";
    public const string SuppressionRuleName = "Suppression";

    private readonly RulesEngine.RulesEngine _engine;

    public AlarmRuleWorkflow(
        AlarmDefinition definition,
        Workflow workflow,
        string triggerExpression,
        string clearExpression,
        string suppressionExpression,
        string compiledHash)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        TriggerExpression = triggerExpression;
        ClearExpression = clearExpression;
        SuppressionExpression = suppressionExpression;
        CompiledHash = compiledHash;
        _engine = new RulesEngine.RulesEngine([workflow]);
    }

    public AlarmDefinition Definition { get; }

    public Workflow Workflow { get; }

    public string WorkflowName => Workflow.WorkflowName;

    public string TriggerExpression { get; }

    public string ClearExpression { get; }

    public string SuppressionExpression { get; }

    public string CompiledHash { get; }

    public long Version => Definition.Version;

    public async ValueTask<AlarmRuleEvaluationResult> EvaluateAsync(
        TagValue value,
        IReadOnlyDictionary<string, object?>? extraParameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        var parameters = BuildParameters(value, extraParameters);
        var errors = new List<string>();

        var ruleResults = await ExecuteWorkflowAsync(parameters, errors).ConfigureAwait(false);

        bool suppressed = IsSuccessful(ruleResults, SuppressionRuleName, errors);
        if (suppressed)
        {
            return new AlarmRuleEvaluationResult
            {
                Definition = Definition,
                IsSuppressed = true,
                Value = value.Value,
                Timestamp = value.Timestamp,
                Errors = errors
            };
        }

        bool triggered = IsSuccessful(ruleResults, TriggerRuleName, errors);
        bool cleared = IsSuccessful(ruleResults, ClearRuleName, errors);

        return new AlarmRuleEvaluationResult
        {
            Definition = Definition,
            IsSuppressed = false,
            IsTriggered = triggered,
            IsCleared = cleared,
            Value = value.Value,
            Timestamp = value.Timestamp,
            Errors = errors
        };
    }

    private async Task<IReadOnlyList<RuleResultTree>> ExecuteWorkflowAsync(
        RuleParameter[] parameters,
        List<string> errors)
    {
        try
        {
            return await _engine.ExecuteAllRulesAsync(WorkflowName, parameters).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            errors.Add($"Workflow: {ex.Message}");
            return [];
        }
    }

    private static bool IsSuccessful(
        IReadOnlyList<RuleResultTree> results,
        string ruleName,
        List<string> errors)
    {
        var result = results.FirstOrDefault(item =>
            string.Equals(item.Rule.RuleName, ruleName, StringComparison.OrdinalIgnoreCase));

        if (result?.ExceptionMessage is not null)
        {
            errors.Add($"{ruleName}: {result.ExceptionMessage}");
        }

        return result?.IsSuccess == true;
    }

    private static RuleParameter[] BuildParameters(
        TagValue value,
        IReadOnlyDictionary<string, object?>? extraParameters)
    {
        var parameters = new List<RuleParameter>
        {
            new("Value", NormalizeValue(value.Value)),
            new("TagId", value.TagId),
            new("TagName", value.TagName),
            new("Quality", value.Quality.ToString()),
            new("Timestamp", value.Timestamp.UtcDateTime)
        };

        if (extraParameters is not null)
        {
            foreach (var (name, parameterValue) in extraParameters)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    parameters.Add(new RuleParameter(name, NormalizeValue(parameterValue)));
                }
            }
        }

        return parameters.ToArray();
    }

    private static object? NormalizeValue(object? value)
    {
        return value switch
        {
            float item => (double)item,
            decimal item => (double)item,
            short item => (double)item,
            int item => (double)item,
            long item => (double)item,
            ushort item => (double)item,
            uint item => (double)item,
            ulong item => (double)item,
            byte item => (double)item,
            sbyte item => (double)item,
            _ => value
        };
    }
}

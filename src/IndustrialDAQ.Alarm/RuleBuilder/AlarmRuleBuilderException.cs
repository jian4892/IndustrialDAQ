namespace IndustrialDAQ.Alarm.RuleBuilder;

public sealed class AlarmRuleBuilderException : Exception
{
    public AlarmRuleBuilderException(string message)
        : base(message)
    {
    }

    public AlarmRuleBuilderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

namespace IndustrialDAQ.Core.Authorization;

public enum PermissionSubjectType : byte
{
    User = 0,
    Role = 1,
    Group = 2,
    System = 3
}

public enum PermissionEffect : byte
{
    Deny = 0,
    Allow = 1
}

/// <summary>
/// Standard runtime actions. Policies are persisted as text so deployments can
/// add domain-specific actions without recompiling this enum.
/// </summary>
public static class PermissionActions
{
    public const string Read = "Read";
    public const string Write = "Write";
    public const string ViewMenu = "ViewMenu";
    public const string TagRead = "TagRead";
    public const string TagWrite = "TagWrite";
    public const string DeviceRead = "DeviceRead";
    public const string AlarmAck = "AlarmAck";
    public const string AlarmShelve = "AlarmShelve";
    public const string Configure = "Configure";
    public const string Operate = "Operate";
    public const string RuleEdit = "RuleEdit";
}

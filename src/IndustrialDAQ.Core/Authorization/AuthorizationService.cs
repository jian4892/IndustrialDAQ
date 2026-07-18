using IndustrialDAQ.Core.ResourceTree;

namespace IndustrialDAQ.Core.Authorization;

/// <summary>
/// 运行时授权服务。
/// 使用不可变的权限快照进行快速判定。默认拒绝所有未匹配的请求。
/// 拒绝策略（Deny）的优先级高于允许策略（Allow）。
/// </summary>
public sealed class AuthorizationService : IAuthorizationService
{
    private readonly IAuthorizationRepository _repository;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private volatile PermissionSnapshot _current = PermissionSnapshot.Empty;

    /// <summary>
    /// 初始化授权服务的新实例。
    /// </summary>
    /// <param name="repository">权限存储库。</param>
    public AuthorizationService(IAuthorizationRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// 获取当前的运行时权限快照。
    /// </summary>
    public PermissionSnapshot Current => _current;

    /// <summary>
    /// 从存储库加载所有策略并构建新快照（支持热重载）。
    /// </summary>
    public async Task<PermissionSnapshot> ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _reloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var policies = await _repository.LoadPoliciesAsync(cancellationToken).ConfigureAwait(false);
            var next = PermissionSnapshot.Build(policies);
            _current = next;
            return next;
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    /// <summary>
    /// 执行核心授权判定。
    /// </summary>
    public ValueTask<AuthorizationDecision> AuthorizeAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        // 在快照中查找匹配的策略候选者（考虑了路径层级继承）
        var candidates = Current.FindCandidates(request);
        if (candidates.Count == 0)
        {
            return ValueTask.FromResult(AuthorizationDecision.Deny("没有匹配的权限策略。"));
        }

        // 优先级最高的胜出者
        var winner = candidates[0];
        if (winner.Effect == PermissionEffect.Deny)
        {
            return ValueTask.FromResult(AuthorizationDecision.Deny("由于匹配的拒绝策略而被禁止。", winner));
        }

        return ValueTask.FromResult(AuthorizationDecision.Allow(winner, "由于匹配的允许策略而被准许。"));
    }

    /// <summary>
    /// 简易权限判定接口。
    /// </summary>
    public async ValueTask<bool> CanAsync(
        PermissionSubject subject,
        ResourcePath resourcePath,
        string action,
        CancellationToken cancellationToken = default)
    {
        var decision = await AuthorizeAsync(new AuthorizationRequest
        {
            Subject = subject,
            ResourcePath = resourcePath,
            Action = action
        }, cancellationToken).ConfigureAwait(false);

        return decision.IsAllowed;
    }
}

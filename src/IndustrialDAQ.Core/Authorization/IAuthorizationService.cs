using IndustrialDAQ.Core.ResourceTree;

namespace IndustrialDAQ.Core.Authorization;

/// <summary>
/// 授权服务接口。
/// 提供基于资源树路径的权限验证能力，支持权限继承和路径匹配。
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// 获取当前的运行时权限快照。
    /// </summary>
    PermissionSnapshot Current { get; }

    /// <summary>
    /// 从持久化存储中异步重载权限策略并发布新快照。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>新发布的快照。</returns>
    Task<PermissionSnapshot> ReloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 对指定的授权请求执行详细的授权判定。
    /// </summary>
    /// <param name="request">授权请求（包含主体、资源路径、操作等）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含判定结果（允许/拒绝）及原因的授权决策对象。</returns>
    ValueTask<AuthorizationDecision> AuthorizeAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查指定主体是否对特定资源拥有执行特定操作的权限（简易布尔判定）。
    /// </summary>
    /// <param name="subject">权限主体（用户或角色）。</param>
    /// <param name="resourcePath">资源路径。</param>
    /// <param name="action">操作名称。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>如果允许操作则返回 true。</returns>
    ValueTask<bool> CanAsync(
        PermissionSubject subject,
        ResourcePath resourcePath,
        string action,
        CancellationToken cancellationToken = default);
}

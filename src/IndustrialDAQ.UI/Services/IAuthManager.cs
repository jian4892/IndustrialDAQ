using IndustrialDAQ.Core.Authorization;

namespace IndustrialDAQ.UI.Services;

/// <summary>
/// 客户端身份验证管理器接口
/// </summary>
public interface IAuthManager
{
    /// <summary>
    /// 当前登录的用户（如果未登录则为默认访客或系统用户）
    /// </summary>
    User CurrentUser { get; }

    /// <summary>
    /// 尝试登录
    /// </summary>
    Task<bool> LoginAsync(string username, string password);

    /// <summary>
    /// 登出
    /// </summary>
    void Logout();
}

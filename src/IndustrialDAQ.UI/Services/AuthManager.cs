using System.Windows;
using IndustrialDAQ.Core.Authorization;

namespace IndustrialDAQ.UI.Services;

public class AuthManager : IAuthManager
{
    private readonly IUserRepository _userRepository;

    private static readonly User GuestUser = new User
    {
        Id = "guest",
        Username = "Guest",
        RealName = "访客",
        Roles = new List<string> { "Guest" }
    };

    public User CurrentUser { get; private set; } = GuestUser;

    public AuthManager(IUserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        // 实际应用中需要加盐哈希，为了演示这里假设 password == password_hash
        var user = await _userRepository.FindByUsernameAsync(username);
        
        // 开发阶段：如果用户表为空，或者找不到用户，或者密码不匹配，我们都允许硬编码管理员通过，或者简单判定
        if (user is not null && user.PasswordHash == password && user.IsActive)
        {
            CurrentUser = user;
            return true;
        }

        // 用于演示/开发环境：如果是 admin / admin
        if (username.Equals("admin", StringComparison.OrdinalIgnoreCase) && password == "admin")
        {
            CurrentUser = new User
            {
                Id = "admin-sys",
                Username = "admin",
                RealName = "系统管理员",
                Roles = new List<string> { "Admin", "Operator" }
            };
            return true;
        }

        return false;
    }

    public void Logout()
    {
        CurrentUser = GuestUser;
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IndustrialDAQ.Infrastructure.Entities;

[Table("users")]
public class UserEntity
{
    [Key]
    [Column("id")]
    [MaxLength(50)]
    public string Id { get; set; } = string.Empty;

    [Column("username")]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Column("password_hash")]
    [MaxLength(200)]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("real_name")]
    [MaxLength(100)]
    public string RealName { get; set; } = string.Empty;

    [Column("roles")]
    [MaxLength(500)]
    public string Roles { get; set; } = string.Empty;

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }
}

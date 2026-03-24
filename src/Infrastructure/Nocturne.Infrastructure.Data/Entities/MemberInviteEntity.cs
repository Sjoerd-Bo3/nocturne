using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Nocturne.Infrastructure.Data.Entities;

[Table("member_invites")]
public class MemberInviteEntity
{
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("created_by_subject_id")]
    public Guid CreatedBySubjectId { get; set; }

    [Required]
    [Column("token_hash")]
    [MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    [Required]
    [Column("role")]
    [MaxLength(32)]
    public string Role { get; set; } = TenantRole.Follower;

    [Column("scopes", TypeName = "jsonb")]
    public List<string>? Scopes { get; set; }

    [Column("label")]
    [MaxLength(255)]
    public string? Label { get; set; }

    [Column("limit_to_24_hours")]
    public bool LimitTo24Hours { get; set; } = false;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("max_uses")]
    public int? MaxUses { get; set; }

    [Column("use_count")]
    public int UseCount { get; set; } = 0;

    [Column("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Computed
    [NotMapped]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    [NotMapped]
    public bool IsRevoked => RevokedAt.HasValue;
    [NotMapped]
    public bool IsExhausted => MaxUses.HasValue && UseCount >= MaxUses.Value;
    [NotMapped]
    public bool IsValid => !IsExpired && !IsRevoked && !IsExhausted;

    // Navigation
    public TenantEntity? Tenant { get; set; }
    public SubjectEntity? CreatedBy { get; set; }
    public ICollection<TenantMemberEntity> CreatedMembers { get; set; } = new List<TenantMemberEntity>();
}

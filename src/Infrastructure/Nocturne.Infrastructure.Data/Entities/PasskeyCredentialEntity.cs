using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// Stores WebAuthn/passkey credentials for passwordless authentication.
/// Each record represents one registered authenticator (platform key, security key, etc.)
/// </summary>
[Table("passkey_credentials")]
public class PasskeyCredentialEntity : ITenantScoped
{
    /// <summary>
    /// Primary key - UUID Version 7
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant this credential belongs to
    /// </summary>
    /// <summary>
    /// The unique identifier of the tenant this record belongs to.
    /// </summary>
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Foreign key to the subject (user) who owns this credential
    /// </summary>
    [Required]
    [Column("subject_id")]
    public Guid SubjectId { get; set; }

    /// <summary>
    /// The credential ID returned by the authenticator during registration
    /// </summary>
    [Required]
    [Column("credential_id")]
    public byte[] CredentialId { get; set; } = [];

    /// <summary>
    /// The public key returned by the authenticator during registration
    /// </summary>
    [Required]
    [Column("public_key")]
    public byte[] PublicKey { get; set; } = [];

    /// <summary>
    /// Signature counter for replay attack detection
    /// </summary>
    [Column("sign_count")]
    public uint SignCount { get; set; }

    /// <summary>
    /// Transport hints reported by the authenticator (e.g. "usb", "ble", "nfc", "internal")
    /// </summary>
    [Column("transports")]
    public List<string> Transports { get; set; } = new();

    /// <summary>
    /// User-provided friendly name for this credential (e.g. "MacBook Touch ID", "YubiKey 5")
    /// </summary>
    [MaxLength(255)]
    [Column("label")]
    public string? Label { get; set; }

    /// <summary>
    /// When this credential was registered
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this credential was last used for authentication
    /// </summary>
    [Column("last_used_at")]
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// The AAGUID of the authenticator, identifying the make/model
    /// </summary>
    [Column("aa_guid")]
    public Guid? AaGuid { get; set; }

    // Navigation properties

    /// <summary>
    /// The subject (user) who owns this credential
    /// </summary>
    public SubjectEntity? Subject { get; set; }
}

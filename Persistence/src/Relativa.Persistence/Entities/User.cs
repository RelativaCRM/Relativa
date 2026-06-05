namespace Relativa.Persistence.Entities;

public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Password { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsArchived { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiresAt { get; set; }
    public bool EmailVerified { get; set; }
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationTokenExpiresAt { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorSecret { get; set; }
    public string? TwoFactorMasterCodeHash { get; set; }
    public UserSettings? Settings { get; set; }
    public ICollection<UserExternalLogin> ExternalLogins { get; set; } = new List<UserExternalLogin>();
    public ICollection<UserBackupCode> BackupCodes { get; set; } = new List<UserBackupCode>();
    public ICollection<UserEmail> Emails { get; set; } = new List<UserEmail>();
    public ICollection<Entity> CreatedEntities { get; set; } = new List<Entity>();
    public ICollection<UserRoleWorkspace> WorkspaceMemberships { get; set; } = new List<UserRoleWorkspace>();
    public ICollection<UserRoleOrganization> OrganizationMemberships { get; set; } = new List<UserRoleOrganization>();
}

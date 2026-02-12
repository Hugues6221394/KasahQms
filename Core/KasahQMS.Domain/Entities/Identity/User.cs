using KasahQMS.Domain.Common;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Entities.Identity;

/// <summary>
/// User entity representing system users.
/// </summary>
public class User : AuditableEntity
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? JobTitle { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsLockedOut { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutEndTime { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public bool RequirePasswordChange { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? ManagerId { get; set; }
    
    // Navigation properties
    public virtual OrganizationUnit? OrganizationUnit { get; set; }
    public virtual User? Manager { get; set; }
    public virtual ICollection<Role>? Roles { get; set; }
    public virtual ICollection<User>? DirectReports { get; set; }
    public virtual ICollection<UserRole>? UserRoles { get; set; }
    
    public User() { }
    
    public static User Create(
        Guid tenantId,
        string email,
        string firstName,
        string lastName,
        string passwordHash,
        Guid createdById)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = passwordHash,
            IsActive = true,
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow,
            RequirePasswordChange = true
        };
    }
    
    public string FullName => $"{FirstName} {LastName}";
    
    public void SetPhoneNumber(string phoneNumber) => PhoneNumber = phoneNumber;
    public void SetJobTitle(string jobTitle) => JobTitle = jobTitle;
    public void AssignToOrganizationUnit(Guid organizationUnitId) => OrganizationUnitId = organizationUnitId;
    public void SetManager(Guid managerId) => ManagerId = managerId;
    
    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        PasswordChangedAt = DateTime.UtcNow;
        RequirePasswordChange = false;
        FailedLoginAttempts = 0;
    }
    
    public void RecordSuccessfulLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        IsLockedOut = false;
        LockoutEndTime = null;
    }
    
    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= 5)
        {
            IsLockedOut = true;
            LockoutEndTime = DateTime.UtcNow.AddMinutes(30);
        }
    }
    
    public void Unlock()
    {
        IsLockedOut = false;
        FailedLoginAttempts = 0;
        LockoutEndTime = null;
    }
    
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}

using FluentAssertions;
using KasahQMS.Domain.Entities.Identity;
using Xunit;

namespace KasahQMS.Tests.Unit.Domain.Entities;

public class UserTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _createdById = Guid.NewGuid();

    private User CreateUser()
    {
        return User.Create(_tenantId, "john@example.com", "John", "Doe", "hashed_pw", _createdById);
    }

    [Fact]
    public void Create_ShouldSetDefaults()
    {
        var user = CreateUser();

        user.Id.Should().NotBeEmpty();
        user.TenantId.Should().Be(_tenantId);
        user.Email.Should().Be("john@example.com");
        user.FirstName.Should().Be("John");
        user.LastName.Should().Be("Doe");
        user.PasswordHash.Should().Be("hashed_pw");
        user.IsActive.Should().BeTrue();
        user.RequirePasswordChange.Should().BeTrue();
        user.IsLockedOut.Should().BeFalse();
        user.FailedLoginAttempts.Should().Be(0);
        user.CreatedById.Should().Be(_createdById);
    }

    [Fact]
    public void FullName_ShouldReturnFirstAndLastName()
    {
        var user = CreateUser();
        user.FullName.Should().Be("John Doe");
    }

    [Fact]
    public void RecordSuccessfulLogin_ShouldResetFailedAttemptsAndLockout()
    {
        var user = CreateUser();
        user.FailedLoginAttempts = 3;
        user.IsLockedOut = true;
        user.LockoutEndTime = DateTime.UtcNow.AddMinutes(30);

        user.RecordSuccessfulLogin();

        user.FailedLoginAttempts.Should().Be(0);
        user.IsLockedOut.Should().BeFalse();
        user.LockoutEndTime.Should().BeNull();
        user.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RecordFailedLogin_ShouldIncrementAttempts()
    {
        var user = CreateUser();

        user.RecordFailedLogin();

        user.FailedLoginAttempts.Should().Be(1);
        user.IsLockedOut.Should().BeFalse();
    }

    [Fact]
    public void RecordFailedLogin_After5Attempts_ShouldLockAccount()
    {
        var user = CreateUser();

        for (int i = 0; i < 5; i++)
            user.RecordFailedLogin();

        user.FailedLoginAttempts.Should().Be(5);
        user.IsLockedOut.Should().BeTrue();
        user.LockoutEndTime.Should().NotBeNull();
        user.LockoutEndTime.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RecordFailedLogin_4Attempts_ShouldNotLock()
    {
        var user = CreateUser();

        for (int i = 0; i < 4; i++)
            user.RecordFailedLogin();

        user.FailedLoginAttempts.Should().Be(4);
        user.IsLockedOut.Should().BeFalse();
        user.LockoutEndTime.Should().BeNull();
    }

    [Fact]
    public void ChangePassword_ShouldResetRequirePasswordChangeAndAttempts()
    {
        var user = CreateUser();
        user.FailedLoginAttempts = 3;

        user.ChangePassword("new_hashed_pw");

        user.PasswordHash.Should().Be("new_hashed_pw");
        user.RequirePasswordChange.Should().BeFalse();
        user.FailedLoginAttempts.Should().Be(0);
        user.PasswordChangedAt.Should().NotBeNull();
        user.PasswordChangedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Unlock_ShouldClearLockoutState()
    {
        var user = CreateUser();
        user.IsLockedOut = true;
        user.FailedLoginAttempts = 5;
        user.LockoutEndTime = DateTime.UtcNow.AddMinutes(30);

        user.Unlock();

        user.IsLockedOut.Should().BeFalse();
        user.FailedLoginAttempts.Should().Be(0);
        user.LockoutEndTime.Should().BeNull();
    }

    [Fact]
    public void Activate_ShouldSetIsActiveTrue()
    {
        var user = CreateUser();
        user.Deactivate();

        user.Activate();

        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var user = CreateUser();

        user.Deactivate();

        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void SetPhoneNumber_ShouldUpdatePhoneNumber()
    {
        var user = CreateUser();
        user.SetPhoneNumber("+1234567890");
        user.PhoneNumber.Should().Be("+1234567890");
    }

    [Fact]
    public void SetJobTitle_ShouldUpdateJobTitle()
    {
        var user = CreateUser();
        user.SetJobTitle("QA Manager");
        user.JobTitle.Should().Be("QA Manager");
    }

    [Fact]
    public void AssignToOrganizationUnit_ShouldSetOrgUnitId()
    {
        var user = CreateUser();
        var orgUnitId = Guid.NewGuid();

        user.AssignToOrganizationUnit(orgUnitId);

        user.OrganizationUnitId.Should().Be(orgUnitId);
    }

    [Fact]
    public void SetManager_ShouldSetManagerId()
    {
        var user = CreateUser();
        var managerId = Guid.NewGuid();

        user.SetManager(managerId);

        user.ManagerId.Should().Be(managerId);
    }
}

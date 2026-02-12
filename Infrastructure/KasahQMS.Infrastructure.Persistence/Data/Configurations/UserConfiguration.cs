using KasahQMS.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KasahQMS.Infrastructure.Persistence.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        
        builder.Property(u => u.TenantId).IsRequired();
        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.LastName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.PhoneNumber).HasMaxLength(50);
        builder.Property(u => u.JobTitle).HasMaxLength(200);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
        builder.Property(u => u.LastLoginIp).HasMaxLength(50);
        
        builder.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
        builder.HasIndex(u => u.TenantId);
        
        builder.HasOne(u => u.OrganizationUnit)
            .WithMany(o => o.Users)
            .HasForeignKey(u => u.OrganizationUnitId)
            .OnDelete(DeleteBehavior.SetNull);
            
        builder.HasOne(u => u.Manager)
            .WithMany(u => u.DirectReports)
            .HasForeignKey(u => u.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);
            
                
        builder.Ignore(u => u.FullName);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        
        builder.Property(r => r.TenantId).IsRequired();
        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Description).HasMaxLength(500);
        
        builder.HasIndex(r => new { r.TenantId, r.Name }).IsUnique();
    }
}

public class OrganizationUnitConfiguration : IEntityTypeConfiguration<OrganizationUnit>
{
    public void Configure(EntityTypeBuilder<OrganizationUnit> builder)
    {
        builder.ToTable("organization_units");
        
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        
        builder.Property(o => o.TenantId).IsRequired();
        builder.Property(o => o.Name).IsRequired().HasMaxLength(200);
        builder.Property(o => o.Code).HasMaxLength(50);
        builder.Property(o => o.Description).HasMaxLength(500);
        
        builder.HasIndex(o => new { o.TenantId, o.Name }).IsUnique();
        
        builder.HasOne(o => o.Parent)
            .WithMany(o => o.Children)
            .HasForeignKey(o => o.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");
        // The relationship is configured in ApplicationDbContext using UsingEntity
    }
}

public class UserPermissionDelegationConfiguration : IEntityTypeConfiguration<UserPermissionDelegation>
{
    public void Configure(EntityTypeBuilder<UserPermissionDelegation> builder)
    {
        builder.ToTable("user_permission_delegations");
        
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        
        builder.Property(d => d.Permission).IsRequired().HasMaxLength(200);
        builder.Property(d => d.DelegatedAt).IsRequired();
        
        // Foreign key to User (receiving delegation)
        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Foreign key to User (delegating)
        builder.HasOne(d => d.DelegatedBy)
            .WithMany()
            .HasForeignKey(d => d.DelegatedById)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Index for efficient queries
        builder.HasIndex(d => new { d.UserId, d.IsActive });
        builder.HasIndex(d => d.DelegatedById);
        builder.HasIndex(d => new { d.UserId, d.Permission, d.IsActive });
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Token).IsRequired().HasMaxLength(500);
        builder.Property(r => r.CreatedByIp).HasMaxLength(50);
        builder.Property(r => r.RevokedByIp).HasMaxLength(50);
        builder.Property(r => r.ReplacedByToken).HasMaxLength(500);
        builder.Property(r => r.ReasonRevoked).HasMaxLength(500);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.Token);
        builder.HasIndex(r => r.UserId);
    }
}

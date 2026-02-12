using KasahQMS.Domain.Entities.Configuration;
using KasahQMS.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KasahQMS.Infrastructure.Persistence.Data.Configurations;

public class AccessPolicyConfiguration : IEntityTypeConfiguration<AccessPolicy>
{
    public void Configure(EntityTypeBuilder<AccessPolicy> builder)
    {
        builder.ToTable("access_policies");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.Name).IsRequired().HasMaxLength(150);
        builder.Property(p => p.Description).HasMaxLength(500);
        builder.Property(p => p.Scope).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Attribute).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Operator).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Value).IsRequired().HasMaxLength(200);

        builder.HasOne(p => p.Role)
            .WithMany()
            .HasForeignKey(p => p.RoleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => new { p.TenantId, p.RoleId });
    }
}

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("system_settings");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.TenantId).IsRequired();
        builder.Property(s => s.Key).IsRequired().HasMaxLength(150);
        builder.Property(s => s.Value).IsRequired().HasMaxLength(500);
        builder.Property(s => s.Description).HasMaxLength(500);

        builder.HasIndex(s => new { s.TenantId, s.Key }).IsUnique();
    }
}


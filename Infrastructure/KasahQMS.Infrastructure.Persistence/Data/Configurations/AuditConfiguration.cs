using KasahQMS.Domain.Entities.AuditLog;
using KasahQMS.Domain.Entities.Audits;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KasahQMS.Infrastructure.Persistence.Data.Configurations;

public class AuditConfiguration : IEntityTypeConfiguration<Audit>
{
    public void Configure(EntityTypeBuilder<Audit> builder)
    {
        builder.ToTable("audits");
        
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        
        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.AuditNumber).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Title).IsRequired().HasMaxLength(500);
        builder.Property(a => a.Description).HasMaxLength(2000);
        builder.Property(a => a.Scope).HasMaxLength(2000);
        builder.Property(a => a.Objectives).HasMaxLength(2000);
        builder.Property(a => a.Conclusion).HasMaxLength(4000);
        
        builder.HasIndex(a => new { a.TenantId, a.AuditNumber }).IsUnique();
        builder.HasIndex(a => a.TenantId);
        builder.HasIndex(a => a.Status);
        
        builder.HasMany(a => a.Findings)
            .WithOne(f => f.Audit)
            .HasForeignKey(f => f.AuditId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasMany(a => a.TeamMembers)
            .WithOne(t => t.Audit)
            .HasForeignKey(t => t.AuditId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditFindingConfiguration : IEntityTypeConfiguration<AuditFinding>
{
    public void Configure(EntityTypeBuilder<AuditFinding> builder)
    {
        builder.ToTable("audit_findings");
        
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        
        builder.Property(f => f.FindingNumber).IsRequired().HasMaxLength(100);
        builder.Property(f => f.Title).IsRequired().HasMaxLength(500);
        builder.Property(f => f.Description).IsRequired().HasMaxLength(4000);
        builder.Property(f => f.FindingType).HasMaxLength(100);
        builder.Property(f => f.Status).HasMaxLength(100);
        builder.Property(f => f.Clause).HasMaxLength(200);
        builder.Property(f => f.Evidence).HasMaxLength(4000);
        builder.Property(f => f.Response).HasMaxLength(4000);
        
        builder.HasIndex(f => f.AuditId);
    }
}

public class AuditTeamMemberConfiguration : IEntityTypeConfiguration<AuditTeamMember>
{
    public void Configure(EntityTypeBuilder<AuditTeamMember> builder)
    {
        builder.ToTable("audit_team_members");
        
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        
        builder.HasIndex(t => t.AuditId);
        builder.HasIndex(t => t.UserId);
    }
}

public class AuditChecklistItemConfiguration : IEntityTypeConfiguration<AuditChecklistItem>
{
    public void Configure(EntityTypeBuilder<AuditChecklistItem> builder)
    {
        builder.ToTable("audit_checklist_items");
        
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        
        builder.Property(c => c.Requirement).IsRequired().HasMaxLength(1000);
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.Category).HasMaxLength(200);
        builder.Property(c => c.Notes).HasMaxLength(2000);
        builder.Property(c => c.Evidence).HasMaxLength(2000);
        
        builder.HasIndex(c => c.AuditId);
    }
}

public class AuditEvidenceConfiguration : IEntityTypeConfiguration<AuditEvidence>
{
    public void Configure(EntityTypeBuilder<AuditEvidence> builder)
    {
        builder.ToTable("audit_evidence");
        
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        
        builder.Property(e => e.FileName).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.FilePath).IsRequired().HasMaxLength(1000);
        builder.Property(e => e.ContentType).HasMaxLength(200);
        
        builder.HasIndex(e => e.AuditFindingId);
    }
}

public class AuditFindingResponseConfiguration : IEntityTypeConfiguration<AuditFindingResponse>
{
    public void Configure(EntityTypeBuilder<AuditFindingResponse> builder)
    {
        builder.ToTable("audit_finding_responses");
        
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        
        builder.Property(r => r.Response).IsRequired().HasMaxLength(4000);
        builder.Property(r => r.ActionTaken).HasMaxLength(4000);
        builder.Property(r => r.ReviewComments).HasMaxLength(2000);
        
        builder.HasIndex(r => r.AuditFindingId);
    }
}

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_log");
        
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        
        builder.Property(e => e.Action).IsRequired().HasMaxLength(100);
        builder.Property(e => e.EntityType).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.OldValues).HasColumnType("text");
        builder.Property(e => e.NewValues).HasColumnType("text");
        builder.Property(e => e.IpAddress).HasMaxLength(50);
        builder.Property(e => e.UserAgent).HasMaxLength(500);
        builder.Property(e => e.FailureReason).HasMaxLength(1000);
        
        builder.HasIndex(e => e.Timestamp);
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.EntityType, e.EntityId });
    }
}

using KasahQMS.Domain.Entities.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KasahQMS.Infrastructure.Persistence.Data.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");
        
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        
        builder.Property(d => d.TenantId).IsRequired();
        builder.Property(d => d.DocumentNumber).IsRequired().HasMaxLength(100);
        builder.Property(d => d.Title).IsRequired().HasMaxLength(500);
        builder.Property(d => d.Description).HasMaxLength(2000);
        builder.Property(d => d.Content).HasColumnType("text");
        builder.Property(d => d.ArchiveReason).HasMaxLength(1000);
        
        builder.HasIndex(d => new { d.TenantId, d.DocumentNumber }).IsUnique();
        builder.HasIndex(d => d.TenantId);
        builder.HasIndex(d => d.Status);
        
        builder.HasOne(d => d.DocumentType)
            .WithMany()
            .HasForeignKey(d => d.DocumentTypeId)
            .OnDelete(DeleteBehavior.SetNull);
            
        builder.HasOne(d => d.Category)
            .WithMany()
            .HasForeignKey(d => d.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
            
        builder.HasMany(d => d.Versions)
            .WithOne(v => v.Document)
            .HasForeignKey(v => v.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasMany(d => d.Approvals)
            .WithOne(a => a.Document)
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("document_versions");
        
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        
        builder.Property(v => v.Content).HasColumnType("text");
        builder.Property(v => v.ChangeNotes).HasMaxLength(2000);
        
        builder.HasIndex(v => new { v.DocumentId, v.VersionNumber }).IsUnique();
    }
}

public class DocumentApprovalConfiguration : IEntityTypeConfiguration<DocumentApproval>
{
    public void Configure(EntityTypeBuilder<DocumentApproval> builder)
    {
        builder.ToTable("document_approvals");
        
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        
        builder.Property(a => a.Comments).HasMaxLength(2000);
        
        builder.HasIndex(a => a.DocumentId);
    }
}

public class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    public void Configure(EntityTypeBuilder<DocumentType> builder)
    {
        builder.ToTable("document_types");
        
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Description).HasMaxLength(500);
        
        builder.HasIndex(t => new { t.TenantId, t.Name }).IsUnique();
    }
}

public class DocumentCategoryConfiguration : IEntityTypeConfiguration<DocumentCategory>
{
    public void Configure(EntityTypeBuilder<DocumentCategory> builder)
    {
        builder.ToTable("document_categories");
        
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Description).HasMaxLength(500);
        
        builder.HasIndex(c => new { c.TenantId, c.Name }).IsUnique();
    }
}

public class DocumentAccessConfiguration : IEntityTypeConfiguration<DocumentAccess>
{
    public void Configure(EntityTypeBuilder<DocumentAccess> builder)
    {
        builder.ToTable("document_access");
        
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        
        builder.HasIndex(a => a.DocumentId);
    }
}

public class DocumentAttachmentConfiguration : IEntityTypeConfiguration<DocumentAttachment>
{
    public void Configure(EntityTypeBuilder<DocumentAttachment> builder)
    {
        builder.ToTable("document_attachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.DocumentId).IsRequired();
        builder.Property(a => a.FilePath).IsRequired().HasMaxLength(500);
        builder.Property(a => a.OriginalFileName).IsRequired().HasMaxLength(255);
        builder.Property(a => a.ContentType).HasMaxLength(100);
        builder.Property(a => a.SourceDocumentId);

        builder.HasOne(a => a.Document)
            .WithMany()
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.DocumentId);
    }
}

public class DocumentAccessLogConfiguration : IEntityTypeConfiguration<DocumentAccessLog>
{
    public void Configure(EntityTypeBuilder<DocumentAccessLog> builder)
    {
        builder.ToTable("document_access_logs");
        
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        
        builder.Property(l => l.Action).IsRequired().HasMaxLength(100);
        builder.Property(l => l.IpAddress).HasMaxLength(50);
        builder.Property(l => l.UserAgent).HasMaxLength(500);
        
        builder.HasIndex(l => l.DocumentId);
        builder.HasIndex(l => l.AccessedAt);
    }
}

public class DocumentTypeApproverConfiguration : IEntityTypeConfiguration<DocumentTypeApprover>
{
    public void Configure(EntityTypeBuilder<DocumentTypeApprover> builder)
    {
        builder.ToTable("document_type_approvers");
        
        // Composite primary key: DocumentTypeId + ApprovalOrder
        // This ensures each document type has a unique sequence of approvers
        builder.HasKey(dta => new { dta.DocumentTypeId, dta.ApprovalOrder });
        
        builder.Property(dta => dta.IsRequired).HasDefaultValue(true);
        
        // Foreign key to DocumentType
        builder.HasOne(dta => dta.DocumentType)
            .WithMany()
            .HasForeignKey(dta => dta.DocumentTypeId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Foreign key to User (Approver)
        builder.HasOne(dta => dta.Approver)
            .WithMany()
            .HasForeignKey(dta => dta.ApproverId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Index for efficient querying by DocumentTypeId
        builder.HasIndex(dta => dta.DocumentTypeId);
    }
}
using KasahQMS.Domain.Entities.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KasahQMS.Infrastructure.Persistence.Data.Configurations;

public class TaskAttachmentConfiguration : IEntityTypeConfiguration<TaskAttachment>
{
    public void Configure(EntityTypeBuilder<TaskAttachment> builder)
    {
        builder.ToTable("task_attachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.TaskId).IsRequired();
        builder.Property(a => a.FileName).IsRequired().HasMaxLength(255);
        builder.Property(a => a.StoragePath).IsRequired().HasMaxLength(500);
        builder.Property(a => a.ContentType).HasMaxLength(100);
        builder.Property(a => a.SizeBytes);
        builder.Property(a => a.UploadedBy).IsRequired();
        builder.Property(a => a.UploadedAt).IsRequired();

        builder.HasOne<QmsTask>()
            .WithMany()
            .HasForeignKey(a => a.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.TaskId);
    }
}

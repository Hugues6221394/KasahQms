using KasahQMS.Domain.Entities.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KasahQMS.Infrastructure.Persistence.Data.Configurations;

public class ChatThreadConfiguration : IEntityTypeConfiguration<ChatThread>
{
    public void Configure(EntityTypeBuilder<ChatThread> builder)
    {
        builder.ToTable("chat_threads");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.TenantId).IsRequired();
        builder.Property(t => t.Type).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(200);
        builder.Property(t => t.CreatedAt).IsRequired();

        builder.HasOne(t => t.OrganizationUnit)
            .WithMany()
            .HasForeignKey(t => t.OrganizationUnitId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(t => t.SecondOrganizationUnit)
            .WithMany()
            .HasForeignKey(t => t.SecondOrganizationUnitId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(t => t.CreatedBy)
            .WithMany()
            .HasForeignKey(t => t.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.TenantId, t.Type, t.OrganizationUnitId });
    }
}

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("chat_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.ThreadId).IsRequired();
        builder.Property(m => m.SenderId).IsRequired();
        builder.Property(m => m.Content).IsRequired().HasMaxLength(4000);
        builder.Property(m => m.CreatedAt).IsRequired();

        builder.HasOne(m => m.Thread)
            .WithMany(t => t!.Messages)
            .HasForeignKey(m => m.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ChatThreadParticipantConfiguration : IEntityTypeConfiguration<ChatThreadParticipant>
{
    public void Configure(EntityTypeBuilder<ChatThreadParticipant> builder)
    {
        builder.ToTable("chat_thread_participants");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.ThreadId).IsRequired();
        builder.Property(p => p.UserId).IsRequired();
        builder.Property(p => p.JoinedAt).IsRequired();

        builder.HasOne(p => p.Thread)
            .WithMany(t => t!.Participants)
            .HasForeignKey(p => p.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

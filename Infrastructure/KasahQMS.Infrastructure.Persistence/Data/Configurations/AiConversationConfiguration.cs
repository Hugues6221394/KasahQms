using KasahQMS.Domain.Entities.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KasahQMS.Infrastructure.Persistence.Data.Configurations;

public class AiConversationConfiguration : IEntityTypeConfiguration<AiConversation>
{
    public void Configure(EntityTypeBuilder<AiConversation> builder)
    {
        builder.ToTable("ai_conversations");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.UserId).IsRequired();
        builder.Property(c => c.ConversationKey).IsRequired().HasMaxLength(64);
        builder.Property(c => c.Title).IsRequired().HasMaxLength(200);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        builder.HasIndex(c => new { c.TenantId, c.UserId, c.ConversationKey }).IsUnique();
    }
}

public class AiConversationMessageConfiguration : IEntityTypeConfiguration<AiConversationMessage>
{
    public void Configure(EntityTypeBuilder<AiConversationMessage> builder)
    {
        builder.ToTable("ai_conversation_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.ConversationId).IsRequired();
        builder.Property(m => m.Role).IsRequired().HasMaxLength(20);
        builder.Property(m => m.Content).IsRequired().HasMaxLength(12000);
        builder.Property(m => m.CreatedAt).IsRequired();

        builder.HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

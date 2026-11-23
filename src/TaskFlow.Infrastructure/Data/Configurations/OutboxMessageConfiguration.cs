using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Data.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Channel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.PayloadJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(m => m.LastError)
            .HasMaxLength(OutboxMessage.MaxErrorLength);

        builder.HasOne<Notification>()
            .WithMany()
            .HasForeignKey(m => m.NotificationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => new { m.Channel, m.Status, m.NextAttemptAt })
            .HasDatabaseName("ix_outbox_messages_channel_status_next_attempt");

        builder.HasIndex(m => new { m.RecipientUserId, m.Channel, m.Status })
            .HasDatabaseName("ix_outbox_messages_recipient_channel_status");
    }
}

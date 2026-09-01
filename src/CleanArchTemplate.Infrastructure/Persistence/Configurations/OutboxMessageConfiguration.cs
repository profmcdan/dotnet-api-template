using CleanArchTemplate.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanArchTemplate.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(m => m.Type).HasColumnName("type").HasMaxLength(512).IsRequired();
        builder.Property(m => m.Topic).HasColumnName("topic").HasMaxLength(255).IsRequired();
        builder.Property(m => m.PartitionKey).HasColumnName("partition_key").HasMaxLength(255).IsRequired();
        builder.Property(m => m.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128);
        builder.Property(m => m.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(m => m.ProcessedAt).HasColumnName("processed_at");
        builder.Property(m => m.Attempts).HasColumnName("attempts").IsRequired();
        builder.Property(m => m.LastError).HasColumnName("last_error").HasMaxLength(2000);
        builder.Property(m => m.DeadLetteredAt).HasColumnName("dead_lettered_at");
        builder.Property(m => m.NextAttemptAt).HasColumnName("next_attempt_at");

        // The processor's hot query: unprocessed, not dead-lettered, due now, oldest first.
        builder.HasIndex(m => new { m.ProcessedAt, m.NextAttemptAt, m.OccurredAt })
            .HasDatabaseName("ix_outbox_pending")
            .HasFilter("processed_at IS NULL AND dead_lettered_at IS NULL");

        builder.HasIndex(m => m.ProcessedAt).HasDatabaseName("ix_outbox_processed_at");
    }
}

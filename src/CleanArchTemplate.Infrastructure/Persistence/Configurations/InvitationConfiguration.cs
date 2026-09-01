using CleanArchTemplate.Domain.Invitations;
using CleanArchTemplate.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanArchTemplate.Infrastructure.Persistence.Configurations;

internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(i => i.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(i => i.Email).HasColumnName("email").HasMaxLength(Email.MaxLength).IsRequired();

        // Hex-encoded SHA-256; the raw token is never written to the database.
        builder.Property(i => i.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();

        builder.Property(i => i.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(i => i.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(i => i.InvitedByUserId).HasColumnName("invited_by_user_id").IsRequired();
        builder.Property(i => i.AcceptedAt).HasColumnName("accepted_at");
        builder.Property(i => i.RevokedAt).HasColumnName("revoked_at");
        builder.Property(i => i.RevokedByUserId).HasColumnName("revoked_by_user_id");
        builder.Property(i => i.LastSentAt).HasColumnName("last_sent_at").IsRequired();
        builder.Property(i => i.SendCount).HasColumnName("send_count").IsRequired();

        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        builder.Property(i => i.CreatedBy).HasColumnName("created_by");
        builder.Property(i => i.UpdatedBy).HasColumnName("updated_by");

        builder.Ignore(i => i.DomainEvents);

        builder.HasIndex(i => i.TokenHash).IsUnique().HasDatabaseName("ix_invitations_token_hash");
        builder.HasIndex(i => i.Email).HasDatabaseName("ix_invitations_email");

        // At most one pending invitation per user, enforced by the database rather than by a read-then-write.
        builder.HasIndex(i => i.UserId)
            .IsUnique()
            .HasDatabaseName("ix_invitations_user_pending")
            .HasFilter("status = 0");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using CleanArchTemplate.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanArchTemplate.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(Email.MaxLength)
            .IsRequired()
            .HasConversion(email => email.Value, value => Email.Create(value).Value);

        builder.Property(u => u.FullName).HasColumnName("full_name").HasMaxLength(User.MaxNameLength).IsRequired();
        builder.Property(u => u.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(255);
        builder.Property(u => u.PasswordChangedAt).HasColumnName("password_changed_at");
        builder.Property(u => u.LastLoginAt).HasColumnName("last_login_at");
        builder.Property(u => u.SuspensionReason).HasColumnName("suspension_reason").HasMaxLength(500);
        builder.Property(u => u.SecurityStamp).HasColumnName("security_stamp").IsRequired();

        // Postgres text[] rather than a join table: roles are a small, always-loaded set.
        builder.Property<List<string>>("_roles")
            .HasColumnName("roles")
            .HasColumnType("text[]")
            .IsRequired();

        builder.Ignore(u => u.Roles);

        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.CreatedBy).HasColumnName("created_by");
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by");
        builder.Property(u => u.DeletedAt).HasColumnName("deleted_at");

        builder.Ignore(u => u.DomainEvents);
        builder.Ignore(u => u.CanAuthenticate);

        // Partial unique index: a revoked invitee frees its address for reuse.
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("ix_users_email_unique")
            .HasFilter("deleted_at IS NULL");

        builder.HasIndex(u => u.Status).HasDatabaseName("ix_users_status");
        builder.HasIndex(u => u.CreatedAt).HasDatabaseName("ix_users_created_at");
    }
}

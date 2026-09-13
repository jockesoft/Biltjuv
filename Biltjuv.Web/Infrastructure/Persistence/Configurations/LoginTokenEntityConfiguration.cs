using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Biltjuv.Web.Infrastructure.Persistence.Entities;

namespace Biltjuv.Web.Infrastructure.Persistence.Configurations;

public sealed class LoginTokenEntityConfiguration : IEntityTypeConfiguration<LoginTokenEntity>
{
    public void Configure(EntityTypeBuilder<LoginTokenEntity> builder)
    {
        builder.ToTable("login_token");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany(x => x.LoginTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(44) // base64 of a 32-byte SHA-256 digest
            .IsRequired();

        builder.HasIndex(x => x.TokenHash).IsUnique();

        builder.Property(x => x.ExpiresUtc)
            .HasColumnName("expires_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.ConsumedUtc)
            .HasColumnName("consumed_utc")
            .HasColumnType("timestamp with time zone");

        // Drives "invalidate every unused token for this user".
        builder.HasIndex(x => new { x.UserId, x.ConsumedUtc });

        builder.Property(x => x.CreatedUtc)
            .HasColumnName("created_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedUtc)
            .HasColumnName("updated_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}

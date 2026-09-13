using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Biltjuv.Web.Infrastructure.Persistence.Entities;

namespace Biltjuv.Web.Infrastructure.Persistence.Configurations;

public sealed class EmailEntityConfiguration : IEntityTypeConfiguration<EmailEntity>
{
    public void Configure(EntityTypeBuilder<EmailEntity> builder)
    {
        builder.ToTable("email");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.Priority)
            .HasColumnName("priority")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.ToAddress)
            .HasColumnName("to_address")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Subject)
            .HasColumnName("subject")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Body)
            .HasColumnName("body")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.HtmlBody)
            .HasColumnName("html_body")
            .HasColumnType("text");

        builder.Property(x => x.SendAttempts)
            .HasColumnName("send_attempts")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.SentUtc)
            .HasColumnName("sent_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CreatedUtc)
            .HasColumnName("created_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedUtc)
            .HasColumnName("updated_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // The timer's "what should I send next" query filters on sent_utc and
        // orders by priority, created_utc — mirror that in the index.
        builder.HasIndex(x => new { x.SentUtc, x.Priority, x.CreatedUtc })
            .HasDatabaseName("ix_email_pending");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Biltjuv.Web.Infrastructure.Persistence.Entities;

namespace Biltjuv.Web.Infrastructure.Persistence.Configurations;

public sealed class UserGameDataEntityConfiguration : IEntityTypeConfiguration<UserGameDataEntity>
{
    public void Configure(EntityTypeBuilder<UserGameDataEntity> builder)
    {
        builder.ToTable("user_game_data");

        builder.HasKey(x => x.UserId);

        builder.Property(x => x.UserId).HasColumnName("user_id");

        builder.HasOne(x => x.User)
            .WithOne(x => x.GameData)
            .HasForeignKey<UserGameDataEntity>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Money)
            .HasColumnName("money")
            .HasDefaultValue(0L)
            .IsRequired();

        builder.Property(x => x.Respect)
            .HasColumnName("respect")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.Health)
            .HasColumnName("health")
            .HasDefaultValue(100)
            .IsRequired();

        builder.Property(x => x.StolenCars)
            .HasColumnName("stolen_cars")
            .HasDefaultValue(0)
            .IsRequired();

        // References WarehouseDefinition.Id from the JSON catalog, not a Postgres row —
        // no foreign key constraint here.
        builder.Property(x => x.WarehouseId)
            .HasColumnName("warehouse_id");

        builder.Property(x => x.NextStealUtc)
            .HasColumnName("next_steal_utc")
            .HasColumnType("timestamp with time zone");

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

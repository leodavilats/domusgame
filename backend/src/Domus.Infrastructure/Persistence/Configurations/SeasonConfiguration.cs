using Domus.Domain.Seasons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domus.Infrastructure.Persistence.Configurations;

public sealed class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.ToTable("Seasons");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).HasMaxLength(80).IsRequired();
        builder.Property(s => s.StartsOn).IsRequired();
        builder.Property(s => s.EndsOn).IsRequired();
        builder.Property(s => s.Status).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();

        builder.Ignore(s => s.IsFinished);

        // RN-02: no maximo uma temporada ativa, garantido pelo banco (indice unico parcial).
        builder.HasIndex(s => s.Status)
            .IsUnique()
            .HasFilter($"\"Status\" = {(int)SeasonStatus.Active}")
            .HasDatabaseName("UX_Seasons_SingleActive");

        builder.HasMany(s => s.Podium)
            .WithOne()
            .HasForeignKey(p => p.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Podium)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class SeasonPodiumEntryConfiguration : IEntityTypeConfiguration<SeasonPodiumEntry>
{
    public void Configure(EntityTypeBuilder<SeasonPodiumEntry> builder)
    {
        builder.ToTable("SeasonPodiumEntries");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Position).IsRequired();
        builder.Property(p => p.ParticipantId).IsRequired();
        builder.Property(p => p.DisplayName).HasMaxLength(40).IsRequired();
        builder.Property(p => p.TotalPoints).IsRequired();
        builder.Property(p => p.TotalTimeMs).IsRequired();

        builder.HasIndex(p => new { p.SeasonId, p.Position }).IsUnique();
    }
}

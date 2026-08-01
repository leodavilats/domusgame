using Domus.Domain.Badges;
using Domus.Domain.Participants;
using Domus.Domain.Rooms;
using Domus.Domain.Rounds;
using Domus.Domain.Seasons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domus.Infrastructure.Persistence.Configurations;

public sealed class ParticipantBadgeConfiguration : IEntityTypeConfiguration<ParticipantBadge>
{
    public void Configure(EntityTypeBuilder<ParticipantBadge> builder)
    {
        builder.ToTable("ParticipantBadges");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.RoomId).IsRequired();
        builder.Property(b => b.ParticipantId).IsRequired();
        builder.Property(b => b.Code).IsRequired();
        builder.Property(b => b.EarnedAt).IsRequired();

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(b => b.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Participant>()
            .WithMany()
            .HasForeignKey(b => b.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Round>()
            .WithMany()
            .HasForeignKey(b => b.SourceRoundId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Season>()
            .WithMany()
            .HasForeignKey(b => b.SourceSeasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.ParticipantId, b.Code })
            .IsUnique()
            .HasDatabaseName("UX_ParticipantBadges_ParticipantCode");
    }
}

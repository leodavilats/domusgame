using Domus.Domain.Participants;
using Domus.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domus.Infrastructure.Persistence.Configurations;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("Rooms");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Name).HasMaxLength(Room.NameMaxLength).IsRequired();
        builder.Property(r => r.InviteCode).HasMaxLength(Room.InviteCodeMaxLength).IsRequired();
        builder.Property(r => r.NormalizedInviteCode).HasMaxLength(Room.InviteCodeMaxLength).IsRequired();
        builder.Property(r => r.InviteRotatedAt).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasIndex(r => r.NormalizedInviteCode).IsUnique();
    }
}

public sealed class RoomMembershipConfiguration : IEntityTypeConfiguration<RoomMembership>
{
    public void Configure(EntityTypeBuilder<RoomMembership> builder)
    {
        builder.ToTable("RoomMemberships");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.RoomId).IsRequired();
        builder.Property(m => m.ParticipantId).IsRequired();
        builder.Property(m => m.JoinedAt).IsRequired();

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(m => m.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Participant>()
            .WithMany()
            .HasForeignKey(m => m.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.RoomId, m.ParticipantId })
            .IsUnique()
            .HasDatabaseName("UX_RoomMemberships_RoomParticipant");

        builder.HasIndex(m => m.ParticipantId);
    }
}

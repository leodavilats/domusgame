using Domus.Domain.Participants;
using Domus.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domus.Infrastructure.Persistence.Configurations;

public sealed class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.ToTable("Participants");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.DisplayName)
            .HasMaxLength(Participant.DisplayNameMaxLength)
            .IsRequired();

        builder.Property(p => p.NormalizedDisplayName)
            .HasMaxLength(Participant.DisplayNameMaxLength)
            .IsRequired();

        builder.Property(p => p.AvatarUrl).HasMaxLength(500);
        builder.Property(p => p.Role).IsRequired();
        builder.Property(p => p.JoinedAt).IsRequired();
        builder.Property(p => p.IsRemoved).IsRequired();

        builder.Ignore(p => p.IsAdmin);

        builder.HasIndex(p => p.NormalizedDisplayName).IsUnique();
        builder.HasIndex(p => p.IsRemoved);

        builder.HasOne<AppUser>()
            .WithOne()
            .HasForeignKey<Participant>(p => p.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

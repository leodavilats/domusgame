using Domus.Domain.Attempts;
using Domus.Domain.Participants;
using Domus.Domain.Rounds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domus.Infrastructure.Persistence.Configurations;

public sealed class AttemptConfiguration : IEntityTypeConfiguration<Attempt>
{
    public void Configure(EntityTypeBuilder<Attempt> builder)
    {
        builder.ToTable("Attempts");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.RoundId).IsRequired();
        builder.Property(a => a.ParticipantId).IsRequired();
        builder.Property(a => a.StartedAt).IsRequired();
        builder.Property(a => a.Status).IsRequired();
        builder.Property(a => a.QuestionCount).IsRequired();
        builder.Property(a => a.TotalPoints).IsRequired();
        builder.Property(a => a.CorrectCount).IsRequired();
        builder.Property(a => a.TotalTimeMs).IsRequired();

        builder.Ignore(a => a.OrderedAnswers);
        builder.Ignore(a => a.IsFinished);
        builder.Ignore(a => a.AnsweredCount);
        builder.Ignore(a => a.MaxPoints);
        builder.Ignore(a => a.NextQuestionOrder);

        builder.OwnsOne(a => a.Scoring, scoring =>
        {
            scoring.Property(s => s.PointsPerCorrectAnswer).HasColumnName("Scoring_PointsPerCorrectAnswer").IsRequired();
            scoring.Property(s => s.MaxSpeedBonus).HasColumnName("Scoring_MaxSpeedBonus").IsRequired();
            scoring.Property(s => s.QuestionTimeLimitSeconds).HasColumnName("Scoring_QuestionTimeLimitSeconds").IsRequired();
            scoring.Ignore(s => s.MaxPointsPerQuestion);
        });
        builder.Navigation(a => a.Scoring).IsRequired();

        builder.HasMany(a => a.Answers)
            .WithOne()
            .HasForeignKey(x => x.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(a => a.Answers).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<Round>()
            .WithMany()
            .HasForeignKey(a => a.RoundId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Participant>()
            .WithMany()
            .HasForeignKey(a => a.ParticipantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.RoundId, a.ParticipantId })
            .IsUnique()
            .HasDatabaseName("UX_Attempts_RoundParticipant");

        builder.HasIndex(a => new { a.RoundId, a.TotalPoints, a.TotalTimeMs });
        builder.HasIndex(a => new { a.ParticipantId, a.RoundId });
    }
}

public sealed class AttemptAnswerConfiguration : IEntityTypeConfiguration<AttemptAnswer>
{
    public void Configure(EntityTypeBuilder<AttemptAnswer> builder)
    {
        builder.ToTable("AttemptAnswers");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.AttemptId).IsRequired();
        builder.Property(a => a.QuestionId).IsRequired();
        builder.Property(a => a.QuestionOrder).IsRequired();
        builder.Property(a => a.ServedAt).IsRequired();
        builder.Property(a => a.Outcome).IsRequired();
        builder.Property(a => a.BasePoints).IsRequired();
        builder.Property(a => a.SpeedBonus).IsRequired();
        builder.Property(a => a.ElapsedMs).IsRequired();

        builder.Ignore(a => a.IsPending);
        builder.Ignore(a => a.IsCorrect);
        builder.Ignore(a => a.Points);

        builder.HasOne<Question>()
            .WithMany()
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AnswerOption>()
            .WithMany()
            .HasForeignKey(a => a.SelectedOptionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.AttemptId, a.QuestionId })
            .IsUnique()
            .HasDatabaseName("UX_AttemptAnswers_AttemptQuestion");

        builder.HasIndex(a => new { a.AttemptId, a.QuestionOrder });
        builder.HasIndex(a => new { a.QuestionId, a.Outcome });
    }
}

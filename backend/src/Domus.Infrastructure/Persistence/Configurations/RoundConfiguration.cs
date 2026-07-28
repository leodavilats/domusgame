using Domus.Domain.Rounds;
using Domus.Domain.Seasons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domus.Infrastructure.Persistence.Configurations;

public sealed class RoundConfiguration : IEntityTypeConfiguration<Round>
{
    public void Configure(EntityTypeBuilder<Round> builder)
    {
        builder.ToTable("Rounds", table =>
        {
            table.HasCheckConstraint("CK_Rounds_Window", "\"ClosesAt\" > \"OpensAt\"");
            table.HasCheckConstraint("CK_Rounds_Scoring",
                "\"Scoring_PointsPerCorrectAnswer\" BETWEEN 1 AND 100 AND " +
                "\"Scoring_MaxSpeedBonus\" BETWEEN 0 AND 50 AND " +
                "\"Scoring_QuestionTimeLimitSeconds\" BETWEEN 10 AND 300");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.SeasonId).IsRequired();
        builder.Property(r => r.WeekNumber).IsRequired();
        builder.Property(r => r.Title).HasMaxLength(120).IsRequired();
        builder.Property(r => r.OpensAt).IsRequired();
        builder.Property(r => r.ClosesAt).IsRequired();
        builder.Property(r => r.Status).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.Ignore(r => r.OrderedQuestions);
        builder.Ignore(r => r.IsDraft);
        builder.Ignore(r => r.IsPublished);
        builder.Ignore(r => r.MaxPoints);

        // Licao como owned type: colunas na propria tabela, sem join.
        builder.OwnsOne(r => r.Lesson, lesson =>
        {
            lesson.Property(l => l.Title).HasColumnName("Lesson_Title").HasMaxLength(160).IsRequired();
            lesson.Property(l => l.ScriptureReference).HasColumnName("Lesson_ScriptureReference").HasMaxLength(160).IsRequired();
            lesson.Property(l => l.Content).HasColumnName("Lesson_Content").IsRequired();
            lesson.Property(l => l.ExternalUrl).HasColumnName("Lesson_ExternalUrl").HasMaxLength(500);
            lesson.Ignore(l => l.IsComplete);
        });
        builder.Navigation(r => r.Lesson).IsRequired();

        builder.OwnsOne(r => r.Scoring, scoring =>
        {
            scoring.Property(s => s.PointsPerCorrectAnswer).HasColumnName("Scoring_PointsPerCorrectAnswer").IsRequired();
            scoring.Property(s => s.MaxSpeedBonus).HasColumnName("Scoring_MaxSpeedBonus").IsRequired();
            scoring.Property(s => s.QuestionTimeLimitSeconds).HasColumnName("Scoring_QuestionTimeLimitSeconds").IsRequired();
            scoring.Ignore(s => s.MaxPointsPerQuestion);
        });
        builder.Navigation(r => r.Scoring).IsRequired();

        builder.HasMany(r => r.Questions)
            .WithOne()
            .HasForeignKey(q => q.RoundId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Questions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<Season>()
            .WithMany()
            .HasForeignKey(r => r.SeasonId)
            .OnDelete(DeleteBehavior.Restrict);

        // RN-11: uma rodada por semana em cada temporada.
        builder.HasIndex(r => new { r.SeasonId, r.WeekNumber }).IsUnique();
        builder.HasIndex(r => new { r.SeasonId, r.Status, r.OpensAt });
        builder.HasIndex(r => new { r.Status, r.OpensAt, r.ClosesAt });
    }
}

public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Questions");

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();

        builder.Property(q => q.RoundId).IsRequired();
        builder.Property(q => q.Order).IsRequired();
        builder.Property(q => q.Text).HasMaxLength(500).IsRequired();
        builder.Property(q => q.MediaType).IsRequired();
        builder.Property(q => q.MediaUrl).HasMaxLength(500);
        builder.Property(q => q.Explanation).HasMaxLength(1000);

        builder.Ignore(q => q.CorrectOption);

        builder.HasMany(q => q.Options)
            .WithOne()
            .HasForeignKey(o => o.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(q => q.Options).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(q => new { q.RoundId, q.Order }).IsUnique();
    }
}

public sealed class AnswerOptionConfiguration : IEntityTypeConfiguration<AnswerOption>
{
    public void Configure(EntityTypeBuilder<AnswerOption> builder)
    {
        builder.ToTable("AnswerOptions");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.QuestionId).IsRequired();
        builder.Property(o => o.Order).IsRequired();
        builder.Property(o => o.Text).HasMaxLength(300).IsRequired();
        builder.Property(o => o.IsCorrect).IsRequired();

        builder.HasIndex(o => new { o.QuestionId, o.Order }).IsUnique();
    }
}

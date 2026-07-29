using Domus.Domain.Attempts;
using Domus.Domain.Participants;
using Domus.Domain.Rounds;
using Domus.Domain.Seasons;
using Domus.Domain.Settings;
using Domus.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Domus.Infrastructure.Persistence;

public sealed class DomusDbContext(DbContextOptions<DomusDbContext> options)
    : IdentityUserContext<AppUser, Guid>(options)
{
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<SeasonPodiumEntry> SeasonPodiumEntries => Set<SeasonPodiumEntry>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<AnswerOption> AnswerOptions => Set<AnswerOption>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<AttemptAnswer> AttemptAnswers => Set<AttemptAnswer>();
    public DbSet<GcSettings> GcSettings => Set<GcSettings>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(DomusDbContext).Assembly);
    }
}

using Domus.Domain.Attempts;
using Domus.Domain.Rounds;
using Xunit;

namespace Domus.Domain.Tests;

public class OptionShufflerTests
{
    [Fact]
    public void Mesma_tentativa_produz_sempre_a_mesma_ordem()
    {
        var round = TestData.PublishedRound(questionCount: 1);
        var question = round.QuestionAtOrder(1)!;
        var attemptId = Guid.CreateVersion7();

        var first = OptionShuffler.ShuffleFor(attemptId, question).Select(o => o.Id).ToArray();
        var second = OptionShuffler.ShuffleFor(attemptId, question).Select(o => o.Id).ToArray();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Ordem_preserva_todas_as_alternativas()
    {
        var round = TestData.PublishedRound(questionCount: 1);
        var question = round.QuestionAtOrder(1)!;

        var shuffled = OptionShuffler.ShuffleFor(Guid.CreateVersion7(), question);

        Assert.Equal(question.Options.Count, shuffled.Count);
        Assert.Equal(
            question.Options.Select(o => o.Id).OrderBy(id => id),
            shuffled.Select(o => o.Id).OrderBy(id => id));
    }

    [Fact]
    public void Tentativas_diferentes_produzem_ordens_diferentes_na_maioria_dos_casos()
    {
        var round = TestData.PublishedRound(questionCount: 1);
        var question = round.QuestionAtOrder(1)!;
        var reference = OptionShuffler.ShuffleFor(Guid.CreateVersion7(), question).Select(o => o.Id).ToArray();

        var different = 0;
        for (var i = 0; i < 30; i++)
        {
            var other = OptionShuffler.ShuffleFor(Guid.CreateVersion7(), question).Select(o => o.Id).ToArray();
            if (!other.SequenceEqual(reference)) different++;
        }

        // Com 3 alternativas, a chance de 30 sorteios sairem todos iguais e desprezivel.
        Assert.True(different > 10, $"Embaralhamento pouco variado: {different}/30");
    }
}

public class ArchitectureTests
{
    /// <summary>
    /// Doc 05, secao 4: a camada de dominio nao pode conhecer EF Core, ASP.NET nem Identity.
    /// Se alguem adicionar a referencia por conveniencia, este teste falha.
    /// </summary>
    [Fact]
    public void Dominio_nao_depende_de_infraestrutura()
    {
        string[] forbidden =
        [
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "Microsoft.Extensions.Identity",
            "Npgsql"
        ];

        var referenced = typeof(Round).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        var violations = referenced
            .Where(name => forbidden.Any(f => name.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert.Empty(violations);
    }
}

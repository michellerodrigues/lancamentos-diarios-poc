namespace MeusLancamentosDiarios.Integrator.TestSupport;

/// <summary>
/// Base dos cenários no estilo Case / When / Then.
///
/// Cada classe de teste descreve UM contexto — "quando o publish falha", "quando a
/// conta não tem predecessor". O xUnit cria a classe e chama <c>InitializeAsync</c>
/// antes de cada <c>[Fact]</c>, então:
///
/// - <see cref="Case"/>  monta o cenário (dublês, dados, expectativas)
/// - <see cref="When"/>  executa a ação única daquele contexto
/// - cada <c>[Fact]</c> afirma um Then, isolado dos demais
///
/// Um Then por fato: quando um quebra, o nome do teste já diz qual comportamento
/// se perdeu, sem precisar abrir o código.
/// </summary>
public abstract class Cenario : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        Case();
        await When();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Dado que… monta o estado inicial do cenário.</summary>
    protected abstract void Case();

    /// <summary>Quando… executa a ação sob teste.</summary>
    protected abstract Task When();
}

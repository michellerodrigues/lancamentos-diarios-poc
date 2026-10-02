using MeusLancamentosDiarios.Integrator.Events.Data;
using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using MeusLancamentosDiarios.Integrator.Events.Publishing;
using MeusLancamentosDiarios.Integrator.Events.Publishing.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MeusLancamentosDiarios.Integrator.Events;

/// <summary>
/// Registro compartilhado pelos tres processos. A WebApi, o Events e o Consumer
/// apontam para o mesmo arquivo SQLite, entao usam exatamente este mesmo registro.
/// </summary>
public static class LancamentosServiceCollectionExtensions
{
    /// <summary>Acesso a tabela LancamentosDiarios e ao read model SaldoConsolidado.</summary>
    public static IServiceCollection AddLancamentosData(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BancoOptions>()
            .Bind(configuration.GetSection(BancoOptions.Secao))
            .Validate(o => o.Valida, "Banco precisa de ConnectionString e TimeoutComandoSegundos maior que zero.")
            .ValidateOnStart();

        services.AddSingleton<IDbConnectionFactory, SqlServerConnectionFactory>();
        services.AddSingleton<ILancamentoRepository, LancamentoRepository>();
        services.AddSingleton<DatabaseBootstrapper>();

        return services;
    }

    /// <summary>Cliente de publicacao no Pub/Sub e criacao de topico/subscription.</summary>
    public static IServiceCollection AddPubSub(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PubSubOptions>()
            .Bind(configuration.GetSection(PubSubOptions.Secao))
            .Validate(o => o.Valida, "PubSub precisa de ProjectId, Topico e Subscription.")
            .ValidateOnStart();

        services.AddSingleton<PubSubProvisionador>();
        services.AddSingleton<IPublicadorEventos, PubSubPublicador>();

        return services;
    }

    /// <summary>O relay (Cadastrado -> Lido -> Enfileirado).</summary>
    public static IServiceCollection AddRelay(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RelayOptions>()
            .Bind(configuration.GetSection(RelayOptions.Secao))
            .Validate(o => o.Valida,
                "Relay precisa de Intervalo, TamanhoLote, MaxTentativas, BackoffMaximo e ReservaExpiraEm maiores que zero.")
            .ValidateOnStart();
        services.AddSingleton<RelayLancamentos>();

        return services;
    }
}

using MeusLancamentosDiarios.Integrator.Bff.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Conversores;
using MeusLancamentosDiarios.Integrator.Bff.Conversores.Contracts;
using MeusLancamentosDiarios.Integrator.Bff.Services.Contracts;
using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;
using MeusLancamentosDiarios.Integrator.Messages.Saldo;

namespace MeusLancamentosDiarios.Integrator.Bff.Services;

public static class ServicesServiceCollectionExtensions
{
    /// <summary>Services das telas e os conversores de-para que eles usam.</summary>
    public static IServiceCollection AddTelaServices(this IServiceCollection services)
    {
        services.AddSingleton<IConversor<LancamentoPendenteResponse, PendenteTela>, PendenteTelaConversor>();
        services.AddSingleton<IConversor<SaldoResponse, SaldoTela>, SaldoTelaConversor>();
        services.AddSingleton<IConversor<CriarLancamentoResponse, LancamentoCriadoTela>, LancamentoCriadoTelaConversor>();

        // Scoped: dependem do cliente HTTP tipado, que vive por requisicao.
        services.AddScoped<ISaldoService, SaldoService>();
        services.AddScoped<ILancamentoService, LancamentoService>();
        services.AddScoped<IAutenticacaoService, AutenticacaoService>();

        return services;
    }
}

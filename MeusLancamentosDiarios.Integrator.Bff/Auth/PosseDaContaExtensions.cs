namespace MeusLancamentosDiarios.Integrator.Bff.Auth;

public static class PosseDaContaExtensions
{
    public static TBuilder ExigirPosseDaConta<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder
            .AddEndpointFilter<TBuilder, PosseDaContaFilter>()
            .WithMetadata(new ExigePosseDaConta());
}

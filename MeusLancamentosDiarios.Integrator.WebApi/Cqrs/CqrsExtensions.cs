using System.Reflection;
using FluentValidation;
using MeusLancamentosDiarios.Integrator.WebApi.Cqrs.Contracts;

namespace MeusLancamentosDiarios.Integrator.WebApi.Cqrs;

public static class CqrsExtensions
{
    private static readonly Type[] HandlersAbertos =
    [
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>)
    ];

    /// <summary>
    /// Varre o assembly e registra todo handler e todo validator encontrado.
    /// Uma feature nova passa a funcionar so por existir, sem editar o Program.
    /// </summary>
    public static IServiceCollection AddCqrs(this IServiceCollection services, Assembly assembly)
    {
        services.AddScoped<IDispatcher, Dispatcher>();

        var registros =
            from tipo in assembly.GetTypes()
            where tipo is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }
            from contrato in tipo.GetInterfaces()
            where contrato.IsGenericType
                  && HandlersAbertos.Contains(contrato.GetGenericTypeDefinition())
            select (Contrato: contrato, Implementacao: tipo);

        foreach (var (contrato, implementacao) in registros)
        {
            services.AddScoped(contrato, implementacao);
        }

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        return services;
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace MeusLancamentosDiarios.Integrator.WebApi.Auth;

/// <summary>
/// Poe o botao "Authorize" no Swagger e marca com cadeado so os endpoints que exigem
/// login, dizendo qual policy cada um pede. O BFF tem uma copia: os dois nao
/// compartilham assembly.
/// </summary>
internal sealed class SegurancaBearerNoOpenApi : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    private const string Esquema = "Bearer";

    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes[Esquema] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Cole o tokenAcesso devolvido por POST /auth/login."
        };

        return Task.CompletedTask;
    }

    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadados = context.Description.ActionDescriptor.EndpointMetadata;

        if (metadados.OfType<IAllowAnonymous>().Any()) return Task.CompletedTask;

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = Esquema }
            }] = []
        });

        // Sem policy explicita, vale a de fallback: so exige estar logado.
        var politicas = metadados.OfType<IAuthorizeData>()
            .Select(a => a.Policy)
            .OfType<string>()
            .Distinct()
            .ToList();

        var acesso = politicas.Count > 0 ? string.Join(" + ", politicas) : "usuario autenticado";

        if (metadados.OfType<ExigePosseDaConta>().Any())
        {
            acesso += " + posse da conta";
        }
        operation.Description = $"**Acesso:** {acesso}." +
                                (operation.Description is null ? "" : $"\n\n{operation.Description}");

        return Task.CompletedTask;
    }
}

using Google.Apis.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Externos.Contracts;
using Microsoft.Extensions.Options;

namespace MeusLancamentosDiarios.Integrator.WebApi.Auth.Externos;

/// <summary>
/// Confere o ID token que o botao do Google entrega ao front: assinatura pelas chaves
/// publicas do Google, emissor, validade e se a audiencia e o nosso Client ID.
/// </summary>
public sealed class ValidadorGoogle(IOptions<AuthOptions> options, ILogger<ValidadorGoogle> logger)
    : IValidadorGoogle
{
    public async Task<IdentidadeGoogle?> ValidarAsync(
        string credencial, CancellationToken cancellationToken = default)
    {
        try
        {
            // ValidateAsync nao recebe CancellationToken.
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                credencial,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [options.Value.Google.ClientId] });

            return new IdentidadeGoogle(
                payload.Subject,
                payload.Email,
                string.IsNullOrWhiteSpace(payload.Name) ? payload.Email : payload.Name,
                payload.EmailVerified);
        }
        catch (InvalidJwtException ex)
        {
            logger.LogWarning("Credencial do Google recusada: {Motivo}", ex.Message);
            return null;
        }
    }
}

using MeusLancamentosDiarios.Integrator.Common.Auth;
using MeusLancamentosDiarios.Integrator.TestSupport;
using MeusLancamentosDiarios.Integrator.WebApi.Auth;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Data.Contracts;
using MeusLancamentosDiarios.Integrator.WebApi.Auth.Tokens;
using MeusLancamentosDiarios.Integrator.WebApi.Features.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace MeusLancamentosDiarios.Integrator.WebApi.Tests.Features.Auth;

/// <summary>
/// Base dos cenarios de autenticacao.
///
/// O repositorio e dublê estrito. Hasher, emissor de token e abertura de sessao sao
/// reais: o que se quer verificar e o token que sai e o que vai para o banco, e um
/// dublê ali esconderia justamente isso.
/// </summary>
public abstract class CenarioDeAutenticacao : Cenario
{
    protected static readonly DateTimeOffset Agora = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    protected const string SenhaCorreta = "Senha1234";

    /// <summary>Calculado uma vez: o PBKDF2 e lento de proposito.</summary>
    protected static readonly string HashDaSenhaCorreta =
        new PasswordHasher<UsuarioEntity>().HashPassword(new UsuarioEntity(), SenhaCorreta);

    protected readonly Mock<IUsuarioRepository> Repositorio = new(MockBehavior.Strict);

    protected readonly FakeTimeProvider Relogio = new(Agora);

    protected readonly PasswordHasher<UsuarioEntity> Hasher = new();

    /// <summary>Os mesmos valores do appsettings.json: o codigo nao tem padrao para eles.</summary>
    protected readonly IOptions<AuthOptions> Opcoes = Options.Create(new AuthOptions
    {
        Jwt = new JwtOptions
        {
            Emissor = "lancamentos-webapi",
            Audiencia = "lancamentos",
            Chave = "chave-de-teste-com-bem-mais-de-32-bytes",
            ValidadeAcesso = TimeSpan.FromMinutes(15),
            ValidadeRenovacao = TimeSpan.FromDays(7)
        },
        Google = new GoogleOptions { ClientId = "client-id-de-teste" },
        RedefinicaoSenha = new RedefinicaoSenhaOptions
        {
            UrlDoFront = "http://localhost:4200/redefinir-senha",
            Validade = TimeSpan.FromMinutes(30)
        }
    });

    /// <summary>A falha que o When capturou, quando o handler recusa.</summary>
    protected Exception? Falha;

    /// <summary>Hash do token de renovacao que a sessao gravou, quando gravou.</summary>
    protected byte[]? RenovacaoGravada;

    protected AberturaDeSessao Sessao() =>
        new(Repositorio.Object, new EmissorDeTokens(Opcoes, Relogio), Opcoes, Relogio);

    protected CadastroDeCliente Cadastro() => new(Repositorio.Object, Hasher, Relogio);

    protected void DadoQueASessaoPodeSerGravada() =>
        Repositorio
            .Setup(r => r.GravarTokenAsync(
                It.IsAny<Guid>(), FinalidadeTokenEnum.Renovacao, It.IsAny<byte[]>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, FinalidadeTokenEnum, byte[], DateTimeOffset, CancellationToken>(
                (_, _, hash, _, _) => RenovacaoGravada = hash)
            .Returns(Task.CompletedTask);

    /// <summary>Cliente da conta ABC1234 com <see cref="SenhaCorreta"/>.</summary>
    protected static UsuarioEntity UmUsuario() => new()
    {
        Id = Guid.CreateVersion7(),
        Email = "cliente@lancamentos.local",
        Nome = "Cliente ABC1234",
        SenhaHash = HashDaSenhaCorreta,
        GoogleId = null,
        ContaId = "ABC1234",
        Role = Roles.Cliente,
        CriadoEm = Agora
    };

    protected async Task<T?> Capturar<T>(Func<Task<T>> acao) where T : class
    {
        try
        {
            return await acao();
        }
        catch (Exception ex)
        {
            Falha = ex;
            return null;
        }
    }
}

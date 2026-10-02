using Dapper;
using MeusLancamentosDiarios.Integrator.Common.Auth;
using MeusLancamentosDiarios.Integrator.Events.Data;
using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MeusLancamentosDiarios.Integrator.WebApi.Auth.Data;

/// <summary>
/// Esquema de usuarios e, no ambiente local, os usuarios de demonstracao.
///
/// Fica na WebApi, e nao no DatabaseBootstrapper compartilhado: so a WebApi conhece
/// usuario. O Events e o Consumer continuam sem saber que login existe.
/// </summary>
public sealed class AuthBootstrapper(
    IDbConnectionFactory factory,
    IPasswordHasher<UsuarioEntity> hasher,
    IOptions<AuthOptions> options,
    TimeProvider relogio,
    ILogger<AuthBootstrapper> logger)
{
    /// <summary>Senha de todos os usuarios de demonstracao.</summary>
    public const string SenhaDemo = "Demo@2026";

    /// <summary>
    /// ABC1234 e XYZ9999 ja tinham lancamentos antes de existir login: ganham um dono.
    /// O admin tambem tem conta propria, porque todo usuario tem exatamente uma.
    /// </summary>
    public static readonly IReadOnlyList<(string Email, string Nome, string ContaId, string Role)> UsuariosDemo =
    [
        ("admin@lancamentos.local", "Administrador", "ADM0001", Roles.Admin),
        ("cliente@lancamentos.local", "Cliente ABC1234", "ABC1234", Roles.Cliente),
        ("cliente2@lancamentos.local", "Cliente XYZ9999", "XYZ9999", Roles.Cliente)
    ];

    private const string Esquema = """
        IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.Usuarios (
                Id        uniqueidentifier  NOT NULL CONSTRAINT PK_Usuarios PRIMARY KEY,
                Email     nvarchar(254)     NOT NULL,
                Nome      nvarchar(100)     NOT NULL,
                SenhaHash varchar(200)      NULL,
                GoogleId  varchar(100)      NULL,
                ContaId   varchar(20)       NOT NULL,
                Role      varchar(20)       NOT NULL,
                CriadoEm  datetimeoffset(7) NOT NULL,
                CONSTRAINT UX_Usuarios_Email UNIQUE (Email),

                -- Uma conta, um dono. E o que garante "cada usuario ve so a sua".
                CONSTRAINT UX_Usuarios_ContaId UNIQUE (ContaId)
            );
        END;

        -- A coluna nasceu Papel e passou a Role. Banco criado antes e renomeado
        -- aqui, uma vez: depois do rename a condicao nao vale mais.
        IF COL_LENGTH(N'dbo.Usuarios', N'Papel') IS NOT NULL AND COL_LENGTH(N'dbo.Usuarios', N'Role') IS NULL
        BEGIN
            EXEC sp_rename N'dbo.Usuarios.Papel', N'Role', N'COLUMN';
        END;

        -- Filtrado: varios usuarios sem Google (NULL) nao contam como duplicata.
        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = 'UX_Usuarios_GoogleId' AND object_id = OBJECT_ID(N'dbo.Usuarios'))
        BEGIN
            CREATE UNIQUE INDEX UX_Usuarios_GoogleId ON dbo.Usuarios (GoogleId) WHERE GoogleId IS NOT NULL;
        END;

        -- Tokens opacos: renovacao de sessao e redefinicao de senha. So o hash fica aqui.
        IF OBJECT_ID(N'dbo.TokensDeUsuario', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.TokensDeUsuario (
                Id          uniqueidentifier  NOT NULL CONSTRAINT PK_TokensDeUsuario PRIMARY KEY,
                UsuarioId   uniqueidentifier  NOT NULL
                            CONSTRAINT FK_TokensDeUsuario_Usuarios REFERENCES dbo.Usuarios (Id),
                Finalidade  tinyint           NOT NULL,
                TokenHash   binary(32)        NOT NULL,
                CriadoEm    datetimeoffset(7) NOT NULL,
                ExpiraEm    datetimeoffset(7) NOT NULL,
                ConsumidoEm datetimeoffset(7) NULL,
                CONSTRAINT UX_TokensDeUsuario_Hash UNIQUE (TokenHash)
            );
        END;

        -- Serve a revogacao: todos os tokens vivos de um usuario.
        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = 'IX_TokensDeUsuario_Usuario' AND object_id = OBJECT_ID(N'dbo.TokensDeUsuario'))
        BEGIN
            CREATE INDEX IX_TokensDeUsuario_Usuario
                ON dbo.TokensDeUsuario (UsuarioId, Finalidade) WHERE ConsumidoEm IS NULL;
        END;
        """;

    public async Task GarantirAsync(CancellationToken cancellationToken = default)
    {
        await using var conexao = await factory.AbrirAsync(cancellationToken);

        await conexao.ExecuteAsync(new CommandDefinition(
            Esquema,
            commandTimeout: factory.TimeoutComandoSegundos,
            cancellationToken: cancellationToken));

        if (options.Value.SemearUsuariosDemo)
        {
            await SemearAsync(cancellationToken);
        }
    }

    private async Task SemearAsync(CancellationToken cancellationToken)
    {
        // Idempotente: quem ja existe (pelo e-mail ou pela conta) fica como esta,
        // inclusive se a senha tiver sido trocada.
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WHERE Email = @Email OR ContaId = @ContaId)
                INSERT INTO dbo.Usuarios (Id, Email, Nome, SenhaHash, GoogleId, ContaId, Role, CriadoEm)
                VALUES (@Id, @Email, @Nome, @SenhaHash, NULL, @ContaId, @Role, @CriadoEm);
            """;

        await using var conexao = await factory.AbrirAsync(cancellationToken);

        foreach (var (email, nome, conta, role) in UsuariosDemo)
        {
            var usuario = new UsuarioEntity
            {
                Id = Guid.CreateVersion7(),
                Email = email,
                Nome = nome,
                ContaId = conta,
                Role = role,
                CriadoEm = relogio.GetUtcNow()
            };

            usuario.SenhaHash = hasher.HashPassword(usuario, SenhaDemo);

            await conexao.ExecuteAsync(new CommandDefinition(
                sql, usuario,
                commandTimeout: factory.TimeoutComandoSegundos,
                cancellationToken: cancellationToken));
        }

        logger.LogWarning(
            "Usuarios de demonstracao garantidos ({Quantidade}). Nao ligue Auth:SemearUsuariosDemo em producao.",
            UsuariosDemo.Count);
    }
}

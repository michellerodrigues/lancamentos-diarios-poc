using Microsoft.Data.SqlClient;

namespace MeusLancamentosDiarios.Integrator.Events.Data.Contracts;

public interface IDbConnectionFactory
{
    /// <summary>Conexao aberta no banco da aplicacao.</summary>
    Task<SqlConnection> AbrirAsync(CancellationToken cancellationToken = default);

    /// <summary>Conexao aberta no master, usada so para criar o banco na subida.</summary>
    Task<SqlConnection> AbrirNoServidorAsync(CancellationToken cancellationToken = default);

    /// <summary>Servidor e banco, sem a senha.</summary>
    string Descricao { get; }

    int TimeoutComandoSegundos { get; }
}

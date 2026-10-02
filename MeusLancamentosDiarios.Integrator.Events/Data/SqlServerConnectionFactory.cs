using MeusLancamentosDiarios.Integrator.Events.Data.Contracts;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace MeusLancamentosDiarios.Integrator.Events.Data;

public sealed class SqlServerConnectionFactory(IOptions<BancoOptions> options) : IDbConnectionFactory
{
    private readonly BancoOptions _opcoes = options.Value;

    public string Descricao => _opcoes.Descricao;

    public int TimeoutComandoSegundos => _opcoes.TimeoutComandoSegundos;

    public async Task<SqlConnection> AbrirAsync(CancellationToken cancellationToken = default)
    {
        var conexao = new SqlConnection(_opcoes.ConnectionString);
        await conexao.OpenAsync(cancellationToken);

        return conexao;
    }

    public async Task<SqlConnection> AbrirNoServidorAsync(CancellationToken cancellationToken = default)
    {
        var conexao = new SqlConnection(_opcoes.ConnectionStringDoServidor);
        await conexao.OpenAsync(cancellationToken);

        return conexao;
    }
}

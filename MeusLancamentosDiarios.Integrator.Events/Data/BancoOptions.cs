using Microsoft.Data.SqlClient;

namespace MeusLancamentosDiarios.Integrator.Events.Data;

public sealed class BancoOptions
{
    public const string Secao = "Banco";

    /// <summary>
    /// Conexao com o SQL Server. Local aponta para o container do docker-compose;
    /// no GCP, para a instancia do Cloud SQL atraves do Auth Proxy.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    public int TimeoutComandoSegundos { get; set; }

    /// <summary>
    /// Cria o banco na subida quando ele ainda nao existe. Util no desenvolvimento,
    /// em que o container sobe vazio. Em producao o banco vem do provisionamento e
    /// a aplicacao nao deve ter permissao para cria-lo — deixe false.
    /// </summary>
    public bool CriarBanco { get; set; }

    public bool Valida => !string.IsNullOrWhiteSpace(ConnectionString) && TimeoutComandoSegundos > 0;

    public string NomeDoBanco =>
        new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;

    /// <summary>
    /// A mesma conexao apontada para o banco master, para poder criar o banco da
    /// aplicacao antes de conectar nele.
    /// </summary>
    public string ConnectionStringDoServidor =>
        new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" }.ToString();

    /// <summary>Servidor e banco, sem a senha — seguro para log.</summary>
    public string Descricao
    {
        get
        {
            var b = new SqlConnectionStringBuilder(ConnectionString);
            return $"{b.DataSource}/{b.InitialCatalog}";
        }
    }
}

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace MeusLancamentosDiarios.Integrator.WebApi.Auth.Tokens;

/// <summary>
/// Token aleatorio sem significado proprio, para renovacao de sessao e redefinicao de
/// senha. O texto vai para o cliente; o banco guarda so o hash.
///
/// SHA-256 simples, sem salt nem PBKDF2: com 256 bits aleatorios nao ha dicionario a
/// atacar, e o hash precisa ser deterministico para servir de chave de busca.
/// </summary>
public sealed record SegredoOpaco(string Texto, byte[] Hash)
{
    public static SegredoOpaco Gerar()
    {
        var texto = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        return new SegredoOpaco(texto, Hashear(texto));
    }

    public static byte[] Hashear(string texto) => SHA256.HashData(Encoding.UTF8.GetBytes(texto));
}

# POCMica.MeusLancamentosDiarios.Integrator.Messages

Requests e responses da WebApi da POC **Lançamentos Diários**: o que o BFF envia, o que
ele lê e o que qualquer outro cliente da WebApi precisaria. Os enums e helpers que esses
contratos usam vêm do pacote `POCMica.MeusLancamentosDiarios.Integrator.Common`, que é
instalado junto.

```bash
dotnet add package POCMica.MeusLancamentosDiarios.Integrator.Messages
```

## Por endpoint

| Endpoint | Request | Response |
|---|---|---|
| `POST /lancamentos` | `CriarLancamentoCommand` | `CriarLancamentoResponse` |
| `POST /lancamentos/lote` | `CriarLancamentosEmLoteCommand` | `CriarLancamentosEmLoteResponse`, com um `LancamentoDoLoteResponse` por item |
| `GET /lancamentos/{id}` | — | `LancamentoDetalheResponse` |
| `GET /saldo/{contaId}` | — | `SaldoResponse`, com os `LancamentoPendenteResponse` |
| `POST /auth/registrar` | `RegistrarUsuarioCommand` | `SessaoResponse` |
| `POST /auth/login` | `LoginCommand` | `SessaoResponse` |
| `POST /auth/google` | `LoginGoogleCommand` | `SessaoResponse` |
| `POST /auth/renovar` | `RenovarSessaoCommand` | `SessaoResponse` |
| `POST /auth/sair` | `EncerrarSessaoCommand` | — |
| `POST /auth/esqueci-senha` | `SolicitarRedefinicaoSenhaCommand` | — |
| `POST /auth/redefinir-senha` | `RedefinirSenhaCommand` | — |
| `GET /auth/config` | — | `ConfiguracaoAuthResponse` |
| `GET /auth/eu` | — | `UsuarioResponse` |

Namespaces: `MeusLancamentosDiarios.Integrator.Messages.Lancamentos`, `.Saldo` e `.Auth`.
Erros voltam como `ProblemDetails` (RFC 9457).

## Exemplo

```csharp
using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Messages.Lancamentos;

var comando = new CriarLancamentoCommand
{
    ContaId = "ABC1234",
    Tipo = TipoLancamentoEnum.Credito,
    Valor = 150.75m,
    DataLancamento = DateOnly.FromDateTime(DateTime.Today)
};

var resposta = await http.PostAsJsonAsync("/lancamentos", comando);
var criado = await resposta.Content.ReadFromJsonAsync<CriarLancamentoResponse>();
```

Enums viajam como texto (`"Credito"`), então o `JsonSerializerOptions` do cliente
precisa do `JsonStringEnumConverter`.

Pacote de POC, sem garantia de compatibilidade entre versões.

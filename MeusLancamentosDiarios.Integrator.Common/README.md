# POCMica.MeusLancamentosDiarios.Integrator.Common

O que o BFF, a WebApi e os workers da POC **Lançamentos Diários** têm em comum e não é
request nem response: enums, helpers, roles e a regra de posse da conta. Sem nenhuma
dependência além do .NET 9 — nem ASP.NET Core, para servir também aos workers.

```bash
dotnet add package POCMica.MeusLancamentosDiarios.Integrator.Common
```

## O que tem dentro

| Namespace | Conteúdo |
|---|---|
| `MeusLancamentosDiarios.Integrator.Common` | `TipoLancamentoEnum`, `StageLancamentoEnum` |
| `MeusLancamentosDiarios.Integrator.Common.Contracts` | `IReferenciaConta`: algo que aponta para uma conta |
| `MeusLancamentosDiarios.Integrator.Common.Cqrs.Contracts` | `ICommand<T>` e `IQuery<T>` |
| `MeusLancamentosDiarios.Integrator.Common.Helpers` | `DinheiroHelper` (centavos ↔ reais) e `LancamentoHelper` (moeda, data e rótulos em pt-BR) |
| `MeusLancamentosDiarios.Integrator.Common.Auth` | `Roles`, `Politicas`, `ClaimsDoToken` e a regra de posse (`ContaId()`, `IsAdmin()`, `PodeAcessarConta()`) |

## Exemplo

```csharp
using System.Security.Claims;
using MeusLancamentosDiarios.Integrator.Common;
using MeusLancamentosDiarios.Integrator.Common.Auth;
using MeusLancamentosDiarios.Integrator.Common.Helpers;

long centavos = DinheiroHelper.ParaCentavos(150.75m);          // 15075
string tela = LancamentoHelper.Moeda(150.75m);                 // "R$ 150,75"
string sinal = LancamentoHelper.Sinal(TipoLancamentoEnum.Debito);

bool pode = usuario.PodeAcessarConta("ABC1234");               // admin: qualquer conta
```

Pacote de POC, sem garantia de compatibilidade entre versões.

# ADR-SW-14 — Sessão do front no `localStorage`

- **Status:** Aceita
- **Escopo:** `.FrontEnd`

## Contexto

A sessão é um JWT de 15 min e um token de renovação opaco, de uso único, válido por 7 dias
([ADR-SOL-06](../../solucao/03-adrs/ADR-SOL-06-identidade-propria-jwt.md)). O app é PWA: precisa
continuar logado depois de um F5 e quando é reaberto do ícone.

## Decisão

- A sessão inteira (`tokenAcesso`, `expiraEm`, `tokenRenovacao`, `usuario`) fica no
  `localStorage`, na chave `lancamentos.sessao`, lida uma vez ao carregar o app.
- O `autenticacaoInterceptor` põe o Bearer em toda chamada ao BFF, menos em `/api/auth/*`.
- **No primeiro 401, uma renovação, e só uma por vez**: várias telas recebendo 401 juntas
  compartilham a mesma promessa (`renovacaoEmCurso`). O pedido é repetido com o token novo.
- Renovação recusada descarta a sessão e leva a `/entrar`. O logout descarta localmente e
  avisa o servidor; sem rede, a renovação só vence.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Cookie `HttpOnly` emitido pelo BFF, com os tokens guardados no servidor | Mais forte contra XSS. Exige sessão no BFF, proteção contra CSRF e muda o contrato de autenticação. É o caminho recomendado para produção |
| `sessionStorage` | Não sobrevive ao fechar a aba nem ao reabrir o PWA |
| Só em memória | Login a cada F5 |

## Consequências

**Ganhos**
- Simples, sobrevive ao F5 e ao PWA reaberto.
- A renovação única evita que duas renovações paralelas pareçam roubo de token.

**Custos**
- **Ao alcance de um XSS.** Um script injetado lê o token de renovação, que vale 7 dias. O
  acesso curto, a rotação e a detecção de reuso limitam o estrago, mas não há CSP hoje.
- **Duas abas derrubam a sessão.** A segunda aba ainda tem o token de renovação que a
  primeira já trocou; o servidor entende como reuso e revoga todas as sessões do usuário
  (lacuna 9 de [requisitos-funcionais.md](../../../requisitos-funcionais.md#lacunas)). A
  correção é ouvir o evento `storage` para sincronizar a sessão entre abas, e coordenar a
  renovação com `BroadcastChannel` ou Web Locks.
- O JWT no armazenamento leva e-mail e nome em texto legível (base64).

## Onde está no código

- [`core/auth.service.ts`](../../../../MeusLancamentosDiarios.Integrator.FrontEnd/src/app/core/auth.service.ts)
- [`core/auth.interceptor.ts`](../../../../MeusLancamentosDiarios.Integrator.FrontEnd/src/app/core/auth.interceptor.ts)

## Revisitar quando

Antes de usuários reais: mover para cookie `HttpOnly`, `Secure`, `SameSite=Strict` emitido
pelo BFF, e ligar uma CSP no nginx. Ver o plano em
[06 · Segurança](../../solucao/06-seguranca-e-threat-model.md#10-plano-de-ação).

# ADR-SOL-11 — PWA em vez de app de loja

- **Status:** Aceita

## Contexto

O comerciante usa o celular no balcão. Um app nativo pede duas bases de código (Android e
iOS), revisão de loja a cada versão e distribuição própria.

## Decisão

- O front é um **PWA**: manifest, ícones de 72 a 512 px e o service worker do Angular, ligado
  só no build de produção.
- Instalação pelo navegador, sem loja: no Android, pelo aviso ou pelo menu do Chrome; no iOS,
  por "Adicionar à Tela de Início".
- O service worker guarda os arquivos do app; o nginx serve `index.html` e `ngsw.json` sem
  cache, para a versão nova ser percebida.
- Sem rede, a tela mantém o último saldo recebido e avisa. Lançar exige conexão.

## Alternativas consideradas

| Alternativa | Por que não |
|---|---|
| Apps nativos | Duas bases de código e duas lojas |
| Capacitor ou Ionic | Útil se a App Store virar requisito; embrulha o mesmo front |
| Flutter | Outra base de código |

A Play Store aceita o PWA embrulhado como TWA (Bubblewrap); a App Store exige um app nativo
em volta.

## Consequências

**Ganhos**
- Uma base de código; atualização sem loja.

**Custos**
- Exige HTTPS de verdade: sem ele o service worker não registra e não há instalação.
- O iOS não sugere a instalação; o usuário precisa saber o caminho.
- A versão nova entra na próxima abertura do app: o BFF precisa aceitar a versão anterior do
  front por um tempo ([compatibilidade](../../software/04-contratos-internos-e-apis.md#9-versionamento-e-compatibilidade)).
- Uso sem rede é parcial: reaberto sem rede, o app não mostra saldo (RF-16).

## Revisitar quando

Notificações (por exemplo, "lançamento consolidado") ou presença na App Store virarem
requisito.

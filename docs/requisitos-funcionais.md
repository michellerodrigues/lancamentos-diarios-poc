# Requisitos funcionais

O que a POC faz, com as regras de cada função e onde elas estão no código. Levantado no
commit `342dcc9`. As classes citadas podem ser procuradas pelo nome no repositório;
quando há duas com o mesmo nome, o projeto vem entre parênteses.

## De onde vêm os requisitos

- **O desafio.** Um comerciante precisa controlar o fluxo de caixa diário com
  lançamentos de crédito e débito, e precisa de um relatório com o saldo diário
  consolidado. Os requisitos não funcionais do mesmo desafio estão em
  [requisitos-nao-funcionais.md](requisitos-nao-funcionais.md).
- **As telas.** O `Saldo Consolidado.pdf`, na raiz, traz dois mockups: a tela de saldo
  (saldo consolidado, última atualização, pendentes por crédito e débito, saldo projetado
  e o aviso de consolidação em andamento) e a tela de novo lançamento (tipo, valor, data
  e observação).
- **O que a POC acrescentou.** Autenticação, perfis, posse da conta, lote, PWA e dados de
  demonstração.

## Resumo

| Grupo | Requisito | Situação |
|---|---|---|
| Lançamentos | [RF-01](#rf-01--registrar-um-lançamento) Registrar um lançamento | Atendido |
| | [RF-02](#rf-02--lançar-em-lote) Lançar em lote | Atendido: só pela API, de propósito |
| | [RF-03](#rf-03--consultar-um-lançamento) Consultar um lançamento | Parcial: só na WebApi, sem BFF nem tela |
| Saldo | [RF-04](#rf-04--ver-o-saldo) Ver o saldo consolidado, os pendentes e o saldo projetado | Parcial: os totais de pendentes por tipo não aparecem |
| | [RF-05](#rf-05--acompanhar-a-consolidação) Acompanhar a consolidação sem recarregar | Atendido |
| | [RF-06](#rf-06--consolidar-o-saldo) Consolidar o saldo, em ordem, de forma assíncrona | Parcial: sob falha, pode deixar lançamento de fora |
| | [RF-07](#rf-07--escolher-a-conta) Escolher a conta (Admin) | Atendido |
| | [RF-08](#rf-08--saldo-diário-consolidado) Saldo diário consolidado e relatório por período | **Não atendido** |
| Acesso | [RF-09](#rf-09--cadastro) Cadastro | Atendido |
| | [RF-10](#rf-10--login-com-e-mail-e-senha) Login com e-mail e senha | Atendido |
| | [RF-11](#rf-11--login-com-google) Login com Google | Atendido, depende de configuração |
| | [RF-12](#rf-12--renovação-de-sessão-e-logout) Renovação de sessão e logout | Atendido |
| | [RF-13](#rf-13--esqueci-a-senha) Esqueci a senha e redefinição | Atendido |
| | [RF-14](#rf-14--perfis-e-posse-da-conta) Perfis e posse da conta | Atendido |
| Apoio | [RF-15](#rf-15--dados-de-demonstração) Dados de demonstração | Atendido |
| | [RF-16](#rf-16--pwa-e-uso-sem-rede) PWA e uso sem rede | Parcial |

## Lançamentos

### RF-01 — Registrar um lançamento

Pela tela de novo lançamento: front → `POST /api/lancamentos` no BFF → `POST /lancamentos`
na WebApi. O lançamento é gravado na hora e consolidado depois.

- **Tipo:** crédito ou débito. A tela abre em crédito.
- **Valor:** maior que zero, até R$ 1.000.000,00, com no máximo duas casas. É gravado em
  centavos. A tela usa máscara de caixa.
- **Data:** obrigatória e não futura no fuso de São Paulo. A tela abre em hoje e não deixa
  escolher depois de hoje.
- **Observação:** opcional, até 200 caracteres; em branco vira nula.
- **Conta:** de 3 a 20 caracteres, letras, números e hífen, normalizada em maiúsculas. O
  cliente não escolhe: o BFF usa a conta do token. O admin pode informar outra conta; sem
  informar, lança na própria.
- **Posse:** conferida no BFF e de novo na WebApi.
- **Sequência:** cada conta numera os próprios lançamentos (`MAX + 1`, com índice único).
- **Resposta:** 201 com o número do lançamento na conta e o estágio. A tela mostra a
  confirmação e recarrega o saldo.
- **Erros de validação:** voltam como ProblemDetails e aparecem na tela.

Onde: `CriarLancamentoValidator`, `CriarLancamentoHandler`, `LancamentoService` (BFF),
`novo-lancamento.ts`.

### RF-02 — Lançar em lote

`POST /lancamentos/lote`, direto na WebApi e só para o admin, sem tela nem rota no BFF.
Serve para simular volume.

- De 1 a 1.000 itens, em contas quaisquer. Cada item segue as regras do RF-01, e o erro
  aponta o item (`Itens[3].Valor`).
- Entra numa transação só: tudo ou nada. Dentro de cada conta, a sequência segue a ordem
  da lista.
- Não publica no request. Tudo nasce aguardando o relay, que publica um lançamento por
  conta por ciclo.

Onde: `CriarLancamentosEmLoteValidator`, `CriarLancamentosEmLoteHandler`,
`LancamentoRepository.InserirLoteAsync`. O
[script de demonstração](../scripts/inicializar-banco-demo.ps1) usa este endpoint.

### RF-03 — Consultar um lançamento

`GET /lancamentos/{id}` na WebApi devolve o lançamento e o ponto da esteira em que ele
está: estágio, tentativas de envio e de processamento, e o erro, se houver. A posse é
conferida na saída, porque a conta só é conhecida depois de ler o lançamento. Id
inexistente devolve 404.

**Parcial:** não há rota no BFF nem tela. Na tela, o estágio aparece só para os pendentes
e na confirmação de um lançamento novo. O BFF ainda responde o 201 do lançamento com
`Location: /api/lancamentos/{id}`, uma rota que ele não tem.

Onde: `ObterLancamentoHandler`, `LancamentosEndpoints`.

## Saldo

### RF-04 — Ver o saldo

Tela de saldo: front → `GET /api/saldo/{conta}` no BFF → `GET /saldo/{conta}` na WebApi.

- **Saldo consolidado:** o que o Consumer já aplicou, com a hora da última atualização em
  Brasília. Conta que nunca consolidou mostra R$ 0,00 e "nunca".
- **"Consolidado até o lançamento nº N":** diz até onde o saldo está em dia.
- **Pendentes:** os lançamentos ainda não consolidados, até 20, do mais recente para o mais
  antigo, com número, tipo, estágio e valor.
- **Saldo projetado:** consolidado + créditos pendentes − débitos pendentes.
- **Aviso:** "Consolidação em andamento", o mesmo texto do mockup, enquanto houver
  pendente.
- **Formatação:** o BFF entrega moeda, data e rótulos já em pt-BR.

**Parcial:**
- no mockup, os pendentes aparecem como uma linha de crédito e uma de débito que fecham com
  o saldo projetado; este documento as lê como totais por tipo. O BFF devolve esses totais,
  mas a tela mostra a lista item a item, sem eles;
- a lista para em 20 itens sem avisar, e aí deixa de fechar com o saldo projetado;
- um lançamento em Erro some da tela: não conta como pendente nem entra no projetado.

Onde: `ObterSaldoHandler`, `LancamentoRepository.ObterSaldoAsync`, `SaldoTelaConversor`,
`saldo-consolidado.html`.

### RF-05 — Acompanhar a consolidação

Enquanto há pendente, o BFF manda o front consultar de novo em 2 s; sem pendente, manda
parar. A tela se atualiza sozinha e para ao sair dela. Uma tela aberta antes de um
lançamento novo só volta a consultar quando é recarregada ou quando o próprio usuário
lança.

Onde: `SaldoTelaConversor` (`intervaloPollingMs`), `lancamentos.service.ts`.

### RF-06 — Consolidar o saldo

O relay publica no Pub/Sub, e o Consumer aplica cada lançamento ao saldo.

- **Estágios:** Cadastrado → Lido → Enfileirado → Em processamento → Consolidado, ou Erro.
- **Ordem por conta:** a ordering key é a conta, e um lançamento não é publicado enquanto
  houver um anterior da mesma conta esperando publicação.
- **Sem soma em dobro:** cada transição confere o estágio esperado, e marcar como
  consolidado e somar ao saldo acontecem na mesma transação.
- **Retentativas:** 5 na publicação, com espera crescente; 5 na consolidação; depois,
  Erro.

**Parcial:** no caminho feliz, tudo consolida em ordem e uma vez só. Sob falha, um
lançamento pode ficar fora do saldo sem que nada o recupere:
- se o Consumer cai entre marcar o lançamento como em processamento e somá-lo, a
  reentrega é tratada como repetida e o lançamento fica pendente para sempre;
- se o Consumer põe um lançamento em Erro, os seguintes da conta consolidam por cima, e
  "consolidado até o lançamento nº N" deixa de garantir que tudo antes de N entrou;
- não há ferramenta para reprocessar um lançamento em Erro.

Os detalhes e as correções estão em [requisitos-nao-funcionais.md](requisitos-nao-funcionais.md).
O saldo é **por conta**, não por dia (ver RF-08).

Onde: `RelayLancamentos`, `ConsolidadorDeMensagem`, `LancamentoRepository`.

### RF-07 — Escolher a conta

O admin vê um campo de conta na tela de saldo, com as contas que já têm lançamento como
sugestão (`GET /contas`). O cliente vê a própria conta, fixa.

Onde: `ListarContasHandler`, `saldo-consolidado.html`.

### RF-08 — Saldo diário consolidado

**Não atendido.** O desafio pede um relatório com o saldo diário consolidado. A POC tem só
o saldo corrente de cada conta:

- a tabela `SaldoConsolidado` tem uma linha por conta, sem data;
- a consolidação soma ao saldo da conta sem olhar a data do lançamento, então um
  lançamento retroativo entra no saldo de hoje;
- nenhuma consulta filtra ou agrupa pela data do lançamento, e não há rota nem tela de
  relatório ou extrato.

Os dados necessários já são gravados: data, tipo e valor de cada lançamento. Faltam um
read model por conta e dia (ou uma consulta agregada por data), um endpoint com período e
a tela.

## Acesso

### RF-09 — Cadastro

`POST /auth/registrar` e a tela "Criar conta". Devolve a sessão já aberta.

- Nome obrigatório; e-mail válido, normalizado em minúsculas; senha de 8 a 128 caracteres,
  com ao menos uma letra e um número. A tela pede confirmação da senha.
- E-mail já cadastrado devolve 409.
- O usuário nasce cliente, com uma conta sorteada no formato `AAA0000` que nunca teve dono
  nem lançamento.
- A senha é guardada com PBKDF2.

### RF-10 — Login com e-mail e senha

`POST /auth/login` e a tela "Entrar". Devolve um JWT de 15 min e um token de renovação de
7 dias. E-mail inexistente e senha errada recebem a mesma recusa, no mesmo tempo.

### RF-11 — Login com Google

`POST /auth/google` recebe o token do Google Identity Services. A WebApi confere a
assinatura e se o token foi emitido para o nosso Client ID.

- Google já vinculado: entra. E-mail já cadastrado: vincula e entra. E-mail novo: cadastra
  como cliente.
- O primeiro acesso exige e-mail verificado pelo Google.
- Sem Client ID configurado, o botão não aparece e o endpoint recusa. Vem desligado por
  padrão; o README explica como ligar.

### RF-12 — Renovação de sessão e logout

- **Renovação** (`POST /auth/renovar`): o token de renovação vale uma vez e é trocado a
  cada uso. Um token já trocado que reaparece derruba todas as sessões do usuário. O front
  renova sozinho no primeiro 401, uma renovação por vez.
- **Logout** (`POST /auth/sair`): revoga a renovação. O JWT já emitido vale até vencer.

### RF-13 — Esqueci a senha

- **Pedido** (`POST /auth/esqueci-senha`): responde 202 exista o e-mail ou não. Manda por
  e-mail um link de uso único, válido por 30 min; um pedido novo invalida o anterior. No
  ambiente local, o e-mail fica no Mailpit, em http://localhost:8025.
- **Redefinição** (`POST /auth/redefinir-senha`): a senha nova segue a regra do cadastro e
  é validada antes de gastar o link. Redefinir revoga todas as renovações de sessão; um JWT
  já emitido vale até vencer, em no máximo 15 min.

### RF-14 — Perfis e posse da conta

- **Perfis:** cliente e admin. O cliente lança e consulta a própria conta. O admin lança e
  consulta qualquer conta, lança em lote e lista as contas.
- **Fechado por padrão:** rota sem regra exige login; o que é público declara isso.
- **Uma conta por usuário,** garantida por índice único.
- **Posse:** conferida no BFF, para recusar cedo, e na WebApi, que guarda o dado. Conta
  alheia devolve 403, e a tela mostra "Você não tem acesso a esta conta."

Também há `GET /auth/eu`, que devolve o usuário autenticado. O front não usa: ele já
recebe o usuário na sessão.

Onde: `Politicas`, `PosseDaContaFilter` (WebApi e BFF), `UsuarioAutenticadoExtensions`.

## Apoio

### RF-15 — Dados de demonstração

- Com `Auth:SemearUsuariosDemo` ligado (no compose e no Development), a WebApi cria três
  usuários com a senha `Demo@2026`: `admin@`, `cliente@` e `cliente2@lancamentos.local`.
- O [script de demonstração](../scripts/inicializar-banco-demo.ps1) lança uma semana de
  movimento nessas contas, pelo lote, e confere o saldo de cada uma. O README explica o uso.

### RF-16 — PWA e uso sem rede

O front é instalável como aplicativo: manifest, ícones e service worker no build de
produção. Sem rede, a tela mantém o último saldo recebido e avisa "Sem conexão".

**Parcial:**
- as respostas da API não ficam em cache, e o último saldo vive só na memória: reaberto
  sem rede, o app não mostra saldo;
- lançar exige conexão;
- o endereço do BFF está fixo em `localhost`.

## Desafio e mockups × construído

| Pedido | Como a POC atende | Situação |
|---|---|---|
| Controle de lançamentos de crédito e débito | RF-01, RF-02 e RF-06 | Atendido |
| Relatório do saldo diário consolidado | Só o saldo corrente por conta (RF-08) | **Não atendido** |
| Tela de saldo: saldo consolidado e última atualização | RF-04 | Atendido |
| Tela de saldo: pendentes numa linha de crédito e numa de débito | Lista item a item, sem os totais por tipo (RF-04) | Parcial |
| Tela de saldo: saldo projetado e aviso de consolidação | RF-04 e RF-05 | Atendido |
| Tela de novo lançamento: tipo, valor, data e observação | RF-01 | Atendido |

## Lacunas

1. **Saldo diário consolidado** (RF-08). É a lacuna principal frente ao desafio.
2. **Extrato.** Depois de consolidado, o lançamento some da tela; não há como ver o
   histórico da conta.
3. **Totais de pendentes por tipo** na tela, como o mockup sugere (RF-04). O BFF já os
   devolve.
4. **Lançamento em Erro não aparece.** Não entra nos pendentes nem no saldo projetado, e
   não há rota nem tela para listar ou reprocessar. Detalhes em
   [requisitos-nao-funcionais.md](requisitos-nao-funcionais.md).
5. **Correção de lançamento.** Não há edição, exclusão nem estorno: corrigir só com um
   lançamento no sentido contrário.
6. **Gestão de usuários.** Todo cadastro nasce cliente; um admin só existe pelos dados de
   demonstração ou mexendo no banco.
7. **Cadastro de contas.** A conta passa a existir no primeiro lançamento, e o admin pode
   lançar numa conta sem dono.
8. **"Hoje" em dois fusos.** A tela calcula a data máxima no fuso do aparelho, e o servidor
   no de São Paulo. Com o aparelho adiantado em relação a São Paulo, a tela aceita uma data
   que o servidor recusa com 400. Com o aparelho atrasado, a tela bloqueia uma data que o
   servidor aceitaria.
9. **Duas abas derrubam a sessão.** O front lê a sessão do armazenamento local só ao abrir.
   Com duas abas, ou o PWA e uma aba, a segunda tenta renovar com o token que a primeira
   já trocou. O servidor entende como token roubado e encerra todas as sessões do usuário.
10. **Data do lançamento sem limite inferior.** Uma data como 02/01/0001 é aceita, o que vai
    pesar quando existir o saldo por dia.

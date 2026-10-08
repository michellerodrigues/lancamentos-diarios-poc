# ADR-SOL-06 — Identidade própria com JWT e Google federado

- **Status:** Aceita

## Contexto

O token precisa dizer quem é o usuário, o que ele pode fazer (role) e sobre qual conta (a
posse é conferida sem ir ao banco). A POC não tem um provedor de identidade contratado, e
login com Google é conveniente para o comerciante.

## Decisão

| Peça | Como |
|---|---|
| Emissor | Só a WebApi, em `/auth/*`. O BFF expõe as mesmas rotas como repasse |
| Token de acesso | JWT HS256, 15 min, com `sub`, `email`, `name`, `jti`, `conta` e `role` |
| Chave | Mínimo de 32 bytes; a mesma na WebApi (emite e valida) e no BFF (valida). Sem ela nada sobe |
| Renovação | Token opaco de 256 bits, uso único, 7 dias. O banco guarda só o SHA-256. Cada renovação troca o par; um token já trocado que reaparece derruba todas as sessões do usuário |
| Logout | Revoga a renovação; o JWT emitido só vence |
| Senha | PBKDF2 pelo `PasswordHasher` do ASP.NET Core; e-mail sem cadastro gasta o mesmo tempo que senha errada |
| Redefinição | Link de uso único, 30 min, por e-mail; resposta 202 exista o e-mail ou não; redefinir revoga as renovações |
| Google | O front obtém o ID token pelo Google Identity Services; a WebApi confere assinatura e audiência. Primeiro acesso cadastra como Cliente; e-mail já cadastrado é vinculado se o Google o der como verificado |

## Alternativas consideradas

| Alternativa | Por que não, por ora |
|---|---|
| Identity Platform ou Firebase Authentication | Gerenciados no GCP, com MFA, política de senha e proteção contra força bruta. Forte candidato para produção; a conta e a role iriam em claims customizados |
| Auth0, Okta, Keycloak | Custo ou operação a mais para uma POC |
| RS256 com JWKS | É o próximo passo: a WebApi assina com a chave privada, publica a pública, e o BFF deixa de ter um segredo capaz de emitir token |
| Sessão por cookie no BFF | Mais forte contra XSS no navegador; ver [ADR-SW-14](../../software/03-adrs/ADR-SW-14-sessao-no-localstorage.md) |

## Consequências

**Ganhos**
- Controle total do token; a posse é decidida pelo claim `conta`.
- A role é relida do banco a cada renovação: uma troca de perfil vale em até 15 min.
- Sem custo de provedor.

**Custos**
- Toda a segurança de senha é nossa: não há limite de tentativas, bloqueio nem MFA
  ([RNF-24](../../../requisitos-nao-funcionais.md#segurança)).
- Com HS256, o BFF pode emitir token.
- Só uma chave é aceita por vez. Trocá-la invalida os JWT já emitidos; o front renova no
  primeiro 401 e segue, porque o token de renovação é opaco e não depende da chave. O cuidado
  é trocar a WebApi e o BFF juntos, ou aceitar as duas chaves durante os 15 min de transição.
- O JWT não pode ser revogado antes de vencer.
- O cadastro responde 409 para e-mail existente: revela quem tem cadastro.
- A vinculação automática do Google a um cadastro com senha, pelo e-mail verificado, é um
  risco aceito e registrado aqui.

## Revisitar quando

Antes de usuários reais: limitar tentativas, aceitar duas chaves durante a rotação, e
escolher entre RS256 com JWKS ou Identity Platform. O plano está em
[segurança](../06-seguranca-e-threat-model.md#10-plano-de-ação).

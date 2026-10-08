# ADR-SOL-12 — E-mail transacional por SMTP genérico

- **Status:** Aceita; provedor a definir

## Contexto

O único e-mail do sistema é o link de redefinição de senha. No local, ele precisa ser
visível sem conta em provedor nenhum. Na nuvem, o provedor ainda não foi escolhido.

## Decisão

- **SMTP genérico** (`SmtpEnviadorEmail`), configurado em `Auth:Email`: host, porta, SSL,
  usuário, senha, remetente.
- **No local, Mailpit** no compose: guarda todo e-mail e mostra em `http://localhost:8025`.
- **Na nuvem, um relay SMTP de provedor**, trocado só por configuração. A senha do SMTP fica
  no Secret Manager.
- O envio acontece dentro da requisição de "esqueci a senha"; falha de SMTP vai para o log e
  a resposta é 202 assim mesmo.

## Alternativas consideradas

| Alternativa | Por que não, por ora |
|---|---|
| API HTTP do provedor (SendGrid, Mailgun, SES) | Mais recursos (modelos, eventos de entrega), mas amarra o código ao fornecedor |
| Gmail API | Amarrada a uma conta Google; não é feita para e-mail transacional |

## Consequências

**Ganhos**
- Trocar de provedor é trocar configuração.
- No local, o fluxo inteiro de redefinição é testável sem internet.

**Custos**
- **Envio dentro da requisição.** A resposta demora mais quando o e-mail existe, o que pode
  revelar quem tem cadastro pelo tempo (RNF-20), e uma lentidão do SMTP vira lentidão da tela.
- `System.Net.Mail.SmtpClient` é marcado pela Microsoft como não recomendado para
  desenvolvimento novo, por não suportar protocolos modernos; o substituto usual é o MailKit.
- Sem rastreio de entrega nem de devolução.
- O domínio remetente precisa de SPF, DKIM e DMARC para o e-mail não cair em spam.
- No GCP a porta 25 de saída é bloqueada. Usar a 587 com STARTTLS (`UsarSsl=true`): o
  `SmtpClient` não fala o TLS implícito da porta 465.

## Revisitar quando

Antes de produção: tirar o envio da requisição (gravar o pedido e enviar em segundo plano,
pelo mesmo padrão de outbox) e trocar o `SmtpClient` pelo MailKit.

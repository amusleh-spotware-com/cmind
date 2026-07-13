---
description: "Registro de usuário autoatendimento seguro com portão de rótulo branco — uma página de inscrição no aplicativo e uma API de provisionamento servidor para servidor, com atributos de usuário configuráveis, aprovação de administrador ou portão de verificação de e-mail e guardas anti-abuso. Desabilitado por padrão."
---

# Registro de usuário

Por padrão o **proprietário/administrador adiciona usuários manualmente** (página Usuários → *Novo Usuário*). Para implantações de rótulo branco
que precisam integrar usuários em escala — ou integrar o aplicativo com outro serviço — cMind também envia um
**caminho de auto-registro seguro**. É **desabilitado por padrão**: uma implantação estoque é inalterada
e tanto a página quanto a API retornam 404 até que uma implantação opte por entrar.

Existem dois pontos de entrada compartilhando um fluxo de domínio:

1. **Página no aplicativo** (`/register`) — uma página de inscrição marcada, mobile-first no mesmo shell que `/login`.
2. **API de provisionamento** (`POST /api/provision`) — um ponto de extremidade servidor para servidor para um serviço integrador
   criar contas, autenticado por um segredo de provisionamento por implantação.

## O que é registrado — minimização de dados

cMind é **ferramentas**: ele compila/executa/faz backtest de cBots e espelha negociações sobre credenciais Open API cTrader *próprias* de cada usuário.
Ele **não abre contas de negociação ou custodia de dinheiro de cliente**, então verificação de identidade KYC/AML é o
obrigação do **corretor**, não dessa plataforma. O formulário de registro portanto
registra **apenas um e-mail por padrão** — o mínimo necessário para fornecer o serviço (GDPR Art. 5(1)(c) dados
minimização; base legal = contrato). cMind deliberadamente **não** envia campos de ID nacional / data de nascimento /
endereço.

Todos os outros atributos são **opt-in por implantação** via `App:Registration:Attributes`, cada independentemente
`Off` / `Optional` / `Required`:

| Atributo | Notas |
|---|---|
| `FullName`, `DisplayName`, `Company` | Texto livre, comprimento-limitado. |
| `Country` | ISO 3166-1 alpha-2, validado contra um conjunto de código fixo. |
| `Phone` | Formato E.164 (`+14155552671`). |
| `Locale` | Forma BCP-47 (`en-US`), normalizado. |
| `MarketingOptIn` | Separado, caixa de seleção **desativada** — nunca agrupado com o consentimento obrigatório (CAN-SPAM). |
| `AgeConfirmation` | Uma caixa de seleção apenas; **nenhuma** data de nascimento é armazenada. |

Atributos vivem no objeto de valor `UserProfile` possuído pelo agregado `AppUser`, validado em
construção. **Apagamento GDPR** (`AppUser.Anonymize()`) limpa o perfil e qualquer token de verificação.

**Consentimento.** Quando `RequireTermsAcceptance` está ativado, o usuário deve aceitar os documentos legais publicados
(Termos, Privacidade, Divulgação de Risco). Aceitação é registrada através do agregado existente `ConsentRecord` —
versão-carimbada, com carimbo de data/hora, com IP de origem — o mesmo armazenamento usado em outro lugar para manutenção de registros MiFID/ESMA-grade.

## Modos de portão

Uma conta auto-registrada não pode entrar até que limpe seu portão (`App:Registration:Mode`):

- **`AdminApproval`** (padrão) — a conta é enfileirada; um proprietário/administrador a aprova na página **Usuários**
  (seção *Aprovação Pendente*). Não precisa de infraestrutura de e-mail.
- **`EmailVerification`** — um link de verificação único e expirando é enviado por e-mail; a conta é ativada quando
  o link é aberto. Requer um transporte de e-mail (`App:Email`). **Se nenhum transporte for configurado, este modo
  automaticamente rebaixa para `AdminApproval`** na inicialização, então habilitando o registro nunca silenciosamente quebra.
- **`Open`** — a conta é ativa imediatamente (confiável/dev apenas).

Usuários auto-registrados são sempre criados como **`User`** (ou `Viewer` se configurado) — o domínio
**duro-recusa** emitir um Proprietário/Administrador através de auto-registro.

## Segurança & anti-abuso

- **Anti-enumeração.** Um e-mail duplicado produz o **mesmo** `202 Accepted` neutro quanto um sign-up fresco e
  cria nada — o aplicativo nunca disclosa se um endereço já tem uma conta.
- **Limite de taxa.** Os pontos de extremidade públicos são acelerados por IP (mais difícil do que o limitador de autenticação).
- **Política de senha.** Comprimento mínimo imposto; senhas são hashadas (Argon2 via `IPasswordHasher`);
  tokens de verificação são armazenados apenas como hashes SHA-256 e são para uso único + expirando.
- **Higiene de e-mail.** Lista de permissões opcional de domínios de e-mail e lista de bloqueio de provedor descartável.
- **CAPTCHA (opcional).** reCAPTCHA / hCaptcha / Turnstile através de seu contrato de verificação compartilhado.
- **Portão de login.** Uma conta pendente é recusada no login com resposta neutra.

## API de provisionamento (integração)

Com `App:Registration:Api:Enabled` e um `Secret` definido, outro serviço pode criar usuários:

```
POST /api/provision
X-Provision-Secret: <o segredo configurado>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

O segredo é comparado em tempo constante. Contas provisionadas são criadas **ativas** (ou convidadas com
`MustChangePassword`) dependendo de `Api.ActivateImmediately` / `Api.InviteMustChangePassword`.

## Habilitando

Registro requer **ambos** o sinalizador de recurso e o comutador mestre:

```jsonc
"App": {
  "Features": { "Registration": true },
  "Registration": {
    "Enabled": true,
    "Mode": "AdminApproval",           // ou EmailVerification / Open
    "DefaultRole": "User",             // nunca Proprietário/Administrador
    "RequireTermsAcceptance": true,
    "AllowedEmailDomains": [],          // vazio = qualquer
    "BlockDisposableEmail": true,
    "Attributes": { "FullName": "Optional", "Country": "Off" },
    "Api": { "Enabled": false, "Secret": "" }
  }
}
```

A seção `App:Email` (SMTP `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`,
`FromName`) configura o transporte usado pelo modo `EmailVerification`; deixe `Host` não definido para executar sem
e-mail (o remetente sem operação). Veja [alternâncias de características](./feature-toggles.md) e [rótulo branco](./white-label.md) para
como implantações ativam características e remarcam. Quando registro é habilitado, a página de login mostra um **Criar
conta** link.

## Testado

Unidade (validação de perfil, guarda de função `SelfRegister`, transições de ativação, tokens para uso único, apagamento),
integração (404 desabilitado por padrão, fluxo de aprovação, rebaixamento de verificação de e-mail, anti-enumeração, guardas de abuso,
atributos obrigatórios, provisionamento + segredo ruim) e E2E (padrão-off login não tem link de inscrição; o
página `/register` renderiza seu estado fechado marcado).

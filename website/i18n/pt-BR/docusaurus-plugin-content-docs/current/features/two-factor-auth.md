---
description: "Autenticação dois-fatores TOTP opcional com inscrição do aplicativo autenticador, códigos de backup para uso único e um comutador de rótulo branco para torná-lo obrigatório para todos os usuários."
---

# Autenticação dois-fatores (2FA)

Contas podem ser protegidas com **autenticação dois-fatores de senha única baseada em tempo (TOTP)** no topo
da senha. É **opt-in** do perfil do usuário por padrão e uma implantação de rótulo branco pode fazer
isto **obrigatório** para todos. Qualquer aplicativo autenticador RFC 6238 funciona — Google Authenticator, Microsoft
Authenticator, Authy, Aegis, FreeOTP — porque a implementação é padrão (SHA-1, 6 dígitos, passo de 30 segundos); nenhum componente de servidor proprietário está envolvido.

## Como funciona

- **Domínio.** MFA vive no agregado `AppUser` (contexto de Acesso). Um usuário é inscrito através
  métodos de intenção clara — `BeginMfaEnrollment`, `ConfirmMfaEnrollment`, `ConsumeBackupCode`,
  `RegenerateBackupCodes`, `DisableMfa` — então os invariantes (um segredo deve ser confirmado antes de ativar;
  um código de backup é para uso único) são impostos em um local.
- **TOTP.** Geração e verificação ficam atrás da interface Core `ITotpAuthenticator`, implementada em
  Infraestrutura com a biblioteca **Otp.NET**. A verificação tolera ±1 passo de tempo de desvio de relógio.
- **Segredo em repouso.** O segredo do autenticador é armazenado **criptografado** via `ISecretProtector`
  (`EncryptionPurposes.MfaSecret`) — nunca em texto puro.
- **Códigos de backup.** Dez códigos de recuperação para uso único são emitidos na inscrição, mostrados **uma vez** e armazenados apenas
  como hashes SHA-256 (`MfaBackupCodes`). Cada funciona exatamente uma vez; um código gasto é rejeitado depois.

## Habilitando (perfil)

Na página **Conta** (`/account`) a seção *Autenticação dois-fatores* mostra o status atual:

1. **Ativar dois-fatores** abre um diálogo MudBlazor com um **código QR** (renderizado no servidor como SVG via
   `Net.Codecrete.QrCodeGenerator`) mais a chave de configuração manual.
2. Digitalize-o, insira o código de 6 dígitos para confirmar — isto verifica o segredo pendente antes de ativar.
3. O diálogo então mostra os **códigos de backup**; salve-os. 2FA agora está ativo.

A mesma seção permite que um usuário inscrito **regenere códigos de backup** ou **desative** 2FA — ambos exigem o
senha da conta para confirmar.

## Entrar com 2FA

Login é um fluxo **dois-passo** uma vez que 2FA é habilitado:

1. **Passo de senha** (`POST /api/auth/login`). No sucesso o cookie de autenticação **não** é emitido ainda; em vez disso um
   cookie **pendente** criptografado e de curta vida (5 minutos) é definido e o usuário é enviado para `/login/2fa`.
2. **Passo de desafio** (`POST /api/auth/login/verify-2fa`). O usuário insere um código TOTP **ou** qualquer código
   código de backup não usado. No sucesso o cookie pendente é descartado e o cookie de autenticação real é emitido.

Tentativas de segundo fator falhadas contam em relação ao existente **bloqueio** de conta (`AuthLockout`) da conta, e a autenticação
endpoints são limitados por taxa.

## 2FA obrigatório para uma implantação de rótulo branco

Um revendedor regulado pode exigir 2FA para **cada** conta:

```jsonc
// appsettings / environment
"App": { "Branding": { "RequireMfa": true } }   // App__Branding__RequireMfa=true
```

Quando `RequireMfa` está ativo e um usuário sem 2FA entra, o passo de senha relata
`mfaSetupRequired` e `MfaEnforcementMiddleware` redireciona suas navegações de página para `/account` até que terminem
inscrição. Padrão para `false`, então uma implantação não configurada mantém 2FA opcional. Veja
[Rótulo Branco](white-label.md).

## Pontos de extremidade

| Método & rota | Finalidade |
| --- | --- |
| `POST /api/auth/login` | Passo de senha; retorna `mfaRequired` (desafio) ou entra |
| `POST /api/auth/login/verify-2fa` | Passo de segundo fator (TOTP ou código de backup) |
| `GET /api/auth/mfa/status` | `MfaEnabled`, pendente, contagem de código de backup restante |
| `POST /api/auth/mfa/setup` | Iniciar inscrição — retorna segredo, URI `otpauth://`, SVG QR |
| `POST /api/auth/mfa/confirm` | Confirmar um código, ativar, retornar códigos de backup |
| `POST /api/auth/mfa/disable` | Desativar (confirmado por senha) |
| `POST /api/auth/mfa/backup-codes/regenerate` | Emitir um novo conjunto (confirmado por senha) |

## Testes

- **Unidade** — `UnitTests/Access/OtpNetTotpAuthenticatorTests.cs` (vetores RFC 6238),
  `AppUserMfaTests.cs` (invariantes de inscrição/transição/uso único), `MfaBackupCodesTests.cs`.
- **Integração** — `IntegrationTests/MfaPersistenceTests.cs` (inscrever → confirmar → consumir, excluir em cascata)
  e `MfaFlowTests.cs` (login dois-passo HTTP completo com TOTP + código de backup e o portão de inscrição obrigatória).
- **E2E** — `E2ETests/MfaFlowTests.cs`: ativar a partir do perfil (QR + confirmar + códigos de backup) e complete um
  sign-in desafiado em viewports desktop e móvel.

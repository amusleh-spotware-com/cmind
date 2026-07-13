---
description: "선택적 TOTP 이중 인증 (인증자 앱 등록, 일회용 백업 코드, 모든 사용자에게 필수로 만드는 화이트라벨 스위치 포함)."
---

# 이중 인증 (2FA)

계정은 암호 위에 **시간 기반 일회용 암호 (TOTP)** 이중 인증으로 보호할 수 있습니다. 기본적으로 사용자의 프로필에서 **옵트인**이며, 화이트라벨 배포는 모든 사람에게 **필수**로 만들 수 있습니다. 모든 RFC 6238 인증자 앱이 작동합니다 — Google Authenticator, Microsoft Authenticator, Authy, Aegis, FreeOTP — 구현이 표준 (SHA-1, 6자리, 30초 스텝)이므로; 독점 서버 구성 요소는 포함되지 않습니다.

## 작동 방식

- **도메인.** MFA는 `AppUser` 집계 (액세스 컨텍스트)에 있습니다. 사용자는 의도 표현 메서드를 통해 등록됩니다 — `BeginMfaEnrollment`, `ConfirmMfaEnrollment`, `ConsumeBackupCode`, `RegenerateBackupCodes`, `DisableMfa` — 따라서 불변량 (비밀은 활성화되기 전에 확인되어야 하고 백업 코드는 일회용)이 한 곳에서 적용됩니다.
- **TOTP.** 생성 및 검증은 Core `ITotpAuthenticator` 인터페이스 뒤에 있으며, **Otp.NET** 라이브러리로 구현된 Infrastructure에 구현됩니다. 검증은 ±1 시간 스텝의 시계 오류를 허용합니다.
- **저장 시 비밀.** 인증자 비밀은 `ISecretProtector`를 통해 **암호화**되어 저장됩니다 (`EncryptionPurposes.MfaSecret`) — 절대 평문이 아닙니다.
- **백업 코드.** 등록 시 10개의 일회용 복구 코드가 발급되며, **한 번만** 표시되고 SHA-256 해시로만 저장됩니다 (`MfaBackupCodes`). 각각은 정확히 한 번 작동; 소비된 코드는 그 이후 거부됩니다.

## 활성화 (프로필)

**계정** 페이지 (`/account`)에서 *이중 인증* 섹션은 현재 상태를 표시합니다:

1. **이중 인증 활성화**는 MudBlazor 대화를 **QR 코드** (서버 측에서 `Net.Codecrete.QrCodeGenerator`를 통해 SVG로 렌더링됨) 및 수동 설정 키로 엽니다.
2. 스캔하고 6자리 코드를 입력하여 확인 — 이는 활성화하기 전에 보류 중인 비밀을 확인합니다.
3. 대화는 **백업 코드**를 표시하고; 저장합니다. 2FA가 이제 켜져 있습니다.

동일한 섹션을 사용하여 등록된 사용자는 **백업 코드 재생성** 또는 **2FA 끄기** — 둘 다 계정 암호를 확인해야 합니다.

## 2FA로 로그인

2FA가 활성화되면 로그인은 **두 단계** 흐름입니다:

1. **암호 단계** (`POST /api/auth/login`). 성공 시 인증 쿠키는 **아직** 발급되지 않습니다; 대신 단기 (5분), 암호화된 *보류 중* 쿠키가 설정되고 사용자는 `/login/2fa`로 전송됩니다.
2. **챌린지 단계** (`POST /api/auth/login/verify-2fa`). 사용자는 TOTP 코드 **또는** 사용하지 않은 백업 코드를 입력합니다. 성공 시 보류 중인 쿠키가 삭제되고 실제 인증 쿠키가 발급됩니다.

실패한 2단계 시도는 기존 계정 **잠금** (`AuthLockout`)으로 계산되고, 인증 엔드포인트는 속도 제한됩니다.

## 화이트라벨 배포를 위한 필수 2FA

규제 리셀러는 **모든** 계정에 대해 2FA를 요구할 수 있습니다:

```jsonc
// appsettings / 환경
"App": { "Branding": { "RequireMfa": true } }   // App__Branding__RequireMfa=true
```

`RequireMfa`가 켜져 있고 2FA 없는 사용자가 로그인하면 암호 단계는 `mfaSetupRequired`를 보고하고 `MfaEnforcementMiddleware`는 그들의 페이지 탐색을 `/account`로 리다이렉트하여 등록을 완료할 때까지. 기본값은 `false`이므로 구성되지 않은 배포는 2FA를 선택적으로 유지합니다. [화이트라벨](white-label.md)을 참조하세요.

## 엔드포인트

| 메서드 및 경로 | 목적 |
| --- | --- |
| `POST /api/auth/login` | 암호 단계; `mfaRequired` (챌린지) 반환 또는 로그인 |
| `POST /api/auth/login/verify-2fa` | 2단계 (TOTP 또는 백업 코드) |
| `GET /api/auth/mfa/status` | `MfaEnabled`, 보류 중, 남은 백업 코드 수 |
| `POST /api/auth/mfa/setup` | 등록 시작 — 비밀, `otpauth://` URI, QR SVG 반환 |
| `POST /api/auth/mfa/confirm` | 코드 확인, 활성화, 백업 코드 반환 |
| `POST /api/auth/mfa/disable` | 끄기 (암호 확인됨) |
| `POST /api/auth/mfa/backup-codes/regenerate` | 신선한 세트 발급 (암호 확인됨) |

## 테스트

- **단위** — `UnitTests/Access/OtpNetTotpAuthenticatorTests.cs` (RFC 6238 벡터), `AppUserMfaTests.cs` (등록/전환/일회용 불변량), `MfaBackupCodesTests.cs`.
- **통합** — `IntegrationTests/MfaPersistenceTests.cs` (등록 → 확인 → 소비, 계단식 삭제) 및 `MfaFlowTests.cs` (전체 HTTP 두 단계 로그인 TOTP + 백업 코드 포함 및 필수 등록 게이트).
- **E2E** — `E2ETests/MfaFlowTests.cs`: 프로필에서 활성화 (QR + 확인 + 백업 코드) 및 챌린지된 로그인 완료, 데스크톱 및 모바일 뷰포트에서.

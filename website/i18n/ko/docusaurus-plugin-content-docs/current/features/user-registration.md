---
description: "안전하고 화이트라벨 게이트된 자체 서비스 사용자 등록 — 온앱 가입 페이지 및 서버 간 프로비저닝 API, 구성 가능한 사용자 속성, 관리자 승인 또는 이메일 검증 게이트, 남용 방지 가드 포함. 기본적으로 비활성화됨."
---

# 사용자 등록

기본적으로 **소유자/관리자가 수동으로 사용자를 추가합니다** (사용자 페이지 → *새 사용자*). 규모로 사용자를 온보드해야 하는 화이트라벨 배포 — 또는 앱을 다른 서비스와 통합 — cMind는 또한 **보안, 자체 서비스 등록** 경로를 배포합니다. **기본적으로 비활성화됨**: 스톡 배포는 변경 없으며 배포가 옵트인할 때까지 페이지와 API는 모두 404를 반환합니다.

하나의 도메인 흐름을 공유하는 두 진입점이 있습니다:

1. **온앱 페이지** (`/register`) — 로그인과 동일한 셸의 브랜드된 모바일 우선 가입 페이지.
2. **프로비저닝 API** (`POST /api/provision`) — 통합 서비스가 배포별 프로비저닝 비밀로 인증된 계정을 생성하는 서버 간 엔드포인트.

## 기록되는 항목 — 데이터 최소화

cMind는 거래 **도구**: cBots를 빌드/실행/백테스트하고 각 사용자의 *자신의* cTrader Open API 자격 증명을 통해 거래를 미러링합니다. **거래 계정을 열거나 고객 자금을 보관하지 않으므로** KYC/AML 신원 확인은 **브로커의** 의무이지 이 플랫폼의 의무가 아닙니다. 따라서 등록 형식은 **기본적으로 이메일만 기록합니다** — 서비스를 제공하는 데 필요한 최소 (GDPR Art. 5(1)(c) 데이터 최소화; 합법적 근거 = 계약). cMind는 의도적으로 **국가 ID / 생년월일 / 주소 필드를 배포하지 않습니다**.

다른 모든 속성은 `App:Registration:Attributes`를 통해 **배포별 옵트인**이며, 각각 독립적으로 `Off` / `Optional` / `Required`:

| 속성 | 노트 |
|---|---|
| `FullName`, `DisplayName`, `Company` | 자유 텍스트, 길이 바운드. |
| `Country` | ISO 3166-1 알파-2, 고정 코드 세트에 대해 검증됨. |
| `Phone` | E.164 형식 (`+14155552671`). |
| `Locale` | BCP-47 모양 (`en-US`), 정규화됨. |
| `MarketingOptIn` | 별도, **선택 해제됨** 체크박스 — 절대 필수 동의와 번들로 제공되지 않음 (CAN-SPAM). |
| `AgeConfirmation` | 체크박스만; **생년월일은 저장되지 않습니다. |

속성은 `AppUser` 집계가 소유한 `UserProfile` 값 개체에 있으며 구성 시 검증됩니다. **GDPR 소거** (`AppUser.Anonymize()`)는 프로필 및 확인 토큰을 스크럽합니다.

**동의.** `RequireTermsAcceptance`이 켜져 있을 때 사용자는 게시된 법률 문서 (약관, 개인정보보호정책, 위험 공개)를 수락해야 합니다. 수락은 기존 `ConsentRecord` 집계를 통해 기록됩니다 — 버전 스탬프, 타임스탠프, 원본 IP — 다른 곳에서 MiFID/ESMA 등급 기록 보관에 사용되는 동일한 저장소.

## 게이트 모드

자체 등록된 계정은 게이트를 지울 때까지 로그인할 수 없습니다 (`App:Registration:Mode`):

- **`AdminApproval`** (기본값) — 계정은 큐에 들어갑니다; 소유자/관리자는 **사용자** 페이지 (*승인 대기 중* 섹션)에서 승인합니다. 메일 인프라가 필요하지 않습니다.
- **`EmailVerification`** — 단일 사용, 만료되는 검증 링크가 이메일로 전송됩니다; 링크가 열리면 계정이 활성화됩니다. 이메일 전송 (`App:Email`)이 필요합니다. **전송이 구성되지 않으면 이 모드는 시작 시 자동으로 `AdminApproval`로 다운그레이드됩니다**. 따라서 등록 활성화는 절대 조용히 깨지지 않습니다.
- **`Open`** — 계정은 즉시 활성화됩니다 (신뢰/개발만).

자체 등록 사용자는 항상 **`User`** (또는 구성된 경우 `Viewer`)로 생성됩니다 — 도메인은 **자체 등록을 통해 소유자/관리자 발행을 거절합니다**.

## 보안 및 남용 방지

- **열거 방지.** 중복 이메일은 신선한 가입과 동일한 **중립** `202 Accepted`를 생성하고 아무것도 생성하지 않습니다 — 앱은 절대 주소가 이미 계정을 가지고 있는지 공개하지 않습니다.
- **속도 제한.** 공개 엔드포인트는 IP별로 스로틀됩니다 (인증 리미터보다 어렵습니다).
- **암호 정책.** 최소 길이 적용; 암호는 해시됩니다 (Argon2 via `IPasswordHasher`); 검증 토큰은 SHA-256 해시로만 저장되고 일회용 + 만료입니다.
- **이메일 위생.** 이메일 도메인의 선택적 허용 목록 및 일회용 공급자 블록리스트.
- **CAPTCHA (선택 사항).** reCAPTCHA / hCaptcha / Turnstile 공유 검증 계약을 통해.
- **로그인 게이트.** 보류 중인 계정은 중립 응답으로 로그인에서 거절됩니다.

## 프로비저닝 API (통합)

`App:Registration:Api:Enabled` 및 `Secret` 설정으로 다른 서비스가 사용자를 생성할 수 있습니다:

```
POST /api/provision
X-Provision-Secret: <구성된 비밀>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

비밀은 상수 시간에 비교됩니다. 프로비저닝된 계정은 `Api.ActivateImmediately` / `Api.InviteMustChangePassword`에 따라 **활성** (또는 `MustChangePassword`로 초대됨)으로 생성됩니다.

## 활성화

등록은 **기능 플래그와 마스터 스위치 모두** 필요합니다:

```jsonc
"App": {
  "Features": { "Registration": true },
  "Registration": {
    "Enabled": true,
    "Mode": "AdminApproval",           // 또는 EmailVerification / Open
    "DefaultRole": "User",             // 절대 Owner/Admin 아님
    "RequireTermsAcceptance": true,
    "AllowedEmailDomains": [],          // 빈 = 모두
    "BlockDisposableEmail": true,
    "Attributes": { "FullName": "Optional", "Country": "Off" },
    "Api": { "Enabled": false, "Secret": "" }
  }
}
```

`App:Email` 섹션 (SMTP `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`, `FromName`)은 `EmailVerification` 모드에서 사용하는 전송을 구성합니다; 메일 없이 실행하려면 `Host`를 미설정 (노옵 발송자) 상태로 두세요. 배포가 기능을 켜고 리브랜드하는 방법에 대해 [기능 토글](./feature-toggles.md) 및 [화이트라벨](./white-label.md)을 참조하세요. 등록이 활성화되면 로그인 페이지에 **계정 생성** 링크가 표시됩니다.

## 테스트됨

단위 (프로필 검증, `SelfRegister` 역할 가드, 활성화 전환, 일회용 토큰, 소거), 통합 (기본값 비활성화 404, 승인 흐름, 이메일 검증 다운그레이드, 열거 방지, 남용 가드, 필수 속성, 프로비저닝 + 나쁜 비밀), E2E (기본값 꺼짐 로그인에 가입 링크 없음; `/register` 페이지는 브랜드된 닫힌 상태를 렌더링합니다).

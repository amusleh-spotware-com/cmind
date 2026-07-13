---
description: "Secure, white-label-gated self-service user registration — หน้า sign-up บน app และ server-to-server provisioning API, พร้อม configurable user attributes, admin-approval หรือ email-verification gating, และ anti-abuse guards ปิดโดย default"
---

# User registration

โดย default **เจ้าของ/admin เพิ่ม users ด้วยตนเอง** (Users page → *New User*) สำหรับ
white-label deployments ที่ต้อง onboard users ใน scale — หรือ integrate app กับ service
อื่น — cMind ยัง ship **secure, self-service registration** path มัน **ปิดโดย default**:
stock deployment ไม่เปลี่ยนและ page และ API ทั้งคู่ return 404 จนกว่า deployment จะ opt in

มีสอง entry points ที่ share one domain flow:

1. **On-app page** (`/register`) — branded, mobile-first sign-up page ใน shell เดียวกับ `/login`
2. **Provisioning API** (`POST /api/provision`) — server-to-server endpoint สำหรับ integrating
   service เพื่อสร้าง accounts, authenticated โดย per-deployment provisioning secret

## อะไรถูกบันทึก — data minimization

cMind เป็น trading **tooling**: มันสร้าง/รัน/backs cBots และ mirror trades over แต่ละ
user's *own* cTrader Open API credentials มัน **ไม่เปิด trading accounts หรือ custody
client money** ดังนั้น KYC/AML identity verification เป็น **broker's** หน้าที่ ไม่ใช่ของ
แพลตฟอร์มนี้ registration form ดังนั้นบันทึก **เฉพาะ email โดย default** — ขั้นต่ำที่ต้องการ
เพื่อให้บริการ (GDPR Art. 5(1)(c) data minimization; lawful basis = contract) cMind
deliberately ships **ไม่มี** national-ID / date-of-birth / address fields

ทุก attribute อื่นเป็น **opt-in ต่อ deployment** ผ่าน `App:Registration:Attributes`,
แต่ละอัน independently `Off` / `Optional` / `Required`:

| Attribute | หมายเหตุ |
|---|---|
| `FullName`, `DisplayName`, `Company` | ข้อความอิสระ, length-bounded |
| `Country` | ISO 3166-1 alpha-2, validated กับ fixed code set |
| `Phone` | E.164 format (`+14155552671`) |
| `Locale` | BCP-47 shape (`en-US`), normalized |
| `MarketingOptIn` | แยกต่างหาก, **unticked** checkbox — ไม่เคย bundle กับ mandatory consent (CAN-SPAM) |
| `AgeConfirmation` | Checkbox เท่านั้น; **ไม่มี** date of birth ถูกเก็บ |

Attributes อยู่ใน `UserProfile` value object ที่ owned โดย `AppUser` aggregate, validated
at construction **GDPR erasure** (`AppUser.Anonymize()`) ขจัด profile และ verification tokens
ใดๆ

**Consent.** เมื่อ `RequireTermsAcceptance` เปิด, user ต้องยอมรับ legal documents ที่
published (Terms, Privacy, Risk Disclosure) Acceptance ถูกบันทึกผ่าน existing
`ConsentRecord` aggregate — version-stamped, timestamped, พร้อม originating IP —
store เดียวกันที่ใช้สำหรับ MiFID/ESMA-grade record-keeping

## Gating modes

self-registered account ไม่สามารถ sign in จนกว่าจะ clear gate ของมัน
(`App:Registration:Mode`):

- **`AdminApproval`** (default) — account ถูก queue; เจ้าของ/admin approve มันบน **Users** page
  (*Pending approval* section) ไม่ต้องการ mail infrastructure
- **`EmailVerification`** — single-use, expiring verification link ถูก email; account activates
  เมื่อ link ถูกเปิด ต้องการ email transport (`App:Email`) **ถ้าไม่มี transport configure,
  mode นี้ automatically downgrade ไปที่ `AdminApproval`** ตอน startup, ดังนั้น enabling
  registration ไม่เคย silently breaks
- **`Open`** — account active ทันที (trusted/dev เท่านั้น)

Self-registered users ถูกสร้างเป็น **`User`** (หรือ `Viewer` ถ้า configure) เสมอ —
domain **hard-refuses** minting Owner/Admin ผ่าน self-registration

## Security & anti-abuse

- **Anti-enumeration.** duplicate email yields **same** neutral `202 Accepted` เหมือน fresh
  sign-up และสร้างอะไรไม่ได้ — app ไม่เคย disclose ว่า address มี account แล้วหรือไม่
- **Rate limiting.** public endpoints ถูก throttle ต่อ IP (harder กว่า auth limiter)
- **Password policy.** Minimum length enforced; passwords ถูก hash (Argon2 ผ่าน
  `IPasswordHasher`); verification tokens ถูกเก็บเป็น SHA-256 hashes และเป็น
  single-use + expiring
- **Email hygiene.** Optional allow-list ของ email domains และ disposable-provider
  block-list
- **CAPTCHA (optional).** reCAPTCHA / hCaptcha / Turnstile ผ่าน shared verify contract
- **Login gate.** pending account ถูกปฏิเสธตอน login พร้อม neutral response

## Provisioning API (integration)

ด้วย `App:Registration:Api:Enabled` และ `Secret` ถูกตั้ง, service อื่นสามารถสร้าง users:

```
POST /api/provision
X-Provision-Secret: <the configured secret>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

secret ถูก compare ใน constant time Provisioned accounts ถูกสร้าง **active** (หรือ invited
ด้วย `MustChangePassword`) ขึ้นอยู่กับ `Api.ActivateImmediately` / `Api.InviteMustChangePassword`

## การเปิดใช้งาน

Registration ต้องการ **ทั้ง** feature flag และ master switch:

```jsonc
"App": {
  "Features": { "Registration": true },
  "Registration": {
    "Enabled": true,
    "Mode": "AdminApproval",           // หรือ EmailVerification / Open
    "DefaultRole": "User",             // ไม่เคย Owner/Admin
    "RequireTermsAcceptance": true,
    "AllowedEmailDomains": [],          // empty = any
    "BlockDisposableEmail": true,
    "Attributes": { "FullName": "Optional", "Country": "Off" },
    "Api": { "Enabled": false, "Secret": "" }
  }
}
```

ส่วน `App:Email` (SMTP `Host`, `Port`, `UseStartTls`, `Username`, `Password`,
`FromAddress`, `FromName`) configure transport ที่ใช้โดย `EmailVerification` mode; ไม่ตั้ง
`Host` เพื่อรันโดยไม่มี mail (no-op sender) ดู [feature toggles](./feature-toggles.md)
และ [white-label](./white-label.md) สำหรับ deployments ว่าจะ turn features on และ
rebrand เมื่อ registration เปิด, login page แสดง **Create account** link

## ทดสอบแล้ว

Unit (profile validation, `SelfRegister` role guard, activation transitions, single-use
tokens, erasure), integration (disabled-by-default 404, approval flow, email-verification
downgrade, anti-enumeration, abuse guards, required attributes, provisioning + bad secret)
และ E2E (default-off login ไม่มี sign-up link; `/register` page render branded closed
state)

---
description: "소매 FX/CFD/crypto 브로커는 법적 + 기록 보관 의무를집니다. 모듈은 네 가지 산업 표준 기둥을 구현합니다: 위험 공개 동의, 변조-evidence 감사 추적, MiFID/ESMA-스타일 기록 보관, GDPR 데이터 권리."
---

# 법률 및 컴플라이언스

소매 FX/CFD/crypto 브로커는 법적 + 기록 보관 의무를집니다. 모듈은 네 가지 산업 표준 기둥을 구현합니다: **위험 공개 동의**, **변조-evidence 감사 추적**, **MiFID/ESMA-스타일 기록 보관**, **GDPR 데이터 권리**. 모두 `Compliance` 기능 플래그로 게이트됩니다.

## 1. 버전화된 법적 문서 + 동의

- `LegalDocument` (애그리게이트) — 버전화된 서비스 약관, CFD **위험 공개** 또는 개인정보 처리방침. 초안이 작성된 다음 **게시됨**; 게시된 버전은 **불변** (편집 시 예외 발생), 사용자가 동의한 정확한 텍스트가 항상 복구 가능합니다.
- `ConsentRecord` (애그리게이트) — 사용자가瞬시 특정 문서 버전을 수락했음을 보여주는 불변 기록, 원본 IP 포함.
- **시행:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)`는 해당 유형의 게시된 문서가 존재하고 사용자가 활성 버전에 동의하지 않은 경우 `403`으로 작업을 차단합니다. **복사 프로필 생성**에 적용됩니다 (`RiskDisclosure`). 게시된 문서 없음 → 작업 허용 — 아직 동의할 것이 없으므로 모듈 활성화가 실제로 공개될 때까지 과거를 차단하지 않습니다.

## 2. 변조-evidence 감사 추적

`AuditLog` 항목은 해시 체인으로 연결됩니다: 각 행은 `PrevHash`와 `Hash = SHA-256(prev | 정형 필드)`를 저장합니다. `AuditChainInterceptor`는 `SaveChanges`에서 투명하게 체인을 적용하므로 기존 감사 호출 사이트가 변경되지 않습니다. `IAuditTrailVerifier.VerifyAsync`는 체인을 다시 걸으며 저장된 해시 또는 백 링크가 더 이상 일치하지 않는 첫 행을 보고 — 과거 기록의 편집 또는 삭제를 감지합니다. 소유자 엔드포인트: `GET /api/compliance/audit/verify`.

## 3. 기록 보관 (MiFID II / ESMA RTS)

기록 보관은 **불변 해시 체인 감사 로그** plus **유지된 동의 기록** 및 (하드 삭제되지 않은) 소프트 삭제된 도메인 기록으로 충족됩니다. UTC 타임스탬프는 주입된 `TimeProvider`에서 가져옵니다. 동의 기록은 문서 버전 + IP를 유지합니다; 게시된 법적 문서는 절대 변형되지 않습니다. 보존 = 이 테이블을 삭제하지 않음 (추가 전용 / 소프트 삭제).

## 4. GDPR 데이터 권리

- `GET /api/compliance/export` — 호출자 데이터의 기계 판독 가능 내보내기 (프로필, 동의, 복사 프로필, 프로프irms 챌린지).
- `POST /api/compliance/erase` — 삭제 권리: `AppUser.Anonymize()`가 PII (이메일, MFA)를 긁어내고 행을 소프트 삭제하여 참조/감사 히스토리를 일관되게 유지합니다.

## API 요약

| 메서드 | 경로 | 역할 | 목적 |
|--------|-------|------|---------|
| GET | `/api/compliance/documents/active` | User+ | 활성 게시 문서 |
| GET | `/api/compliance/consent/status` | User+ | 미처리 동의 현황 |
| POST | `/api/compliance/consent` | User+ | 문서의 활성 버전 수락 |
| GET | `/api/compliance/export` | User+ | GDPR 데이터 내보내기 |
| POST | `/api/compliance/erase` | User+ | 자신의 계정 GDPR 삭제 |
| POST | `/api/compliance/documents` | Owner | 문서 초안 작성 |
| POST | `/api/compliance/documents/{id}/publish` | Owner | 버전 게시 |
| GET | `/api/compliance/audit/verify` | Owner | 감사 해시 체인 검증 |

UI: `/settings/legal` (내비 *설정 → 법적 및 개인정보*, `Compliance`로 게이트)는 수락 버튼이 있는 미처리 계약 + GDPR 내보내기/삭제 작업을 표시합니다.

## 테스트

- **단위** — `UnitTests/Compliance/LegalDocumentTests.cs` (초안/게시/불변성, 동의 캡처), `AuditChainTests.cs` (해시 링크, 변조 감지, 콘텐츠 민감도).
- **통합** — `IntegrationTests/CompliancePersistenceTests.cs` (실제 Postgres의 활성 버전 + 동의 쿼리), `AuditChainIntegrityTests.cs` (체인이 온전한 것으로 검증된 다음 SQL 수준 변조 감지), `ComplianceFlowTests.cs` (WebApplicationFactory, 격리 DB: 동의 게이트가 위험 공개 수락 전까지 복사 생성 차단; GDPR 내보내기; 감사 검증).
- **E2E** — `E2ETests/ComplianceTests.cs`: 법적 및 개인정보 페이지가 렌더링되고 GDPR 내보내기가 실제 브라우저에서 사용자 데이터를 반환합니다.

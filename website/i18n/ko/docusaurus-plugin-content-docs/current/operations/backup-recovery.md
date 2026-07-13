---
description: "이것은 거래/금융 앱입니다: 데이터베이스에는 거래 계정, 복사 프로필, 프로프irms 챌린지, 감사 체인 및 Data Protection 키링이 있습니다. 그것을 잃으면 돈이 손실되고 규제/감사 의무가 깨집니다. 백업하고 복원이 작동함을 증명하세요."
---

# 백업 및 재해 복구

이것은 거래/금융 앱입니다: 데이터베이스에는 거래 계정, 복사 프로필, 프로프irms 챌린지, 감사 체인 및 Data Protection 키링이 있습니다. 그것을 잃으면 돈이 손실되고 규제/감사 의무가 깨집니다. 백업하고 **복원이 작동함을 증명하세요**.

## 목표

| 메트릭 | 목표 | 의미 |
|--------|--------|---------|
| RPO (최대 데이터 손실) | ≤ 5분 | 지점 시간 복구 (연속 WAL) 사용, 야간 덤프만 아닙니다. |
| RTO (최대 가동 중지 시간) | ≤ 1시간 | 복구 + 앱이 복원된 데이터베이스를 가리키는 시간. |
| 백업 보존 | ≥ 35일 | 지연 발견 손상 + 월별 감사 창을 포함합니다. |
| 복구 드릴 | 월별 | 테스트되지 않은 백업은 백업이 아닙니다. |

## 백업해야 할 것

1. **Postgres 데이터베이스** — 모든 앱 데이터 (단일 논리 데이터베이스 `appdb`).
2. **Data Protection 키링** — 데이터베이스 내에 **지속됨** (`PersistKeysToDbContext<DataContext>`) 및 PFX 암호화를 통해 `App:DataProtectionCertBase64`. 보호 인증서 + 해당 비밀번호 (`App:DataProtectionCertPassword`)는 DB **외부**의 시크릿 관리자에 저장되는 시크릿입니다. 인증서 없이는 복원 후 시크릿(cTID 비밀번호, Open API 토큰, 노드 시크릿, AI 키)을 해독할 수 없습니다.

## 관리형 Postgres (권장)

두 클라우드 IaC 경로 모두 내장 PITR이 있는 관리형 Postgres를 프로비저닝합니다 — 활성화 및 보존 확인:

- **Azure** (`deploy/azure/main.bicep`, Flexible Server): `backup.backupRetentionDays` (≥ 35) 및 컴플라이언스가 요구하는 경우 `geoRedundantBackup` 설정. *지점 시간 복구*를 새 서버로 복원한 다음 앱의 `appdb` 연결 문자열을 업데이트합니다.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): `backup_retention_period` (≥ 35) 및 `backup_window` 설정; 자동 백업 + 선택적 리전 간 복사 유지. *지점 시간 복구*를 통해 복원한 다음 앱을 repotoint합니다.

관리형 PITR은 앱 변경 없이 RPO 목표를 제공합니다 — 앱은 새 연결 문자열만 필요합니다 (기존 재시도 실행 전략, [scaling.md](../deployment/scaling.md) 참조가 cutover 블립을容忍합니다).

## 자체 호스팅 Postgres

- **연속 아카이빙 (PITR):** WAL 아카이빙 활성화 (`archive_mode=on`, 객체 저장소로의 `archive_command`) + 주기적 `pg_basebackup`. 복원 = 기본 백업 복원 + 대상 시간까지 WAL 리플레이. 이것이 RPO 목표를 충족합니다.
- **논리적 덤프 (보조):** 야간 `pg_basebackup`으로 `-Fc appdb`를 오프박스 스토리지로. RPO 목표를 충족하기에 단독으로 불충분합니다.
- 미사용 시 암호화; 데이터베이스 호스트가 아닌 곳에 저장.

## 복구 드릴 (월별 실행)

1. 최신 백업을 (10분 전의 "지점 시간 복구") **스크래치** 데이터베이스로 복원, 프로덕션이 아닙니다.
2. 이를 가리키는 폐기물 앱 인스턴스(또는 psql 세션)를 가리킵니다.
3. 스키마 확인: `dotnet ef migrations list`가 보류 중인 마이그레이션을 보여주지 않고 앱이 시작되어 `/health` 준비가 됩니다.
4. **감사 체인이 온전한지 확인** `IAuditTrailVerifier`를 통해 (변조-evidence `AuditChainInterceptor` 체인) — 복원 후 깨진 체인은 손상 또는 변조를 의미합니다.
5. 시크릿 해독이 작동하는지 확인 (예: Open API授权가 해독됨) — Data Protection 인증서 + 비밀번호가 올바르게 복원되었음을 증명합니다.
6. 드릴 결과를 기록하고 (소요된 시간 vs RTO) 스크래치 데이터베이스를 파괴합니다.

가능한 환경에서는 CI에서 1–4단을 자동화합니다 (Testcontainer에 시드된 백업을 복원하고, `dotnet ef migrations list` + 감사 체인 검증 실행)하여 손상된 백업 회귀가 필요하기 전에 catch됩니다.

## 실제 복원 후

1. DB 복원 (사고 전 까지의 지점 시간 복구).
2. Data Protection 인증서 + 비밀번호가 사고 전 사용된 것과 **동일한지** 확인.
3. 앱 `appdb` 연결 문자열을 repotoint하고 레플리카를 롤합니다.
4. 시작은 어드바이저리 잠금 하에서 마이그레이션을 실행합니다 ([scaling.md](scaling.md) 참조) — N 레플리카와 안전합니다.
5. 복사/프로펌 슈퍼바이저가 임대를 회수하고 **브로커에서 재동기화**합니다 (cTrader가 진실 공급원) — 열린 포지션이 자동으로 수렴됩니다 — 오래된 지역 상태를 신뢰하지 않습니다.
6. 감사 체인 + 최근 거래 데이터의 스팟 확인.

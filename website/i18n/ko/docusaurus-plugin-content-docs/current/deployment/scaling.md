---
description: "cMind는 최소한의 운영자 노력으로 확장됩니다. 두 가지 상태 저장 워크로드 — 실행/백테스트 실행, 복사 거래 — 모두 데이터베이스를 조정 지점으로 사용하므로..."
---

# 수평 확장

cMind는 최소한의 운영자 노력으로 확장됩니다. 두 가지 상태 저장 워크로드 — 실행/백테스트 실행, 복사 거래 — 모두 데이터베이스를 조정 지점으로 사용하므로 복제본을 추가할 때 외부 조정자 (ZooKeeper, 리더 선택)가 필요하지 않습니다.

## 복사 거래 (자체 복구 리스)

각 노드는 `CopyEngineSupervisor`를 실행합니다 (`App:Copy:Enabled`에서 게이트됨). 매 조정 사이클마다 감독자는:

1. **주장** 할당되지 않았거나 리스 만료된 모든 실행 프로필을 하나의 원자 `UPDATE`에서 — 두 경쟁하는 감독자는 절대 같은 프로필을 모두 주장하지 않습니다. 따라서 프로필은 정확히 하나의 노드에 의해 복사됩니다 (이중 주문 없음).
2. **갱신** 호스팅하는 프로필의 리스.
3. 할당된 프로필을 호스팅하고, 실행 호스트에 액세스 토큰 로테이션을 즉시 푸시합니다 (이벤트 스트림 드롭 없음).

노드 충돌 → 갱신이 중지됨; `App:Copy:LeaseTtl`이 경과하면 생존 노드는 다음 사이클에서 해당 프로필을 회수하고 거래를 복제하지 않고 조정에서 상태를 재구축합니다. **확장 출력** = 복제본 추가; 할당되지 않은/자유 프로필은 자동으로 선택됩니다.

**우아한 스케일인 / 롤링 업데이트 (S1)** = `SIGTERM`에서 `CopyEngineSupervisor.StopAsync` **이 노드의 리스를 해제합니다** (`AssignedNode`/`LeaseExpiresAt` → null) 따라서 생존자는 *매우 다음* 조정 사이클에서 그것들을 회수합니다 — **전체** `LeaseTtl` 후가 아닙니다. 하드 충돌만 전체 TTL을 기다립니다. 복사 에이전트의 `terminationGracePeriodSeconds` (기본값 30)은 Pod이 죽기 전에 해제를 완료할 시간을 제공합니다.

### 노브 (`App:Copy`)

| 설정 | 기본값 | 노트 |
|---------|---------|-------|
| `Enabled` | `false` | 노드에 대해 복사 호스팅을 활성화합니다. |
| `ReconcileInterval` | `30s` | 노드가 주장/갱신/조정하는 빈도. |
| `LeaseTtl` | `120s` | 침묵하는 노드의 프로필이 회수되기 전의 유예. 느린 사이클이 허위 인수를 유발하지 않도록 몇 조정 간격을 유지하세요. |
| `NodeName` | 머신 이름 | 두 감독자가 호스트를 공유할 때 명확하게 설정합니다. |

Kubernetes에서 복사 감독자는 배포로 실행됩니다. `replicas`를 원하는 병렬 처리로 설정합니다. 각 Pod은 안정적인 `NodeName` (기본값: Pod 호스트 이름)을 가져오므로 리스는 Pod별로 귀속됩니다. 데이터베이스는 단일 진실 원천 — 끈기 있는 세션, Pod별 마이그레이션할 상태 없음.

**균형 잡힌 배포 (S4):** `App:Copy:MaxProfilesPerNode` > 0으로 설정하여 노드가 호스팅하는 실행 프로필 수를 제한합니다. 각 감독자는 원자적 `FOR UPDATE SKIP LOCKED` 경계 청구를 통해 **최대** 나머지 헤드룸만 청구하므로 프로필은 복제본 전체에 **확산**됩니다. 첫 감독자가 모두 잡지 않음 — 단일 핫 Pod / SPOF 없음. 건너뛴 잠금 청구는 동시 청구에서도 "정확히 하나의 프로필당 노드" 보장을 유지합니다 (이중 호스팅 없음). `0` (기본값) = 무한 (한 노드가 모든 것을 호스팅함, 변경 없음).

**규모 (S7/S8):** 각 Pod은 `ReconcileInterval`의 최대 20%만큼 조정을 지터링합니다 (`CopyEngineSupervisor.JitteredInterval`) N 복제본이 청구/갱신 `UPDATE`를 동시에 실행하지 않도록 합니다 (Postgres 뇌우 떼). `copyAgent.replicas > 1`일 때 차트는 또한 복제본을 노드 전체에 확산합니다 (`topologySpreadConstraints`)하고 `PodDisruptionBudget` (`minAvailable: 1`)을 추가하여 드레인/업그레이드가 복사 용량을 절대 0으로 가져가지 않도록 합니다.

## 실행/백테스트 실행

`NodeScheduler`는 `MaxInstances`를 존중하여 가장 로드가 적은 적격 노드를 선택합니다. 원격 노드 에이전트는 자체 등록 및 하트비트 (`App:Discovery`), `NodeHeartbeatMonitor`는 하트비트가 `Discovery:HeartbeatTtl`를 초과할 때 노드를 도달 불가능으로 표시합니다. 노드 에이전트를 추가하여 실행 용량을 추가합니다. 죽은 에이전트는 자동으로 라우팅 주변입니다.

## 스케일아웃 / 롤링 배포 시 마이그레이션

모든 Web/MCP 복제본은 시작 시 `OwnerSeeder`를 실행하여 EF 마이그레이션을 적용하고 소유자를 시드합니다. N 복제본이 동시에 시작할 때 그것을 안전하게 만들기 위해 마이그레이션 + 시드는 **Postgres 세션 자문 잠금** 내에서 실행됩니다 (`MigrationLock.RunExclusiveAsync`, 키 `DatabaseDefaults.MigrationAdvisoryLockKey`): 첫 번째 복제본은 잠금을 획득하고 마이그레이션 및 시드; 나머지는 잠금에서 차단한 다음 마이그레이션이 이미 적용됨 (노옵)과 소유자가 이미 존재함을 찾습니다. 별도의 마이그레이션 작업 또는 리더 선택이 필요하지 않습니다. 첫 실행 시드를 추가하는 경우 **동일한 보호된 블록 내에** 배치하여 단일 작성자가 되도록 합니다.

## 노드 에이전트 HTTP 복원력

메인 노드는 목적별로 나뉜 세 개의 클라이언트를 통해 HTTP를 통해 각 `CtraderCliNode` 에이전트와 통신하므로 불안정한 노드 또는 네트워크는 절대 상태를 손상시키지 않습니다:

- **읽기** (`status` / `report` / `stats`) — 멱등성 GET, 일시적 오류 시 재시도 (지수 백오프 + 지터, `NodeAgentHttp.ReadRetryCount`) 시도당 및 전체 타임아웃 포함.
- **쓰기** (`start` / `stop` / `clean`) — 비멱등성 POST, 타임아웃이지만 **절대 재시도 없음**: 재시도된 `start`는 컨테이너를 이중 시작할 수 있습니다.
- **스트림** (`logs`) — 오래 지속된 `docker logs -f` 스트림은 무한 타임아웃과 복원력 파이프라인 없음을 가져옵니다. 따라서 tail이 절대 깎이지 않습니다.

도달 불가능 상태를 유지하는 노드는 하트비트 + [고아 인스턴스 회수](../operations/node-discovery.md)로 처리됩니다. HTTP 계층은 일시적 blips만 부드럽게 합니다.

## 무상태 계층

Web (Blazor Server + API) 및 MCP 서버는 데이터베이스 뒤에서 무상태이며 자유롭게 복제됩니다. 인증은 쿠키 기반입니다. Web을 로드 밸런서 뒤에 수평으로 확장합니다. MCP 서버는 별도 프로세스/배포이므로 Web과 독립적으로 확장됩니다.

## 데이터베이스 연결 복원력

데이터베이스를 여는 모든 호스트는 **재시도 실행 전략**을 사용하므로 일시적 연결 해제 또는 관리형 Postgres 장애 조치 (RDS / Flexible Server 패칭)가 사용자에게 오류로 표면화되는 대신 재시도됩니다:

- Web 및 MCP는 Aspire Npgsql 구성 요소를 통해 컨텍스트를 `DisableRetry=false` 및 명시적 `CommandTimeout` (`DatabaseDefaults.CommandTimeoutSeconds`)로 등록합니다.
- CopyAgent (비 Aspire)는 `UseAppNpgsql`을 통해 등록하여 `DatabaseDefaults`에서 동일한 `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + 명령 타임아웃을 적용합니다.

모든 쓰기는 단일 `SaveChanges` / 단일 `ExecuteUpdate` / 단일 `ExecuteSql` 문이므로 재시도 전략은 안전합니다 (다중 문 트랜잭션은 수동 `strategy.ExecuteAsync` 래핑이 필요하지 않음). 수동 트랜잭션 또는 한 논리 작업에서 여러 `SaveChanges`를 추가하는 경우 `db.Database.CreateExecutionStrategy().ExecuteAsync(...)`로 래핑하세요 — 그렇지 않으면 재시도 중에 throw합니다.

## 확장 출력 체크리스트

- [ ] 추가된 연결 로드에 대해 크기 조정된 Postgres (각 Web/MCP/노드 복제본이 풀을 엽니다).
- [ ] 복사 프로필을 호스팅해야 하는 모든 노드에서 `App:Copy:Enabled=true`.
- [ ] 공동 위치한 감독자당 고유한 `App:Copy:NodeName` (K8s: 기본값 Pod별 좋음).
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`.
- [ ] 권한 있는 Docker를 사용할 수 있는 곳에 배포된 노드 에이전트 (AKS/EKS/EC2/VM, Fargate 아님).
- [ ] 다중 복제본 Web: `signalr` 연결 문자열 설정 (Redis 백플레인) **및** 인그레스 세션 선호도 활성화 (끈기 있는 세션) Blazor 회로가 라이브 Pod에 다시 연결되도록 합니다. 구성 요소 예외는 `MainLayout` `ErrorBoundary`에 의해 catch됩니다 (친화적 재시도, 회로는 생존함).

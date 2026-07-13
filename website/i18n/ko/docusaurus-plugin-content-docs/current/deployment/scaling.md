---
description: "cMind는 최소 운영 노력으로 스케일 아웃됩니다. 실행/백테스트 실행과 카피트레이딩이라는 두 개의 상태 저장 작업이 모두 데이터베이스를 조정점으로 사용하므로 레플리카 추가는 외부 코디네이터( ZooKeeper, 리더 선출 없음)를 필요로 하지 않습니다."
---

# 수평 스케일링

cMind는 최소 운영 노력으로 스케일 아웃됩니다. 실행/백테스트 실행과 카피트레이딩이라는 두 개의 상태 저장 작업이 모두 데이터베이스를 조정점으로 사용하므로 레플리카 추가는 외부 코디네이터( ZooKeeper, 리더 선출 없음)를 필요로 하지 않습니다.

## 카피트레이딩 (자체 치유 임대)

각 노드는 `CopyEngineSupervisor`( `App:Copy:Enabled`에 게이트)를 실행합니다. 매 조정 주기, 감독자:

1. 할당되지 않았거나 임대가 만료된 모든 실행 중인 프로필을 **클레임**합니다, 하나의 원자적 `UPDATE`로 — 두 개의 레이스 감독자가 동일한 프로필을 동시에 클레임하지 않으므로 프로필이 정확히 하나의 노드에서 복사됩니다(이중 주문 없음).
2. 호스팅하는 프로필의 임대를 **갱신**합니다.
3. 할당된 프로필을 호스트하고, 이벤트 스트림 드롭 없이 실행 중인 호스트에 액세스 토큰 회전을 제자리에 푸시합니다.

노드 크래시 → 갱신 중지; `App:Copy:LeaseTtl`가 지나면 모든 생존 노드가 다음 주기에서 해당 프로필을 회수하고, 거래를 복제하지 않고 조정에서 상태를 재구축합니다. **스케일 아웃** = 레플리카 추가; 할당되지 않은/유휴 프로필이 자동으로 선택됩니다.

**우아한 스케일 인 / 롤링 업데이트 (S1)** = `SIGTERM`에서 `CopyEngineSupervisor.StopAsync`는 **이 노드의 임대를 해제**합니다(`AssignedNode`/`LeaseExpiresAt` → null) 그래서 생존자가 *매우 다음* 조정 주기에 이를 회수합니다 — 전체 `LeaseTtl` 후가 아닙니다. 하드 크래시만 TTL을 기다립니다. 카피 에이전트의 `terminationGracePeriodSeconds`(기본값 30)는 파od가 중지되기 전에 해제 시간이 완료되도록 합니다.

### 노브(`App:Copy`)

| 설정 | 기본값 | 참고 |
|---------|---------|---------|
| `Enabled` | `false` | 노드에서 카피 호스팅을 켭니다. |
| `ReconcileInterval` | `30s` | 노드가 클레임/갱신/조정하는 빈도. |
| `LeaseTtl` | `120s` | 조용한 노드의 프로필이 회수되기 전의 은총. 느린 주기가 가짜 핸드오프를 일으키지 않도록 몇 가지 조정 간격을 유지합니다. |
| `NodeName` | 머신 이름 | 두 감독자가同一 호스트를 공유할 때 구별됩니다. |

Kubernetes에서 카피 감독자는 Deployment로 실행됩니다; `replicas`를 원하는 병렬 처리로 설정합니다. 각 pod는 안정적인 `NodeName`(기본값: pod 호스트 이름)을 얻으므로 임대는 pod별로 attributed됩니다. 데이터베이스가 단일 진실 공급자입니다 — 고정 세션, 마이그레이션할 per-pod 상태 없음.

**균형 잡힌 배포(S4):** `App:Copy:MaxProfilesPerNode` > 0으로 설정하여 노드가 호스트하는 실행 중인 프로필 수를 제한합니다. 각 감독자는 원자적 `FOR UPDATE SKIP LOCKED` 제한 클레임을 통해 **최대** 남은 여유분만 클레임하므로 프로필이 레플리카에 **분산**되어 첫 번째 감독자가 모든 것을 가져가는 대신 — 단일 핫 pod / SPOF 없음. 스킵 잠금 클레임은 동시 클레임에서도 "프로필당 정확히 하나의 노드" 보장을 유지합니다(이중 호스팅 없음). `0`(기본값) = 무제한(한 노드가 모든 것을 호스트, 변경 없음).

**규모에서(S7/S8):** 각 pod는 `CopyEngineSupervisor.JitteredInterval`) `ReconcileInterval`의 최대 20%까지 조정하여 N 레플리카가 동시에 클레임/갱신 `UPDATE`를 실행하지 않도록 합니다. `copyAgent.replicas > 1` 차트가 레플리카를 노드에 분산시키고(`topologySpreadConstraints`) `PodDisruptionBudget`(`minAvailable: 1`)을 추가하여 드레인/업그레이드가 카피 용량을 0으로 만들지 않습니다.

## 실행/백테스트 실행

`NodeScheduler`가 `MaxInstances`를 존중하는 최소 부하의 적합한 노드를 선택합니다; 원격 노드 에이전트가 자체 등록 및 하트비트(`App:Discovery`), `NodeHeartbeatMonitor`가 하트비트가 `Discovery:HeartbeatTtl`를 초과할 때 노드에 연결할 수 없음을 표시합니다. 실행 에이전트를 추가하여 실행 용량을 추가합니다; 사망한 에이전트는 자동으로 라우팅됩니다.

## 스케일 아웃 / 롤링 배포의 마이그레이션

모든 Web/MCP 레플리카가 시작 시 `OwnerSeeder`를 실행하여 EF 마이그레이션을 적용하고 소유자를 시드합니다. N 레플리카가 동시에 시작할 때 안전한 이유: 마이그레이션 + 시드가 **Postgres 세션 어드바이저리 잠금**(`MigrationLock.RunExclusiveAsync`, 키 `DatabaseDefaults.MigrationAdvisoryLockKey`) 내부에서 실행됩니다: 첫 번째 레플리카가 이를 획득하여 마이그레이션하고 시드합니다; 나머지는 잠금에서 블록된 다음 마이그레이션이 이미 적용되었음을 발견합니다(무 operación) 및 소유자가 이미 존재합니다. 별도의 마이그레이션 작업 또는 리더 선출이 필요하지 않습니다. 첫 실행 시딩을 추가하는 경우 단일 작성기 내에서 동일하게 보호된 블록 **내에** 배치하여 단일 작성기임을 보장합니다.

## 노드-에이전트 HTTP 회복력

메인 노드는 세 개의 목적 분할 클라이언트를 통해 각 `CtraderCliNode` 에이전트와 HTTP로通信합니다:

- **읽기**(`status` / `report` / `stats`) — 일시적 실패 시 재시도되는 멱등 GET(지수 백오프 + 지터, `NodeAgentHttp.ReadRetryCount`) 및 시도별 및 총 시간 제한.
- **쓰기**(`start` / `stop` / `clean`) — 비멱등 POST, 시간 제한되지만 **재시도 절대 안 함**: 재시도된 `start`가 컨테이너를 이중 실행할 수 있습니다.
- **스트림**(`logs`) — 수명이 긴 `docker logs -f` 스트림이 무한 시간 제한과 회복력 파이프라인이 없어서 테일링이 절대 중단되지 않습니다.

연결할 수 없는 노드는 하트비트 + [고아 인스턴스 회수](../operations/node-discovery.md)로 처리됩니다; HTTP 레이어는 일시적 블립만 완화합니다.

## 무상태 계층

Web(Blazor Server + API) 및 MCP 서버는 데이터베이스 뒤에서 무상태이며 자유롭게 복제됩니다. Auth는 쿠키 기반입니다; 부하 분산기 뒤에서 Web을 수평으로 확장합니다. MCP 서버는 별도의 프로세스/Deployment이므로 Web과 독립적으로 확장됩니다.

## 데이터베이스 연결 회복력

데이터베이스를 여는 모든 호스트는 일시적 연결 끊김 또는 관리형 Postgres 장애 조치(RDS / Flexible Server 패치)가 사용자에게 오류로 표시되는 대신 재시도되는 **재시도 실행 전략**을 사용합니다:

- Web 및 MCP는 `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + 명령 시간 제한(`DatabaseDefaults.CommandTimeoutSeconds`)과 함께 Aspire Npgsql 컴포넌트를 통해 컨텍스트를 등록합니다.
- CopyAgent(비 Aspire)는 동일한 `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + 명령 시간 제한을 `DatabaseDefaults`에서 적용하는 `UseAppNpgsql`을 통해 등록합니다.

모든 쓰기는 단일 `SaveChanges` / 단일 `ExecuteUpdate` / 단일 `ExecuteSql` 문장이므로 재시도 전략이 안전합니다(수동 `strategy.ExecuteAsync` 래핑이 필요한 다중 문 트랜잭션 없음). 수동 트랜잭션이나 하나의 논리적 작업에서 여러 `SaveChanges`를 추가하는 경우 `db.Database.CreateExecutionStrategy().ExecuteAsync(...)`로 래핑합니다 — 그렇지 않으면 재시도에서 예외를 발생시킵니다.

## 스케일 아웃을 위한 체크리스트

- [ ] Postgres가 추가된 연결 로드를 위해 크기 조정됨(각 Web/MCP/노드 레플리카가 풀을 엽니다).
- [ ] 카피 프로필을 호스트해야 하는 모든 노드에서 `App:Copy:Enabled=true`.
- [ ] 동일 배치 감독자마다 구별되는 `App:Copy:NodeName`(K8s: 기본값 per-pod 적합).
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`.
- [ ] 권한 있는 Docker를 사용할 수 있는 곳에 노드 에이전트 배포(AKS/EKS/EC2/VM, Fargate 아님).
- [ ] 다중 레플리카 Web: `signalr` 연결 문자열(Redis 백플레인) 설정 **및** 수신 세션 어피니티(고정 세션) 활성화하여 Blazor 서킷이 활성 pod에 다시 연결되도록 합니다. 구성 요소 예외는 `MainLayout` `ErrorBoundary`(우아한 재시도, 서킷存活)에 의해 catch됩니다.

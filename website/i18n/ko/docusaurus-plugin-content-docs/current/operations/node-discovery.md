---
description: "cTrader CLI 노드는 자체 등록 + 하트비트로 클러스터에 조인합니다 — 수동 항목 없음. Consul/Nomad/kubeadm 에이전트와 동일한 패턴: 에이전트 부팅..."
---

# 노드 자동 디스커버리

cTrader CLI 노드는 **자체 등록 + 하트비트**로 클러스터에 조인합니다 — 수동 항목 없음. Consul/Nomad/kubeadm 에이전트와 동일한 패턴: 에이전트는 메인 노드 위치 + 공유 클러스터 비밀을 알고 부팅한 다음 계속 자신을 공지합니다.

> Docker Compose 및 `kind` Kubernetes 클러스터에서 종단간 검증됨: 에이전트는 자체 등록, DB에서 도달 가능으로 표시, 하트비트가 TTL 지나서 중지하면 자동으로 도달 불가능으로 표시, 재개하면 온라인으로 돌아옵니다.

## 작동 방식

```
CtraderCliNode 에이전트                    메인 (Web)
------------------                       ----------
POST /api/nodes/register  ── 조인 토큰 ──▶ 토큰 검증 (상수 시간)
  { name, baseUrl, mode,                 프로토콜 버전 검증
    maxInstances, dataDir,               이름으로 CtraderCliNode 업서트
    protocolVersion }                    LastHeartbeatAt, IsReachable=true 스탬프
        ▲                                  └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  HeartbeatInterval마다          NodeHeartbeatMonitor (백그라운드):
        └──────────────────────────────── now - LastHeartbeatAt > HeartbeatTtl인 경우
                                             → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **등록 == 하트비트.** 에이전트는 `HeartbeatIntervalSeconds`에서 재-POST합니다. 첫 호출은 노드를 생성합니다 (`NodeRegistered` 이벤트); 나중 호출은 생존성을 새로고칩니다. 중단 후 재개된 하트비트는 노드를 다시 도달 가능으로 뒤집습니다 (`NodeCameOnline`).
- **생존성 조정.** `NodeHeartbeatMonitor`는 마지막 하트비트가 `HeartbeatTtl`을 초과하는 노드를 도달 불가능으로 표시합니다. 스케줄러 (`IsActive`/`AcceptsRun`/`AcceptsBacktest`는 도달 가능성에서 게이트됨)는 다시 보고할 때까지 작업 배치를 중지합니다.
- **고아 인스턴스 회수.** `NodeInstanceReclaimer` (백그라운드)는 도달 불가능한 노드에 고립된 모든 비 터미널 인스턴스를 **Failed** (`FailureReason = "Node unreachable - instance reclaimed"`, `InstanceFailed` 도메인 이벤트 → 사용자 알림)로 전환하므로 충돌/분할된 노드는 절대 인스턴스를 "Running" 상태로 영구히 유지할 수 없습니다. 회수는 노드의 마지막 하트비트가 `HeartbeatTtl + InstanceReclaimGrace` 이상 부실한 후에만 발생하여 짧은 blip이 먼저 복구될 수 있는 기회를 제공합니다. 회수된 **실행은 자동 재스케줄되지 않습니다**: 분할되었지만 살아있는 노드는 여전히 컨테이너를 실행 중일 수 있고 컨테이너 수준의 울타리가 없으므로 재시작하면 이중 실행의 위험이 있습니다 — 사용자는 의도적으로 회수된 실행을 재시작합니다. 백테스트는 자체 종료하므로 회수된 백테스트는 단순히 재실행됩니다.
- **신원은 노드 이름입니다.** 메인은 `NodeName`으로 업서트하므로 재시작 시 IP/URL이 변경되는 Pod은 신원을 유지하고 새 `AdvertiseUrl`을 재등록합니다.
- **모드는 첫 등록에서 고정됩니다.** 노드 모드 (`Run`/`Backtest`/`Mixed`)는 유지된 유형이며 하트비트에서 변경할 수 없습니다; 다른 모드로 재등록은 생존성에 대해 존중되지만 모드 변경은 무시됩니다 (경고로 기록됨). 모드 변경하려면: 노드 삭제, 재등록하도록 놔둡니다.

## 구성

메인 (Web) — `App:Discovery`:

| 키 | 기본값 | 의미 |
|-----|---------|---------|
| `Enabled` | `false` | 등록 엔드포인트 + 모니터의 마스터 스위치. |
| `JoinToken` | — | 공유 클러스터 비밀 (≥ 32자) 에이전트는 제공해야 함. |
| `HeartbeatTtl` | `00:01:30` | 침묵 노드가 도달 불가능으로 표시되기 전의 유예. |
| `InstanceReclaimGrace` | `00:01:00` | 도달 불가능한 노드에 고립된 인스턴스가 회수 (실패)되기 전의 `HeartbeatTtl` 이상의 추가 마진. |
| `MonitorInterval` | `00:00:30` | 모니터 및 인스턴스 회수기가 스윕하는 빈도. |
| `HeartbeatInterval` | `00:00:30` | 제안된 속도로 에이전트에 반환되는 값. |

에이전트 (CtraderCliNode) — `NodeAgent`:

| 키 | 의미 |
|-----|---------|
| `MainUrl` | 메인 노드의 기본 URL. 빈 = 수동 등록 모드 (루프 노옵). |
| `AdvertiseUrl` | 메인이 **이** 에이전트에 도달하는 데 사용하는 URL. |
| `NodeName` | 고유 이름; 공백인 경우 머신 이름으로 기본. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | 스케줄러가 존중하는 용량 힌트. |
| `HeartbeatIntervalSeconds` | 재등록 속도. |
| `JwtSecret` | 메인의 `JoinToken`과 같아야 함 — 등록 베어러 및 디스패치 JWT 서명 키 모두. |

## 보안 모델 (v1)

자동 등록된 노드는 **하나의 클러스터 비밀** (`JoinToken` == 각 에이전트의 `JwtSecret`)을 공유합니다. 메인은 각 디스패치 요청에 그 비밀로 5분 HS256 JWT 서명; 에이전트 검증. 요구 사항:

- `JoinToken`을 ≥ 32자로 유지하고 로테이션 (메인의 `App:Discovery:JoinToken` 및 모든 에이전트의 `NodeAgent:JwtSecret`을 함께 업데이트).
- 프로덕션에서 메인 및 에이전트 앞에서 TLS 종료 (리버스 프록시 / 인그레스).
- 에이전트는 여전히 `AllowedImagePrefix`와 일치하는 이미지만 실행합니다.

**경화 후속 (v1 아님):** 등록 시 고유 노드별 비밀 발급 (kubeadm 스타일 부트스트랩 → 노드별 자격 증명) 따라서 단일 손상된 에이전트는 피어에 대한 디스패치 토큰을 위조할 수 없습니다. 등록 흐름은 이미 응답 본문을 반환합니다 — 노드별 비밀을 돌려받을 자연스러운 장소.

## 수동 노드는 여전히 작동합니다

`POST /api/nodes` (관리자 UI)은 자체 노드별 비밀로 고정된 노드를 등록하는 것을 계속합니다. 디스커버리는 추가입니다.

화이트라벨 배포는 **수동 제어를 숨길 수 있습니다** (또는 전체 노드 표면) 그리고 순수하게 자동 디스커버리에 의존합니다: `App:Branding:NodesUi=Monitor`는 수동 추가/삭제를 드롭하고, `Hidden`는 탐색, 페이지 및 수동 API를 제거하며, `App:Branding:RestrictNodesToOwner`는 표면을 소유자만으로 바닥칩니다. 여기의 자체 등록 + 하트비트 엔드포인트는 모든 모드에서 영향을 받지 않습니다. [화이트라벨 → 노드 UI 가시성](../features/white-label.md#nodes-ui-visibility)을 참조하세요.

---
description: "cTrader CLI 노드는 자체 등록 + 하트비트로 클러스터에 합류합니다 — 수동 입력 없음. Consul/Nomad/kubeadm 에이전트와 동일한 패턴: 에이전트가 메인 노드 위치를 알고 부팅되고 공유 클러스터 시크릿을 가진 다음 지속적으로 자신을 announcement합니다."
---

# 노드 자동 검색

cTrader CLI 노드는 **자체 등록 + 하트비트**로 클러스터에 합류합니다 — 수동 입력 없음. Consul/Nomad/kubeadm 에이전트와 동일한 패턴: 에이전트가 메인 노드 위치 + 공유 클러스터 시크릿을 알고 부팅된 다음 지속적으로 자신을 announcement합니다.

> Docker Compose 및 `kind` Kubernetes 클러스터에서 종단간 검증됨: 에이전트가 자체 등록, DB에서 도달 가능, 하트비트 중지 후 TTL 경과 시 자동 표시, 이음.

## 작동 원리

```
CtraderCliNode agent                         Main (Web)
------------------                         ----------
POST /api/nodes/register  ── join token ──▶ verify token (constant-time)
  { name, baseUrl, mode,                    verify protocol version
    maxInstances, dataDir,                   upsert CtraderCliNode by name
    protocolVersion }                        stamp LastHeartbeatAt, IsReachable=true
        ▲                                     └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  every HeartbeatInterval            NodeHeartbeatMonitor (background):
        └──────────────────────────────────── if now - LastHeartbeatAt > HeartbeatTtl
                                                 → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **등록 == 하트비트.** 에이전트가 `HeartbeatIntervalSeconds`마다 다시 POST합니다. 첫 번째 호출은 노드를 생성합니다 (`NodeRegistered` 이벤트); 이후 호출은 라이브러스를 새로고칩니다. 정전 후 이음된 하트비트는 노드를 다시 도달 가능으로 전환합니다 (`NodeCameOnline`).
- **라이브러리 조정.** `NodeHeartbeatMonitor`는 마지막 하트비트가 `HeartbeatTtl`를 초과한 노드를 도달 불가로 표시합니다. 스케줄러 (`IsActive`/`AcceptsRun`/`AcceptsBacktest`가 도달 가능성에 게이트됨)는 다시 보고할 때까지 배치를 중지합니다.
- **고아 인스턴스 회수.** `NodeInstanceReclaimer` (백그라운드)는 도달 불가 노드에서 고립된 비터미널 인스턴스를 **Failed**로 전환합니다 (`FailureReason = "Node unreachable - instance reclaimed"`, `InstanceFailed` 도메인 이벤트 → 사용자 알림), 그래서 고장/분할된 노드가 인스턴스를 "Running" 영원히 고착시키지 못합니다. 회수는 노드의 마지막 하트비트가 `HeartbeatTtl + InstanceReclaimGrace`를 초과한 후에 만 fire되어, 짧은 블립이 먼저 복구될 기회를 얻습니다. 회수된 **실행은 자동 재스케줄되지 않습니다**: 분할되었지만 살아 있는 노드가 여전히 컨테이너를 실행 중일 수 있으며 컨테이너 수준 펜싱이 없으므로 재-launch하면 이중 실행 위험이 있습니다 — 사용자가 의도적으로 회수된 실행을 다시 시작합니다. 백테스트는 자체 종료되므로 회수된 백테스트는 단순히 다시 실행됩니다.
- **ID는 노드 이름입니다.** 메인이 `NodeName`으로 upserts하므로 IP/URL이 재시작 시 변경되는 포드는 ID를 유지하고 새 `AdvertiseUrl`로 자체 등록합니다.
- **모드는 첫 번째 등록에서 고정됩니다.** 노드 모드 (`Run`/`Backtest`/`Mixed`)는 지속된 유형이며 하트비트에서 변경될 수 없습니다; 다른 모드로의 재등록은 라이브러리에 대해 honour되지만 모드 변경은 무시됩니다 (경고로 로그됨). 모드 변경: 노드를 삭제하고 재등록하도록 합니다.

## 구성

메인 (Web) — `App:Discovery`:

| 키 | 기본값 | 의미 |
|-----|---------|---------|
| `Enabled` | `false` | 등록 엔드포인트 + 모니터용 마스터 스위치. |
| `JoinToken` | — | 에이전트가 제시해야 하는 공유 클러스터 시크릿 (≥ 32자). |
| `HeartbeatTtl` | `00:01:30` | 조용한 노드가 도달 불가로 표시되기 전의 시간. |
| `InstanceReclaimGrace` | `00:01:00` | 고립된 인스턴스가 도달 불가 노드에서 회수(실패)되기 전 `HeartbeatTtl` 이상의 여유 마진. |
| `MonitorInterval` | `00:00:30` | 모니터 및 인스턴스 회수 작업의 실행 빈도. |
| `HeartbeatInterval` | `00:00:30` | 에이전트에 반환되는 권장 캐던스. |

에이전트 (CtraderCliNode) — `NodeAgent`:

| 키 | 의미 |
|-----|---------|
| `MainUrl` | 메인 노드의 기본 URL. 빈 = 수동 등록 모드 (루프 no-op). |
| `AdvertiseUrl` | 메인이 **이** 에이전트에 도달하기 위한 URL. |
| `NodeName` | 고유 이름; 빈이면 기본값으로 머신 이름. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | 스케줄러가 존중하는 용량 힌트. |
| `HeartbeatIntervalSeconds` | 재등록 캐던스. |
| `JwtSecret` | 메인의 `JoinToken`과 동일해야 합니다 — 등록 bearer 및 디스패치 JWT 서명 키 모두. |

## 보안 모델 (v1)

자동 등록 노드는 하나의 클러스터 시크릿 (`JoinToken` == 각 에이전트의 `JwtSecret`)을 공유합니다. 메인은 해당 시크릿으로 각 디스패치 요청에 5분 HS256 JWT에 서명합니다; 에이전트가 검증합니다. 요구 사항:

- `JoinToken`을 ≥ 32자로 유지하고 회전합니다 (메인의 `App:Discovery:JoinToken` 및 모든 에이전트의 `NodeAgent:JwtSecret`을 함께 업데이트).
- 프로덕션에서 메인 및 에이전트 앞에 TLS를 종료합니다 (역방향 프록시 / 인그레스).
- 에이전트는 `AllowedImagePrefix`와 일치하는 이미지만 실행합니다.

**후속 강화 (v1 아님):** 등록 시 고유 per-노드 시크릿을 발급합니다 (kubeadm 스타일 부트스트랩 → per-노드 자격 증명) so 단일 compromised 에이전트가 피어에 대한 디스패치 토큰을Forge할 수 없습니다. 등록 흐름은 이미 응답 본문에 반환합니다 — minted per-노드 시크릿을 다시 제공하는 자연스러운 장소입니다.

## 수동 노드는 여전히 작동합니다

`POST /api/nodes` (관리자 UI)는 계속 고정 노드를 자체 per-노드 시크릿으로 등록합니다. 검색은 추가적입니다.

화이트라벨 배포는 수동 컨트롤(또는 전체 Nodes 표면)을 숨기고 자동 검색에만 의존할 수 있습니다: `App:Branding:NodesUi=Monitor`는 수동 추가/삭제를 삭제하고, `Hidden`은 내비, 페이지 및 수동 API를 제거하며, `App:Branding:RestrictNodesToOwner`는 표면을 소유자 전용으로 낮춥니다. 여기서 자체 등록 + 하트비트 엔드포인트는 모든 모드에서 영향을 받지 않습니다. [White-label → Nodes UI visibility](../features/white-label.md#nodes-ui-visibility) 참조.

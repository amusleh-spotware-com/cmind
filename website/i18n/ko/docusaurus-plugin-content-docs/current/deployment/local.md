---
title: 로컬에서 실행하세요
description: Docker Compose(또는 개발을 위한 .NET Aspire)를 사용하여 몇 분 안에 당신의 자신의 컴퓨터에서 cMind를 실행하세요.
sidebar_position: 1
---

# cMind를 로컬에서 실행하세요 🖥️

이것은 cMind를 실제로 보는 가장 빠른 방법입니다 — 당신의 자신의 컴퓨터에서 전체 인스턴스. 커피를 집으세요; 당신은 아마 그것이 차가워지기 전에 로그인할 것입니다.

:::tip[당신이 끝에서 가질 것]
**localhost:8080**에서 실행 중인 웹 앱, **localhost:8081**에서 MCP 서버, Postgres 데이터베이스, cBot을 빌드하고 백테스트하기 위한 준비된 로컬 워커 노드. 모두 당신의 컴퓨터에, 모두 당신의 것.
:::

**시작하기 전에, 당신은 다음 중 하나가 필요합니다:**

- **방금 Docker** → Option A를 사용하세요(.NET SDK 필요 없음). 첫 번째 모습을 위해 권장됨.
- **.NET 10 SDK + Docker** → 코드를 해킹하고 싶으면 Option B를 사용하세요.

두 경로 모두 교차 플랫폼입니다(Windows / macOS / Linux).

## Option A — Docker Compose (.NET SDK 필요 없음)

전제 조건: Docker Desktop(또는 Docker Engine + compose 플러그인).

```bash
cp .env.example .env        # PG_PASSWORD, OWNER_EMAIL, OWNER_PASSWORD 편집
docker compose up --build
```

- 웹 UI: <http://localhost:8080> (`.env`에서 소유자로 로그인; 첫 번호 로그인에서 암호 변경을 강요).
- MCP 서버: <http://localhost:8081/mcp>.
- Postgres 데이터는 `pgdata` 볼륨에서 유지; 스키마는 시작 시 자동으로 마이그레이션됩니다.

웹 컨테이너는 호스트 Docker 소켓(`/var/run/docker.sock`)을 마운트하므로 브라우저 내 빌더와 시드된 **LocalNode**는 당신의 컴퓨터에서 cTrader Console 컨테이너를 빌드 + 실행합니다.

**교차 플랫폼 노트**
- Docker Desktop(Windows/macOS)은 소켓을 `/var/run/docker.sock`에서 노출 — compose mount는 그대로 작동합니다.
- Linux: 당신의 사용자가 소켓에 접근할 수 있는지 확인하거나, 충분한 권한을 가진 compose를 실행하세요.
- 웹 이미지는 `linux/amd64`입니다; Apple Silicon에서 Docker는 에뮬레이션 아래에서 실행합니다.

중지 및 닦기:

```bash
docker compose down          # 데이터 유지
docker compose down -v       # 또한 데이터베이스 볼륨 삭제
```

## Option B — .NET Aspire (개발용)

전제 조건: .NET 10 SDK + Docker.

```bash
dotnet run --project src/AppHost
```

Aspire는 Postgres, Web, MCP, pgAdmin을 오케스트레이션합니다; 연결 문자열 + OTLP를 배선합니다; 대시보드를 엽니다. 소유자 자격 증명을 Aspire 파라미터(`OwnerEmail`, `OwnerPassword`)로 설정하세요.

기존 Postgres에 대해 방금 웹 앱을 실행:

```bash
dotnet run --project src/Web
```

## 로컬에서 워커 노드 추가

시드된 LocalNode는 이미 당신의 컴퓨터에서 작업을 실행합니다. 로컬에서 **auto-discovery**를 실행하려면, Web 앱을 가리키는 노드 에이전트를 시작하세요([노드 디스커버리](../operations/node-discovery.md) 참조) `NodeAgent:MainUrl=http://host.docker.internal:8080`과 일치하는 `JoinToken`을 사용합니다.

## 문제 해결 🔧

Docker는 의견이 있습니다. 여기는 일반적인 용의자입니다:

| 증상 | 가능한 원인 & 수정 |
|---|---|
| 8080/8081에서 `port is already allocated` | 다른 것이 포트를 사용하고 있습니다. 그것을 중지하거나, `docker-compose.yml`의 매핑을 변경하세요. |
| 웹는 시작하지만 빌드/백테스트는 실패 | Docker 소켓이 마운트되거나 접근 가능하지 않습니다. Linux에서, 당신의 사용자가 `/var/run/docker.sock`에 도달할 수 있는지 확인하세요. |
| 소켓의 `permission denied`(Linux) | 당신의 사용자를 `docker` 그룹에 추가하세요(`sudo usermod -aG docker $USER`) 그리고 다시 로그인하거나, 충분한 권한을 가진 실행하세요. |
| 매우 느린 첫 번째 실행 | 첫 번째 빌드는 이미지를 pull하고 컴파일합니다 — 그 이후의 실행은 훨씬 빠릅니다. Apple Silicon의 `linux/amd64` 웹 이미지는 에뮬레이션 아래에서 실행됩니다. |
| 로그인할 수 없음 | 당신의 `.env`에서 `OWNER_EMAIL` / `OWNER_PASSWORD`를 확인하세요. 첫 번째 로그인은 암호 변경을 강요합니다. |
| 업그레이드 후 데이터베이스 이상 | `docker compose down -v`는 깨끗한 상태를 위해 볼륨을 닦습니다(당신은 로컬 데이터를 잃을 것입니다). |

여전히 갇혀있나요? [토론 열기](https://github.com/amusleh-spotware-com/cmind/discussions) — 우리는 친절합니다. 다음 정류장: [실제로 배포 →](./cloud.md)

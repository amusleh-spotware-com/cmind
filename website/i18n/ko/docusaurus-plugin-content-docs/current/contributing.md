---
slug: /contributing
title: 기여
description: cMind에 기여하는 방법 — 인간 또는 AI 지원 PR 환영. 10분 내에 첫 기여.
sidebar_position: 5
---

# cMind에 기여하기 🛠️

여기 있어주셔서 감사합니다. cMind는 누군가 이슈를 열거나, 정확한 cTrader 동작을 보고하거나, 이 문서의 오타를 수정하거나, PR을 배포할 때마다 더 좋아집니다. **당신은 .NET 마법사일 필요가 없습니다** — 테스터, 트레이더, 문서 수정자는 애그리게이트를 작성하는 사람들만큼 소중합니다.

:::tip[정규 가이드는 저장소에 있습니다]
이 페이지는 친절한 온-램프입니다. 전체, 항상 최신 프로세스 — 기본 규칙, 코딩 규약, 검토 흐름 — 은 **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**에 있습니다.
:::

## 약 10분 내에 당신의 첫 기여

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 경고, 아니면 CI는 정중하게 거절합니다
dotnet test           # unit + integration + E2E
```

수정할 것을 찾았나요? 브랜치, 변경, 테스트 추가, PR 열기. 그게 전체 루프입니다.

## 도움이 되는 방법 (모두 코드는 아닙니다)

| 기여 | 노력 | 위치 |
|---|---|---|
| 🐛 재현 가능한 버그 보고 | 10분 | [버그 보고](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| 💡 기능 제안 | 10분 | [기능 요청](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| 📖 이 문서 개선 | 15분 | `website/docs/` 아래 편집 및 PR |
| 🧪 누락된 테스트 추가 | 30분 | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| 🧠 정확한 cTrader 동작 보고 | 10분 | [토론 열기](https://github.com/amusleh-spotware-com/cmind/discussions) |

## 하우스 규칙 (짧은 버전)

cMind는 **실제 자금**을 이동하므로 몇 가지는 협상할 수 없습니다 — 그리고 솔직히, 그들은 코드베이스에서 작업하는 것을 즐겁게 만듭니다:

- **엄격한 도메인 주도 설계.** 비즈니스 로직은 애그리게이트 및 값 객체에 있고, 절대 엔드포인트나 UI에 없습니다. (저장소에는 그것을 위한 친절한 플레이북이 있습니다.)
- **3개의 테스트 계층, 모든 변경.** Unit + integration + E2E, *실패 경로 포함*(dropped 연결, 거절된 주문, 죽은 노드). 녹색 테스트는 입장 가격입니다.
- **0개의 경고.** `TreatWarningsAsErrors=true`. 현대 C# 14 표현.
- **비밀 없음, 마법 문자열 없음, `DateTime.UtcNow` 없음** (대신 `TimeProvider` 주입).
- **같은 커밋의 문서.** 동작 변경 → 문서 업데이트. 예, 이 사이트도 포함됩니다.

[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)와 [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md)에서 각 규칙 뒤의 *이유*를 포함한 전체 세부 사항.

## AI를 사용하여 기여하기 🤖

우리는 **AI 지원 PR**을 정말로 환영합니다 — 이 프로젝트는 에이전트와 인간 모두에 의해 작업되도록 구축되었습니다. 만약 당신이 Claude, Copilot 또는 유사한 것을 운영 중이라면: [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md)를 가리키고, 중첩된 `CLAUDE.md` 파일을 읽게 하고, 동일한 표준으로 유지하세요(테스트, 0개 경고, DDD). 좋은 AI PR은 좋은 인간 PR과 구별할 수 없습니다 — 동일한 검토, 동일한 환영.

## 서로에게 훌륭하세요

우리는 [행동 강령](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md)을 가지고 있습니다. 요점: 친절하세요, 선의를 가정하세요, 그리고 반대편에 사람(또는 사람의 에이전트)이 있다는 것을 기억하세요. 일찍 질문을 하세요 — 그것이 강점입니다, 귀찮음이 아닙니다.

환영합니다. 당신이 무엇을 만들 것인지 보고 싶을 수 없을 정도입니다. 🎉

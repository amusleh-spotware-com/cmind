---
description: "cMind는 네이티브 앱처럼 휴대폰이나 데스크톱에 설치 — 홈 화면 아이콘, 독립형 윈도우, 스플래시, 친화적 오프라인 페이지. 모바일 우선 및..."
---

# 설치 가능한 앱 (PWA)

cMind는 네이티브 앱처럼 휴대폰이나 데스크톱에 설치 — 홈 화면 아이콘, 독립형 윈도우, 스플래시, 친화적 오프라인 페이지. **모바일 우선**이고 완전히 반응형입니다. [ui-guidelines.md](../ui-guidelines.md)를 참조하세요.

## "설치 가능"이 여기서 의미하는 것 — 그리고 솔직한 한계

Blazor **Server**는 라이브 SignalR 회로를 통해 렌더링하므로 앱은 완전히 오프라인으로 실행할 수 없습니다. PWA가 제공하는 것:

- **설치 가능** — 유효한 웹 매니페스트 + 아이콘, 따라서 브라우저는 *설치* / *홈 화면에 추가*를 제공합니다.
- **앱 셸 캐시됨** — 서비스 워커는 정적 자산 (CSS, 아이콘, 매니페스트)을 캐시하고 네트워크가 끊어질 때 브라우저 오류 대신 **오프라인 페이지**를 표시합니다.
- **네이티브 느낌** — 독립형 디스플레이, 브랜드된 테마 색상/상태 표시줄, 앱 아이콘, iOS 홈 화면 아이콘.

오프라인 상호 작용을 **제공하지 않습니다** — 이는 Blazor WebAssembly (별도의 향후 트랙)가 필요합니다. 라이브 기능의 오프라인 사용을 약속하지 마세요.

## 조각

| 조각 | 위치 |
|-------|-------|
| 매니페스트 (동적, 브랜드됨) | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest` (익명) |
| 아이콘 (192, 512, 512-마스크 가능, apple-touch-180) | `Web/wwwroot/icons/` |
| 서비스 워커 (앱 셸) | `Web/wwwroot/service-worker.js` |
| 오프라인 폴백 페이지 | `Web/wwwroot/offline.html` |
| 등록 + iOS 태그 + 설치 프롬프트 캡처 | `Web/Components/App.razor` |
| 경로 상수 | `Core.Constants.PwaRoutes` |

### 매니페스트

`BrandingOptions`에서 동적으로 제공되므로 리셀러의 제품 이름, 색상 및 아이콘이 설치된 앱으로 전달됩니다: `ProductName`의 `name`/`short_name`, `description`, `AppBarColor`의 `theme_color`, `BackgroundColor`의 `background_color`, `display: standalone` 및 아이콘 세트 (깨끗한 Android 아이콘을 위한 **마스크 가능** 512 포함). 익명 — 설치 프롬프트는 로그인 전에 작동해야 합니다.

### 서비스 워커

앱 셸만. **절대** Blazor 회로 (`/_blazor`), 프레임워크 (`/_framework`) 또는 SignalR 허브 (`/hubs`)를 가로채지 않습니다 — 항상 네트워크. 탐색은 오프라인 페이지를 폴백으로 하는 네트워크 우선; 정적 자산 (`/css`, `/icons`, `/_content`)는 백그라운드 재검증을 통해 캐시 우선. `updateViaCache: 'none'`으로 등록되므로 워커 업데이트는 안정적으로 적용됩니다. 캐시는 버전화됨 (`cmind-shell-v<n>`) — 셸 변경에서 범프.

### iOS

iOS는 매니페스트 아이콘/스플래시를 무시하므로 `App.razor`는 또한 `apple-touch-icon` 및 `apple-mobile-web-app-*` 메타 태그를 방출합니다. iOS는 `beforeinstallprompt` 없음; 사용자는 Safari의 *홈 화면에 추가*를 통해 설치합니다. `beforeinstallprompt`는 사용자 정의 설치 affordance에 대해 Chromium/Android에서 `window.deferredInstallPrompt`로 캡처됩니다.

## 테스트

- **E2E** — `E2ETests/PwaTests.cs`: `application/manifest+json`으로 제공되는 매니페스트, 마스크 가능한 것을 포함한 비어있지 않은 아이콘, `display: standalone`, 링크된 `apple-touch-icon` 및 서비스 워커는 등록 + 활성화합니다. `MobileLayoutTests` / `MobileDialogTests`는 PWA가 설치하는 모바일 셸을 커버합니다.

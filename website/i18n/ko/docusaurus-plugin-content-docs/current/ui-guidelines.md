---
description: "이 앱의 모든 새로운 또는 변경된 UI 부분에 대한 바인딩(Blazor 페이지, 대화, 컴포넌트). 이것은 CLAUDE.md에서 참조되는 진실의 소스입니다."
---

# UI 설계 가이드라인 — 필수

**모든** 이 앱의 새로운 또는 변경된 UI 부분에 대한 바인딩(Blazor 페이지, 대화, 컴포넌트). 이것은 `CLAUDE.md`에서 참조되는 진실의 소스입니다. 규칙이 당신을 막으면, 멈추고 묻으세요 — 그것을 위반하는 UI를 배포하지 마세요. `plans/ui-overhaul.md`에 뿌리 내림.

## 1. 모바일 우선, 항상

- **모바일 360–430px에서 먼저 작성하고**, `min-width` 미디어 쿼리 / MudBlazor 중단점 props로 위쪽을 강화합니다. 절대 `max-width` 재정의를 가진 데스크톱 우선이 아닙니다.
- **너비 320–1920px의 어디서나 수평 스크롤 없음.** 콘텐츠가 뷰포트보다 너비가 넓으면, 그것은 버그입니다.
- 터치 대상 ≥ **44px** (`var(--app-touch-target)`). 텍스트 입력 ≥ 16px 폰트(iOS 포커스 온 줌을 멈춤).
- notches 존경: `env(safe-area-inset-*)`를 사용하세요; 뷰포트는 이미 `viewport-fit=cover`를 설정합니다.
- `prefers-reduced-motion` 명예 — 동작으로 전달된 필수 정보 없음.

## 2. 설계 토큰 — 하드코딩된 값 없음

- 모든 색상/반경/간격은 **설계 토큰**에서 나옵니다: MudBlazor 테마(`Web/Components/Theme.cs`) + `Web/Branding/BrandingCss.cs`에 의해 내보내진 CSS 사용자 정의 프로퍼티(`var(--app-primary)`, `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **컴포넌트나 CSS 규칙에 hex 색상, 반경, 또는 브랜드 문자열을 절대 하드코딩하지 마세요.** 토큰을 읽으세요. 토큰은 화이트라벨 `BrandingOptions`에서 흐르므로, 리셀러의 팔레트는 당신의 UI에 무료로 도달해야 합니다.
- 새로운 브랜드 영향 값 → 토큰 + 브랜딩 필드를 추가하세요; 인라인하지 마세요.

## 3. 반응형 레이아웃 & 데이터

- **테이블은 휴대폰에서 카드로 축소됩니다.** 모든 `MudTable`은 `Breakpoint="Breakpoint.Sm"`를 설정하고 모든 `MudTd`는 `DataLabel`을 가집니다. 모바일에 raw 넓은 테이블 없음. (템플릿: `Components/Pages/Nodes.razor`.)
- 그리드: `MudItem xs="12" sm="6" md="4"` — 휴대폰에서 전체 너비, 다중 열 위쪽.
- 모바일에서 양식 단일 열; 큰 탭 대상; 입력에 `inputmode`/`autocomplete`; money/percent에 대한 numeric/decimal inputmode.
- 모든 리스트/상세에서 **로딩, 빈, 에러** 상태를 제공하세요 — 모바일을 위해 사이즈. 
- 모바일 **bottom navigation** (`Components/Layout/BottomNav.razor`)은 주요 휴대폰 nav입니다; 그룹화된 drawer는 전체 메뉴입니다. 높은 트래픽 대상을 거기 추가하세요; ≤5개 항목을 유지하세요.

## 4. 대화 (생성/편집)

- 모든 추가/생성/편집/신규 작업은 **MudBlazor 대화**(`IDialogService.ShowAsync<TDialog>`)를 사용합니다, 절대 인라인 페이지 양식이 아닙니다. 대화는 `Web/Components/Dialogs/`에 있고, `[Parameter]`s를 노출하고, 중첩된 `public sealed record …Result(...)`을 반환합니다. 리스트 행 작업(시작/중지/삭제)은 인라인 아이콘 버튼으로 유지됩니다.
- 휴대폰에서, 대화는 **전체 화면 / 전체 너비** 및 키보드 인식이어야 합니다.

## 5. 인라인 도움 — 모든 제어

- 모든 명확하지 않은 옵션, 선택, 스위치, 또는 작업은 **`<HelpTip Text="…" />`** (`Components/HelpTip.razor`)을 가져옵니다 — 데스크톱에서 hover, 모바일에서 **tap**. 문서에서 텍스트를 소싱하세요 지도가 행동과 동기 유지되도록; 동일한 커밋에서 모두 업데이트하세요.

## 6. 화이트라벨

- 제품 이름, 로고, 설명, 지원/회사, 색상, 파비콘 모두 `BrandingOptions`에서 나옵니다. 그것들을 참조하세요(`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), 절대 리터럴 "cMind" 또는 브랜드 색상이 아닙니다. PWA 매니페스트, 아이콘, 테마 색상, 로그인 hero는 모두 브랜드입니다.

## 7. PWA

- 앱은 설치 가능합니다. 매니페스트 엔드포인트(`/manifest.webmanifest`)를 브랜드로 유지하세요, 아이콘 현재(192/512/maskable + apple-touch), service worker app-shell-only(절대 Blazor circuit/`_framework`/hubs 터칭), 그리고 오프라인 페이지 작동. 새로운 정적 경로 → 매니페스트 `scope`를 유지하세요.
- Blazor Server는 실시간 SignalR circuit이 필요합니다 → **설치 가능 + app-shell**, 전체 오프라인이 아닙니다. 오프라인 상호작용을 약속하지 마세요.

## 8. 접근성

- 입력에 레이블, 사용자 정의 제어에 `aria-*`, 가시적 포커스, 논리적 포커스 순서. 테마는 화이트라벨 가능하기 때문에, 고정된 팔레트가 아닌 활성 테마에 대해 **대비**를 검증하세요.

## 9. E2E — 테스트되지 않은 UI는 배포되지 않습니다(차단)

모든 사용자 대면 변경은 `tests/E2ETests`에서 Playwright E2E를 배포하고, 실제 사용자처럼 구동되며, **모바일 장치 에뮬레이션** 더하기 데스크톱에서:

- 새로운 경로 → `PageSmokeTests` **그리고** `MobileLayoutTests`에 추가하세요(렌더링, bottom nav, 에러 UI 없음).
- 테이블/페이지 변환 → 모바일 **no-overflow** 세트에 그 경로를 추가하세요.
- 새로운 흐름 → 현실적인 모바일 여정(생성/편집/저장 왕복) **그리고** unhappy path(잘못된 입력, 빈 리스트, 역할별 permission-denied).
- 새로운 help tip → tap에서 그것이 열리는지 주장하세요(`HelpTipTests` 패턴).
- `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync`(장치 에뮬레이션)를 사용하세요.
- `dotnet test` "완료"되기 전에 녹색. 에뮬레이트된 WebKit ≠ 모바일 Safari — 실제 장치 게이팅은 별도의 릴리스 단계입니다.

## 10. 정의 완료 (UI)

- [ ] 모바일 우선; 수평 오버플로우 없음 320–1920px; 터치 대상 ≥44px.
- [ ] 오직 설계 토큰 — 0개의 하드코딩된 색상/반경/브랜드 문자열.
- [ ] 테이블 → 휴대폰의 카드(`DataLabel` + `Breakpoint.Sm`); 로딩/빈/에러 상태 현재.
- [ ] 대화를 통해 생성/편집; 모바일에서 전체 화면.
- [ ] 모든 제어는 문서에서 소싱된 `HelpTip`을 가집니다.
- [ ] 화이트라벨 + PWA 존경.
- [ ] 모바일 + 데스크톱 E2E 추가(연기, no-overflow, 여정, unhappy path); `dotnet test` 녹색.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` 터치된 파일에서 깨끗함.

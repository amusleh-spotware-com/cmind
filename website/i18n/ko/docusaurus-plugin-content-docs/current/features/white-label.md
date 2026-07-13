---
description: "리셀러 리브랜드 앱 — 배포 구성을 통해 제품 이름, 로고, 파비콘, 색상, 사용자 정의 CSS — 코드 변경 없음. 모든 브랜딩 값은 기본값으로 동일..."
---

# 화이트라벨 브랜딩

리셀러 리브랜드 앱 — 제품 이름, 로고, 파비콘, 색상, 사용자 정의 CSS — 배포 구성을 통해 코드 변경 없음. 모든 브랜딩 값 **기본값으로 stock 신원**: 미구성 배포는 이전과 동일해 보입니다; 리셀러는 필요한 것만 재정의합니다.

## 모델

- `Core.Options.BrandingOptions` — `App:Branding`에서 바인딩. 문자열 기반 (구성 에지); 각 색상은 테마 구축 시 검증됩니다.
- `Core.Branding.HexColor` — CSS 16진수 색상 (`#RGB` / `#RRGGBB`)의 값 객체, 불변, 자체 검증. 잘못된 색상은 테마 구축 시 `DomainException` (`domain.branding.color_invalid`)을 발생시킵니다 — 잘못 구성된 배포는 깨진 팔레트를 렌더링하는 대신 시작 시 빠르게 실패합니다.
- `Web.Components.Theme.Build(BrandingOptions)` — MudBlazor 테마를 브랜딩에서 생성. 구성에서 오는 브랜드 팔레트 항목만; 타이포그래피, 레이아웃, 중립 표면 톤은 고정되어 제품이 리셀러 전반에 걸쳐 일관된 모양을 유지합니다.
- `Web.Branding.IBrandingThemeProvider` — 싱글톤, 테마를 한 번 빌드, 옵션 변경 시 재빌드. `MainLayout`/`EmptyLayout`에는 `MudThemeProvider`, 앱바에는 제품 이름/로고에 주입됩니다. `App.razor`는 페이지 `<head>` (`제목, 설명, 파비콘, 테마-색상, 사용자 정의 CSS`)에 대해 `IOptionsMonitor<AppOptions>`를 직접 읽습니다.

## 구성

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "Description": "AcmeFX — copy trading and strategy automation.",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "AppBarColor": "#0B1220",
      "BackgroundColor": "#0E1525",
      "SurfaceColor": "#161E30",
      "SuccessColor": "#3FB950",
      "ErrorColor": "#F85149",
      "WarningColor": "#D29922",
      "InfoColor": "#2D7FF9",
      "CustomCss": ".mud-appbar { letter-spacing: 1px; }"
    }
  }
}
```

환경 변수 형식: `App__Branding__ProductName=AcmeFX`, `App__Branding__PrimaryColor=%232D7FF9`.

| 키 | 효과 | 기본값 |
|-----|--------|--------|
| `ProductName` | 앱바 텍스트 + 페이지 `<title>` | `cMind` |
| `LogoUrl` | 앱바 로고 이미지; 비어 있으면 제품 이름 텍스트 표시 | *(빈)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | stock 설명 |
| `PrimaryColor` / `SecondaryColor` | 악센트, 드로어 아이콘, 버튼 | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | 크롬 + 표면; `AppBarColor`는 `<meta theme-color>` + PWA 매니페스트 `theme_color`를驱动하고, `BackgroundColor`는 매니페스트 `background_color`입니다 | dark 팔레트 |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | 상태 색상 | stock |
| `CustomCss` | `<head>`에 주입된 `<style>` (배포 신뢰) | *(빈)* |
| `ShowSiteLink` | 대시보드에 "Powered by cMind" 크레딧 링크 표시 | `true` |
| `RequireMfa` | 모든 사용자에게 앱 사용 전 이중 인증 설정 요구 | `false` |
| `NodesUi` | Nodes 표면ship 정도: `Full` (목록 + 수동 추가/삭제), `Monitor` (읽기 전용 목록, 추가/삭제 없음), `Hidden` (내비, 페이지, 수동 API 없음) | `Full` |
| `RestrictNodesToOwner` | `true`이면 소유자만 노드查看/관리 가능; 그렇지 않으면 전체 관리자-이상 스태프 표면查看 가능. 일반 사용자는 어떤 경우든 노드를 절대 확인하지 않음 | `false` |

`LogoUrl`/`FaviconUrl`에서 참조된 자산은 Web 앱 `wwwroot`에서 제공(예: `wwwroot/branding/` 폴더 마운트) 또는 모든 절대 URL.

`App:Branding`는 시작 시 검증됩니다 (`BrandingOptionsValidator`, `ValidateOnStart` 통해 실행): 모든 색상은 유효한 16진수여야 하고, `CustomCss`는 `<`/`>`를 포함할 수 없습니다 (`<style>` 태그에서 벗어날 수 없음). 잘못 구성된 배포는 깨진 페이지를 렌더링하는 대신 명확한 메시지로 부팅에 실패합니다.

## Powered-by 링크

대시보드는 프로젝트 문서 사이트를 가리키는 작고 절제된 **"Powered by cMind"** 크레딧 링크를 렌더링합니다. `App:Branding:ShowSiteLink`로 제어되며 **`true`가 기본값**입니다 — 미구성 배포는 이를 표시합니다. 완전한 화이트라벨 인스턴스를 실행하는 리셀러는 `App__Branding__ShowSiteLink=false`로 설정하여 완전히 제거합니다.

링크는 대시보드 컴포넌트에서 방출되며 `IBrandingThemeProvider` / `BrandingOptions`를 통해 플래그를 읽으므로 전환은 구성 전용 변경입니다 (재빌드 없음). [White-label for business](../white-label-for-business.md#the-powered-by-cmind-link)에서 비즈니스 용 요약을 참조하세요.

## 브로커 허용 목록

화이트라벨 배포는 사용자가 추가할 수 있는 브로커의 거래 계정을 제한할 수 있습니다 — 따라서 cMind를 자신의 고객 전용으로 실행하는 브로커는 자신의 북만 제공합니다. `App:Accounts`에서 구성:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

환경 변수 형식: `App__Accounts__AllowedBrokers__0=Pepperstone`.

**동작:**

- **빈 목록 (기본값) ⇒ 무제한.** 모든 브로커가 허용되며 **검증 실행 안 함** — stock 배포는 완전히 변경되지 않습니다.
- **비어 있지 않음 ⇒ 제한됨.** cMind는 사용자가 추가하려는 모든 계정을 목록에 대해 확인합니다 (대소문자 무시):
  - **Open API (OAuth) 링크** — 브로커 이름은 cTrader Open API에서 권위 있게 보고되므로, disallowed 계정은 단순히 **건너뛰기**됩니다 (동일한授權의 allowed 계정은 여전히 링크됨);授权 페이지에서 사용자에게 건너뛰어진 브로커를 알립니다.
  - **수동 cID (사용자 이름 / 비밀번호)** — 사용자가 입력한 브로커는 신뢰되지 않습니다. cMind는 cTrader CLI를 통해 제공된 브로커-probe cBot을 실행하여 (`Account.BrokerName` 읽기) 계정의 실제 브로커를 **검증**하고 검증된 이름을 persists합니다. disallowed 브로커는 알림과 함께 거부됩니다; 검증 실패(잘못된 자격 증명, 노드 없음, 타임아웃)도 표시되며 계정이 추가되지 않습니다.

**모델:**

- `Core.Options.AccountsOptions` — `App:Accounts`에서 바인딩 (`AllowedBrokers`, `BrokerProbeTimeout`, `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — 값 객체 (트림, 대소문자 무시 동등).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`; 빈 = 모두 허용. `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount` 내부의 불변량으로 시행됩니다 (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — Web 호스트(도커 소켓 있음)에서 프로브 컨테이너를 실행, 로그를 테일링하고 `Core.Accounts.BrokerProbeOutput`를 통해 브로커를 파싱합니다. 허용 목록이 제한된 경우에만 호출됩니다.

**브로커-probe cBot:** 사전 빌드된 `broker-probe.algo`가 Web 앱과 함께 제공됩니다 (`src/Web/BrokerProbe/`, 출력으로 `broker-probe/broker-probe.algo`로 복사됨), 따라서 기본값 `App:Accounts:BrokerProbeAlgoPath`가 즉시 해결됩니다 — 상대 경로는 앱 기준 디렉토리에 대해 확인되고 절대 경로는 그대로 사용됩니다. algo가 없으면 수동 cID 검증은 실패합니다 — 제한된 허용 목록의 계정은 Open API 경로를 통해 여전히 연결될 수 있으며 프로브가 필요하지 않습니다.

## 브로커 허용 목록 — 테스트

- **단위** — `UnitTests/Accounts/`: `BrokerName`/`BrokerAllowlist` 값 객체, `BrokerProbeOutput` 파서, `CTraderIdAccount` 허용 목록 불변량.
- **통합** — `IntegrationTests/BrokerAllowlistTests.cs`: 가짜 검증기(무제한 / 검증됨 / disallowed / 검증 실패)로 수동 cID 엔드포인트 + Open API 링커가 disallowed 계정을 건너뛰기. `BrokerVerifierLiveTests.cs`는 cID 자격 증명 + algo가 제공되면 **실제** 프로브를 실행합니다 (그렇지 않으면 깔끔하게 건너뛰기).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: 제한된 배포가 실제 UI를 통해 수동 추가를 거부하고 "검증 불가" 알림을 표시합니다(계정 행 추가 안 함).

## 노드 UI 가시성

노드는 대부분의 테넌트가 수동으로 관리하지 않는 인프라입니다 — cTrader CLI 에이전트가 [자체 등록 및 하트비트](../operations/node-discovery.md)하므로 화이트라벨 배포는 수동 컨트롤 또는 Nodes 표면 전체를 숨기고 자동 검색을 통해 여전히 건강한 클러스터를 실행할 수 있습니다. 두 가지 구성 전용 브랜딩 키가 이를 제어합니다:

```json
{
  "App": {
    "Branding": {
      "NodesUi": "Monitor",
      "RestrictNodesToOwner": true
    }
  }
}
```

환경 변수 형식: `App__Branding__NodesUi=Hidden`, `App__Branding__RestrictNodesToOwner=true`.

**`NodesUi` — 세 가지 모드:**

- **`Full` (기본값)** —stock 제품: 노드 목록 plus 수동 **새 노드** 및 **삭제** 컨트롤. `POST`/`DELETE /api/nodes` 작동합니다.
- **`Monitor`** — 읽기 전용 표면: 목록 및 라이브 통계는 유지되지만 수동 추가 및 삭제가 제거됩니다. 노드는 자동 검색을 통해서만 나타납니다. `POST`/`DELETE /api/nodes`는 **404**를 반환합니다.
- **`Hidden`** — Nodes 내비 링크 및 페이지가 완전히 사라지고 페이지 경로가 대시보드로 리다이렉트됩니다; 수동 추가/삭제 API가 꺼집니다. 클러스터는 자동 검색 전용입니다.

**`RestrictNodesToOwner`**는 노드를查看및 관리할 수 있는 사용자를 floors합니다. 기본값 `false`는 표준 **관리자-이상** 스태프 표면(`AdminOrAbove`)을 유지합니다; `true`로 설정하면 **소유자만**(`Owner`)됩니다. 어떤 방식든 **일반 사용자는 절대 노드를 확인하지 않습니다** — 이것은 소유자 전용과 더 넓은 스태프 표면 사이만 선택합니다.

노드 **자동 검색은 두 키의 영향을 받지 않습니다**: 익명 `POST /api/nodes/register` 자체 등록 + 하트비트 엔드포인트는 항상 작동하므로 `Hidden`/`Monitor` 배포는 여전히 자동으로 클러스터를 확장합니다.

**모델:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — 모드 + 소유자 제한을 합성하는 단일 단일 진실 공급원: `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)`. 내비 (`NavMenu.razor`), 페이지 (`Pages/Nodes.razor`) 및 엔드포인트 (`NodeEndpoints`)가 모두 이를 읽어 UI와 API가 절대 동의하지 않도록 합니다.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — `App:Branding`에서 바인딩.

## 노드 UI 가시성 — 테스트

- **단위** — `UnitTests/Nodes/NodesUiAccessTests.cs`: 모든 모드 + 기본 브랜딩에서의 페이지 가시성, 수동 관리 및 필수 정책 해석.
- **통합** — `IntegrationTests/NodeUiGatingTests.cs`: 실제 HTTP + Postgres 기준 — `Full`은 수동 추가를 허용하고, `Monitor`/`Hidden`은 추가 및 삭제에 404를 반환하며, `RestrictNodesToOwner`는 관리자를 금지하면서 소유자가 여전히 목록을 읽습니다.
- **E2E** — `E2ETests/NodesUiTests.cs` (기본값 `Full`: 내비 링크 + 페이지 + 새 노드 버튼 렌더링) 및 `E2ETests/NodesHiddenTests.cs` (`Hidden`: 내비 링크 사라지고, `/nodes`가 리다이렉트됩니다).

## 디자인 토큰 (CSS 변수)

브랜딩은 MudBlazor뿐만 아니라 앱 자체 스타일시트 + 사용자 정의 컴포넌트에도 도달합니다. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)`는 `App.razor`의 `site.css` 바로 뒤에 주입된 `:root`에 브랜드 팔레트를 CSS 사용자 정의 속성으로 방출합니다 (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, …). `site.css` 및 모든 컴포넌트는 `var(--app-*)`를 읽습니다 — **하드 코딩된 색상 없음** — 리셀러의 팔레트가 무료로 어디서나 흐릅니다(로그인 영웅, 하단 내비, 도움말 팁, 오프라인 페이지 포함). 중립 표면 톤은 `site.css :root`의 기본값입니다; `CustomCss`(마지막에 주입)는 토큰을 재정의할 수 있습니다. [ui-guidelines.md](../ui-guidelines.md) §2 참고.

## 브랜드 PWA

설치 가능한 앱도 브랜드됩니다 — 매니페스트 엔드포인트 (`/manifest.webmanifest`)는 `BrandingOptions`에서 빌드됩니다 (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor` → theme/background). [pwa.md](pwa.md) 참고.

## 테스트

- **단위** — `UnitTests/Branding/HexColorTests.cs`: 유효/잘못된 16진수 검증.
- **통합** — `IntegrationTests/ThemeBuildTests.cs`: 색상이 팔레트에 매핑되고 잘못된 색상이 예외를 발생시킵니다; `IntegrationTests/BrandingHttpTests.cs`: 사용자 정의 `ProductName`/설명/테마-색상이 제공된 페이지 `<head>`에 렌더링됩니다(실제 Postgres의 WebApplicationFactory +), 기본값은stock 이름을 유지합니다.
- **E2E** — `E2ETests/BrandingTests.cs`: 실제 브라우저의 앱바에 브랜드된 제품 이름이 렌더링됩니다.

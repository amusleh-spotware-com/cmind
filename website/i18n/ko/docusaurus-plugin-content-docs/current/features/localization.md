# 로컬라이제이션 (i18n)

cMind는 완전히 로컬라이저블하며 **cTrader 자체가 지원하는 동일한 23개 언어**로 배포되므로 거래자는 플랫폼을 — 그리고 이 문서를 읽습니다 — 자신의 언어로. 영어는 폴백입니다. 누락된 번역은 공백 또는 원본 키를 표시하는 대신 우아하게 영어로 저하됩니다.

## 지원되는 언어

아랍어 (RTL), 중국어 (간체), 체코어, 영어, 프랑스어, 독일어, 그리스어, 헝가리어, 인도네시아어, 이탈리아어, 일본어, 한국어, 말레이어, 폴란드어, 포르투갈어 (브라질), 러시아어, 세르비아어, 슬로바키아어, 슬로베니아어, 스페인어, 태국어, 터키어, 베트남어.

단일 진실 원천은 `Core.Constants.SupportedCultures` — 요청 문화 미들웨어, 언어 전환기, 리소스 패리티 테스트 및 하드코드 문자열 게이트는 모두 여기에서 읽습니다. 언어를 추가하는 것은 거기에 한 줄 변경 + 리소스 파일입니다.

## 작동 방식 (Blazor Server)

- **리소스.** UI 문자열은 `src/Web/Resources/Ui.resx` (영어 기본)에 있으며 언어당 하나의 `Ui.<culture>.resx`. 구성 요소는 `IStringLocalizer<Ui>` — `@L["key"]`를 통해 읽으며 절대 리터럴이 아닙니다. `.resx` 파일은 `tools/i18n/ui-translations.json`에서 생성됩니다 (`pwsh tools/i18n/gen-resx.ps1`), 번역자 친화적 진실 원천.
- **문화 해결.** `RequestLocalizationMiddleware`는 먼저 `.AspNetCore.Culture` 쿠키에서 문화를 선택하고, 그 다음 브라우저의 `Accept-Language`, 그 다음 영어.
- **전환.** 앱 바 언어 전환기 (그리고 **설정 → 언어** 섹션)은 `GET /set-culture` 엔드포인트로 탐색합니다 — Blazor 회로 외부의 전체 새로고침, 회로는 라이브로 문화를 변경할 수 없기 때문입니다. 쿠키를 작성하고, 로그인한 사용자에 대해 선택을 프로필 (`UserProfile.Locale`)에 유지합니다; 새로고침은 선택한 언어로 신선한 회로를 부팅합니다.
- **유지 및 로그인.** 저장된 프로필 로케일은 로그인 시 문화 쿠키에 기록되므로 사용자는 모든 장치에서 자신의 언어로 착륙합니다.
- **오른쪽에서 왼쪽.** 아랍어 (그리고 향후 RTL 언어)는 `<html dir="rtl">`을 설정하고 레이아웃을 MudBlazor의 `MudRTLProvider`로 래핑하여 전체 셸을 미러링합니다.
- **ICU.** Web 호스트는 ICU 활성화 (`InvariantGlobalization=false`)로 실행됩니다; 와이어/파싱 코드는 `CultureInfo.InvariantCulture`에 남아 있으므로 문화별 UI 형식만 영향을 받습니다 — 절대 백테스트 또는 CSV 아닙니다.

## 게이트 — 하드코드 UI 텍스트 없음

새로운 사용자 대면 문자열은 **불가능** 로컬라이즈되지 않은 상태로 병합됨:

- 빌드 실패 아치 가드 테스트 (`NoHardcodedUiTextTests`)는 마이그레이션된 `.razor` 파일을 스캔하고 `@L["…"]` 조회가 아닌 리터럴, 텍스트 베어링 속성 (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`, `aria-label`, `alt`)에 실패합니다.
- 리소스 패리티 테스트 (`ResourceParityTests`)는 모든 언어가 키를 놓치거나 공백 값을 배포하면 빌드를 실패합니다 — 모든 언어는 항상 모든 키를 가집니다.

## 문자열 추가 또는 변경

1. **모든** 문화에 대해 `tools/i18n/ui-translations.json`에서 키를 추가/편집합니다.
2. `.resx` 재생성: `pwsh tools/i18n/gen-resx.ps1`.
3. `@L["your.key"]`를 포함한 구성 요소에서 참조합니다.
4. `dotnet test` — 패리티 및 하드코드 텍스트 게이트는 정직하게 유지합니다.

## 문서 로컬라이제이션

이 문서도 로컬라이즈됩니다. Docusaurus i18n은 모든 23개 로케일 (`website/i18n/`)에 대해 구성되며, 내비게이션 표시줄의 로케일 드롭다운 및 아랍어용 RTL. `npm run write-translations -- --locale <code>`로 로케일의 번역 파일을 스캐폴드하고 `website/i18n/<code>/` 아래에서 번역합니다. 로컬라이제이션 명령에 따라 **모든 문서 추가 또는 변경은 동일한 변경에서 모든 로케일 업데이트를 의미합니다.**

# 지역화 (i18n)

cMind는 완전히 지역화 가능하며 **cTrader 자체가 지원하는 동일한 23개 언어**로 제공됩니다, 그래서 트레이더는 자신의 언어로 플랫폼을 사용하고 이 문서를 읽습니다. 영어가 폴백입니다; 누락된 번역은 빈 칸이나 원시 키를 표시하는 대신 우아하게 영어로 저하됩니다.

## 지원 언어

아랍어(RTL), 중국어(간체), 체코어, 영어, 프랑스어, 독일어, 그리스어, 헝가리어, 인도네시아어, 이탈리아어, 일본어, 한국어, 말레이어, 폴란드어, 포르투갈어(브라질), 러시아어, 세르비아어, 슬로바키아어, 슬로베니아어, 스페인어, 태국어, 튀르키예어, 베트남어.

단일 진실 공급자는 `Core.Constants.SupportedCultures`입니다 — 요청-문화 미들웨어, 언어 스위처, 자원 패리티 테스트 및 하드 코딩된 문자 없음 게이트가 모두 여기서 읽습니다. 언어 추가는 해당 리소스 파일 외에 한 줄 변경입니다.

## 작동 원리 (Blazor Server)

- **리소스.** UI 문자열은 `src/Web/Resources/Ui.resx`(영어 베이스) 및 언어당 하나의 `Ui.<culture>.resx`에 있습니다. 컴포넌트는 `IStringLocalizer<Ui>`를 통해 읽습니다 — `@L["key"]`, 절대 원시 문자 리터럴 아님. `.resx` 파일은 `tools/i18n/ui-translations.json`(`pwsh tools/i18n/gen-resx.ps1`)에서 생성되며, 이는 번역사 친화적 진실 공급자입니다.
- **문화 해석.** `RequestLocalizationMiddleware`가 `.AspNetCore.Culture` 쿠키 أولاً, 그 다음 브라우저의 `Accept-Language`, 그 다음 영어를 선택합니다.
- **전환.** 앱바 언어 스위처(및 **Settings → Language** 섹션)는 `GET /set-culture` 엔드포인트로 이동합니다 — Blazor 서킷은 라이브로 문화 를 변경할 수 없으므로 전체 새로고침입니다. 쿠키를 쓰고, 로그인한 사용자의 경우 프로필에도 선택 사항을 유지합니다(`UserProfile.Locale`); 새로고침이 선택한 언어로 새 서킷을 부팅합니다.
- **지속성 및 로그인.** 저장된 프로필 로케일은 로그인 시 문화 쿠키에 다시 기록되므로 사용자가 모든 기기에서 자신의 언어에 도착합니다.
- **오른쪽에서 왼쪽.** 아랍어(및 향후 RTL 언어)는 `<html dir="rtl">`를 설정하고 레이아웃을 MudBlazor의 `MudRTLProvider`로 감싸서 전체 셸을 미러링합니다.
- **ICU.** Web 호스트는 ICU 활성화 상태로 실행됩니다(`InvariantGlobalization=false`); 와이어/파싱 코드는 `CultureInfo.InvariantCulture`에 있으므로 백테스트나 CSV는 절대 영향받지 않습니다 — 문화별 UI 포맷만 영향받습니다.

## 게이트 — 하드 코딩된 UI 텍스트 없음

표준 범위에서 지역화되지 않은 새 사용자 제공 문자열은 **병합될 수 없습니다**:

- 빌드 실패 아치 가드 테스트(`NoHardcodedUiTextTests`)가 마이그레이션된 `.razor` 파일을 스캔하고 `@L["…"]` 조회가 아닌 리터럴 텍스트 bearing 속성(`Label`, `Text`, `Title`, `Placeholder`, `HelperText`, `aria-label`, `alt`)에서 실패합니다.
- 자원 패리티 테스트(`ResourceParityTests`)가 언어에 키가 누락되거나 빈 값이 있으면 빌드를 실패시킵니다 — 모든 언어는 항상 모든 키를 갖습니다.

## 문자열 추가 또는 변경

1. `tools/i18n/ui-translations.json`에서 **모든** culture에 키를 추가/편집합니다.
2. `.resx`를 재생성합니다: `pwsh tools/i18n/gen-resx.ps1`.
3. `@L["your.key"]`로 컴포넌트에서 참조합니다.
4. `dotnet test` — 패리티 및 하드코딩된 텍스트 게이트가 당신을 정직하게 합니다.

## 문서 지역화

이 문서도 지역화되어 있습니다. Docusaurus i18n은 모든 23개 로케일에 대해 구성되어 있으며(`website/i18n/`), 네비게이션 바에 로케일 드롭다운과 아랍어용 RTL이 있습니다. `npm run write-translations -- --locale <code>`로 로케일의 번역 파일을 스캐폴딩하고 `website/i18n/<code>/` 아래에서 번역합니다. 지역화 의무에 따라 **문서 추가 또는 변경은 같은 변경으로 모든 로케일을 업데이트하는 것을 의미합니다.**

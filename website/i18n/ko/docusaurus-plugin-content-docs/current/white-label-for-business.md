---
slug: /white-label-for-business
title: 비즈니스를 위한 화이트라벨
description: cMind를 당신의 자신의 브랜드 제품으로 배포하세요 — prop firms, 브로커, 복사 거래 비즈니스를 위해. config를 통해 모든 표면을 리브랜드하세요, 코드 변경 없음.
sidebar_position: 4
---

# 당신의 비즈니스를 위한 화이트라벨 cMind 🏢

prop firm을 운영하거나, 브로커 데스크, 또는 복사 거래 서비스? cMind는 처음부터 당신의 자신의 제품으로 **리셀되도록** 구축되었습니다. 모든 표면 — 이름, 로고, 파비콘, 색상, 심지어 설치 가능한 휴대폰 앱 — 당신의 브랜드로 구부립니다. 당신의 고객은 *당신의* 회사를 봅니다. 코드 변경 없음, 포크 없음, 방금 config.

:::tip TL;DR
`App:Branding`을 당신의 이름, 색상, 로고에 가리키세요. 재시작. 끝. 전체 기술 참조는 [화이트라벨 기능 문서](./features/white-label.md)에 있습니다.
:::

## 당신이 리브랜드할 수 있는 것

| 표면 | 무엇이 변경 |
|---|---|
| **제품 이름** | 앱 바 텍스트 + 브라우저 탭 제목 |
| **로고 & 파비콘** | 당신의 표시 어디든지, 브라우저 탭 포함 |
| **색상** | 전체 팔레트 — primary, surfaces, status 색상 — 전체 UI를 통해 흐르고 *그리고* 앱의 자신의 CSS design tokens를 통해 |
| **설치 가능한 앱(PWA)** | 홈 화면 추가 이름, 아이콘, 스플래시는 당신의 브랜드를 사용합니다 |
| **Meta / SEO** | 설명 및 지원 URL은 당신의 것입니다 |
| **사용 정의 CSS** | 마지막 5%를 위해 당신 자신의 광택을 주입합니다 |

모든 것은 스톡 cMind 정체성으로 기본 설정되므로, 당신은 당신이 신경 쓰는 것만 재정의합니다.

## 60초 리브랜드

당신의 배포에서 이것들을 설정하세요(JSON config 또는 환경 변수):

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "ShowSiteLink": false
    }
  }
}
```

환경 변수 형식: `App__Branding__ProductName=AcmeFX`. 색상은 시작 시 검증됩니다 — 나쁜 hex 값은 깨진 페이지를 렌더링하는 대신 명확한 메시지로 부팅에 실패합니다. 좋고 크게, 정확히 당신이 원할 때.

## "Powered by cMind" 링크

**기본적으로**, 대시보드는 작은, 세련된 **"Powered by cMind"** 링크를 보여줍니다 이 사이트로 방문자를 가리킵니다. 그것은 기본적으로 켜져 있습니다 우리가 프로젝트를 자랑스러워하고 다른 트레이더가 그것을 찾는 데 도움이 되기 때문입니다 — 하지만 **당신의 호출**입니다.

- **유지하세요**(기본): 대시보드의 미묘한 크레딧 링크. 당신에게 비용이 들지 않고, 프로젝트를 도움.
- **숨기세요**: `App__Branding__ShowSiteLink=false`를 설정하고 그것은 완전히 사라집니다 — 제품이 명확하게 *당신의*것인 완전히 화이트라벨된 배포를 위해.

정확히 그것이 렌더링되는 곳을 위해 [화이트라벨 기능 문서](./features/white-label.md#powered-by-link)를 참조하세요.

## 멀티테넌트, 고객별 브랜딩

브랜딩이 방금 배포 config이기 때문에, 각 테넌트 배포는 자신의 정체성을 전달할 수 있습니다. 고객별로 별도의 인스턴스를 실행하거나, 당신의 자신의 제어 평면에서 브랜딩을 구동하세요 — 앱은 `IOptionsMonitor`에서 읽으므로, 옵션이 변경할 때 라이브 테마를 재구축할 수도 있습니다.

이것을 쌍으로:

- **[기능 토글](./features/feature-toggles.md)** — 각 테넌트가 어떤 기능을 볼지 결정합니다.
- **[Prop-firm 규칙](./features/prop-firm.md)** — 실시간 자산 추적을 사용하여 당신의 도전 규칙을 시행합니다.
- **[성과 수수료](./features/copy-performance-fees.md)** + **[제공자 마켓플레이스](./features/copy-provider-marketplace.md)** — 복사 거래를 수익화합니다.
- **[준수](./features/compliance.md)** — 당신의 규제자가 물어볼 감사 증적을 유지합니다.

## 자산 & 호스팅

당신의 로고/파비콘을 Web 앱의 `wwwroot/branding/`에 드롭하세요(또는 `LogoUrl`/`FaviconUrl`을 모든 절대 URL에 가리키세요). 당신에게 맞는 것처럼 배포하세요 — [Docker](./deployment/local.md), [Kubernetes](./deployment/kubernetes.md), [Azure](./deployment/cloud-azure.md), 또는 [AWS](./deployment/cloud-aws.md).

당신의 것으로 만들 준비되셨나요? [기술 화이트라벨 레퍼런스로 시작하세요 →](./features/white-label.md)

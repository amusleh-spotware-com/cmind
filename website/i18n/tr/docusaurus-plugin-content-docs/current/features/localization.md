---
title: Yerelleştirme (I18N)
description: 23 dil, RTL desteği, Docusaurus i18n, IStringLocalizer<Ui>.
sidebar_position: 37
---

# Yerelleştirme & Çok Dil Desteği

cMind 23 dilde destekler — Arapça, Çince, Fransızca, Almanca, İbranice, İspanyolca, Türkçe, vb.

## Desteklenen Diller

```csharp
public static readonly List<string> SupportedCultures =
  ["ar", "cs", "da", "de", "es", "fi", "fr", "hu", "it", "ja", "ko",
   "nl", "no", "pl", "pt", "ro", "ru", "sv", "th", "tr", "uk", "zh-Hans", "zh-Hant"];
```

## UI Metin

Tüm Blazor bileşenleri IStringLocalizer<Ui> kullanır:

```csharp
@L["welcome_message"]
```

`tools/i18n/ui-translations.json` tüm anahtarlarını tüm dillerde. Oluştur resx:

```bash
pwsh tools/i18n/gen-resx.ps1
```

## RTL (Sağdan Sola)

Arapça, Farsça, İbranice:

```csharp
<MudRTLProvider>
  <MudThemeProvider Theme="MyTheme" />
  <LayoutView Body="@Body" />
</MudRTLProvider>
```

## Docusaurus i18n

Belgeler çevrilir: `website/i18n/<locale>/docusaurus-plugin-content-docs/current/`.

Parity kapı (`npm run i18n:check`) tüm yerel kopyaları köleleştiri.

Daha fazla: [Kullanıcı Kaydı →](./user-registration.md)

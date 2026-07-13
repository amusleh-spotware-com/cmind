#  Localization（i18n）

cMindは完全にローカライズ可能で、**cTrader自体がサポートする23の同じ言語**で出荷されるため、
トレーダーは自分の言語でプラットフォームを使用しこれらのドキュメントを読むことができます。
英語がフォールバック; 不足している翻訳は空白や生キーを表示するのではなく、正常に英語に低下します。

## 対応言語

アラビア語（RTL）、中国語（簡体字）、チェコ語、英語、フランス語、ドイツ語、ギリシャ語、ハンガリー語、
Indonesian、イタリア語、**日本語**、韓国語、マレー語、ポーランド語、ポルトガル語（ブラジル）、
ロシア語、セルビア語、スlovak語、Slovenian、スペイン語、タイ語、Turkish、ベトナム語。

単一の 情報源は`Core.Constants.SupportedCultures` —
request-culture middleware、language switcher、resource-parity test、
no-hardcoded-string gateはすべてここから読み取ります。
言語追加はそこでの1行の変更プラスそのリソースファイルのみです。

## 動作原理（Blazor Server）

- **リソース。** UI文字列は`src/Web/Resources/Ui.resx`（英語のベース）+ 言語ごとの1つの
  `Ui.<culture>.resx`に配置。コンポーネントは`IStringLocalizer<Ui>` —
  `@L["key"]`で読み取り、而不是リテラル。
  `.resx`ファイルは`tools/i18n/ui-translations.json`
 （`pwsh tools/i18n/gen-resx.ps1`）から生成され、翻訳者に優しい情報源。
- **Culture解決。** `RequestLocalizationMiddleware`は`.AspNetCore.Culture` cookieからcultureを選択、
  次にブラウザの`Accept-Language`、それから英語。
- **切り替え。**  app-bar language switcher（および**Settings → Language** セクション）は
  `GET /set-culture`エンドポイントにナビゲート —
  Blazorサーキットはライブでcultureを変更できないため、full-reload outside the circuit。
  cookieを書き込み、サインインしているユーザーの場合はプロファイルに選択を永続化
  （`UserProfile.Locale`）;
  リロードは選択した言語で新鮮なサーキットを起動。
- **永続化とログイン。** 保存されたプロファイルlocaleはサインイン時にculture cookieに書き戻されるため、
  ユーザーはすべてのデバイスで自分の言語でランディング。
- **右から左へ。** アラビア語（および将来のRTL言語）は`<html dir="rtl">`を設定し、
  MudBlazorの`MudRTLProvider`でレイアウトをラップしてシェル全体をミラーリング。
- **ICU。** WebホストはICU enabledで実行
  （`InvariantGlobalization=false`）;
  wire/parseコードは`CultureInfo.InvariantCulture`で stay、因此 per-culture UI書式設定のみが影響 —
  決してバックテストやCSVではありません。

## ゲート — ハードコードされたUIテキストなし

新しいユーザー向け文字列は、カバーされたスコープでローカライズなしではマージできません：

- ビルド失敗arch-guard test（`NoHardcodedUiTextTests`）が移行された`.razor`ファイルをスキャンし、
  `@L["…"]`ルックアップでない限り、任意のテキスト-bearing属性
  （`Label`、`Text`、`Title`、`Placeholder`、`HelperText`、`aria-label`、`alt`）の_literal_で失敗。
- resource-parity test（`ResourceParityTests`）が任意の言語がキーを欠いているか
  空白の値をshipいる場合にビルドを失敗 — すべての言語常にすべてのキーを持つ。

## 文字列の追加または変更

1. **すべてのculture**の`tools/i18n/ui-translations.json`でキーを追加/編集。
2. `.resx`を再生成：`pwsh tools/i18n/gen-resx.ps1`。
3. コンポーネントで`@L["your.key"]`で参照。
4. `dotnet test` — parityとhardcoded-text gateがあなたを正直に保ちます。

## ドキュメントのローカライズ

これらのドキュメントもローカライズされています。
Docusaurus i18nは23のすべてのlocaleに設定されています
（`website/i18n/`）、navbarにlocaleドロップダウン、アラビア語にはRTL。
localeの翻訳ファイルを足場搭建`npm run write-translations -- --locale <code>`で、
`website/i18n/<code>/`の下で翻訳。
ローカライゼーションマンデート 따르면、**任意のドキュメントを追加または変更することは、**
  **同じ変更ですべてのlocaleを更新することを意味します。**

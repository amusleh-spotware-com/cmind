# Локализација (i18n)

cMind је потпуно локализован и испоручује се у **истих 23 језика koje cTrader сам подржава**, тако да трговац
користи платформу — и чита ову документацију — на свом језику. Енглески је fallback; било koji
 недостајући превод се глациоузно деградира до енглеског уместо да прикаже празнину или сирови кључ.

## Подржани језици

Арапски (RTL), Кинески (Поједностављени), Чешки, Енглески, Француски, Немачки, Грчки, Мађарски,
Индонежански, Италијански, Јапански, Корејски, Малајски, Пољски, Португалски (Бразил), Руски, Српски,
Словачки, Словеначки, Шпански, Тајландски, Турски, Вијетнамски.

Јединствени извор истине је `Core.Constants.SupportedCultures` — request-culture middleware, language
switcher, resource-parity тест, и no-hardcoded-string gate сви читају из њега. Додавање језика je
промена од једне линије тамо плус његови resource фајлови.

## Како функционише (Blazor Server)

- **Ресурси.** UI стрингови живи у `src/Web/Resources/Ui.resx` (енглеска база) плус један
  `Ui.<culture>.resx` по језику. Компоненте их читају преко `IStringLocalizer<Ui>` — `@L["key"]`,
  никада литералу. `.resx` фајлови се генеришу из `tools/i18n/ui-translations.json`
  (`pwsh tools/i18n/gen-resx.ps1`), преводилачки-пријатељски извор истине.
- **Резолуција културе.** `RequestLocalizationMiddleware` бира културу из `.AspNetCore.Culture`
  колачића прво, затим прегледачев `Accept-Language`, затим енглески.
- **Пребацивање.** Language switcher у app-bar-у (и секција **Settings → Language**) naviguje до
  ендпоинта `GET /set-culture` — потпуно поновно учитавање ван Blazor circuit-а, зато što circuit не може
  да промени културу live. Пише колачић и, за пријављеног корисника, перзистује избор у његов
  профил (`UserProfile.Locale`); поновно учитавање покреће fresh circuit на изабраном језику.
- **Перзистенција и пријава.** Сачувана locale профила се враћа у culture колачић при пријави,
  тако да корисник слеће на свом језику на сваком уређају.
- **Desno-na-levo.** Арапски (и било koji будући RTL језици) поставља `<html dir="rtl">` и обмотава layout у
  MudBlazor-ов `MudRTLProvider`, огледајући целу љуску.
- **ICU.** Web хост ради са омогућеним ICU-ом (`InvariantGlobalization=false`); wire/parse код остаје на
  `CultureInfo.InvariantCulture`, тако да је само per-culture UI форматирање погођено — никада backtest или CSV.

## Бараж — без хард-кодираног UI текста

Нови user-facing стрингови **не могу** бити мерджани нелокализовано у покривеном scope-у:

- Arch-guard тест koji не успева на build-у (`NoHardcodedUiTextTests`) скенира мигриране `.razor` фајлове и не успева на
  било ком литералу, text-bearing атрибуту (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`,
  `aria-label`, `alt`) koji није `@L["…"]` lookup.
- Resource-parity тест (`ResourceParityTests`) не успева на build-у ако било koji језици недостаје кључ или испоручи
  празну вредност — сваки језици увек има сваки кључ.

## Додавање или мењање стринга

1. Додај/уреди кључ у `tools/i18n/ui-translations.json` за **сваку** културу.
2. Регенериши `.resx`: `pwsh tools/i18n/gen-resx.ps1`.
3. Референцирај га у компоненти са `@L["your.key"]`.
4. `dotnet test` — parity и hardcoded-text баражevi те држе.

## Локализација документације

Ова документација је такође локализована. Docusaurus i18n је конфигурисан за све 23 локале (`website/i18n/`), са
locale dropdown-ом у navbar-у и RTL за арапски. Скелетација locale-ових фајлова за превод са
`npm run write-translations -- --locale <code>` и преводи под `website/i18n/<code>/`. Према
локализационом мандату, **додавање или мењање било ког документа значи ажурирање сваког локала у истој промени.**

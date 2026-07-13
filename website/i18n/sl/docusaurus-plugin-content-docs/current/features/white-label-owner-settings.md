---
id: white-label-owner-settings
title: Možnosti white-label v nastavitvah lastnika
sidebar_label: White-label nastavitve lastnika
---

# Možnosti white-label v nastavitvah lastnika

Vsaka white-label možnost, ki jo namestitev lahko nastavi prek konfiguracije (`appsettings`/env), je **prav tako
nastavljiva med izvajanjem s strani lastnika aplikacije**, iz **Settings → Deployment**, brez ponovnega uvajanja. Prevzetje lastnika
**zmaga nad konfiguracijo**; čiščenje ga povrne na nameščeno (ali
vgrajeno privzeto) vrednost.

To zrcali, kako white-label *namestitev* konfigurira izdelek — isti gumbi, isti učinek —
torej lahko upravljavec prilagaja blagovno znamko, vrata in politiko v živo in takoj vidi rezultat.

## Kje živi

- **UI:** odsek **Deployment** samo za lastnike v nastavitvenem dialogu in globinska povezava
  **`/settings/deployment`**. Možnosti so združene v **zavihek na kategorijo** (Blagovna znamka, Tema,
  Funkcije, Registracija, Računi, E-pošta, AI, Open API, Prop firm), mobilno-prvi, z okenskim
  dialogom na namizju in celozaslonsko površino na telefonih.
- **API:** `/api/whitelabel` (samo za lastnika, nikoli funkcijsko vrata):
  - `GET /api/whitelabel` — vsaka možnost z njeno veljavno vrednostjo, izvorom (`Config` / `Owner` /
    `Default`) in ali je prevzetje nastavljeno. **Skrivnosti so zamaskirane** (vrednost se nikoli ne vrne).
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — nastavi prevzetje (validirano na vrsto možnosti). Preseljana
    vrednost na **skrivnosti** ohrani obstoječo skrivnost.
  - `DELETE /api/whitelabel/{key}` — počisti eno prevzetje (povrni v konfig).
  - `POST /api/whitelabel/reset` — počisti **vsa prevzetja** (povrni namestitev v čisto konfig).

## Kako prevzetja začnejo veljati

Lastnikova prevzetja so shranjena kot šifrirana `AppSetting` vrsticah in plastirana na vrhu vezanega
`AppOptions` z okrašenim `IOptionsMonitor<AppOptions>`. Ker vsak potrošnik že bere možnosti
skozi ta monitor, prevzetje velja **v živo** čez celotno aplikacijo — tema, naslov strani, MFA
vrata, AI-providerska vrata, seznam dovoljenih brokerjev, politika registracije, nastavitve e-poštnega transporta itd. posodobijo
se ob naslednjem branju (tema/blagovna znamka se takoj znova upodobi). Če je zbirka podatkov začasno nedostopna,
plast **odpove odprto** na konfigurirano osnovno črto, torej branje prevzetja nikoli ne more pokvariti aplikacije.

**Funkcijske zastavice** so del iste površine, vendar so vztrajane prek obstoječe funkcijske preslikave
shranjevanja (`IFeatureGate`), torej zavihek Funkcije in samostojni preklopniki funkcij nikoli ne razidujejo.

**Skrivnosti** (SMTP geslo, CAPTCHA skrivnost, zagonska skrivnost) so šifrirane pri miru
(`ISecretProtector`, namen `whitelabel.secret`), samo za pisanje v UI in nikoli vrnjene prek API.

## Delegirane možnosti

**Skupna aplikacija Open API** poverilnice in **omejitve hitrosti na tip sporočila** sta upravljani na
odseku nastavitev **Open API** (glej dokumentacijo o kopiranju trgovanja / Open API). Pojavljajo se v katalogu Deployment
kot *delegirani* vnosi (samo za branje tukaj, s povezavo), torej se nič ne dvoji in garancija sinhronizacije še vedno šteje, da so zajeti.

## Vedno usklajeni (vsiljeni)

Dodajanje nove white-label možnosti v konfiguracijo **mora** površinsko prikazati v nastavitvah lastnika v istem
commitu. To vsiljuje `WhiteLabelCatalogParityTests`: odseva čez vsako lastnost zapisa white-label možnosti in propade gradnjo, razen če je lastnost registrirana v
`Core/WhiteLabel/WhiteLabelCatalog` (ali eksplicitno navedena v `IntentionallyExcluded` z razlogom).
Glej mandat 10 v `CLAUDE.md`.

## Opombe

- Omogočanje SMTP na namestitvi, ki se je začela **brez** konfigurirane e-pošte, potrebuje ponovni zagon (pošiljatelj
  tip je izbran ob zagonu); gostitelj/poverilnice že konfiguriranega pošiljatelja se posodobijo v živo.
- **Oznake/opisi možnosti** so tehnični identifikatorji konfiguracijskih gumbov, prikazani kot podatki; oznake zavihkov in
  ves interaktivni okvir so v celoti lokalizirani.

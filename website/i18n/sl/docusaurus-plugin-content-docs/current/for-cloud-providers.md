---
slug: /for-cloud-providers
title: cMind za oblačne in VPS ponudnike
description: Zakaj bi oblačni ali VPS ponudnik moral ponuditi upravljano nameščanje cMind — pripravljen, razlikujoč se izdelek za algoritmične trgovce, posrednike in lastnine, z jasnimi načini monetiziranja računanja, belo označenega ponovnega prodaje in upravljanega AI.
keywords:
  - upravljano gostovanje
  - VPS ponudnik
  - oblačni ponudnik
  - gostovanje trgovalne platforme
  - belo označeni ponovno prodajalec
  - upravljano gostovanje AI
sidebar_position: 7
---

# cMind za oblačne in VPS ponudnike 🖥️

Že najemete računanje. cMind je pripravljen, odprtokodni izdelek, ki ga lahko zavijete okrog tega
računanja: **ponudite upravljano nameščanje cMind** in pridobite visoko vrednostno, lepljivo, računalsko
teško bremensko platformo — algoritmične trgovce, posrednike, lastnine in trgovalne skupnosti, ki
želijo, da platforma teče brez da bi postali sami operativni tim.

:::tip[Povzetek]
Tečite brezstansko raven + Postgres + floto vozlišč; odjemalci dobijo poimenovano URL. Monetizirajte
naročilo, računanje, belo označeno in AI. → [Namestite v oblak](./deployment/cloud.md)
:::

## Zakaj ponuditi upravljan cMind

- **Brez gradnje stroškov.** Je odprta koda, MIT licencirana in že dokumentirana, testirana in
  spakirana v kontejnjih. Pakirati in upravljati jo boste vi — ne gradite je.
- **Razlikujoč se izdelek za donosno nišo.** Algoritmično trgovanje je računalsko teško: testiranja in
  živa vozlišča sežgejo CPU, ki je *obračunana uporaba*, ki jo že prodajate.
- **Lepljivi odjemalci.** Trgovci, ki gradijo in tečejo strategije znotraj platforme, se ne brišejo
  naključno.
- **Spremeni pomanjkljivost v dodatno prodajo.** cMind je samohostiran po oblikovanju — za odjemalce, ki
  "ne želijo biti operativni tim," *vi* ste odgovor.

## Kdo kupuje upravljan cMind od vas

- **Posamezni kvanti in trgovci**, ki ga želijo hostirati. → [Za trgovce](./for-traders.md)
- **Posredovalci cTraderja**, ki tečejo belo oznako za svoje odjemalce. → [Za posrednike](./for-brokers.md)
- **Lastnine in podjetja za kopiranje trgovanja**, ki potrebujejo belo označeno, revizijsko infrastrukturo.

## Kaj "upravljan cMind" pomeni tečeti

Delate tri ravni; odjemalec dobije poimenovano spletno URL:

| Raven | Kaj je | Kje teče |
|---|---|---|
| Brezstansko (Web + MCP) | Aplikacija + API + strežnik MCP | Katera koli platforma za kontejnike, samodejno skalirana |
| Podatkovna baza | PostgreSQL | Upravljan Postgres (RDS / Flexible Server / vaš lastni) |
| Flota vozlišč | Gradnja in tečanje kontejnerjev cTraderja | **VMs ali Kubernetes — potrebuje priviligirani Docker** |

:::warning[Ena stvar za obseg vnaprej]
Agenti vozlišč gradijo in tečejo kontejnerje cTraderja, zato potrebujejo **priviligirani Docker**. To
izključuje brezzasebne kontejnerske čase izvajanja (Azure Container Apps, AWS Fargate) *za agente* —
tečite tiste na [Kubernetesem](./deployment/kubernetes.md), VM ali EC2. Brezstansko raven teče
kjerkoli.
:::

Resni vodniki razvoja za kopiranje in lepljenje to konkretizirajo: [pregled oblaka](./deployment/cloud.md) ·
[Azure](./deployment/cloud-azure.md) · [AWS](./deployment/cloud-aws.md) ·
[Kubernetes](./deployment/kubernetes.md) · [Skaliranje](./deployment/scaling.md).

## Kako ga monetizirate

- **Upravljano gostovanje naročilo.** Mesečni načrti Starter / Team / Business, dimenzioniti po
  floti vozlišč in pojavnosti testiranja.
- **Merjenje uporabe in računanja.** Naračunajte testiranje-ure, živa-vozlišče-ure in hranjenje — naravno
  izmerjeno s floto kontejnerjev, ki jo že tečete.
- **Belo označene ponovno prodaje ravni.** Zaračunajte več za popolno ponovno branding (logotip, barve, PWA,
  `ShowSiteLink=false`) in za omogočanje premijskih zmožnosti prek
  [stikalnih zmožnosti](./features/feature-toggles.md). → [Belo označena](./features/white-label.md)
- **Upravljan AI.** Vključite privzeti ključ ponudnika AI, tako da vsak uporabnik odjemalca dobije AI brez
  nastavitve, in zvišajte uporabo — ali ponudite prinesimo svoj ključ. → [Značilnost AI](./features/ai.md)
- **Lastna in kopiranje trgovanja delitvi dohodkov.** Histe za gostovanje, ki tečejo izzive in provizije za
  zmogljivost in vzamejo rezino platforme. → [Lastna](./features/prop-firm.md) ·
  [Provizije za zmogljivost](./features/copy-performance-fees.md) ·
  [Trg ponudnikov](./features/copy-provider-marketplace.md)
- **Postavitev, vključitev in SLA.** Priložite profesionalne storitve in premijsko podporo.

## Večkratni vzorci najemnika

- **Nameščanje na najemnika (priporočeno).** Ena poimenovana instanca na odjemalca — močna izolacija,
  poimenovanje in podatkovna baza na najemnika, oznaka sporočila vozlišča na najemnika. Poimenovanje se
  bere iz `IOptionsMonitor`, zato vsaka instanca nosi svojo identiteto.
  → [Večkratna poimenovanja najemnika](./white-label-for-business.md#večkratna-poimenovanja-po-kupcu) ·
  [Odkrivanje vozlišča](./operations/node-discovery.md)
- **Skupni ravnini nadzora (napredni).** Tečite mnogo instanc iz vaše lastne ravnine zagotavljanja,
  seeded branding in značilnosti na najemnika programsko.

## Merjena uporaba za obračunavanje

Samo za lastnika/administratorja **`GET /api/usage`** končna točka vrne povzetek, ki ga lahko ponudnik
puli in obračuna — brez nove domene ali obstojnosti, projicira obstoječe stanje:

```json
{
  "users": { "total": 42 },
  "nodes": { "total": 6, "online": 5 },
  "instances": { "total": 1280, "backtestsRunning": 3, "runsRunning": 11 },
  "cbots": { "total": 210 },
  "tradingAccounts": { "total": 88 }
}
```

Puli ga na nameščanje po najemniku za spremljanje ceno na osnovi sedežev, na osnovi flote ali na osnovi
bremenskega dela. Parirajte s [beležko in opazovanjem](./operations/logging.md) za boljše
računalsko merjenje.

## Vzdrževanje marž predvidljiva

Razširite vozlišča na zahtevo, delite Postgres ravni in samodejno skalira brezstansko raven. Operativne
površine, ki jih potrebujete, so že tam:

- [Skaliranje in samodejno zdravljenje](./deployment/scaling.md)
- [Beležka in opazovanje](./operations/logging.md)
- [Varnostna kopija in okrevanje](./operations/backup-recovery.md)

## Začni

1. Postavite referenčno nameščanje iz [vodiči za oblak](./deployment/cloud.md).
2. Šablonirajte ga na najemnika (branding + присоединись token + DB) in povežite obračunavanje z
   računanjem.
3. Navedite — sedaj imate upravljan algoritmičen trgovalni platform za prodajo.

## Prispevajte nazaj

Ponudniki, ki tečejo cMind v obsegu, se soočajo z ostrimi robovi prvi. Pristop vaših operativnih
popravkov in IaC izboljšav drži vašo floto poceni vzdrževanja — začnite z
[Vodiču za prispevanje](./contributing.md).

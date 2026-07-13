---
slug: /for-brokers
title: cMind za posredovalnike cTraderja
description: Zakaj bi posrednik cTraderja moral tečeti belo oznako cMind za njegove odjemalce — nudite trgovcem AI, kopiranje trgovanja in izzive za lastnine pod vašo blagovno znamko, omejite račune na vašo posredovalno hišo in pridobite prednost pred konkurenti.
keywords:
  - posrednik cTraderja
  - belo označena trgovalna platforma
  - tehnologija posredovalca
  - kopiranje trgovanja za posrednike
  - Orodja za trgovanje AI
  - programska oprema za lastnino
sidebar_position: 6
---

# cMind za posrednike cTraderja 🏦

Tečete posredovalno hišo cTraderja. Vaši odjemalci že lahko trgujejo — vendar lahko tudi vsi
odjemalci drugih posredovalnih hiš. **cMind omogoča, da svojim trgovcem preddate polno AI-omogočeno
trgovalno operacijsko platformo, poimenovano po vaši znamki**, tako da gradijo, testirajo, tečejo,
kopirajo in spremljajo strategije znotraj *vaše* ekosistema namesto, da bi se zatikali k orodju
tretjega partnerja. To je lepljivi odjemalci, več količine in prava prednost pred posredovalci, ki
nudijo samo terminal.

:::tip Povzetek
Tečite belo oznako cMind za svoje odjemalce. Omejite račune na **vašo** posredovalno hišo, vključite AI in
kopiranje trgovanja ter ga pošljite pod vašo blagovno znamko. → [Belo označena za poslovanje](./white-label-for-business.md)
:::

## Prednost, ki jo dobite pred drugimi posredovalci

- **Diferenciirajte se v orodjih, ne samo v razponih.** Nudite odjemalcem generiranje AI cBotov, testiranje na
  upravljanem grozdu, kopiranje trgovanja in izzive za lastnine — zmožnosti, ki jih večina posredovalcev preprosto
  ne nudi.
- **Obdržite odjemalce v svoji ekosistemu.** Kadar trgovci gradijo in tečejo svoje strategije znotraj vaše
  poimenovane platforme, ostanejo. Zadržanje je cea igra.
- **Pod vašo blagovno znamko, na vaši domeni.** Ime, logotip, barve, favicon, celo namestljiva aplikacija za telefon —
  vse je vaše. Nihče ne vidi "cMind." → [Belo označena značilnost](./features/white-label.md)

## Služite samo vašim računom (seznam dovoljenih posredovalcev)

Ali tečete belo oznako za *vaše* odjemalce? Omejite, katere brokerjske trgovalne račune sme uporabnik dodati,
tako da vaša nameščanja služijo le vaši knjigi:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Ime vaše posredovalne hiše"]
    }
  }
}
```

Ko je seznam dovoljenih nastavljen, cMind preveri vsak račun, ki ga uporabnik poskuša dodati — tako preko
cTrader Open API kot prek ročnega prijave cID (preverjeno z branjem resničnega imena posredovalca računa) —
in zavrne kateri koli račun, ki ni na vašem seznamu. Pustite prazno in vsak posrednik je dovoljen (privzeto).
Glejte [Dokument belo označene značilnosti](./features/white-label.md#seznam-dovoljenih-posredovalcev) za
polno mehaniko.

## Pošljite eno aplikacijo Open API za vse svoje uporabnike

Preskočite napor po uporabniku: zagotovite **eno aplikacijo cTrader Open API** in vsak odjemalec
avorizira svoje račune preko nje — noben odjemalec ne registrira svoje. Registrirajte en sam URL
za preusmeritev, spustite kredenciale v konfiguraciji ali nastavitvah lastnika, in skupni način se
vključi za vse. Ste pogajali višjo mejo sporočila cTraderja? Prilagodite **omejitve hitrost odjemalca
po vrsti sporočila** (ali onemogočite tempiranje). → [Skupna aplikacija Open API in omejitve hitrost](./features/open-api-shared-app.md)

## Novi načini za ustvarjanje dohodka

- **AI, brez trenja za odjemalce.** Zagotovite privzeti ključ ponudnika AI na ravni nameščanja in
  vsak odjemalec dobije značilnosti AI takoj — ni prijave drugje. Povečajte ga ali ga vključite v
  premijske ravni. Odjemalci lahko še vedno prinesejo svoj ključ. → [Značilnost AI](./features/ai.md)
- **Izzivi za lastnino.** Tečite izzive finančnega trgovca z evidentiranjem lastnega kapitala v živo in
  uveljavljenimi pravili ter zaračunajte vstopnice. → [Pravila lastnine](./features/prop-firm.md)
- **Poslovanje s kopiranjem trgovanja.** Provizije za zmogljivost in trg ponudnikov spremenita kopiranje
  trgovanja v dohodek. → [Provizije za zmogljivost](./features/copy-performance-fees.md) ·
  [Trg ponudnikov](./features/copy-provider-marketplace.md)
- **Ravni značilnosti.** Odločite, katere zmožnosti vsak segment odjemalca vidi s
  [stikalnimi zmožnostmi](./features/feature-toggles.md).

## Regulirana, revizijska, večkratna najemnika

- **[Skladnost](./features/compliance.md)** dnevniki vam omogočajo sledilnik revizije, ki ga bo vaš regulator
  zahteval.
- **[Dvofaktorska avtentikacija](./features/two-factor-auth.md)** je mogoče narediti obvezno na ravni
  nameščanja.
- **Poimenovanje po odjemalcu** — tečite ločeno poimenovano instanco na segment, poganjano iz
  vaše lastne ravnine nadzora. → [Večkratna poimenovanja najemnika](./white-label-for-business.md#večkratna-poimenovanja-po-kupcu)

## Kako začeti

1. Preberite [Belo označena za poslovanje](./white-label-for-business.md) za 60-sekundno ponovno branding.
2. Nastavite `App:Accounts:AllowedBrokers` na vašo posredovalno hišo in izberite [nabor zmožnosti](./features/feature-toggles.md).
3. [Namestite](./deployment/cloud.md) — Docker, Kubernetes, Azure ali AWS.

Ali ne želite sam tečeti infrastrukture? Ponudnik gostovanja je lahko upravljal cMind za vas —
pokažite jim [Za oblačne in VPS ponudnike](./for-cloud-providers.md).

## Oblikujte vozovnik

cMind je odprta koda. Posredovalci, ki gradijo nanjo, dobijo večji vpliv na to, kam se obrne — zahtevajte
integracije in nadzore, ki jih potrebujete, in jih prispevajte nazaj prek
[Vodiča za prispevanje](./contributing.md).

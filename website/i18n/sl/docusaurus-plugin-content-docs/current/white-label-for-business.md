---
slug: /white-label-for-business
title: Belo označena za poslovanje
description: Pošljite cMind kot svoj lastni poimenovani izdelek — za lastnine, posrednike in podjetja za kopiranje trgovanja. Ponovno branding vsake površine prek konfiguracije, brez sprememb kode.
sidebar_position: 4
---

# Belo označeni cMind za vaše poslovanje 🏢

Ali tečete lastno hišo, posredovalni pult ali storitev kopiranja trgovanja? cMind je bil
izgrajen od prvega dne, da bi se **prodal kot vaš lastni izdelek**. Vsaka površina — ime, logotip,
favicon, barve, celo namestljiva aplikacija za telefon — se upogne vaši blagovski znamki. Vaši
odjemalci vidijo *vašo* podjetje. Brez sprememb kode, brez forka, le konfiguracija.

:::tip[Povzetek]
Pokažite `App:Branding` na svoje ime, barve in logotip. Ponovno zaženite. Dokončano. Polna tehnična
referenca je v [dokumentu belo označene značilnosti](./features/white-label.md).
:::

## Kaj lahko ponovno branding

| Površina | Kaj se spremeni |
|---|---|
| **Ime izdelka** | Besedilo aplikacije bar + naslov zavihka brskalnika |
| **Logotip in favicon** | Vaše oznake povsod, vključno z zavihkom brskalnika |
| **Barve** | Celotna paleta — primarna, površine, statusne barve — teče čez cel uporabniški vmesnik *in* lastno CSS aplikacije prek žetonov oblikovanja |
| **Namestljiva aplikacija (PWA)** | Ime, ikona in splash za dodaj na domačo zaslon uporabljajo vašo znamko |
| **Meta / SEO** | Opis in URL podpore sta vaša |
| **Prilagojeni CSS** | Vnesite svoj lesk za zadnjih 5% |

Vse privzeto nastavka na identiteto zaloge cMind, zato samo preglasujete, kar vas zanima.

## 60-sekundna ponovno branding

Nastavite te na svojem nameščanju (JSON konfiguracija ali spremenljivke okolja):

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

Oblika spremenljivke okolja: `App__Branding__ProductName=AcmeFX`. Barve se preverjajo ob zagonu —
slaba heksadecimalna vrednost ne uspe z jasnim sporočilom namesto renderiranja zlomljene strani.
Lepo in glasno, točno takrat, ko ga želite.

## Povezava "Poganja cMind"

Po **privzeto**, nadzorna plošča prikazuje majhno, okusno **"Poganja cMind"** povezavo, ki
obiskovalce vrne na to spletno mesto. Je privzeto, ker smo ponosni na projekt in pomaga drugim
trgovcem, da ga najdejo — vendar je to **vaša odločitev**.

- **Ohranite ga** (privzeto): subtilna kreditna povezava na nadzorni plošči. Vas ne stane nič,
  pomaga projektu.
- **Skrij ga**: nastavite `App__Branding__ShowSiteLink=false` in povsem izgine — odlično za
  popolnoma belo označeno nameščanje, kjer je izdelek nedvomno *vaš*.

Glejte [dokument belo označene značilnosti](./features/white-label.md#poganja-povezava) za
točno, kje se prikaže.

## Večkratni najemnik, branding po odjemalcu

Ker je branding samo konfiguracija nameščanja, lahko vsako nameščanje najemnika nosi svojo identiteto.
Tečite ločeno instanco na odjemalca ali vodite branding iz vaše lastne ravnine nadzora — aplikacija
ga bere iz `IOptionsMonitor`, zato se lahko celo živi ponovno gradnja teme, kadar se opcije spremenijo.

Parirajte to z:

- **[Stikalnimi zmožnostmi](./features/feature-toggles.md)** — odločite, katere zmožnosti vsak
  najemnik vidi.
- **[Lastnine pravila](./features/prop-firm.md)** — uveljavljajte svoja pravila za izzive z evidentiranjem
  lastnine v živo.
- **[Provizije za zmogljivost](./features/copy-performance-fees.md)** + **[trg ponudnikov](./features/copy-provider-marketplace.md)** — monetizirajte kopiranje trgovanja.
- **[Skladnost](./features/compliance.md)** — ohranite sledilnik revizije, ki ga bo vaš regulator
  zahteval.

## Sredstva in gostovanje

Spustite svoj logotip/favicon v `wwwroot/branding/` aplikacije Web (ali pokažite `LogoUrl`/`FaviconUrl`
na kateri koli absolutni URL). Namestite kako se vam zdi — [Docker](./deployment/local.md),
[Kubernetes](./deployment/kubernetes.md), [Azure](./deployment/cloud-azure.md) ali
[AWS](./deployment/cloud-aws.md).

Pripravljeni, da ga naredite svojega? Začnite s [tehnično referenco belo označeno →](./features/white-label.md)

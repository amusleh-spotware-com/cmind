---
title: 0005 — Az AI-kliens nyers HTTP-t használ, nem az Anthropic SDK-t
description: Miért az IAiClient az Anthropic API-t egy típusú HttpClient-en keresztül hívja meg az oficial SDK helyett, és miért az AI teljes egészében egy kulcson zárolódik.
---

# 0005 — Az AI-kliens nyers HTTP-t használ, nem az Anthropic SDK-t

## Kontextus

Minden AI-funkció (stratégia-generálás, önjavítás, kockázat-védelem, haláleset-következtetések) az Anthropic API-t hívja meg. Az SDK-függőség egy tranzitív felületet ad hozzá, amelyet nem irányítunk, az mi kiadási ciklusunkat az övékhez köti, és elrejtni az a pontos megállapodást, amelyre reziliencia és költség miatt szükségünk van.

## Döntés

Az `IAiClient` az Anthropic-ot **nyers HTTP**-n keresztül hívja meg egy típusú `HttpClient`-en keresztül — szándékosan **nem** az SDK. Az `AiFeatureService` az egyetlen orkesztrátora a Web-végpontoknak, az MCP `AiTools`-nak és az `AiRiskGuard`-nak. Az egész felület **`AppOptions.Ai.ApiKey`-en zárolódik**: kulcs nélkül minden funkció `AiResult.Fail`-t ad vissza, és az alkalmazás változatlan marad.

## Következmények

- Nincs szükség kulcs az építéshez, teszteléshez vagy E2E-hez — a CI és a helyi fejlesztés az AI nélküli teljes alkalmazást futtatja.
- A kérés/válasz-forma, az újrapróbálkozás/időtúllépés-szabályzat és a token-számlálás explicit módon az enyénk.
- Az új Anthropic-funkciókat manuálisan kell bekötni; kényelemért kontrollt és egy kisebb függőségi felületet cserélünk. Lásd a jelenlegi modell-azonosítókat és paramétereket a `claude-api` referenciában.

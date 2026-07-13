---
description: "Vozlišča CLI cTraderja se pridružijo grozdu s samoregustraci + srčnim utripom — ni ročnega vnosa. Isti vzorec kot agenti Consul/Nomad/kubeadm: agent se zaganja poznavanje..."
---

# Samodejno odkrivanje vozlišča

Vozlišča CLI cTraderja se pridružijo grozdu s **samoregustracija + srčnim utripom** — ni ročnega
vnosa. Isti vzorec kot agenti Consul/Nomad/kubeadm: agent se zaganja poznavanje lokacije glavnega
vozlišča + skupne gesle grozda, nato se nenehno najavljajo.

> Preverjeno od konca do konca na Docker Compose in `kind` grozdu Kubernetes: agenti samoregustrirajo,
> se pojavijo v DB dosegljivo, samodejno označeni nedosegljivo, ko srčni utripi prenehajo mimo TTL,
> vrnejo se na spletu, ko se nadaljujejo.

## Kako deluje

```
Agent CtraderCliNode                         Glavno (splet)
------------------                         ----------
POST /api/nodes/register  ── žeton sporočanja ──▶ preverit žeton (konstantni čas)
  { name, baseUrl, mode,                    preverit verzijo protokola
    maxInstances, dataDir,                   upsert CtraderCliNode po imenu
    protocolVersion }                        žig LastHeartbeatAt, IsReachable=true
        ▲                                     └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  vsak HeartbeatInterval            NodeHeartbeatMonitor (ozadje):
        └──────────────────────────────────── če sedaj - LastHeartbeatAt > HeartbeatTtl
                                                 → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **Registracija == srčni utrip.** Agent ponovno-POSTs na `HeartbeatIntervalSeconds`. Prvi klic
  ustvari vozlišče (`NodeRegistered` dogodek); poznejši klici osvežijo živost. Ponovno srčni utrip
  po motnji prestavi vozlišče nazaj dosegljivo (`NodeCameOnline`).
- **Usklajeno živosti.** `NodeHeartbeatMonitor` označuje vozlišča, čiji zadnji srčni utrip presega
  `HeartbeatTtl` nedosegljivo. Razpečevalnik (`IsActive`/`AcceptsRun`/`AcceptsBacktest` vrata na
  dosegljivosti) preneha postavljati delo, dokler ponovno ne poročajo.
- **Zahranjeno instanci. Popravilo** `NodeInstanceReclaimer` (ozadje) preslika katero koli ne-
  terminarno instanci zapuščeno na nedosegljivem vozlišču v **Neuspešna** (`FailureReason = "Node
  unreachable - instance reclaimed"`, `InstanceFailed` domenski dogodek → obvestilo uporabnika), tako
  da ruševski/delitev vozlišče nikoli ne more pustiti instanci zapičeno "Tečeče" za vedno.
  Pravilo samo spremembe, ko je vozlišče zadnji srčni utrip zastarel čez `HeartbeatTtl +
  InstanceReclaimGrace`, dajejo blisku šanso, da se najprej okrepi. Reklamirani **teki se
  ne-preplanirani samodejno**: delitev-toda-žive vozlišče, ki se še izvršuje kontejner in ni
  kontejnerja-ravni ograje, torej ponovno zagnanjena bi delovanju tveganja dvojnega izvršitve —
  uporabnik ponovno zaženete reklamiran tek namerno. Testiranja same exit, zato je reklamiran
  testiranje preprosto ponovno tečeno.
- **Identiteta je ime vozlišča.** Glavno upserts po `NodeName`, torej pod, katerega IP/URL spremeni
  ob ponovno zagonu, drži identiteto, ponovno-registracija nova `AdvertiseUrl`.
- **Način fiksen ob prvi registraciji.** Način vozlišča (`Run`/`Backtest`/`Mixed`) je obstojana
  vrsta, ne more spremeniti na srčni utrip; ponovno-registracija z drugačnim načinom čaščena za
  živost, vendar je sprememba načina prezrta (beležena kot opozorilo). Za spremembo načina: izbriši
  vozlišče, pustiti ga ponovno registrira.

## Konfiguracija

Glavno (splet) — `App:Discovery`:

| Ključ | Privzeto | Pomen |
|-----|---------|---------|
| `Enabled` | `false` | Master preklapljač za registriranje končne točke + monitor. |
| `JoinToken` | — | Skupna skrivnost grozda (≥ 32 znakov) agenti morajo predstaviti. |
| `HeartbeatTtl` | `00:01:30` | Milost pred nemim vozliščem, označenim nedosegljivo. |
| `InstanceReclaimGrace` | `00:01:00` | Dodatna meja čez `HeartbeatTtl`, preden zapuščena instanci na nedosegljivem vozlišču reklamira (neuspešna). |
| `MonitorInterval` | `00:00:30` | Kako pogosto nadzornik in instance-reclaimer sweep. |
| `HeartbeatInterval` | `00:00:30` | Vrednost, ki je bila vrljena agentom kot sugeriran kadenci. |

Agent (CtraderCliNode) — `NodeAgent`:

| Ključ | Pomen |
|-----|---------|
| `MainUrl` | Osnovna URL glavnega vozlišča. Prazna = način ročnega registriranja (nop zanke). |
| `AdvertiseUrl` | URL glavno uporablja za doseg **tega** agenta. |
| `NodeName` | Edinstveno ime; privzeto na ime stroja, če prazna. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | Namig zmogljivosti, ki ga spoštuje razpečevalnik. |
| `HeartbeatIntervalSeconds` | Ponovno-registriranje kadenci. |
| `JwtSecret` | Mora biti enaka glavnemu `JoinToken` — tako registriranje nosilca kot ključ za podpisovanje JWT. |

## Varnostni model (v1)

Samoregustriran vozlišča delijo **eno skrivnost grozda** (`JoinToken` == vsak agent `JwtSecret`).
Glavno podpiše vsako zahtevo za pošiljanje kot 5-minutni HS256 JWT s to skrivnostjo; agent
preveri. Zahtevki:

- Ohranite `JoinToken` ≥ 32 znakov in ga rotirajte (ažurirajte glavno `App:Discovery:JoinToken` in
  vsakega agenta `NodeAgent:JwtSecret` skupaj).
- Končajte TLS pred glavnim in agenti v produkciji (obratni proxy / ingress).
- Agent še vedno samo tečejo slike ustrezne `AllowedImagePrefix`.

**Ojačanje nadslednje (ne v1):** izdati edinstveno na vozlišče skrivnost ob registraciji (kubeadm-slog
bootstrap → na vozlišče pooblastilo), zato ena ogrožena agenta ne more lažna pošiljanja žetonov za
kolege. Pretok registracije že vrne telo odgovora — naravna mesto za vrnjene žetone na vozlišče.

## Ročna vozlišča še delujejo

`POST /api/nodes` (admin UI) se nadaljuje za registriranje pivotnih vozlišč z lastno na vozlišče
skrivnost. Odkrivanje je seštevka.

Belo označena nameščanja lahko **skrijejo ročne kontrole** (ali celotna vozlišča površino) in se
zanašajo čisto na samodejno odkrivanje: `App:Branding:NodesUi=Monitor` padec ročne dodaj/izbriši,
`Hidden` odstrani nav, stran in ročna API in `App:Branding:RestrictNodesToOwner` tla površino na
samo-lastnikom. Samoregustracija + končna točka srčnega utripa je tukaj nespremenjeno v vsakem
načinu. Poglejte [Belo označena → Vidnost vozlišča UI](../features/white-label.md#vidnost-vozlisca-ui).

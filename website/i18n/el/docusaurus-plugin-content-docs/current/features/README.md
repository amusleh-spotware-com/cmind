---
slug: /features
title: Δυνατότητες — ο πλήρης γύρος
description: Όλα όσα μπορεί να κάνει το cMind — copy trading, AI, build & backtest, prop-firm guards, white-label, PWA, MCP, και άλλα.
sidebar_label: Επισκόπηση
---

# Δυνατότητες — ο πλήρης γύρος 🧭

Καλώς ήρθατε στον μεγάλο γύρο. Το cMind συγκεντρώνει πολλά χαρακτηριστικά σε μία εφαρμογή, οπότε ορίστε ο χάρτης. Κάθε δυνατότητα
διαθέτει τη δική της εξαντλητική τεκμηρίωση — κάντε κλικ σε ό,τι σας ενδιαφέρει.

## 🔁 Copy trading

Το κορυφαίο χαρακτηριστικό. Αντιγράψτε έναν master λογαριασμό σε πολλούς και κρατήστε τους συγχρονισμένους ακόμη και όταν το διαδίκτυο
δεν λειτουργεί σωστά.

- **[Copy trading](./copy-trading.md)** — το κύριο χαρακτηριστικό: αντιγραφή, τύποι παραγγελιών, SL/TP, ολίσθηση, desync/resync.
- **[Transparency εκτέλεσης](./copy-execution-transparency.md)** — δείτε ακριβώς τι αντιγράφτηκε, πότε και γιατί.
- **[Χρεώσεις απόδοσης](./copy-performance-fees.md)** — χρεωθείτε για το σήμα σας, με στυλ high-water-mark.
- **[Provider marketplace](./copy-provider-marketplace.md)** — επιτρέψτε στους traders να ανακαλύπτουν και να ακολουθούν providers.
- **[Ειδοποιήσεις](./copy-notifications.md)** — γίνετε ενήμεροι όταν κάτι χρειάζεται την προσοχή σας.
- **[AI copy recommender](./ai-copy-recommender.md)** — αφήστε το AI να σας προτείνει ποιον να ακολουθήσετε.
- **[Open API token lifecycle](./token-lifecycle.md)** — πώς το cMind κρατάει ένα και μόνο έγκυρο token ανά cID.

## 📊 Το κέντρο ελέγχου σας

- **[Dashboard](./dashboard.md)** — το live, mobile-first command center: KPIs με sparklines, ένα activity chart, ένα status ring, ένα live feed, και (για admins) cluster health. Ανανεώνεται μόνο του.

## 🧠 Πυρήνας AI

Όχι μια συνομιλία προσαρτημένη στο πλάι — AI που πραγματικά κάνει τη δουλειά.

- **[AI assistant, agent, risk guard & alerts](./ai.md)** — δημιουργία στρατηγικών, self-repairing builds, ένας risk guard στο background που μπορεί να σταματήσει αυτόματα bots, και έξυπνες ειδοποιήσεις.

## 🛠️ Build & run

- **[Build & backtest cBots](./build-and-backtest.md)** — το in-browser Monaco IDE, C#/Python templates, sandboxed builds, και live equity curves.
- **[MCP server](./mcp.md)** — εκθέστε τα εργαλεία του cMind μέσω HTTP + SSE ώστε οι AI clients να μπορούν να το λειτουργούν.

## 🏢 Λειτουργήστε το ως επιχείρηση

- **[White-label / branding](./white-label.md)** — αναδιαμορφώστε κάθε επιφάνεια μέσω config.
- **[Prop-firm challenge simulation](./prop-firm.md)** — επιβάλλετε κανόνες daily-loss, drawdown, και target με live equity.
- **[Feature toggles](./feature-toggles.md)** — αποφασίστε τι βλέπει κάθε deployment/tenant.
- **[Compliance / legal](./compliance.md)** — το audit trail και η νομική επιφάνεια.

## 📱 Η εμπειρία

- **[Installable app (PWA)](./pwa.md)** — mobile-first, offline shell, add-to-home-screen.
- **[UI design system & mobile-first](../ui-guidelines.md)** — τα design tokens και οι κανόνες πίσω από την εμφάνιση.

## ⚙️ Κάτω από το καπό

Τα λειτουργικά bits που κρατούν όλα αυτά σε εξέλιξη:

- **[Node fleet & discovery](../operations/node-discovery.md)** — πώς τα nodes αυτορεγιστρώνονται και επιδιορθώνονται.
- **[Horizontal scaling](../deployment/scaling.md)** — προσθέστε replicas, χωρίς εξωτερικό συντονιστή.
- **[Logging & audit](../operations/logging.md)** — structured logs + OpenTelemetry.
- **[Deployment](../deployment/local.md)** — βάλτε το να λειτουργεί οπουδήποτε.

:::note Κρατώντας τα docs ειλικρινή
Κάθε doc δυνατότητας κρατείται σε συμφωνία με τον κώδικα — αλλάξτε τη συμπεριφορά, ενημερώστε το doc, ίδιο
commit. Αν ποτέ δείτε drift, είναι ένα bug: παρακαλώ
[ανοίξτε ένα issue](https://github.com/amusleh-spotware-com/cmind/issues/new/choose) ή στείλτε ένα PR. 🙏
:::

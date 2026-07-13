---
description: "deploy/azure/main.bicep προμήθειες stateless tier στα Azure Container Apps συν Postgres Flexible Server + Log Analytics."
---

# Ανάπτυξη Azure — βήμα προς βήμα

`deploy/azure/main.bicep` προμήθειες stateless tier στα **Azure Container Apps** συν **Postgres Flexible Server** + Log Analytics.

## 1. Προαπαιτούμενα

- Azure CLI (`az login` έκανε), συνδρομή, άδεια να δημιουργήσουν ομάδες πόρων.
- Τρεις εικόνες pushed στο κατάστημα Azure μπορεί να τραβήξει (π.χ. GHCR δημόσιο, ή ACR).

## 2. Δημιουργία ομάδας πόρων

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Ανάπτυξη του Bicep

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

Δημιουργεί: Container Apps περιβάλλον, Web (εξωτερική ingress), MCP (εξωτερική ingress), Postgres Flexible Server + `appdb`, Log Analytics, **χώρο εργασίας-βάση Application Insights** συστατικό. Ανακάλυψη στο για Web. Η συμβολοσειρά σύνδεσης ενέχεται στο Web + MCP ως `APPLICATIONINSIGHTS_CONNECTION_STRING`, έτσι ίχνη + μετρικές εξαγωγές ναι στο App Insights ενώ τα κούρνια κάθονται στον ίδιο Log Analytics χώρο εργασίας — δεν χρειάζεται συλλέκτης. Περάστε `-p otlpEndpoint=...` για *επίσης* προώθηση στο OTLP συλλέκτη.

## 4. Λάβετε τα URLs

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

Ανοίξτε `webUrl`, συνδεθείτε με ιδιοκτήτη (αναγκάστηκε αλλαγή κωδικού πρόσβασης κατά την πρώτη σύνδεση).

## 5. Προσθήκη πράκτορες κόμβων (ξεχωριστά)

Οι Container Apps δεν μπορούν να τρέχουν προνόμια/DinD, έτσι τρέχουν πράκτορες αλλού, δείχνουν στο `webUrl`:

- **AKS** — ανάπτυξη Helm chart ([kubernetes.md](kubernetes.md)) με `nodeAgent.privileged=true`, κλίμακα Web/MCP σε 0 εάν θέλω μόνο agent tier εκεί.
- **VM / VMSS** — τρέχουν `cmind-node-agent` εικόνα `--privileged` με `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm reachable url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`.

Οι πράκτορες αυτο-εγγράφονται εντός ενός διαστήματος καρδιακής συχνότητας — δείτε [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Επαλήθευση

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # συμπιεσμένο JSON logs
curl -s <webUrl>/version
```

## Σημειώσεις παραγωγής

- Front Web με Azure Front Door / App Gateway για TLS + WAF.
- Αποθηκευμένα μυστικά σε Key Vault. περάστε σταθερό Data Protection cert (`App__DataProtectionCertBase64` / `...Password`) έτσι keyring επιζούν replicas restarts.
- App Insights (ίχνη+μετρικές) + Log Analytics (logs) συρόμενα αυτόματα. συσχετίζετε στο `trace_id`. Δείτε [../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics).
- Ορίστε `otlpEndpoint` param (ή `OTEL_EXPORTER_OTLP_ENDPOINT` στις εφαρμογές) να *επίσης* προώθηση στο συλλέκτη.
- Container Apps `scale` κανόνες (min/max) συρόμενα σε Bicep.

## Πράκτορας αντιγραφής + Key Vault (S5)

`deploy/azure/main.bicep` επίσης προμήθειες **copy-agent** Container App φιλοξενίας `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) με **όχι ingress** — εργάτης που κρατά μακροχρόνιες cTrader sockets. Διαβάζει συμβολοσειρά σύνδεσης DB από **Azure Key Vault** μυστικό μέσω **χρήστη-ανατεθειμένη διαχειριζόμενη ταυτότητα** (Key Vault Secrets User ρόλο) παρά inline plaintext μυστικό. Κάθε replicas's `NodeName` προεπιλογή στο όνομα κοντέινερ του (μοναδικό), έτσι DB lease αποδίδει τρέχουσα προφίλ ανά replica και δύο replicas ποτέ διπλό-κεντρική ένα. Κλιμάκωση `minReplicas`/`maxReplicas` για προσθήκη χωρητικότητας αντιγραφής. DataProtection keyring δοχείο μέσω Postgres, έτσι κάθε replica μπορεί να αποκρυπτογραφήσει αποθηκευμένα Open API tokens. Δοδέκατα: `copyAgentName`, `keyVaultName`.

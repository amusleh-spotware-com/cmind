---
description: "Ogni ora mostrata appare nel tuo fuso orario — rilevato dal browser alla prima visita e modificabile dalle Impostazioni. Archiviazione e API restano in UTC."
---

# Fuso orario

Ogni ora mostrata dall'app è resa nel tuo fuso orario, non del server. La tua scelta viene salvata nel profilo e ti segue tra i dispositivi.

Alla prima visita l'app adotta automaticamente il fuso del tuo browser. Puoi cambiarlo in qualsiasi momento da Impostazioni → Fuso orario; il default di distribuzione è l'opzione white-label App:Branding:DefaultTimeZone (predefinito UTC). Gli orari sono sempre archiviati e restituiti dall'API in UTC — solo la visualizzazione viene convertita.

- Ordine di risoluzione: fuso del profilo, poi il cookie, poi il default di distribuzione, poi UTC.
- Il rilevamento viene eseguito una volta e non sovrascrive mai un fuso che hai scelto.
- La formattazione segue la tua lingua; le etichette relative come «2 minuti fa» non sono influenzate.

---
description: "Every displayed time is rendered in the user's own time zone, detected from the browser on first visit and changeable from Settings. Storage and APIs stay UTC."
---

# Time zone

Every time the app shows you — instance timestamps, alert times, calendar releases, the dashboard "updated"
clock — is rendered in **your** time zone, not the server's. Your choice is saved to your profile, so it
follows you across devices and sessions.

## How the zone is resolved

The effective zone is resolved in this order:

1. **Your saved profile zone** — carried in your session as a claim and applied on every render.
2. **The time-zone cookie** — set when the browser is auto-detected or when you pick a zone (used before
   sign-in and during server-side rendering).
3. **The deployment default** — `App:Branding:DefaultTimeZone` (owner-tunable, see below).
4. **UTC** — the final fallback.

Storage and the wire never change: all timestamps are stored and returned by the API in **UTC** (ISO-8601).
Only the *presentation* is converted, using your zone and your language's formatting.

## First visit — automatic local zone

On your first visit, the app reads your browser's IANA zone
(`Intl.DateTimeFormat().resolvedOptions().timeZone`) and adopts it automatically — no setup needed. It is
then persisted to your profile, so every future device already knows it. Detection runs only once (a cookie
suppresses it afterwards) and never overrides a zone you chose explicitly.

## Changing your zone

Open **Settings → Time zone** and pick any zone from the searchable list. Because a Blazor Server circuit
cannot change its zone live, the choice is applied through the `/set-timezone` endpoint with a full reload:
the cookie is written, the choice is saved to your profile, and the fresh page renders every time in the new
zone. The panel shows your current zone.

## Owner setting — deployment default

The default zone for users who have not chosen one (and whose browser was not detected) is the white-label
option **`App:Branding:DefaultTimeZone`** (default `UTC`). An owner can retune it at runtime from
**Settings → Deployment** exactly like any other white-label option — see
[White-label owner settings](./white-label-owner-settings.md). It accepts any IANA id (e.g. `Europe/London`);
a Windows id is normalized to its canonical IANA form.

## Notes

- Zone ids are validated against the platform's IANA database; an unknown id is rejected and ignored.
- Display formatting follows your language (culture) — see [Localization](./localization.md).
- Relative labels ("2 minutes ago") are zone-agnostic and unaffected.

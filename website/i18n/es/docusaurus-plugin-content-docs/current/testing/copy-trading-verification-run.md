---
description: "Verificación completa del trabajo copy-trading restante — todo abajo **realmente ejecutado**, no solo autorizado."
---

# Copy-trading verification run (2026-07-10)

Verificación completa del trabajo copy-trading restante — todo abajo **realmente ejecutado**, no solo autorizado.

## En vivo (cuentas demo de cTrader reales) — 8/8 aprobado
1:1 · 1:many · inverso · cross-cID · cierre parcial · **límite pendiente + cancelar** · **stop final** · actualización de token.
Escenarios en vivo agregados `RunPendingAsync` / `RunTrailingAsync` (+ `LoadSpotPriceAsync`, `OpenPositionSnapshot.StopLoss/TrailingStopLoss`).

## Integración (Postgres real, Testcontainers) — aprobado
- `CopyNodeAffinityTests` — supervisor de reclamo atómico real: primer nodo reclama todos los perfiles en ejecución, segundo reclama **0** (sin copia doble); pausa libera + reclamación.
- `TokenRotationSignatureTests` — firma cambia solo en rotación real de token.

## En clúster (kind + Helm) — aprobado
Instalé `kind`/`kubectl`/`helm`, ejecuté `scripts/k8s-e2e.sh` contra clúster real de kind:
- **Trabajo determinista: 101 aprobado** en clúster.
- **Trabajo en vivo: 8 aprobado** en clúster (init-container `seed-secrets` copia Secret → emptyDir escribible, cuentas demo reales).
- Trabajo `Complete 1/1`, script salida 0.

## Errores encontrados mientras se verificaba (fijo + re-verificado)
- **Eventos pendientes**: cTrader adjunta *marcador de posición de posición que no se abre* a límite/stop reposado `ORDER_ACCEPTED`/`CANCELLED`. `SourceExecutionsAsync` ahora clasifica colocación/cancelación como evento de orden antes de rama de posición, pero deja límite/stop *llenar* (ej. cierre activado por stop-loss) caer a ruta de cierre.
- **Tokens de actualización de un solo uso**: cTrader rota el token de actualización cada actualización. Caché de solo lectura que no puede persistir se auto-invalida. El trabajo K8s en vivo por lo tanto copia Secret en **emptyDir escribible**; Job por defecto a suite determinista. `SaveTokens` ahora mejor esfuerzo. Símbolos en vivo forzados a FX (BTCUSD trailing enmiendas corredor rechazado).
- Nombres de imagen de script fijos para coincidir con `registry/repository` de Helm dividido + `pullPolicy=Never`.

## Programa de mirroring avanzado + token-lifecycle + escalado (2026-07-10) — tiers deterministas aprobados

El programa de seguimiento agrega filtrado de tipo de orden, copia de vencimiento de orden pendiente, mirroring de rango de mercado / deslizamiento de límite de parada, toggles de copia SL/TP, intercambio de token en lugar graceful (token válido único por cID), simulador fiel a cTrader, arrendamiento de nodo auto-sanable, archivo unificado de credenciales dev.

- **Unit — 210 aprobado** (`dotnet test tests/UnitTests`). Cobertura de copia nueva: filtro de tipo de orden (abierto + pendiente), espejo de deslizamiento de rango de mercado + precio base, copia de vencimiento on/off, deslizamiento de límite de parada, enmienda pendiente, inicio-con-apertura-maestra, desconexión→maestra-comerciada→reconexión resincronización (apertura faltante + cierre huérfano), intercambio de token en lugar (sin reinicio), invalidación cross-cID, invariantes de dominio, propiedad de arrendamiento, bump de versión de token.
- **Integración (Postgres real, Testcontainers) — aprobado**: `CopyNodeAffinityTests` (reclamo atómico, sin copia doble, liberación de pausa, **reclamación de arrendamiento expirado por otro nodo**), `TokenRotationSignatureTests` (firma cambia en bump de versión de token), `OpenApiAuthorizationPersistenceTests` (TokenVersion persiste + incrementos en actualización).
- **E2E** (`tests/E2ETests`): viaje de ida y vuelta de opción de destino ahora asevera filtro de tipo de orden, vencimiento de copia, deslizamiento de copia junto con ciclo de vida completo.
- **Build**: limpio bajo `TreatWarningsAsErrors`; Rider `get_file_problems` limpio en archivos cambiados.

Escenarios en vivo (cuentas demo de cTrader reales) para parada pendiente, rango de mercado, vencimiento, inicio-con-apertura, rotación de token a mitad de ejecución autorizada contra el mismo motor; ejecutar con `secrets/dev-credentials.local.json` unificado per [dev-credentials.md](dev-credentials.md).

## Seguimiento conocido
La ejecución en vivo en clúster roté token de un solo uso; regenerar caché local con `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests` (cTrader limitó su página OAuth justo después de la ejecución — reintentar cuando se despeje).

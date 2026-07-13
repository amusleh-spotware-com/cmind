---
description: "Suite de estrés. Martilla partes de app cuya falla cuesta dinero a usuarios — principalmente copy trading — con cargas de trabajo hostiles, randomizadas, inyectadas de fallo. Asevera que el sistema…"
---

# Stress testing

Suite de estrés. Martilla partes de app cuya falla cuesta dinero a usuarios — principalmente **copy trading** — con cargas de trabajo hostiles, randomizadas, inyectadas de fallo. Asevera que el sistema permanezca correcto. Vive en `tests/StressTests`, se ejecuta en puerta verde normal de `dotnet test`.

## Enfoque — Deterministic Simulation Testing (DST)

La mejor manera de estressar sistemas financieros distribuidos = **deterministic simulation testing**, per TigerBeetle, FoundationDB, Antithesis: ejecutar lógica real contra mundo *simulado*, conducir con carga de trabajo aleatoria **semillada** + fallos inyectados, aseverar invariantes en quietud. Todo semillado + determinista → cualquier fallo se reproduce exacto desde semilla. Combinado con:

- **Inyección de fallo de ingeniería caótica** (estilo Netflix Chaos Monkey) — caídas de conexión, rechazos de orden, rotación de token, muerte de nodo.
- **Invariantes basados en propiedades** — sin aseverar secuencias de llamada exactas; aseverar propiedades que deben mantener sin importar cómo se intercalen los eventos (convergencia, sin huérfanos, como máximo un tenedor de arrendamiento).

La app ya envía modelo de mundo DST perfecto: `FakeTradingSession`, sesión de Open API en memoria fiel a cTrader. La suite de estrés la reutiliza (vinculada, única fuente de verdad) no mock, por lo que el corredor simulado se comporta como uno real.

## Qué cubre

### Copy trading (enfoque principal)

Conducido vía `CopyDstWorld` (`tests/StressTests/CopyTrading/`), ejecuta `CopyEngineHost` en vivo contra sesión falsa, emite carga de trabajo consistente con membresía de fuente:

| Escenario | Estresa |
|---|---|
| `Mass_fan_out…` | 1 fuente → 80 destinos, 150 aperturas luego cierres; fan-out completo + drenaje |
| `High_frequency_open_close…` | 300 aperturas/cierres intercaladas rápidas; sin posiciones fugadas |
| `Partial_close_and_scale_in_storm…` | churn parcial de cierre + scale-in; estabilidad de conjunto de etiquetas |
| `Connection_flap_storm…` | reconexión/desconexión de socket repetida + resync a mitad de vuelo; convergencia de resync |
| `Order_rejection_cascade…` | un subconjunto rechaza cada orden; destinos saludables no afectados, luego auto-sanación vía resync |
| `Token_rotation_storm…` | intercambios rápidos de token en lugar durante una tormenta de orden |
| `Randomized_chaos_workload…` (10 semillas) | **el núcleo DST** — cada tipo de evento + cada fallo intercalado impredeciblemente |
| `CopyLeaseReclaimStressTests` | muerte de nodo + reclamación de arrendamiento en clúster escalado (dominio puro, `FakeTimeProvider`) |

**Invariante de convergencia.** En reposo, cada destino saludable refleja exactamente el conjunto de posiciones de fuente aún abiertas — sin huérfanos, ninguno faltante. Aseverado en conjunto de etiquetas (escala-in legítimamente abre segunda posición de destino bajo la misma etiqueta de fuente, así que etiquetas duplicadas esperadas). Se permite al destino actualmente rechazando órdenes quedarse atrás, reconciliado una vez sanado.

**Invariante de arrendamiento.** En clúster donde los nodos mueren + reviven en cronograma sembrado, como máximo un nodo jamás mantiene arrendamiento válido en un perfil; arrendamiento del nodo muerto expira exacto en expiración, se reclama; clúster saludable se resuelve con cada perfil sostenido por exactamente un nodo. Refleja el predicado de reclamo de `CopyEngineSupervisor` contra los métodos de dominio de arrendamiento de `CopyProfile`.

### Thread-safety del arnés

`FakeTradingSession` single-threaded; la carga de trabajo de estrés lo muta desde el hilo de prueba mientras el host lee/escribe desde su bucle. `SyncTradingSession` lo envuelve, hace cada operación de sesión atómica en una puerta (sin mantener puerta durante callback de reconexión — invertiría orden de bloqueo vs `_stateGate` del host y entraría en deadlock). El simulador mismo se deja intacto.

## Errores encontrados

- **Carrera de resync de inicio en `CopyEngineHost`.** `OnReconnected` cableado antes de carga de referencia inicial + primer resync, que se ejecutó sin `_stateGate`. El aleteo de socket durante el inicio ejecutó segundo resync concurrente, corrompió dicts de estado no concurrente del host (`_symbolDetails`, `_sourceVolumes`). Fijo: ejecutar carga de inicio + primer resync bajo puerta. Carrera de producción, no artefacto de prueba — la carga de trabajo caótica DST la superficializó.

## Ejecutando

```bash
dotnet test tests/StressTests/StressTests.csproj
```

Suite **serializada** (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`): cada prueba gira bucle de fondo del host en vivo, conduce a quietud bajo reloj de pared, así que la ejecución paralela inanición de tareas del host hace que los timeouts de convergencia sean inestables. Las cargas de trabajo se dimensionan para terminar en segundos para que la suite permanezca en puerta verde predeterminada. La falla imprime su semilla; re-ejecuta esa semilla para reproducir intercalado exacto.

## Extendiendo

- Nuevo comportamiento de copia → agregar op de fuente a `CopyDstWorld` (mantener membresía del libro de fuente consistente con flujo de eventos) + caso ponderado en `CopyChaosDstTests`. Si puede crear o retirar una posición de destino, asegúrate de que el invariante de convergencia aún se mantenga.
- Nuevo fallo → agregar inyector a `CopyDstWorld` (delegar a superficie de control de `FakeTradingSession` vía `SyncTradingSession`) + ejercitar en escenario nombrado más mezcla caótica.
- Mantener simulador fiel a cTrader (ver mandato de `CLAUDE.md` raíz); nunca lo debilites para hacer pasar una prueba de estrés.

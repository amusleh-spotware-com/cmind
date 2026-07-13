---
description: "Suite de stress. Falsifica partes de la app cuyo fallo cuesta dinero a los usuarios - principalmente copy trading - con cargas de trabajo hostiles, aleatorizadas e inyectadas con fallos."
---

# Pruebas de stress

Suite de stress. Falsifica partes de la app cuyo fallo cuesta dinero a los usuarios - principalmente copy trading - con cargas de trabajo hostiles, aleatorizadas e inyectadas con fallos. Afirma que el sistema se mantiene correcto.

## Enfoque - Pruebas de Simulacion Deterministica (DST)

La mejor manera de hacer stress de sistemas financieros distribuidos es DST: ejecutar logica real contra un mundo simulado, impulsar con carga de trabajo aleatoria con semilla + fallos inyectados, afirmar invariantes en quiescencia. Combinado con:

- Inyeccion de fallos al estilo Chaos Monkey.
- Invariantes basadas en propiedades.

## Que cubre

### Copy trading (enfoque principal)

| Escenario | Falsifica |
|---|---|
| Mass fan out | 1 fuente -> 80 destinos, 150 abre/cierra |
| High frequency open close | 300 rapid open/close interleaved |
| Partial close and scale in storm | partial close + scale in churn |
| Connection flap storm | disconnect/reconnect repetido |
| Order rejection cascade | subconjunto rechaza cada orden |
| Token rotation storm | swaps de token durante tormenta de ordenes |
| Randomized chaos workload (10 seeds) | todos los tipos de evento + fallos intercalados |
| CopyLeaseReclaimStressTests | muerte de nodo + reclamacion de lease |

Invariante de convergencia: cada destino saludable refleja exactamente el conjunto de posiciones abiertas de la fuente.

## Bugs encontrados

- Carrera de resincronizacion de inicio en CopyEngineHost. OnReconnected conectado antes de la carga inicial + primera resincronizacion, que se ejecutaba sin _stateGate.

## Ejecucion

dotnet test tests/StressTests/StressTests.csproj

La suite es serializada: cada prueba hace girar el host en segundo plano, conduce a quiescencia, afirma. Las cargas de trabajo se dimensionan para terminar en segundos.

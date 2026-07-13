---
description: "Agent Studio — crea agentes de trading impulsados por personajes, sin código, con una personalidad y arquetipo que gestionen cuentas hacia tus objetivos bajo el Kernel de Autonomía y Seguridad (envolvente de riesgo, disyuntor, interruptor de emergencia, consentimiento de descargo versionado)."
---

# Agent Studio

Agent Studio te permite crear un **agente de trading con personalidad** — sin código — y darle la gestión
de tus cuentas hacia objetivos medibles. Un agente es como un cBot impulsado por la personalidad: eliges
un arquetipo y actitud, establecer las barreras de seguridad, y se ejecuta bajo el **Kernel de Autonomía
y Seguridad**.

Abre **AI → Agent Studio** (`/agent-studio`).

## Crear un agente

El diálogo **Nuevo agente** recopila, sin código:

- **Nombre** y **arquetipo** — Scalper, Day Trader, Swing Trader, Position Trader, News Trader,
  Contrarian, Mean Reversion o Breakout/Momentum. Cada preconfiguración establece un ritmo y postura sensatos.
- **Actitud** — controles deslizantes de agresividad, paciencia y seguimiento de tendencias.
- **Nivel de autonomía** — **Asesor** (solo propone) o **Aprobación requerida** (actúa solo después de tu
  aprobación por acción). **Totalmente automático** (sin aprobación por operación) requiere además un **envolvente de riesgo**
  y aceptación del descargo de responsabilidad antes de poder activarse.

La persona se compila **determinísticamente** en el aviso del sistema del agente (ningún LLM la crea), por lo que
la misma configuración siempre produce las mismas instrucciones — reproducibles y auditables.

## El registro

Cada agente se muestra en una tabla de sala de control: **qué agente, su tipo, cuántas cuentas gestiona, sus
objetivos, estado de ejecución y última acción**, con controles de **Iniciar / Detener / Parar**. El interruptor de parada detiene
un agente en ejecución inmediatamente.

## La seguridad es un invariante del dominio, no una configuración

Todo lo que toca el dinero se encamina a través del **Kernel de Autonomía y Seguridad**:

- **Envolvente de riesgo** — límites duros por orden (pérdida diaria máxima, exposición abierta, tamaño de posición, apalancamiento,
  pérdidas consecutivas, órdenes/hora, símbolos permitidos). Cada orden se valida contra él antes del envío;
  una violación se rechaza, no se limita. Se requiere antes de que un agente pueda alcanzar Totalmente automático.
- **Disyuntor** — detiene determinísticamente el riesgo nuevo en una racha de pérdidas, una violación de pérdida diaria, un **incumplimiento de objetivo de rendimiento duro**,
  o **indisponibilidad del proveedor de IA** (un modelo inactivo o alucinante nunca abre
  posiciones nuevas).
- **Consentimiento de descargo versionado** — se requiere una aceptación única y versionada para activar Totalmente automático
  (consentimiento legalmente requerido, no aprobación por operación); actualizar el descargo fuerza una nueva aceptación.
- **Interruptor de parada** — una detención de emergencia idempotente en cada agente en ejecución.

## Objetivos

Dale a un agente **objetivos medibles** — p. ej. *mantener la reducción máxima por debajo del 4%*, *factor de ganancia al menos
1.5*, *tasa de ganancia ≥ 55%*. Cada objetivo es **Duro** (una barrera de seguridad — una violación activa el disyuntor) o
**Suave** (solo guía el razonamiento), evaluado como En camino / En riesgo / Incumplido.

## La tubería de decisión

Una vez iniciado, un agente ejecuta un **bucle supervisado 24/7** (`AgentRuntimeService`). Cada ciclo, para cada
cuenta gestionada, lee el **estado de cuenta determinístico** (la verdad fundamental, nunca la memoria del modelo);
pide al motor de decisión una acción; la pasa a través de la **puerta de seguridad** (`AgentDecisionProcessor`) —
nivel de autonomía → disyuntor → envolvente de riesgo; escribe un **`AgentDecisionRecord`** de solo anexar; y
se detiene o se ejecuta según la puerta indique. El bucle es **aislado de fallas** (el fallo de un agente nunca toca
a otro o al host) y **seguro por defecto**: es inerte a menos que la IA esté configurada *y*
`App:Ai:AgentRuntimeEnabled` esté configurado, y nunca abre riesgo nuevo mientras el proveedor de IA no esté disponible.

- **Puerta de aprobación** — la orden propuesta de un agente **Aprobación requerida** se registra como **Pendiente** y no
  hace nada hasta que el propietario la apruebe (`POST /api/agent-studio/{id}/decisions/{seq}/approve` o
  `/reject`); **Totalmente automático** se abre a través del envolvente sin aprobación por operación; **Asesor** solo
  propone.
- **Registro de auditoría** — cada decisión es reproducible: razonamiento (XAI), la evidencia que citó, el veredicto
  de la puerta, la intención de orden y si se ejecutó, en `GET /api/agent-studio/{id}/decisions`.
- **Sala de investigación** — un debate multiagente bajo demanda: analistas de Alfa/Sentimiento/Técnico/Riesgo cada uno dan
  una vista y un Revisor sintetiza una propuesta (`POST /api/agent-studio/{id}/debate`).
- **Memoria** — el agente recuerda cada decisión y retoma la memoria reciente en su siguiente aviso para
  continuidad (`GET /api/agent-studio/{id}/memory`).

La pestaña **Detalles** de cada fila del registro abre el feed de decisión del agente (con Aprobar/Rechazar en órdenes pendientes),
su memoria, y una pestaña Ejecutar-debate.

## Ámbito

Enviado: el ciclo de vida completo del agente, la puerta de seguridad determinística, el tiempo de ejecución 24/7, la
puerta de aprobación humana en el bucle, el registro de auditoría, y la **integración activa de la Open API cTrader** — la tienda de estado de cuenta
(lee el saldo real, posiciones y exposición abierta en lotes) y el ejecutor de órdenes (coloca órdenes de mercado reales, lotes→volumen a través del
tamaño de lote de símbolo), ambos resolviendo las credenciales de OAuth de cada cuenta gestionada y
degradándose de forma segura cuando una cuenta no está vinculada. **Requiere la clave de API de Anthropic** para que el modelo
genere órdenes (hasta entonces el motor se detiene); lo que aún está por venir son roles de debate multiagente y
memoria/reflexión en capas. El tiempo de ejecución está desactivado a menos que `App:Ai:AgentRuntimeEnabled` esté configurado, por lo que el trading en vivo solo
ocurre en una aceptación de participación totalmente consentida y explícita.

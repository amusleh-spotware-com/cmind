---
description: "Ayudante de IA. Recomienda configuración segura de destino de copia de trading desde perfil de riesgo de seguidor + descripción de cuenta fuente (maestra). Expuesto sobre API REST, herramienta MCP…"
---

# Recomendador de perfil de copia de IA

Ayudante de IA. Recomienda configuración segura de destino de copia de trading desde perfil de riesgo de seguidor + descripción de cuenta fuente (maestra). Expuesto sobre API REST, herramienta MCP, página de Copia de trading. Solo asesor — nunca crear/mutar perfil; humano (o llamada MCP de seguimiento) aplica configuración.

## Modelo

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — construir solicitud desde
  aviso `AiPrompts.CopyProfileSystem`, devuelve `AiResult` cuyo texto = objeto JSON de sugerencias
  de configuración: `riskMode` (un nombre `MoneyManagementMode`), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`,
  `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, `rationale` corto.
- Como cada característica de IA, controlado en `App:Ai:ApiKey`: sin clave → llamada devuelve
  `AiResult.Fail(disabled)`, aplicación sin afectar.

## Superficies

| Superficie | Entrada |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (característica `Ai`, rol Usuario+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (característica `CopyTrading`, delega al servicio de IA) |
| UI | página Copia de trading → botón **Sugerir IA**; la recomendación renderiza en alerta en línea |

Recomendación no auto-aplicada a propósito: seguidor revisa, luego crea perfil /
destino a través de diálogo de Copia de trading normal (o cliente MCP analiza JSON + llamadas crean
puntos finales).

## Pruebas

- **Unidad** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: perfil de riesgo + descripción de fuente
  reenviado a cliente de IA bajo aviso de sistema de perfil de copia (NSubstitute).
- **Integración** — `IntegrationTests/AiRecommendDisabledTests.cs`: sin clave de API → real
  `AnthropicAiClient` + `AiFeatureService` degrade a resultado de fallo (aplicación se ejecuta sin clave).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: botón **Sugerir IA** llama punto final + renderiza
  resultado (mensaje "no configurado" elegante en ambiente de prueba), probando ruta UI → punto final → IA.

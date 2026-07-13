---
description: "Esta es una aplicación de trading/finanzas: la base de datos contiene cuentas de trading, perfiles de copia, desafíos de empresa prop, cadenas de auditoría, y el anillo de clave de Protección de Datos…"
---

# Copia de seguridad y recuperación de desastres

Esta es una aplicación de trading/finanzas: la base de datos contiene cuentas de trading, perfiles de copia, desafíos
de empresa prop, cadenas de auditoría, y el anillo de clave de Protección de Datos. Perderlo pierde dinero y rompe
obligaciones regulatorias/auditoría. Haz copia de seguridad, y **prueba que la restauración funciona**.

## Objetivos

| Métrica | Objetivo | Significado |
|--------|--------|---------|
| RPO (pérdida máxima de datos) | ≤ 5 min | Usa recuperación de punto en tiempo (WAL continuo), no solo volcados nocturnos. |
| RTO (tiempo máximo de inactividad) | ≤ 1 h | Tiempo para restaurar + apuntar la aplicación a la base de datos restaurada. |
| Retención de copia de seguridad | ≥ 35 días | Cubre corrupción descubierta tardíamente + ventanas de auditoría mensuales. |
| Simulacro de restauración | mensual | Una copia de seguridad no probada no es una copia de seguridad. |

## Lo que debe hacerse copia de seguridad

1. **La base de datos Postgres** — todos los datos de aplicación (base de datos lógica única `appdb`).
2. **El anillo de clave de Protección de Datos** — persistido **en** la base de datos
   (`PersistKeysToDbContext<DataContext>`) y PFX-encriptado vía `App:DataProtectionCertBase64`.
   Viaja junto en la copia de seguridad de BD, **pero el certificado de protección + su contraseña
   (`App:DataProtectionCertPassword`) son secretos almacenados fuera de la BD** — haz copia de seguridad en tu
   administrador de secretos. Sin el certificado no puedes descifrar secretos (contraseñas de cTID, tokens Open API,
   secretos de nodo, clave de IA) después de una restauración.

## Postgres administrado (recomendado)

Ambas rutas IaC de nube aprovisionan Postgres administrado con PITR incorporado — habilita + verifica retención:

- **Azure** (`deploy/azure/main.bicep`, Servidor Flexible): establece
  `backup.backupRetentionDays` (≥ 35) y `geoRedundantBackup` donde cumplimiento lo requiere. Restaura
  con *Restauración de punto en tiempo* a un nuevo servidor, luego actualiza la cadena de conexión `appdb` de la aplicación.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): establece `backup_retention_period` (≥ 35) y
  `backup_window`; mantén copias de seguridad automatizadas + copia opcional entre regiones. Restaura con
  *RestoreDBInstanceToPointInTime*, luego vuelve a apuntar la aplicación.

PITR administrado da el RPO ≤ 5 min sin cambios de aplicación — la aplicación solo necesita la nueva cadena de conexión
(y la estrategia de ejecución de reintento existente, véase [scaling.md](../deployment/scaling.md), tolera la
inclinación de cutover).

## Postgres autoalojado

- **Archivado continuo (PITR):** habilita archivado WAL (`archive_mode=on`, `archive_command` a
  almacenamiento de objetos) + un `pg_basebackup` periódico. Restaurar = restaurar copia de seguridad base + reproducir WAL al
  tiempo objetivo. Esto es lo que cumple el objetivo RPO.
- **Volcados lógicos (secundario):** nocturnos `pg_dump -Fc appdb` a almacenamiento fuera de caja para portabilidad /
  restauraciones parciales. No suficiente solo para el objetivo RPO.
- Encripta copias de seguridad en reposo; almacena fuera del host de la base de datos.

## Simulacro de restauración (ejecuta mensualmente)

1. Restaura la copia de seguridad más reciente (PITR a "ahora − 10 min") en una base de datos **scratch**, no producción.
2. Apunta una instancia de aplicación desechable (o una sesión psql) a ella.
3. Verifica esquema: `dotnet ef migrations list` muestra sin migraciones pendientes, aplicación inicia y se vuelve
   `/health`-lista.
4. **Verifica la cadena de auditoría** está intacta e ininterrumpida vía `IAuditTrailVerifier` (el `AuditChainInterceptor`
   resistente a falsificación cadena) — una cadena rota después de restauración significa corrupción o falsificación.
5. Confirma descifrado de secreto funciona (p. ej. una autorización Open API se descifra) — prueba la Protección de Datos
   certificado + contraseña se restauraron correctamente.
6. Registra el resultado del simulacro (tiempo tomado vs RTO) y destruye la base de datos scratch.

Automatiza pasos 1–4 en CI donde el ambiente lo permite (restaurar una copia de seguridad sembrada en un Testcontainer,
ejecuta `dotnet ef migrations list` + verificación de cadena de auditoría) para que regresión de copia de seguridad rota sea atrapada
antes de necesitarla.

## Después de una restauración real

1. Restaura BD (PITR justo antes del incidente).
2. Asegúrate que el certificado Protección de Datos + contraseña sean los **mismos** en uso antes del incidente.
3. Vuelve a apuntar la cadena de conexión `appdb` de la aplicación; rueda las réplicas.
4. El inicio ejecuta migraciones bajo el bloqueo asesor (véase scaling.md) — seguro con N réplicas.
5. Supervisores de copia/empresa prop reclaman sus arrendamientos y **resincronización desde el broker** (cTrader es la
   fuente de verdad), entonces posiciones abiertas reconvergen automáticamente — nada es confiado desde
   estado local antiguo.
6. Verifica cadena de auditoría + verifica datos de trading recientes.

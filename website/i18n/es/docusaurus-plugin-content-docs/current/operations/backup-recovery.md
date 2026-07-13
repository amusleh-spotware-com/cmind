---
description: "Esta es una app de trading/financiera: la base de datos contiene cuentas de trading, perfiles de copia, desafíos de empresas propietarias, cadenas de auditoría y el anillo de claves de Data Protection…"
---

# Backup y recuperación ante desastres

Esta es una app de trading/financiera: la base de datos contiene cuentas de trading, perfiles de copia, desafíos de empresas propietarias,
cadenas de auditoría y el anillo de claves de Data Protection. Perderla pierde dinero y rompe
obligaciones regulatorias/de auditoría. Respaldala, y **demuestra que la restauración funciona**.

## Objetivos

| Métrica | Objetivo | Significado |
|--------|---------|-------------|
| RPO (máx. pérdida de datos) | ≤ 5 min | Usa recuperación punto-en-tiempo (WAL continuo), no solo dumps nocturnos. |
| RTO (máx. tiempo de inactividad) | ≤ 1 h | Tiempo para restaurar + volver a apuntar la app a la base de datos restaurada. |
| Retención de backup | ≥ 35 días | Cubre una corrupción descubierta tardíamente + ventanas de auditoría mensuales. |
| Simacro de restauración | mensual | Un backup no probado no es un backup. |

## Qué debe ser respaldado

1. **La base de datos Postgres** — todos los datos de la app (base de datos lógica única `appdb`).
2. **El anillo de claves de Data Protection** — persistido **en** la base de datos
   (`PersistKeysToDbContext<DataContext>`) y encriptado vía PFX a través de `App:DataProtectionCertBase64`.
   Viaja junto en el backup de BD, **pero el certificado de protección + su contraseña
   (`App:DataProtectionCertPassword`) son secretos almacenados fuera de la BD** — respáldalos en tu
   administrador de secretos. Sin el cert no puedes desencriptar secretos (contraseñas cTID, tokens Open API,
   secretos de nodo, clave de IA) después de una restauración.

## Postgres gestionado (recomendado)

Ambos caminos de IaC en la nube aprovisionan Postgres gestionado con PITR incorporado — habilita + verifica la retención:

- **Azure** (`deploy/azure/main.bicep`, Flexible Server): establece
  `backup.backupRetentionDays` (≥ 35) y `geoRedundantBackup` donde el cumplimiento lo requiera. Restaura
  con *Point-in-time restore* a un nuevo servidor, luego actualiza la connection string de `appdb` de la app.
- **AWS** (`deploy/aws`, RDS Postgres, Terraform): establece `backup_retention_period` (≥ 35) y
  `backup_window`; mantén automated backups + copia opcional cross-region. Restaura con
  *RestoreDBInstanceToPointInTime*, luego vuelve a apuntar la app.

El PITR gestionado da el RPO de ≤ 5 min sin cambios en la app — la app solo necesita la nueva connection string
(y la estrategia de ejecución con reintento existente, ver [scaling.md](../deployment/scaling.md), tolera el
corte).

## Postgres autoalojado

- **Archivo continuo (PITR):** habilita archivado WAL (`archive_mode=on`, `archive_command` a
  almacenamiento de objetos) + un `pg_basebackup` periódico. Restauración = restaura backup base + replay WAL al
  tiempo objetivo. Esto es lo que cumple el objetivo de RPO.
- **Dumps lógicos (secundario):** `pg_dump -Fc appdb` nocturno a almacenamiento fuera de caja para portabilidad /
  restauraciones parciales. No suficiente solo para el objetivo de RPO.
- Encripta los backups en reposo; almacena fuera del host de la base de datos.

## Simacro de restauración (ejecutar mensualmente)

1. Restaura el último backup (PITR a "ahora − 10 min") en una base de datos **temporal**, no producción.
2. Apunta una instancia de app desechable (o una sesión psql) a ella.
3. Verifica el schema: `dotnet ef migrations list` no muestra migraciones pendientes, la app inicia y se
   convierte en `/health`-ready.
4. **Verifica que la cadena de auditoría** esté intacta y sin romper a través de `IAuditTrailVerifier` (la cadena
   de `AuditChainInterceptor` a prueba de manipulaciones) — una cadena rota después de la restauración significa corrupción o manipulación.
5. Confirma que la desencriptación de secretos funciona (p. ej. una autorización Open API se desencripta) — prueba que el
   cert de Data Protection + contraseña fueron restaurados correctamente.
6. Registra el resultado del simacro (tiempo vs RTO) y destruye la base de datos temporal.

Automatiza los pasos 1–4 en CI donde el entorno lo permita (restaura un backup sembrado en un Testcontainer,
ejecuta `dotnet ef migrations list` + la verificación de cadena de auditoría) para que una regresión de backup roto se detecte
antes de que la necesites.

## Después de una restauración real

1. Restaura la BD (PITR a justo antes del incidente).
2. Asegúrate de que el cert de Data Protection + contraseña sean los **mismos** usados antes del incidente.
3. Vuelve a apuntar la connection string de `appdb` de la app; haz rolling de las réplicas.
4. El startup ejecuta las migraciones bajo el advisory lock (ver scaling.md) — seguro con N réplicas.
5. Los supervisores de copy/prop-firm reclaman sus leases y **resincronizan desde el broker** (cTrader es la
   fuente de la verdad), así que las posiciones abiertas convergen automáticamente — nada se confía del estado local
   stale.
6. Verifica la cadena de auditoría + spot-check de datos de trading recientes.

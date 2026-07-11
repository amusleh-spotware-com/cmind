# src/Infrastructure — EF Core, encryption, external clients

Implements Core interfaces (repositories, dispatchers). Anti-corruption for external systems
(cTrader Console CLI, GHCR, Anthropic, node HTTP) lives here — translate at the edge, never let their
shapes leak into Core. The domain does not know EF exists.

## EF gotchas (each one bit us in prod, missed by in-memory unit tests)

- **Soft delete:** entities inherit `AuditedEntity`/`ISoftDeletable`; `DataContext` has a global
  query filter + converts `Deleted` → `Modified`+`IsDeleted` in `SaveChanges`. Don't hard-delete.
- **TPH derived-property config:** never add `e.Property<T>(nameof(Subclass.Prop)).IsRequired(false)`
  from a **base** type's `EntityTypeBuilder` for a property on a **derived** TPH type — it silently
  produces a property EF never persists. TPH makes subclass-only props column-nullable automatically.
- **TPH `OfType<Intermediate>()` over the soft-delete filter does not translate on Npgsql** — throws
  at runtime (500). Query without narrowing (by unique key, then `is RemoteNode`) or enumerate concrete
  leaf subtypes + `ToListAsync()` + `.Cast<>()`. (see `NodeEndpoints.RegisterNodeAsync`)
- **Nested `(i as T) != null ? (i as T)!.Prop : …` in an `IQueryable.Select()` don't translate** —
  silent wrong/null vs real Postgres. Materialize with `ToListAsync()` first, switch in C#.
- **`Instance.IsActive`/`IsTerminal`, `Node` computed props are C#-only**, not mapped columns —
  filtering on them in `IQueryable` throws at translation. Materialize first.
- **Don't project an entity with a one-to-one nav cycle** (`Node.LatestStats`/`NodeStats.Node`) into
  an API response — System.Text.Json has no cycle detection → serializes to `MaxDepth` → 500. Project
  scalar fields only.
- **`AddDbContextPool`** ctor gotcha applies — see memory if pooling a context.

## Other

- Never log/store secrets plaintext — `ISecretProtector` with `EncryptionPurposes`. Data Protection
  key ring PFX-encrypted via base64 env var.
- New persistence concern (EF config, converters, TPH mapping) stays here, not in Core. Read models /
  list projections for the UI query EF directly (CQRS-lite) — don't force them through aggregate repos.
- Every persistence/endpoint change ships an **integration test** against real Postgres (Testcontainers).
- Modern C# 14 per root `CLAUDE.md`.

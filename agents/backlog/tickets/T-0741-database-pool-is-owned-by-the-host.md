---
id: T-0741
title: The database pool is owned by the host (A4)
status: done
size: S
owner: —
created: 2026-09-14
updated: 2026-09-14
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

Owner ruling 2026-09-14 (A4): *"Fix."* `DbContextBindingExtensions.AddDbContextBindings` registered
the `NpgsqlDataSource` as a pre-built instance (`services.AddSingleton(dataSource)`), which the
container never disposes, so the pool outlived every host; `HostTestApplicationFactory` carried a
pruning workaround (`ConnectionIdleLifetime = 10; ConnectionPruningInterval = 5`) to drain the leak.

## Doing

- Register the data source through the container (`services.AddSingleton(_ => dataSource)`) so the
  host disposes it on shutdown; keep the eager type-catalog reload and the load-bearing hosted-service
  order.
- Remove the HostTests pruning workaround if the suite stays green without it.
- A test that disposing the service provider disposes the data source.

## NOT

No change to migrations, Aspire wiring or connection strings.

## Status log

- 2026-09-14 — shipped in `777a9555` (factory registration; the DbContext resolves the container's one
  data source; `BootDatabaseIoTests` pins both facts; the HostTests workaround dropped — 218 hosts now
  close their own pools) and `3f2a0052` (the two pool-ownership comments say the real mechanism —
  the container disposes what a factory returned and never an instance handed to it; the source is
  built above the factory so the eager probe runs at composition; `BaseIntegrationTest`'s
  `Pooling=false` rationale now names the never-disposed provider `BaseTransactionalPostgresSqlTest`
  builds). Recorded in ADR-0062 §Consequences as amended.

# Engineering context

## About this repository

This repository builds the NServiceBus.TransactionalSession package, which enables transactional message sessions outside message handlers.

- `src/NServiceBus.TransactionalSession/` — package implementation
- `src/NServiceBus.TransactionalSession.Tests/` — unit tests
- `src/NServiceBus.TransactionalSession.AcceptanceTests/` — acceptance tests
- `src/NServiceBus.TransactionalSession.CustomTestingPersistence/` — test persistence implementation
- `src/msbuild/` — shared build configuration
- `.github/workflows/` — CI, release, security, and dependency automation

## Start here

- [Transactional session documentation](https://docs.particular.net/nservicebus/transactional-session/) — public usage and API documentation
- [README.md](../README.md) — package overview and documentation link
- [Contributing](https://docs.particular.net/platform/contributing) — contribution process

## Architecture and design

- [Architecture and design decisions](decisions/) — where to record new decisions

### Decisions recorded in pull requests

- Dispatch the Outbox control message outside the transaction so it can be sent immediately — [#403](https://github.com/Particular/NServiceBus.TransactionalSession/pull/403)

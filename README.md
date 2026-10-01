# swiftbets-gateway

[![ci](https://github.com/remonenaidoo/swiftbets-gateway/actions/workflows/ci.yml/badge.svg)](https://github.com/remonenaidoo/swiftbets-gateway/actions/workflows/ci.yml)

The single public origin of SwiftBets: a YARP backend-for-frontend that turns an opaque browser session cookie (httpOnly, Secure, SameSite=Strict, plus a required CSRF header on mutations) into a bearer token downstream. Tokens stay in a Redis session the browser never sees; sessions rotate on refresh, are listed and revoked per device at `/api/session/devices`, and refresh is single-flight so a single-use refresh token is never spent twice. It passes mobile bearer tokens through, strips inbound identity headers, and enforces Redis-backed per-route rate limits.

## Hosts

- `SwiftBets.Gateway.Api`: the reverse proxy and BFF.

## Data and events

- **Owns:** Redis (rate-limit windows, browser sessions and the per-user session index).
- **Events:** None; HTTP only.

## Layout

Clean Architecture, enforced by project references and `*.ArchitectureTests`:

```
src/*.Domain          pure domain, no references
src/*.Application     use cases and ports; depends on Domain and contracts only
src/*.Infrastructure  adapters (Dapper + embedded .sql, Kafka, Redis); implements Application ports
src/*.Api | *.Worker  composition root: observability, error envelope, health, metrics
src/*.Migrator        DbUp scripts under Migrations/, run once before the host starts
```

Every host exposes `/health/live`, `/health/ready` (checks its real dependencies), `/metrics` (Prometheus), logs compact JSON with correlation ids, and exports traces over OTLP.

## Build and test

```bash
../swiftbets-platform/scripts/fetch-shared-packages.sh .   # or pack-local.sh for unreleased shared changes
dotnet test SwiftBets.Gateway.slnx
```

Integration tests use Testcontainers and need Docker. The whole platform runs from `swiftbets-platform` with `make up`.

## Images

Multi-arch (amd64 + arm64), non-root, chiseled runtime:

- `ghcr.io/remonenaidoo/swiftbets-gateway`

## License

MIT

# Backend Learning Path — .NET + Redis + MSSQL

**Goal:** go from "frontend dev who knows nothing about backend" to "can contribute features to `api-member`" via eight end-to-end POCs, each pushable to GitHub, each introducing new concepts on top of the previous.

**Honest timeline:** ~8–10 hrs/week of evenings/weekends.

- Phase 1 alone: ~6 weeks
- All three phases: ~4–6 months
- No shortcut — most of the growth comes from hours-under-fingertips, not reading.

**Important rule:** do not plan POCs 2–8 in detail now. YAGNI — priorities will shift after POC 1 ships. This document is a reference sheet, not a commitment.

---

## Phase 1 — Foundation (can't skip)

| #   | POC                             | New concepts it introduces                                                                                                                            |
| --- | ------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **URL Shortener** (in progress) | ASP.NET Core shape, DI lifetimes, layered architecture, cache-aside, Redis `INCR`, `IHostedService`, Docker Compose, Swagger, FluentValidation basics |
| 2   | **Todo API with Auth**          | JWT, password hashing (BCrypt), `[Authorize]` attribute, refresh tokens, user-scoped queries, cookies vs bearer tokens                                |
| 3   | **Notes API with Tests**        | xUnit, Moq, `WebApplicationFactory` integration tests, Testcontainers for real Redis/MSSQL inside tests                                               |

✅ **Exit criterion:** you can read `api-member` and open a simple PR.

## Phase 2 — Real-world patterns

| #   | POC                                              | New concepts                                                                                                                             |
| --- | ------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------- |
| 4   | **Expense API w/ Validation & Error Handling**   | FluentValidation in depth, `ProblemDetails` (RFC 7807), global exception middleware, correlation IDs, structured error responses         |
| 5   | **"Order + Inventory" two-service system**       | `IHttpClientFactory`, typed clients, Polly retries / circuit breaker / timeouts — directly mirrors api-member's service-to-service calls |
| 6   | **Event-driven "signup → welcome email" worker** | Redis Pub/Sub or RabbitMQ, producer/consumer pattern, idempotency, retry semantics, dead-letter queues                                   |

✅ **Exit criterion:** you can own a full feature end-to-end in `api-member`.

## Phase 3 — Operations & scale

| #   | POC                          | New concepts                                                                                                     |
| --- | ---------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| 7   | **Observable API**           | Serilog structured logging, OpenTelemetry traces/metrics, health checks, `/metrics` endpoint, reading dashboards |
| 8   | **Migrations & Performance** | SqlSugar/EF migrations, indexes, explain plans, N+1 detection, k6 load testing, basic profiling                  |

✅ **Exit criterion:** genuinely proficient; can lead a feature and reason about production behavior.

---

## Deliberately NOT on this list

These are real topics but not in `api-member` and not on the junior → mid backend ladder. Defer until actually needed:

- Kubernetes
- gRPC
- Microservices architecture (beyond the 2-service integration in POC 5)
- GraphQL
- Event sourcing / CQRS
- Service meshes
- Serverless

## Revisiting this path

After **each** POC, revisit this list and ask:


After **each** POC, revisit this list and ask:

1. Did I struggle with anything in this POC? Does a later POC need to move earlier to fill that gap?
2. What am I actually curious about now?
3. Has my job/goal changed?

Let the path serve you, not the other way around.

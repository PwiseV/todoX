# todoX Constitution

## Core Principles

### I. Scope Isolation

All work under this constitution MUST be confined to the `backend-dotnet/`
directory. The existing `frontend/` and `backend/` (Node.js) directories are
frozen references and MUST NOT be modified, renamed, deleted, or reorganized
as part of this rewrite. New shared assets (docs, scripts) MAY live outside
`backend-dotnet/` only when they add read-only guidance (e.g., updates to
`docs/`) and MUST NOT alter the runtime behavior of the frozen directories.

**Rationale**: The rewrite is a parallel exercise — the current Node backend
must remain a working reference for behavior comparison, and the frontend is
the acceptance harness for contract fidelity. Any drift in the frozen dirs
compromises the ability to A/B test the new implementation.

### II. Prescribed Technology Stack

The new backend MUST be implemented as an ASP.NET Core Web API on **.NET 10**
using the **controllers** (not Minimal API) programming model. Persistence
MUST use **Entity Framework Core** targeting **PostgreSQL**. The database
MUST be provisioned locally via **Docker Compose** so a fresh clone can boot
the stack with a single command. No alternate frameworks, ORMs, database
engines, or hosting models are permitted without a constitutional amendment.

**Rationale**: This is a scoped practice project; freezing the stack up front
prevents scope creep and keeps review effort focused on API parity rather
than technology debates.

### III. Frontend Compatibility (NON-NEGOTIABLE)

Routes, HTTP methods, query parameters, JSON field names (including `_id`
and `__v`), status values, and success response shapes MUST match
`docs/api-contract.md`. The existing `frontend/` MUST work against the new
backend **without any frontend code change**. Known defects listed in
`docs/api-contract.md` §5 MAY be fixed only if the frontend does not depend
on them; every such deviation MUST first be recorded in a new §6
"Intentional Deviations" of `docs/api-contract.md`.

**Rationale**: The frontend is the acceptance test. A rewrite that requires
FE changes defeats the point of the exercise and hides behavioral drift.

### IV. Test-First for Business Rules (NON-NEGOTIABLE)

Every business rule enumerated in `docs/api-contract.md` §4 — date filtering
(`today`/`week` starting Monday/`month`), status filtering (including the
`completed` → `complete` mapping), pagination (defaults, clamping of `page`
and `limit`), sort order (`sortOrder` then `createdAt` desc), and the
statistics contract (`totalCount` respects both filters; `activeCount` /
`completeCount` respect only `dateQuery`) — MUST be covered by **xUnit**
tests in the `backend-dotnet/` solution. Tests MUST be written **before or
alongside** the implementation of each rule and MUST fail against an empty
implementation. Rules without a corresponding failing-first test MUST NOT be
merged. Contract regressions detected by these tests block release.

**Rationale**: Business rules are the load-bearing part of this contract;
implementation details (LINQ, EF translation, timezone handling) will change
during learning, but the assertions codify the invariants that keep the
frontend working.

### V. Supply Chain & Secret Hygiene

Secrets (database passwords, connection strings with credentials, API keys)
MUST NEVER be committed to the repository. Local development configuration
MUST use `appsettings.Development.json` (git-ignored where it contains
secrets) or **.NET User Secrets**; production-style configuration MUST come
from environment variables or Docker Compose `.env` files that are also
git-ignored. NuGet dependencies are restricted to the set explicitly approved
in the implementation plan (`/speckit-plan` output). Adding, upgrading across
major versions, or replacing a package requires an amendment to the plan
before the change may be merged; ad-hoc dependency additions are prohibited.

**Rationale**: Secret leakage is irreversible once pushed. Dependency sprawl
is the fastest way to lose the ability to reason about a codebase — locking
the manifest to a reviewed list keeps the practice project auditable.

## Technology Stack Requirements

- **Runtime**: .NET 10 SDK. Target framework `net10.0`.
- **Web framework**: ASP.NET Core Web API, controller-based
  (`[ApiController]`), attribute routing.
- **ORM**: EF Core with the `Npgsql.EntityFrameworkCore.PostgreSQL` provider.
  Migrations MUST be checked into the repo and applied via `dotnet ef
  database update` (or an equivalent programmatic call at boot in dev).
- **Database**: PostgreSQL, provisioned only through the project's
  `docker-compose.yml`. No reliance on a host-installed Postgres.
- **Testing**: xUnit as the sole test framework for the .NET solution. In-
  memory or Testcontainers-based Postgres is allowed for integration tests
  provided it is on the approved dependency list.
- **API prefix**: All endpoints MUST live under `/api` and match the routes
  in `docs/api-contract.md`. The health check MUST remain at `/api/health`.
- **Timezone**: Server MUST resolve `today`/`week`/`month` boundaries using
  the same effective timezone as the Node backend (`Asia/Ho_Chi_Minh`
  unless overridden). Behavior MUST be deterministic and testable — clock
  and timezone dependencies MUST be injected, not read from
  `DateTime.Now` in business logic.
- **Frontend contract surface**: JSON must serialize with the exact property
  names in `docs/api-contract.md` §2 (including `_id`, `__v`, camelCase
  timestamps). If EF-generated identifiers differ, an adapter/DTO layer MUST
  bridge them; the DB schema is free to differ from the wire format as long
  as the wire format matches.

## Development Workflow & Quality Gates

- **Branching**: Rewrite work happens on branches derived from
  `dotnet-backend` (or its successor). PRs targeting frozen directories
  (`frontend/`, `backend/`) MUST be rejected in review.
- **Local bootstrap**: A fresh clone MUST be able to run `docker compose up`
  followed by the standard `dotnet run` to reach a working `/api/health`
  response. Any deviation from this two-command bootstrap is a blocker.
- **Test gate**: `dotnet test` MUST pass locally and in CI before merge.
  Coverage of the business rules listed in Principle IV is enforced by the
  presence of named tests, not by a coverage percentage; missing rule tests
  are a merge blocker.
- **Contract verification**: Before merging any endpoint change, the author
  MUST run the existing frontend against the new backend and confirm the
  affected flows still work. This manual check complements — does not
  replace — the xUnit suite.
- **Secret scanning**: Reviewers MUST reject any diff that introduces
  literal credentials in tracked files (including `appsettings.json`,
  Compose files, or test fixtures). Use placeholders + local overrides.
- **Dependency review**: Every NuGet package MUST be listed in `plan.md`.

## Governance

This constitution supersedes ad-hoc preferences and prior conventions within
`backend-dotnet/`. Amendments follow this procedure:

Amendments are committed separately with a version bump; direct commits to
feature branches are allowed. Bump the version per semver:

- **MAJOR**: a principle is removed or its meaning is inverted.
- **MINOR**: a new principle or section is added, or an existing one is
  materially expanded.
- **PATCH**: wording, typo, or clarification with no behavioral change.

Update `LAST_AMENDED_DATE` to the commit date (ISO `YYYY-MM-DD`).
`RATIFICATION_DATE` is immutable. Downstream artifacts affected by the
amendment (plan, tasks, docs) MUST be updated in the same commit or an
immediately-following commit.
Runtime development guidance lives in `docs/api-contract.md` (contract
truth) and the plan/tasks artifacts produced by Spec Kit commands.

**Version**: 1.0.0 | **Ratified**: 2026-09-26 | **Last Amended**: 2026-09-26

# Enterprise Management System — Development Plan

> Status: **PLANNING ONLY — no application code written yet.**
> Last updated: 2026-08-20

---

## 1. Environment Audit (verified on this machine)

| Requirement | Status | Detail |
|---|---|---|
| OS | OK | Ubuntu 24.04.4 LTS (noble), x86_64 |
| Git | OK | 2.43.0 |
| Node.js | OK (with caveat) | v18.19.1 — see *Vite version constraint* below |
| npm | OK | 9.2.0 |
| Network → nuget.org | OK | reachable |
| Network → registry.npmjs.org | OK | reachable |
| Network → dot.net | OK | reachable |
| Disk free | OK | 43 GB free on `/` |
| **.NET 8 SDK** | **MISSING** | apt candidate `8.0.130-0ubuntu1~24.04.1` exists but apt needs sudo |
| **Docker / Docker Compose** | **MISSING** | not installed; installing requires sudo |
| **PostgreSQL** | **MISSING** | no server installed, nothing listening on 5432 |
| sudo | **PASSWORD REQUIRED** | non-interactive sudo unavailable to the agent |
| `curl` | missing | `wget` is present and used instead |

### Commands used for the audit

```bash
dotnet --version; dotnet --list-sdks
node --version; npm --version
docker --version; docker compose version
psql --version
git --version
cat /etc/os-release
apt-cache policy dotnet-sdk-8.0
sudo -n true
ss -ltnp | grep -E '5432|5000|5173'
git rev-parse --show-toplevel
wget -q --spider https://api.nuget.org/v3/index.json
npm ping
```

---

## 2. Blockers and Decisions Required

### B1 — .NET 8 SDK is not installed

Two viable paths:

**Option A (recommended): user-local install, no sudo.**
```bash
wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel 8.0        # installs to ~/.dotnet
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
```
- Pros: no root, no interference with system packages, easy to remove, easy to pin the exact SDK version.
- Cons: `PATH` must be exported per shell (fixed by appending to `~/.bashrc`).

**Option B: system install.**
```bash
sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0
```
- Pros: on `PATH` for every shell and every user.
- Cons: requires you to type the sudo password; historically Ubuntu's dotnet packaging has conflicted with Microsoft's own feed if both are configured.

**Recommendation: Option A.** It is reproducible and needs nothing from you but approval.

### B2 — Docker is not installed, and PostgreSQL is not installed

The project needs a real PostgreSQL server. Without Docker, options are:

| Option | Command | Needs sudo | Trade-off |
|---|---|---|---|
| **C1: Install Docker Engine** | `sudo apt-get install docker.io docker-compose-v2` + `sudo usermod -aG docker $USER` (requires re-login) | Yes | Unblocks *everything*: Postgres, the compose phase, and Testcontainers-based integration tests. Best long-term. |
| **C2: Install PostgreSQL natively** | `sudo apt-get install postgresql-16` | Yes | Unblocks development immediately, but Dockerfiles/compose can be **written but never verified**, and Testcontainers integration tests cannot run. |
| **C3: Use a free hosted Postgres** (Neon / Supabase / ElephantSQL) | none | No | Zero local install; needs a connection string in an env var. Docker phase still unverifiable. |

**Recommendation: C1 (Docker).** It is the only option that lets every phase of this plan actually be *verified* rather than merely written. Rule 6 of your brief — "do not claim a feature is implemented unless it actually works" — means that if we skip Docker, the README and I will both explicitly state that the Docker setup is **untested on this machine**.

Whichever you pick, the sudo commands must be run **by you** in your own terminal; I cannot supply a password.

### B3 — Git repository layout

`/home/lpt-24-048/kb` is itself a Git repo (branch `master`) containing many unrelated projects, with a large uncommitted diff in `AI-Affiliate-Marketing-Automation-Platform`. This project is a subdirectory of it.

Options:
1. **Dedicated repo** at `Enterprise_Management_System/.git`, and add the folder to `kb/.gitignore`. Clean history, matches the commit list in your brief, portfolio-ready to push to GitHub. **Recommended.**
2. Commit into the existing `kb` repo — history mixes with unrelated projects; bad for a portfolio link.

### B4 — Node 18 constrains the Vite version

Vite 7 requires Node 20+. Node here is 18.19.1.

- **Recommended: Vite 5.x + React 18 + TypeScript 5.x** — fully supported on Node 18.19, stable, and what most teams still run.
- Alternative: install Node 20 via `nvm` (no sudo) and use Vite 7. Adds a moving part for little gain right now, but is an easy upgrade later.

---

## 2b. Decisions Confirmed (2026-08-20)

| Ref | Decision | Consequence |
|---|---|---|
| B1 | **.NET 8 SDK: user-local install** via `dotnet-install.sh` into `~/.dotnet` | No sudo. `PATH` and `DOTNET_ROOT` appended to `~/.bashrc`. Agent performs this. |
| B2 | **Docker Engine will be installed** | Requires the user to run the sudo commands below. Unblocks Postgres, Phase 13 integration tests, and Phase 16 compose — every phase becomes verifiable. |
| B3 | **Dedicated Git repo** at `Enterprise_Management_System/.git`; folder added to `kb/.gitignore` | Clean, portfolio-ready history. |
| B4 | **Vite 5 + React 18 + TypeScript 5** | Supported on the installed Node 18.19.1. No nvm needed. |

### User-run prerequisite (sudo — the agent cannot do this)

```bash
sudo apt-get update
sudo apt-get install -y docker.io docker-compose-v2
sudo usermod -aG docker $USER
# then LOG OUT AND BACK IN (or run: newgrp docker) so group membership takes effect
docker run --rm hello-world      # verification
docker compose version           # verification
```

Until `docker run --rm hello-world` succeeds, Phase 0 cannot complete.

---

## 3. Architecture

### 3.1 Layering (Clean Architecture, 4 projects)

```
        ┌─────────────────────────────┐
        │  EnterpriseManagement.Api   │  controllers, middleware, DI wiring, Swagger
        └──────────────┬──────────────┘
                       │ depends on
        ┌──────────────▼──────────────┐   ┌───────────────────────────────┐
        │ EnterpriseManagement        │   │ EnterpriseManagement          │
        │        .Application         │◄──┤        .Infrastructure        │
        │  DTOs, service interfaces,  │   │  EF Core DbContext, entity    │
        │  business logic, validators │   │  configs, JWT, hashing, repos │
        └──────────────┬──────────────┘   └───────────────┬───────────────┘
                       │                                  │
        ┌──────────────▼──────────────────────────────────▼───────────────┐
        │              EnterpriseManagement.Domain                        │
        │      entities, enums, domain exceptions — zero dependencies     │
        └─────────────────────────────────────────────────────────────────┘
```

Compile-time reference graph (no cycles):

- `Domain` → *(nothing)*
- `Application` → `Domain`
- `Infrastructure` → `Application`, `Domain`
- `Api` → `Application`, `Domain`, and `Infrastructure` **only in `Program.cs`** for DI registration

**Why the arrow from Infrastructure points *up* into Application:** Application declares interfaces (`IEmployeeService`, `IJwtTokenGenerator`, `IApplicationDbContext`), Infrastructure implements them. This is the *Dependency Inversion Principle* — the policy layer owns the contract, the detail layer conforms. The practical payoff: Application can be unit-tested with no database, and swapping PostgreSQL for SQL Server touches exactly one project.

**Why Api still references Infrastructure:** something must call `services.AddScoped<IEmployeeService, EmployeeService>()`. Purists hide this behind a `DependencyInjection.cs` extension method per layer, which is what we will do — the controllers themselves never see an Infrastructure type.

### 3.2 Layer responsibilities

| Layer | Contains | Must NOT contain |
|---|---|---|
| Domain | Entities, enums, domain exceptions, domain constants | EF Core attributes, `DbContext`, HTTP types, DTOs |
| Application | DTOs, service interfaces, service implementations, FluentValidation validators, paging primitives | SQL, `DbContext` concrete type, `HttpContext` |
| Infrastructure | `AppDbContext`, `IEntityTypeConfiguration<T>`, migrations, JWT generator, BCrypt hasher, audit writer | Controllers, HTTP status decisions |
| Api | Controllers, middleware, `Program.cs`, Swagger config, auth policy registration | Business rules, LINQ queries against the DB |

### 3.3 Where business logic lives — and why no repository-per-entity

We use **services over `DbContext` directly**, not a generic `IRepository<T>`.

- **Why:** `DbContext` *is already* the Unit of Work and `DbSet<T>` *is already* a repository. Wrapping `IQueryable` in a repository that returns `List<T>` destroys composability — you lose server-side paging, filtering, and projection, which are explicit requirements of this project.
- **Alternative considered:** generic repository + specification pattern. It buys database-agnostic unit tests, but adds a layer most teams later regret.
- **How we keep Application testable anyway:** Application depends on a narrow `IApplicationDbContext` interface exposing the `DbSet`s and `SaveChangesAsync`. Infrastructure's `AppDbContext` implements it.
- **Where a repository *is* justified:** none identified yet. If one appears (e.g. a raw-SQL dashboard aggregate), it gets its own focused interface, and the reason will be documented at that point.

### 3.4 Data model (draft — refined in Phase 2)

```
Role ──< UserRole >── User ──1:0..1── Employee ──>── Department
                        │                 │
                        │                 ├──< ProjectEmployee >── Project ──>── Employee (Manager)
                        │                 │                            │
                        │                 └──< TaskItem (Assignee) ────┘
                        │
                        └──< AuditLog
```

Design decisions to confirm in Phase 2:

1. **User and Employee are separate tables, linked 1:0..1.**
   *Why:* `User` is an identity/authentication concern (email, password hash, refresh tokens, lockout). `Employee` is an HR record (employee code, job title, hire date, department, salary band). Not every user is an employee (a service or super-admin account) and not every employee necessarily has a login. Merging them creates a table with two lifecycles and forces auth columns into HR queries.
   *Alternative:* one `User` table with HR columns — fewer joins, simpler CRUD. Defensible for a small app; the split is the standard enterprise choice and gives a better interview answer.

2. **Roles are a table with a `UserRole` join, not an enum column.**
   *Why:* a user can legitimately hold more than one role (a manager who is also an admin), and adding a role becomes a data change rather than a code deploy + migration. The three seeded roles are `ADMIN`, `MANAGER`, `EMPLOYEE`.
   *Alternative:* `Role` enum column — simpler and one fewer join, but single-role-only.
   *Note on over-engineering:* we deliberately stop here. No permissions table, no claims-per-permission matrix — that is real ASP.NET Core Identity territory and is beyond scope.

3. **`ProjectEmployee` is an explicit join entity, not a skip-navigation.** It carries payload: `RoleOnProject`, `AssignedAt`, `UnassignedAt`. Any join table with payload must be a first-class entity.

4. **Project manager is a `ManagerEmployeeId` FK on `Project`.** This is what MANAGER authorization checks resolve against.

5. **Statuses are enums stored as `int`** (`ProjectStatus`, `TaskStatus`, `TaskPriority`) — with the `string` alternative and its trade-off (readable in SQL vs. wider index and no rename safety) discussed at implementation time.

6. **Audit columns:** `CreatedAt`/`UpdatedAt` (UTC, `timestamptz`) on every mutable entity, populated centrally by overriding `SaveChangesAsync`, not by hand in each service.

### 3.5 Planned indexes (each must earn its place)

| Table | Index | Justification |
|---|---|---|
| User | UNIQUE(`Email`) | Login looks up by email on every authentication; also enforces the business rule |
| Employee | UNIQUE(`EmployeeCode`) | Natural business key; searched directly |
| Employee | (`DepartmentId`) | `GET /api/employees?departmentId=` is a primary filter; also supports the FK |
| Employee | (`LastName`, `FirstName`) | Default sort order for the employee list |
| Project | (`Status`) | `?status=ACTIVE` filter + dashboard "active projects" count |
| Project | (`ManagerEmployeeId`) | MANAGER role scoping — "projects I manage" runs on nearly every manager request |
| TaskItem | (`ProjectId`, `Status`) | Composite: tasks are almost always listed per project and filtered by status; the leading column also serves project-only queries |
| TaskItem | (`AssignedEmployeeId`, `Status`) | Composite: the EMPLOYEE role's "my tasks" view, usually filtered by status |
| TaskItem | (`DueDate`) | Overdue-task dashboard aggregate |
| ProjectEmployee | UNIQUE(`ProjectId`, `EmployeeId`) | Prevents duplicate assignment; serves membership lookups |
| AuditLog | (`CreatedAt` DESC) | The audit log is read newest-first and paginated |
| AuditLog | (`UserId`, `CreatedAt` DESC) | Filtering an audit trail by actor |

Deliberately **not** indexed: `Department.Name` (tiny table, sequential scan is faster), `TaskItem.Title` (covered by search strategy below, not a btree), every remaining FK that is never filtered on independently.

**Search strategy:** `?search=john` across employee name/email/code. Phase 8 starts with `ILIKE '%term%'` (correct, but cannot use a btree index), then demonstrates the fix — a PostgreSQL **trigram GIN index** (`pg_trgm`) — and shows the `EXPLAIN ANALYZE` before/after. This is the honest version of "database optimization": show the naive query, measure it, improve it, measure again. No invented percentages.

### 3.6 API response design

- **Single resources return the resource directly**, not wrapped. `GET /api/employees/5` → the employee JSON, HTTP 200. Wrapping every success in `{success, data}` duplicates what the status code already says and annoys every client.
- **Collections return a paged envelope**, because the metadata has nowhere else to live:
  ```json
  { "data": [], "page": 1, "pageSize": 20, "totalCount": 150, "totalPages": 8, "hasNextPage": true }
  ```
- **Errors return one consistent shape** from the exception middleware, based on RFC 7807 fields plus a trace id:
  ```json
  { "success": false, "message": "Resource not found", "statusCode": 404, "traceId": "0HN7...", "errors": null }
  ```
  Validation failures (400) populate `errors` as `field → string[]`.
- `201 Created` carries a `Location` header; `204 No Content` for deletes and status updates that return nothing.

### 3.7 Middleware order

```
Request
  → CorrelationIdMiddleware       (assign/propagate X-Correlation-Id; push to log scope)
  → Serilog RequestLoggingMiddleware (one structured line per request)
  → ExceptionHandlingMiddleware   (outermost try/catch around everything below)
  → HTTPS redirection
  → CORS
  → Rate limiter                  (on the auth endpoints)
  → Authentication                (parse + validate JWT → ClaimsPrincipal)
  → Authorization                 (evaluate [Authorize] policies)
  → Endpoint (controller)
Response
```

Order matters and will be explained when built: correlation id must be first so every later log line carries it; exception handling must sit above the endpoint but below logging so failures are still logged with their correlation id; authentication must precede authorization because you cannot authorize a principal you have not yet built.

Never logged: passwords, password hashes, `Authorization` headers, JWTs, connection strings.

---

## 4. Proposed folder structure

```
Enterprise_Management_System/
├── DEVELOPMENT_PLAN.md
├── README.md
├── INTERVIEW_PREPARATION.md
├── .gitignore
├── .editorconfig
├── docker-compose.yml
├── .env.example                       # no real secrets, ever
├── EnterpriseManagement.sln
├── src/
│   ├── EnterpriseManagement.Domain/
│   │   ├── Common/                    # BaseEntity, IAuditableEntity
│   │   ├── Entities/                  # User, Role, UserRole, Department, Employee,
│   │   │                              #   Project, ProjectEmployee, TaskItem, AuditLog
│   │   ├── Enums/
│   │   └── Exceptions/                # NotFoundException, ConflictException, ...
│   ├── EnterpriseManagement.Application/
│   │   ├── Common/                    # PagedResult<T>, QueryParameters, ApiError
│   │   ├── Interfaces/                # IApplicationDbContext, I*Service, IJwtTokenGenerator,
│   │   │                              #   IPasswordHasher, ICurrentUser, IAuditService
│   │   ├── Dtos/                      # per feature folder
│   │   ├── Services/
│   │   ├── Validators/                # FluentValidation
│   │   └── DependencyInjection.cs
│   ├── EnterpriseManagement.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Configurations/        # one IEntityTypeConfiguration per entity
│   │   │   ├── Migrations/
│   │   │   └── Seed/
│   │   ├── Identity/                  # JwtTokenGenerator, BCryptPasswordHasher
│   │   ├── Auditing/
│   │   └── DependencyInjection.cs
│   └── EnterpriseManagement.Api/
│       ├── Controllers/
│       ├── Middleware/
│       ├── Authorization/             # policies + resource handlers
│       ├── Extensions/
│       ├── appsettings.json           # structure only, no secrets
│       ├── Program.cs
│       └── Dockerfile
├── tests/
│   └── EnterpriseManagement.Tests/
│       ├── Unit/
│       └── Integration/
└── client/                            # React + TypeScript + Vite
    ├── src/
    │   ├── api/                       # axios instance, interceptors, per-resource services
    │   ├── auth/                       # AuthContext, ProtectedRoute, RoleGate
    │   ├── components/                # Button, Input, Modal, Table, Pagination,
    │   │                              #   SearchBar, Loading, ErrorMessage
    │   ├── features/                  # dashboard, users, employees, departments,
    │   │                              #   projects, tasks, profile, auditLogs
    │   ├── hooks/
    │   ├── types/
    │   └── routes.tsx
    ├── Dockerfile
    └── nginx.conf
```

---

## 5. Phase Plan

Each phase follows the same contract: **explain the concept → explain the files → explain the design choice and its alternative → implement → build/test → report the verification commands and their real output → name the commit point.**

| # | Phase | Deliverable | Verified by | Teaching focus |
|---|---|---|---|---|
| 0 | Environment setup | .NET 8 SDK, Postgres reachable, `.gitignore`, `.editorconfig`, repo init | `dotnet --version`, `psql -c 'select 1'` | SDK vs runtime, project layout |
| 1 | Solution scaffolding | 4 projects + test project, references wired | `dotnet build` | Clean Architecture, project references, DI basics |
| 2 | Domain layer | Entities, enums, base classes, domain exceptions | `dotnet build` | POCOs, why Domain has no dependencies |
| 3 | Persistence | `AppDbContext`, fluent configs, first migration, seed data | `dotnet ef migrations add`, `dotnet ef database update`, `\d+` in psql | EF Core, Fluent API, migrations, conventions |
| 4 | Cross-cutting | Exception middleware, correlation id, Serilog, `PagedResult<T>` | manual request → JSON error shape | Middleware pipeline, structured logging |
| 5 | Authentication | Register, login, BCrypt, JWT issue + validate, Swagger bearer | login returns a token; a protected endpoint 401s without it | Hashing vs encryption, JWT anatomy, claims |
| 6 | Authorization | Role policies + a resource handler for "manager owns project" | 403 for the wrong role, 200 for the right one | Roles vs policies vs resource-based auth |
| 7 | Departments + Users | Full CRUD; the reusable paging/sort/filter primitives | Swagger CRUD round-trip | DTO mapping, whitelisted sorting, IQueryable |
| 8 | Employees | CRUD + search + filter + sort + pagination; index work | `EXPLAIN ANALYZE` before/after trigram index | Projection, `AsNoTracking`, query plans |
| 9 | Projects | CRUD, employee assignment, manager scoping | manager can only touch own projects | Many-to-many with payload, resource auth |
| 10 | Tasks | CRUD, assignment, status transition rules | invalid transition → 422 | Business rules in the service layer |
| 11 | Audit logging | Audit writer + `GET /api/audit-logs` | log rows appear after login/create/update | Cross-cutting concerns, what never to log |
| 12 | Dashboard | `GET /api/dashboard/summary` in a minimal number of round-trips | captured SQL shows the query count | Aggregation, N+1 avoidance |
| 13 | Tests | xUnit; unit tests for services, integration tests via `WebApplicationFactory` | `dotnet test` | Test pyramid, fakes vs real DB |
| 14 | React foundation | Vite + TS, router, axios layer, AuthContext, protected routes, shared components | `npm run build`, login through UI | SPA auth, interceptors, 401 handling |
| 15 | React features | 9 pages with loading/error/validation/paging/filter/sort | click through every page | Container/presentational split, typed API |
| 16 | Docker | API + client Dockerfiles, `docker-compose.yml`, env vars | `docker compose up` end to end | Multi-stage builds, service networking |
| 17 | Documentation | `README.md`, `INTERVIEW_PREPARATION.md` (50 Q&A) | review | consolidation |

**Dependency note:** Phase 13's integration tests and Phase 16 both require Docker (decision **B2**, confirmed). Both are therefore in scope and will be genuinely verified.

---

## 6. Commit Points

One commit per phase, conventional-commit style. Nothing is committed until the phase actually builds and its verification commands pass. No synthetic history is fabricated.

```
chore: bootstrap repo, gitignore, editorconfig
feat: initialize .NET 8 clean architecture solution
feat: add domain entities and enums
feat: configure PostgreSQL, EF Core, initial migration and seed
feat: add exception handling, correlation id and structured logging
feat: implement JWT authentication
feat: implement role and policy based authorization
feat: implement department and user management
feat: implement employee management with search, filter, sort, pagination
feat: implement project management and employee assignment
feat: implement task management and status transitions
feat: add audit logging
feat: add dashboard summary endpoint
test: add unit and integration tests
feat: add React client with auth and protected routes
feat: implement React feature pages
feat: add Docker and docker compose support
docs: add README and interview preparation guide
```

---

## 7. What This Plan Explicitly Does Not Include

Named so scope stays honest, and so they can be discussed as "what I would do next" in an interview:

- Refresh tokens and token revocation (access tokens only, short-lived)
- ASP.NET Core Identity (hand-rolled auth is used, because it teaches more)
- CQRS / MediatR (services are sufficient at this size; adding it here is over-engineering)
- Multi-tenancy, soft delete, event sourcing, outbox pattern
- Email verification, password reset flows
- CI/CD pipeline, Kubernetes manifests
- Redis caching, background jobs

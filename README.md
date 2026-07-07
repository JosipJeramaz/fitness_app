# Fitness Challenge

A full-stack **Fitness Challenge** application that gamifies physical activity. Users log activities
across several sports whose native metrics differ (distance in km, duration in `mm:ss`, step counts).
The backend normalizes every activity into a single integer **Points** metric so everyone competes
fairly on one **global leaderboard**, and each user gets a **personal dashboard** visualizing their
trends and habits over time.

**What's inside**

- **Global leaderboard** — deterministic ranking by total points, rank-trend arrows over a selectable
  window (7/14/30 days), server-side paging/sorting, and a cumulative **points race chart** of the top 5.
- **Personal dashboard** — points-over-time chart, preferred-sports breakdown, a GitHub-style
  **activity calendar heatmap** with streak stats (current/longest streak, active days, best day), and a
  paged, sortable activity history.
- **Activity logging** — a dialog that adapts its form to the chosen sport, with client-side validation
  mirroring the server rules; users can delete their own entries (with confirmation).
- **JWT authentication** (feature-flagged bonus) — users log activities only for themselves.

**Tech stack:** C# / ASP.NET Core Web API on **.NET 8 (LTS)**, EF Core 8 + SQLite, FluentValidation,
Swagger · **Angular 20** (standalone components + signals), Angular Material, Chart.js (`ng2-charts`).

---

## Quick start

```bash
git clone <repo-url>
cd fitness_app
```

### Option A — Docker (one command)

```bash
docker compose up --build
```

- Frontend: **http://localhost:4200** (nginx, proxies `/api` to the backend container)
- Backend + Swagger: **http://localhost:5080/swagger**
- Demo data is **seeded automatically** (SQLite lives in the `api-data` volume).
- The compose demo runs with **auth ON** — log in as any seeded user: username `first.last`
  (e.g. `ada.lovelace`), password `password123`. Flip `Auth__Enabled` to `"false"` in
  `docker-compose.yml` for a fully open, spec-literal core.

### Option B — run locally without Docker

| Tool     | Version              |
|----------|----------------------|
| .NET SDK | **8.0** (pinned via `global.json`) |
| Node.js  | **20.19+ or 22.12+**, npm 10+ |

**Backend** (start this first):

```bash
cd backend
dotnet restore
dotnet run --project FitnessChallenge.Api
```

API at **http://localhost:5080**, Swagger at `/swagger`. Migrations are applied automatically on
startup and demo data is seeded in Development — no manual DB step needed.

**Frontend:**

```bash
cd frontend/fitness-challenge-web
npm install
npm start                # ng serve → http://localhost:4200
```

The dev API base URL is `src/environments/environment.ts` (`http://localhost:5080/api`).

### Tests

```bash
cd backend && dotnet test                                            # xUnit — 130 tests
cd frontend/fitness-challenge-web && npm test -- --watch=false --browsers=ChromeHeadless   # Karma/Jasmine
```

---

## Architecture

### Backend — Clean Architecture

```
backend/
├─ FitnessChallenge.Domain/          # entities, enums, PURE ScoringService — zero dependencies
├─ FitnessChallenge.Application/     # use-case services, DTOs, validation, repository abstractions
├─ FitnessChallenge.Infrastructure/  # EF Core (SQLite), repositories, JWT, seeding, migrations
├─ FitnessChallenge.Api/             # controllers, Program.cs, filters/middleware, Dockerfile
└─ FitnessChallenge.Tests/           # xUnit: scoring, validation, services, integration, auth
```

Dependencies point inwards only (`Api → Application → Domain`, `Infrastructure → Application → Domain`),
enforced by the compiler through project references.

**Layer responsibilities**

- **Domain** holds the entities (`Activity`, `User` — encapsulated, private setters, static factories)
  and the **`ScoringService`**: a pure, dependency-free implementation of the points rules, trivially
  unit-testable. Entities never score themselves — points are computed once and passed in.
- **Application** owns the use cases (`ActivityService`, `LeaderboardService`, `DashboardService`,
  `UserService`, `AuthService`) behind interfaces, the wire DTOs, and validation. Business-flow outcomes
  travel as `Result<T>`/`Error` values (Validation/Unauthorized/Forbidden/NotFound/Conflict) instead of
  exceptions. Repository and unit-of-work **abstractions** live here; implementations don't.
- **Infrastructure** implements persistence with EF Core + SQLite (the `DbContext` *is* the unit of
  work — repositories only stage changes, services commit), JWT token issuing, and the deterministic
  demo-data seeder.
- **Api** is thin: controllers bind HTTP to Application services and map `Error` values to RFC-9457
  `ProblemDetails` via one helper; a global action filter runs FluentValidation before every action.

**Request flow — `POST /api/activities`**

1. `FluentValidationFilter` (global) runs `IngestActivityRequestValidator` → invalid bodies short-circuit
   as `400 ValidationProblemDetails`, keyed per field.
2. The validator delegates the whole metric matrix to **`ActivityInterpreter`** — the *single source of
   truth* for ingestion rules (sport↔metric matching, per-sport caps, datetime window). The service
   re-runs the same interpreter defensively, so the two paths can never drift.
3. When auth is enabled, the controller checks the JWT `sub` claim against the target `userId` (`401`/`403`).
4. `ActivityService` calls the Domain `ScoringService`, stores the activity **with its points
   denormalized on the row**, and commits via the unit of work → `201` with the computed points.

**Cross-cutting techniques**

- Points math uses **`decimal`** end-to-end (never `double`) — `0.29 km × 100` must be exactly `29`.
- Points are **denormalized at ingestion**, so leaderboard/dashboard/trend reads are simple SQL sums —
  no recalculation jobs; deleting a row is automatically consistent everywhere.
- `DateTimeOffset` is persisted as **UTC ticks (`long`)** so SQLite can compare/order/index timestamps;
  hot paths ride indexes on `Activity(UserId, DateTimeUtc)` and `Activity(DateTimeUtc)`.
- Reads use **`AsNoTracking` + projections**; list endpoints sort in SQL with a deterministic id
  tie-break so pages never overlap.
- The clock is injected via **`TimeProvider`** everywhere — leaderboard-trend and validation tests pin it.
- **JWT is feature-flagged** (`Auth:Enabled`): the pipeline is always wired, enforcement is a per-request
  decision in the controllers, so the core stays open when the flag is off.

### Frontend — Angular 20

```
src/app/
├─ core/        # models (wire DTOs), API services, auth interceptor (Bearer header)
├─ features/    # leaderboard, dashboard, activity-entry dialog, auth dialogs, registration
└─ shared/      # reusable pieces: confirm dialog, trend indicator, calendar heatmap, chart palette
```

- **Standalone components + signals** throughout — server-driven state lives in signals; derived view
  state (chart data, column sets, heatmap grid) in `computed()`.
- All mutations go through dialogs (`ActivityEntryDialog`, `ConfirmDialog` for delete/logout); the
  activity form enables exactly one metric control per sport and mirrors every server rule
  (caps, datetime window, `mm:ss` format) so users rarely see a server-side `400`.
- `ActivityService.activityLogged` is a signal bumped after each successful ingest — an open dashboard
  refreshes itself when you log an activity from the toolbar dialog.
- Paging/sorting is server-side; the UI never re-sorts locally (ordering is owned by SQL).

### Testing

- **Backend (130 tests):** table-driven scoring suite (every worked example from the assignment plus
  boundary/decimal-drift cases), the full ingestion validation matrix, service tests with hand-rolled
  stubs (no mocking library), and end-to-end integration tests via `WebApplicationFactory` (happy path,
  paging/sorting, race-chart/leaderboard consistency, auth + ownership flows).
- **Frontend:** component specs for the dashboard (paging, delete flow, column visibility), the activity
  dialog (metric switching, caps, datetime window) and the heatmap (grid/streak math).

---

## Scoring rules

Every activity is normalized to integer **Points** at ingestion. The order of operations differs per
family — that's the crux of correctness:

| Activity     | Rate               | Rule                                               |
|--------------|--------------------|-----------------------------------------------------|
| Running      | 1 km = 100 pts     | `floor(distanceKm × rate)` (multiply, then floor)  |
| Walking      | 1 km = 50 pts      | `floor(distanceKm × rate)`                         |
| Cycling      | 1 km = 25 pts      | `floor(distanceKm × rate)`                         |
| Swimming     | 1 minute = 15 pts  | `floor(seconds / 60) × rate` (whole minutes first) |
| Gym          | 1 minute = 5 pts   | `floor(seconds / 60) × rate`                       |
| Daily Steps  | 100 steps = 1 pt   | `floor(steps / 100)`                               |

Examples: Running `42.195 km → 4219`, Walking `1.55 km → 77`, Swimming `"1:55" → 15`, Gym `"0:59" → 0`,
Daily Steps `399 → 3`.

---

## API reference

Base path: `/api`. All responses are JSON (camelCase); enums serialize as strings (e.g. `"running"`, `"dailySteps"`).

| Method & path                              | Description                                             |
|--------------------------------------------|---------------------------------------------------------|
| `POST /api/users`                          | Register a user `{ firstName, lastName, userName?, password? }` → `201 { id }`; duplicate name or username → `409`. `userName` + `password` go together (both or none) and enable login. |
| `GET  /api/users`                          | List users                                              |
| `POST /api/activities`                     | Ingest one activity (validate → score → store) → `201`; invalid → `400` |
| `DELETE /api/activities/{id}`              | Hard-delete one activity → `204`; not the owner → `403`; unknown id → `404` (ownership enforced only when auth is enabled) |
| `GET  /api/users/{userId}/activities`      | A user's activities (`?from`, `?to`, `?sport`, `?sortBy=datetime\|points`, `?sortDir=asc\|desc`, `?limit`, `?offset`; default newest first). Returns an `{ items, totalCount }` envelope. |
| `GET  /api/leaderboard`                    | Ranked users with trend (`?windowDays=7`, `?limit`, `?offset`, `?sortBy=rank\|trend`, `?sortDir`). Same `{ items, totalCount }` envelope. |
| `GET  /api/leaderboard/race`               | Cumulative points race of the top users (`?days=7\|30`; omit for "since first activity"; `?top=5`) |
| `GET  /api/users/{userId}/dashboard`       | Totals, points-over-time and sport breakdown (`?groupBy=day\|week`) |
| `POST /api/auth/login`                     | Exchange credentials for a JWT (bonus; feature-flagged) |
| `POST /api/dev/seed`                       | Seed demo data (Development only)                       |

### Activity ingestion schema

```jsonc
{
  "userId": "string",
  "datetime": "2026-06-30T10:30:00Z",   // ISO-8601, at most one year in the past, not in the future
  "sport": "running|walking|cycling|gym|swimming",  // OMIT for Daily Steps
  "distance": 42.195,                    // distance sports (km; worth >=1 pt: running >=0.01, walking >=0.02, cycling >=0.04; max 100/80/300)
  "duration": "mm:ss",                   // duration sports (seconds 00–59; >= 1:00 and <= 1080:00 = 18 h)
  "steps": 10000                         // Daily Steps only (integer 100–100000)
}
```

Exactly one metric must be present and must match the sport. Anything else (wrong metric, extra metric,
`steps` sent with a `sport`, bad `mm:ss`, a value worth 0 points, a value over the per-entry cap,
missing/invalid `datetime`, unknown user) returns `400`.

---

## Design decisions & assumptions

- **Paged envelope on list responses.** The assignment doesn't prescribe a response shape for serving
  fitness data; `{ "items": […], "totalCount": N }` was chosen because a "X–Y of Z" paginator needs the
  pre-paging total. Trivial to revert to a bare array (or an `X-Total-Count` header) if required.
- **Name uniqueness / normalization.** `(FirstName, LastName)` is compared case-insensitively and
  trimmed (internal whitespace collapsed). Enforced by a service-layer check **and** a DB unique index
  on normalized columns — the index is the race backstop (mapped to `409`).
- **Daily Steps is not a `sport` value** — it is inferred from an absent `sport` + present `steps`.
- **Hard delete for activities.** Aggregates are computed at read time from denormalized per-row points,
  so removal is automatically consistent — no soft-delete filtering or recalculation. Someone else's
  activity → `403` (not `404`): activity lists are public by design, so there is no existence-leak
  argument, and `403` mirrors the ingest ownership semantics.
- **Per-entry value bounds.** Each activity must be worth **at least 1 point** (so 1 m of running, 99
  steps or 0:59 of gym — all of which floor to 0 — are rejected) and stays under a plausibility **cap**
  (running ≤ 100 km, walking ≤ 80 km, cycling ≤ 300 km, gym/swimming ≤ 18 h, steps ≤ 100 000) so a single
  typo can't ruin the leaderboard. The point calculation itself is unchanged and spec-literal (floor); this
  only restricts what can be *ingested*. Deliberately per-entry — no per-day aggregate check, so ingestion
  stays a pure validation with no DB read. Mirrored client-side; single sources of truth:
  `ActivityInterpreter` (backend), `ACTIVITY_KINDS` (frontend).
- **Datetime window:** at most **one year back** and **not in the future** (a 5-minute tolerance absorbs
  client/server clock skew). Both bounds rejected with `400` and mirrored in the form.
- **Unknown user on ingest → `400`** (keeping every invalid ingest a `400` per the spec); unknown user
  on read endpoints → `404`.
- **Leaderboard:** includes all registered users (zero-activity users at the bottom); tie-break is
  points desc → last name → first name → id, giving unique deterministic ranks. The rank **trend is
  snapshot-free**: `previousRank` is recomputed from activities `≤ now − window`, so no history tables.
  Users with no prior-window activity ranked at the **bottom of that snapshot** (0 points), so a new
  competitor who enters mid-board shows a genuine "up" — there is no special "new" state.
- **Enum casing:** camelCase strings (`DailySteps → "dailySteps"`). **Decimals** stored as SQLite `TEXT`
  to preserve exact precision.

---

## Bonus features

- **JWT authentication (feature-flagged).** Off by default in `appsettings.json` so the core grades
  literally; the docker-compose demo runs with it ON. When enabled, ingest/delete require a bearer token
  whose subject matches the target user (`401`/`403`). Two login modes: username + password, or — only
  for users who never set credentials — a passwordless first + last name fallback (a set password can
  never be bypassed by name).
- **Deterministic demo seed.** Five athletes with 40 days of generated history, each following a distinct
  storyline so the leaderboard, race chart and dashboards show a real competition.
   Login: `first.last` / `password123`.
- **Race chart** — cumulative points per top athlete with timeframe toggle; lines are anchored at each
  user's pre-window total so every line's final value equals their leaderboard total (covered by an
  integration test), and a line only starts on the athlete's first scoring day (no fake zero run-up).
- **Calendar heatmap + streaks** on the dashboard — 12 months of GitHub-style cells (intensity = points
  relative to the user's best day) with current/longest streak, active days and best day beneath it.
- **UX details:** confirmation dialogs for destructive actions (delete, logout), snackbar feedback,
  self-refreshing dashboard after logging from the toolbar dialog, per-direction sort buttons with
  page-reset semantics ("page 3" means nothing after a re-order).

# Fastasys

Fastasys is a Docker-first monorepo containing an ASP.NET Core API on .NET 10 and an Angular frontend. Docker is the only host prerequisite for the supported developer workflow: the Dev Container supplies .NET, Node.js, the Angular CLI, and EF Core tooling.

## Repository layout

- `backend/src/Fastasys.ApiService/` — ASP.NET Core API and its EF Core PostgreSQL migrations
- `backend/tests/Fastasys.Tests/` — API unit and integration tests
- `frontend/` — Angular single-page application
- `infrastructure/` — versioned runtime-dependency configuration; no secrets or persisted data
- `docker-compose.yml` — local PostgreSQL, migration job, API, and frontend stack
- `.devcontainer/` — the reproducible VS Code development environment

## Start locally

Install Docker Desktop (or a compatible Docker runtime), then run from the repository root:

```sh
docker compose up -d --build
```

The first start builds the images, starts PostgreSQL, applies the EF Core migrations once, then starts the API and frontend.

- Frontend: <http://localhost:4200>
- API: <http://localhost:8080>
- API documentation: <http://localhost:8080/scalar/v1>
- PostgreSQL: `localhost:5432`

Stop the stack with `docker compose down`. Add `-v` only when you deliberately want to remove the local PostgreSQL data volume. The former MySQL volume is not touched by this change and may be removed manually only if its old local data is no longer wanted.

The Compose defaults are development-only. Copy `.env.example` to `.env` and choose a local password before using anything beyond an isolated local environment:

```sh
cp .env.example .env
```

## Recommended: develop in a Dev Container

1. Install Docker Desktop and VS Code.
2. Install VS Code's **Dev Containers** extension.
3. Clone this repository and open its root folder in VS Code.
4. Run **Dev Containers: Reopen in Container**.
5. After setup completes, run `docker compose up --build` in the integrated terminal.

The Dev Container installs .NET 10, Node 26, project-local Angular tooling, and the pinned EF Core CLI tool. It also recommends C# Dev Kit, Angular, Docker, ESLint, Prettier, EditorConfig, YAML, and GitHub Actions support. Nothing needs to be installed globally on the host beyond Docker and the editor integration.

VS Code tasks are available through **Terminal: Run Task** for starting/stopping the stack and running backend or frontend checks.

## Work on an individual service

The normal stack command is preferred. If you need frontend hot reload, start the database and API first:

```sh
docker compose up postgres migrations api -d --build
```

Then, in another Dev Container terminal:

```sh
npm run start --prefix frontend
```

The Angular development server uses `frontend/proxy.conf.json` to send `/api` requests to the API.

For API-only work outside Compose, start PostgreSQL first, then run:

```sh
dotnet run --project backend/src/Fastasys.ApiService/Fastasys.ApiService.csproj
```

Development startup applies pending migrations and seeds development data. Production does not do this; schema deployment is a distinct deployment responsibility.

## Database migrations

EF Core migrations live in `backend/src/Fastasys.ApiService/Data/Migrations/` and are committed with application code. Do not create a separate database folder for application-schema changes.

After changing the EF model, create and inspect a migration inside the Dev Container:

```sh
dotnet tool run dotnet-ef migrations add DescriptiveChangeName --project backend/src/Fastasys.ApiService/Fastasys.ApiService.csproj --startup-project backend/src/Fastasys.ApiService/Fastasys.ApiService.csproj
```

Compose applies migrations for local development. For an actual deployment pipeline, generate a reviewed idempotent SQL artifact or a migration bundle and run it as a dedicated job with a schema-deployment identity; do not grant the runtime API schema-changing permissions.

## Checks

Run the same checks as CI from the Dev Container:

```sh
dotnet restore Fastasys.slnx
dotnet tool restore
dotnet format Fastasys.slnx --verify-no-changes --no-restore
dotnet build Fastasys.slnx --configuration Release --no-restore
dotnet tool run dotnet-ef migrations has-pending-model-changes --project backend/src/Fastasys.ApiService/Fastasys.ApiService.csproj --startup-project backend/src/Fastasys.ApiService/Fastasys.ApiService.csproj --configuration Release --no-build
dotnet test backend/tests/Fastasys.Tests/Fastasys.Tests.csproj --configuration Release --no-build --no-restore
npm run format:check --prefix frontend
npm run lint --prefix frontend
npm run test --prefix frontend
npm run build:prod --prefix frontend
```

## CI

GitHub Actions runs for pull requests, pushes to `main`, and manual dispatches. Backend and frontend jobs run in parallel on Ubuntu runners, cache NuGet/npm packages, and independently verify formatting, builds, tests, and migration drift. CI tracks the newest GA .NET 10 SDK and newest Node 26 release without crossing either major version. Dependabot opens weekly update pull requests for Actions, NuGet, npm, and Docker base images.

Image publishing and production deployment are intentionally not configured yet: they require choosing a container registry, target-server model, and secret-management approach. Keycloak will be added under `infrastructure/` only once its realm, clients, and roles are defined.

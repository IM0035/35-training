# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

A training project for practicing AI-agent-assisted development. Two parts:

- `documents/` — the exercise guides (in Traditional Chinese). Start at `documents/README.md`; the four-exercise curriculum is in `documents/activities/activity-guideline.md`. `documents/references/` holds guides on agent configuration, prompting, and token usage.
- `training-repo/` — **OrderHub**, an ASP.NET Core MVC app that is the subject of the exercises (read the code, fix bugs, add a feature, refactor). All application code and all commands below live here.

Note: exercises 2's "customer complaints" describe **intentional bugs** planted for learners to find. Do not treat existing OrderHub behavior as necessarily correct — verify against the intended rules described in `documents/README.md` and `activity-guideline.md`.

## Commands

Run all of these from `training-repo/` (the directory holding `OrderHub.sln`):

```powershell
dotnet build                                 # build the solution
dotnet test                                  # run all tests (xUnit, EF Core InMemory — no SQL Server needed)
dotnet run --project src/OrderHub.Web        # run the web app
```

Run a single test class / method:

```powershell
dotnet test --filter "FullyQualifiedName~OrderServicePricingTests"
dotnet test --filter "DisplayName~CreateOrder"
```

Reset the database back to seed data (needs `dotnet-ef`; install with `dotnet tool install -g dotnet-ef`):

```powershell
dotnet ef database drop -f -p src/OrderHub.Infrastructure -s src/OrderHub.Web
dotnet run --project src/OrderHub.Web        # re-migrates + re-seeds on startup
```

## Environment

- .NET 8 SDK (9.x/10.x also build it). Web app requires a local **SQL Server** (Developer/Express/LocalDB) — the connection string is in `src/OrderHub.Web/appsettings.Development.json`; adjust `Server=` for your instance.
- On startup `Program.cs` runs `db.Database.Migrate()` then `DbSeeder.SeedAsync` — the DB `OrderHubTraining` is created and seeded automatically (20 customers, 50 products, 200 orders over ~90 days, fixed random seed so everyone gets identical data).
- **Tests never touch SQL Server** — they use EF Core InMemory (`TestSetup.cs`).

## Architecture

Three-project layering, dependencies point inward (`Web → Core → Infrastructure`, with Core defining the interfaces Infrastructure implements):

- **`OrderHub.Web`** — Controllers, Views (Razor + Bootstrap 5, all assets local, no CDN), ViewModels. Thin: controllers only wire service calls to views.
- **`OrderHub.Core`** — Domain models (`Domain/`), service interfaces + business logic (`Services/`), repository interfaces (`Interfaces/`), shared types (`Common/ServiceResult.cs`, `Common/PagedResult.cs`). This is where all business rules live (discounts, stock, status transitions).
- **`OrderHub.Infrastructure`** — `OrderHubDbContext`, repositories (the only code that touches `DbContext`), EF Core migrations, `DbSeeder`.

DI is registered in `Program.cs`; everything is `AddScoped`.

## Conventions (follow these when adding code)

- Keep controllers thin — business logic goes in a Core service, injected via its interface.
- Only repositories touch `DbContext`. Services and controllers must not use EF Core directly.
- Services return `ServiceResult<T>` (see `Common/ServiceResult.cs`) to express expected failures — do not throw for validation/business errors. Errors surface via `TempData["Error"]` / `TempData["Success"]` (shared alert block in `Views/Shared/_Layout.cshtml`).
- Views bind to a ViewModel (mapping written by hand), never to a domain model directly.
- Validate user input with DataAnnotations + `ModelState`; bad input must render form errors, **never** a 500.
- Money is always `decimal`. Discount rules (Standard: none, Silver: 5%, Gold: 10%) are centralized in `OrderService` — compute totals via `CalculateTotal`, don't re-derive discounts elsewhere.
- Reference implementations to match when writing new code: `ProductsController.cs`, `ProductService.cs` / `IProductService.cs`, `Views/Products/Index.cshtml`.

## Don'ts

- Don't hand-edit files under `src/OrderHub.Infrastructure/Migrations/**` — migrations are history.
- Don't add NuGet packages, or change `appsettings*.json` connection strings, without asking.
- Don't refactor code unrelated to the current task.

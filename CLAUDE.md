# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Architecture Overview

Nimblist is a collaborative shopping list app. The monorepo lives under `src/nimblist/` and contains four components:

- **`nimblist.api`** — ASP.NET Core 8 REST API + Razor Pages (Identity UI). Entry point: `Program.cs`.
- **`Nimblist.data`** — EF Core 9 data layer: `NimblistContext`, models, migrations, and CSV-seeded category data.
- **`Nimblist.Frontend`** — React 19 + TypeScript SPA (Vite). Entry point: `src/main.tsx`.
- **`Nimblist.classification`** — Python Flask service that classifies item names using pre-trained Logistic Regression models (joblib). Endpoint: `POST /predict`.
- **`Nimblist.recipescraper`** — Python Flask service that scrapes recipe data from URLs using `recipe-scrapers`. Endpoint: `POST /scrape`.
- **`Nimblist.test`** — xUnit + Moq tests for the backend.

Solution file: `src/nimblist/nimblist.sln`

### Request flow

Browser → React SPA (`:5173`) → API (`:64212`) → PostgreSQL (`:5433`)  
Items creation/update also calls the classification service (`http://nimblist.classification:5000/predict`) via `IHttpClientFactory`.  
Real-time list updates use SignalR (`/hubs/shoppinglist`), backed by a Redis pub/sub channel prefix `Nimblist.SignalR.*`.

### Auth

ASP.NET Core Identity with cookie auth (30-day sliding expiration). Google OAuth is supported via `Authentication:Google` config. API routes return 401/403 instead of redirecting — the cookie redirect override is in `Program.cs`. All API controllers require `[Authorize]`; all data queries filter by `ClaimTypes.NameIdentifier`.

### Data model key relationships

`ApplicationUser` → `ShoppingList` → `Item` (cascade delete)  
`Item` → `Category` / `SubCategory` (FK, SetNull on delete)  
`ShoppingList` ↔ `Family` via `ListShare`; `Recipe` ↔ `Family` via `RecipeShare`  
`ApplicationUser` → `PreviousItemName` (autocomplete history)  
`ApplicationUser` → `ItemClassificationFeedback` (cascade delete; Category/SubCategory SetNull) — stores user-corrected item classifications for ML retraining; exportable via `GET /api/classificationfeedback/export`  
`ApplicationUser` → `Recipe` → `RecipeIngredient` (cascade delete) — saved recipes imported from URLs via the scraper service; `RecipeIngredient` has optional `ParsedName` and `ParsedQuantity` fields populated by `ingredient-parser-nlp` in the scraper  
`Recipe` → `RecipeShare` (cascade delete); `RecipeShare` targets either a `UserId` or a `FamilyId` (one nullable FK per share row)  
`ApplicationUser` → `MealPlan` → `MealPlanEntry` (cascade delete) — meal planning calendar; `MealPlanEntry.PlannedDate` is `DateOnly` (maps to PostgreSQL `date`)  
`Recipe` → `MealPlanEntry` (cascade delete — removing a recipe removes its calendar entries)  
`MealPlan` → `MealPlanShare` (cascade delete); same user-or-family nullable FK pattern as other share tables

CORS requires explicit origin config in `appsettings.json` under `CorsSettings:AllowedOrigins` (semicolon-separated). `AllowCredentials()` is always set, so `AllowAnyOrigin()` cannot be used.

---

## Commands

### Frontend (`src/nimblist/Nimblist.Frontend/`)

```bash
npm run dev           # Dev server at https://localhost:5173 (HTTPS via mkcert)
npm run build         # tsc + Vite production build
npm run lint          # ESLint
npm run test          # Vitest (watch mode)
npm run test:coverage # Vitest with LCOV coverage → ./coverage/
```

### Backend

```bash
# From repo root or src/nimblist/
dotnet build src/nimblist/nimblist.sln
dotnet test src/nimblist/nimblist.sln

# Run a single test class
dotnet test src/nimblist/Nimblist.test/ --filter "FullyQualifiedName~MyTestClass"

# EF migrations (run from nimblist.api directory)
dotnet ef migrations add <Name> --project "../nimblist.data/Nimblist.data.csproj"
dotnet ef database update
```

### Python classification service

```bash
cd src/nimblist/Nimblist.classification
pip install -r requirements.txt
python app.py          # Dev (port 5000)
```

### Docker (full stack)

```bash
docker compose up      # Starts api, db, redis, classification, recipescraper
```

---

## Frontend conventions

- **State:** Zustand (`src/store/authStore.ts`) for auth only; local state otherwise.
- **Routing:** React Router v7; protected pages wrap with `<ProtectedRoute>`.
- **Real-time:** `useShoppingListHub` hook manages SignalR connection lifecycle and list-group subscriptions.
- **Styling:** Tailwind CSS 4 (no CSS modules). The Vite plugin is `@tailwindcss/vite`.
- **TypeScript:** Strict mode, no unused vars/params (`tsconfig.app.json`). `jsdom` environment in Vitest.
- **HTTP:** All API calls go through `authenticatedFetch` (`src/components/HttpHelper.ts`) — adds `credentials: 'include'` and `VITE_API_BASE_URL` prefix. Never use raw `fetch` for API calls.
- **Last list:** `localStorage` key `nimblist_last_list` stores the last-viewed list ID. Set on list load, cleared on logout, used by `HomePage` to redirect back to it.
- **Error boundary:** `<ErrorBoundary>` wraps the app in `main.tsx` (class component in `src/components/ErrorBoundary.tsx`).
- **Sharing UI:** Reusable `<SharePanel>` component (`src/components/SharePanel.tsx`) handles list, recipe, and meal plan sharing. Props: `endpoint` (GET shares), `postEndpoint` (POST new share), `resourceId`, `resourceKey` (`'listId' | 'recipeId' | 'mealPlanId'`), `isOwner`. Non-owners see a read-only message; owners see current shares with remove buttons and a family dropdown to add new shares.
- **Email-to-userId lookup:** Adding a family member requires two calls — `GET /api/auth/lookup?email=X` to resolve the email to a userId, then `POST /api/familymembers` with the userId. The lookup endpoint is auth-gated and returns only `{ userId, email }`.
- **Meal Planner page:** `MealPlannerPage.tsx` — weekly calendar grid (Mon–Sun) with plan selector, week navigation, per-day add-entry form (recipe + meal type), per-entry delete and "Add to list" inline flow. The `MealPlanEntriesController` `addtolist` endpoint reuses `IClassificationService` and SignalR — same pattern as `RecipesController.AddIngredientsToList`.

## Backend conventions

- **DTO pattern:** Controllers return DTOs (e.g., `ItemWithCategoryDto`), not EF models directly. Never return `ApplicationUser`-related navigation properties directly — they expose `PasswordHash`, `SecurityStamp`, etc.
- **JSON cycles:** `ReferenceHandler.IgnoreCycles` is applied to both MVC and SignalR serialization.
- **Migrations auto-apply:** `dbContext.Database.Migrate()` runs at startup.
- **Classification config:** `ClassificationService:PredictUrl` in `appsettings.json`. Classification logic lives in `Services/ClassificationService` (injected as `IClassificationService`) — not inline in controllers.
- **Recipe scraper config:** `RecipeScraperService:ScrapeUrl` in `appsettings.json`.
- **Cookie persistence:** Google OAuth sign-in uses `isPersistent: true` in `ExternalLogin.cshtml.cs` (both returning-user and first-registration paths) — overrides the scaffolded default of `false`.
- **Shared resource access:** `GetAccessibleXxxIdsAsync(userId)` (implemented per-controller) unions own IDs + user-share IDs + family-share IDs into a `HashSet<Guid>`. This pattern is used by `RecipesController` and `MealPlansController`; apply it to any new shareable resource. Read/add endpoints use this set; delete remains owner-only.
- **`IsOwned` in DTOs:** When a resource can be shared, include `bool IsOwned` in its detail and summary DTOs (computed as `entity.UserId == userId` in the controller). The frontend uses this to conditionally show owner-only controls (delete, share management).
- **Ingredient parsing:** `RecipeIngredient` stores `ParsedName` (nullable, max 300) and `ParsedQuantity` (nullable, max 100). The scraper service uses `ingredient-parser-nlp` to populate these. When adding ingredients to a shopping list, `ParsedName ?? Text` is used for classification and the item name; `ParsedQuantity` maps to `Item.Quantity`.
- **`DateOnly` in EF/API:** `MealPlanEntry.PlannedDate` uses `DateOnly`, which EF Core 9 + Npgsql maps to PostgreSQL `date`. System.Text.Json in .NET 8 serialises `DateOnly` as `"YYYY-MM-DD"` without a custom converter.

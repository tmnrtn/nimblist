# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Architecture Overview

Nimblist is a collaborative shopping list app. The monorepo lives under `src/nimblist/` and contains four components:

- **`nimblist.api`** — ASP.NET Core 8 REST API + Razor Pages (Identity UI). Entry point: `Program.cs`.
- **`Nimblist.data`** — EF Core 9 data layer: `NimblistContext`, models, migrations, and CSV-seeded category data.
- **`Nimblist.Frontend`** — React 19 + TypeScript SPA (Vite). Entry point: `src/main.tsx`.
- **`Nimblist.classification`** — Python Flask service that classifies item names using pre-trained Logistic Regression models (joblib). Endpoint: `POST /predict`.
- **`Nimblist.recipescraper`** — Python Flask service that scrapes recipe data from URLs using `recipe-scrapers` and parses ingredients using `ingredient-parser-nlp`. Endpoints: `POST /scrape`, `POST /parse-ingredients`, `POST /scrape-image`. Has an optional LLM fallback (via OpenRouter or Ollama) for pages without schema markup, and a `/scrape-image` endpoint that extracts recipes directly from images (photo of a recipe card, book page, etc.) using a vision model.
- **`Nimblist.test`** — xUnit + Moq tests for the backend.

Solution file: `src/nimblist/nimblist.sln`

### Request flow

Browser → React SPA (`:5173`) → API (`:64212`) → PostgreSQL (`:5433`)  
Items creation/update also calls the classification service (`http://nimblist.classification:5000/predict`) via `IHttpClientFactory`.  
Real-time list updates use SignalR (`/hubs/shoppinglist`), backed by a Redis pub/sub channel prefix `Nimblist.SignalR.*`.

### Auth

ASP.NET Core Identity with cookie auth (30-day sliding expiration). Google, Facebook, and Microsoft OAuth are supported via `Authentication:Google`, `Authentication:Facebook`, and `Authentication:Microsoft` config sections respectively — each provider is only registered when its credentials are present (absent credentials are silently skipped, not an error). API routes return 401/403 instead of redirecting — the cookie redirect override is in `Program.cs`. All API controllers require `[Authorize]`; all data queries filter by `ClaimTypes.NameIdentifier`.

**Roles:** Two roles exist — `Admin` and `Standard`. Both are seeded at startup. New users (email and OAuth registration) are automatically assigned the `Standard` role. To promote an existing user to Admin without code changes, set `AdminSettings:AdminEmail` in `appsettings.json` (or the `AdminSettings__AdminEmail` env var); the startup seed checks this value and assigns the Admin role if the user exists. `GET /api/auth/userinfo` returns `roles` (string array) and `isAdmin` (bool) so the frontend can gate admin-only UI.

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
`ApplicationUser` → `UserPushSubscription` (cascade delete) — Web Push subscriptions; upserted by endpoint via `POST /api/pushsubscriptions`, removed via `DELETE /api/pushsubscriptions`  
`LlmSettings` — single-row table (Id = 1) storing admin-managed LLM config (provider, model, vision model, API key, base URL) **and** `ImageSearchApiKey` (Brave Search API key for recipe image search); read by `RecipesController` on every import call (LLM fields) and by `ImageSearchController` on every image search (falling back to env vars for LLM if empty; image search returns 503 if key absent)  
`ApplicationUser` → `Tag` (cascade delete) — user-scoped colour tags for recipes; `Tag` has `Id`, `Name`, `Color` (nullable string, e.g. `"green"`), `UserId`; managed via `TagsController` (`GET/POST/PUT/DELETE /api/tags`); assigned to recipes via `POST/DELETE /api/recipes/{id}/tags/{tagId}` (implicit join table `RecipeTag`)  
`ShoppingList.IsTemplate` (bool, default false) — templates are shown in a separate section of the lists page and excluded from "add to list" dropdowns. Template operations: `POST /api/shoppinglists/{id}/createfrom` creates a new list pre-populated from the template; `POST /api/shoppinglists/{id}/appendto/{existingListId}` appends template items to an existing list. The "add a week" shortcut posts to `appendto` targeting the same list. Duplicate detection on item add: `ItemsController.PostItem` checks for existing items with the same name (case-insensitive) on the same list and returns a duplicate flag in the response so the frontend can warn the user.

CORS requires explicit origin config in `appsettings.json` under `CorsSettings:AllowedOrigins` (semicolon-separated). `AllowCredentials()` is always set, so `AllowAnyOrigin()` cannot be used.

---

## Commands

### Frontend (`src/nimblist/Nimblist.Frontend/`)

```bash
npm run dev           # Dev server at https://localhost:5173 (HTTPS via mkcert)
npm run build         # tsc + Vite production build
npm run lint          # ESLint
npm run test -- --run # Vitest (always use --run to ensure the process terminates)
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

Runs on **Python 3.11** (Docker image `python:3.11-slim`).

```bash
cd src/nimblist/Nimblist.classification
pip install -r requirements.txt   # local dev (loose pins OK)
python app.py                      # Dev (port 5000)
```

Set `FLASK_HOST=0.0.0.0` to bind to all interfaces (e.g. in Docker). Default is `127.0.0.1`.

### Python recipe scraper service

Runs on **Python 3.11** (Docker image `python:3.11-slim`).

```bash
cd src/nimblist/Nimblist.recipescraper
pip install -r requirements.txt   # local dev (loose pins OK)
python app.py                      # Dev (port 5001)
```

Set `FLASK_HOST=0.0.0.0` to bind to all interfaces. Default is `127.0.0.1`.

#### LLM fallback (optional)

LLM config can be set in two ways (DB takes precedence over env vars):

1. **Admin UI (recommended for production):** `GET/PUT /api/admin/llm-settings` — changes take effect immediately without a container restart. The API key is masked in GET responses.
2. **Env vars (local dev / bootstrap):** Set these when running the scraper directly:

| Variable | Values | Notes |
|---|---|---|
| `LLM_PROVIDER` | `openrouter` \| `openai` \| `anthropic` \| `gemini` \| `ollama` \| `` | Empty disables all LLM features |
| `LLM_MODEL` | e.g. `anthropic/claude-3-haiku` / `llama3.2` | Text fallback model |
| `LLM_VISION_MODEL` | e.g. `anthropic/claude-3-haiku` / `llava` | Vision model for `/scrape-image`; falls back to `LLM_MODEL` if unset |
| `OPENROUTER_API_KEY` | `sk-or-...` | Required when using `openrouter` |
| `OLLAMA_BASE_URL` | e.g. `http://localhost:11434` | Required when using `ollama` |

In PowerShell for local dev:
```powershell
$env:LLM_PROVIDER = "openrouter"
$env:LLM_MODEL    = "anthropic/claude-3-haiku"
$env:OPENROUTER_API_KEY = "sk-or-..."
python app.py
```

For Docker, set `RECIPESCRAPER_LLM_PROVIDER`, `RECIPESCRAPER_LLM_MODEL`, `OPENROUTER_API_KEY`, and `OLLAMA_BASE_URL` in your `.env` file — they are forwarded to the container via `docker-compose.yml`.

### Python dependency lock files

Both Python services have a `requirements.lock` alongside `requirements.txt`. The lock file contains exact `==` pinned versions with SHA-256 hashes and is used by Docker builds (`pip install --require-hashes -r requirements.lock`). `requirements.txt` keeps the loose `>=` bounds and is used for local dev.

**When adding or upgrading a Python dependency:**
1. Edit `requirements.txt` with the new/changed constraint.
2. Regenerate the lock file from the service directory:
   ```bash
   pip-compile requirements.txt --output-file requirements.lock --no-header --generate-hashes
   ```
3. Commit both `requirements.txt` and `requirements.lock`.

The scraper uses `recipe-scrapers` v15+. The scrape endpoint tries the site-specific scraper first, then falls back with `supported_only=False` if `WebsiteNotImplementedError` is raised. The old `wild_mode=True` parameter was removed in v15. If `recipe-scrapers` fails or returns no ingredients, the LLM fallback uses `trafilatura` to extract clean page text before sending it to the LLM. The `/scrape-image` endpoint accepts `{ image_url }` (public URL) or `{ image, media_type }` (base64); it requires a vision-capable model configured via `LLM_VISION_MODEL` (falls back to `LLM_MODEL`).

**Ingredient quantity formatting:** `ingredient-parser-nlp` returns `Fraction` objects (e.g. `Fraction(3, 2)`). The `_format_quantity()` helper in `app.py` converts these to mixed-number strings (`"1 1/2"`) before storing, avoiding improper fractions like `"3/2"` that break the frontend scaling parser.

### Docker (full stack)

```bash
# Must be run from the repo root — the API Dockerfile uses context: . which resolves to repo root
docker compose -f src/nimblist/docker-compose.yml -f src/nimblist/docker-compose.override.yml up

# Rebuild a single service (e.g. after changing Python dependencies)
docker compose -f src/nimblist/docker-compose.yml -f src/nimblist/docker-compose.override.yml up --build Nimblist.recipescraper
```

When running via JetBrains Rider, use Rider's Docker Compose integration directly — it handles the build context correctly and mounts source for fast-mode debugging.

#### Dev HTTPS setup (first time / after cert expiry)

Rider's fast-mode containers serve HTTP only. To enable HTTPS on port 64213, export the dev cert into the UserSecrets folder (which Rider already mounts) and create `appsettings.Development.json` (gitignored):

```powershell
dotnet dev-certs https -ep "$env:APPDATA\Microsoft\UserSecrets\nimblist.pfx" -p nimblist-dev
dotnet dev-certs https --trust
```

```json
// src/nimblist/nimblist.api/appsettings.Development.json  (do not commit)
{
  "Kestrel": {
    "Endpoints": {
      "Http":  { "Url": "http://*:8080" },
      "Https": {
        "Url": "https://*:8081",
        "Certificate": { "Path": "/root/.microsoft/usersecrets/nimblist.pfx", "Password": "nimblist-dev" }
      }
    }
  }
}
```

---

## Frontend conventions

- **State:** Zustand (`src/store/authStore.ts`) for auth only; local state otherwise. The auth store now includes `isAdmin` (bool) and `roles` (string array) populated from `GET /api/auth/userinfo`.
- **Routing:** React Router v7; protected pages wrap with `<ProtectedRoute>`. Admin-only pages wrap with `<AdminRoute>` (`src/components/AdminRoute.tsx`), which redirects non-admins to home. The `/admin` route renders `AdminPage.tsx` (Users, Families, LLM Settings, and Classification Feedback tabs).
- **Real-time:** `useShoppingListHub` hook manages SignalR connection lifecycle and list-group subscriptions.
- **Styling:** Tailwind CSS 4 (no CSS modules). The Vite plugin is `@tailwindcss/vite`.
- **TypeScript:** Strict mode, no unused vars/params (`tsconfig.app.json`). `jsdom` environment in Vitest.
- **Test env vars:** `.env.test` is gitignored. Set test-only environment variables in the `test.env` block of `vitest.config.ts` instead (e.g. `VITE_API_BASE_URL`).
- **HTTP:** All API calls go through `authenticatedFetch` (`src/components/HttpHelper.ts`) — adds `credentials: 'include'` and `VITE_API_BASE_URL` prefix. Never use raw `fetch` for API calls. `authenticatedFetch` returns the response for all status codes (does not throw on non-2xx); callers must check `response.ok` themselves.
- **Last list:** `localStorage` key `nimblist_last_list` stores the last-viewed list ID. Set on list load, cleared on logout, used by `HomePage` to redirect back to it.
- **Error boundary:** `<ErrorBoundary>` wraps the app in `main.tsx` (class component in `src/components/ErrorBoundary.tsx`).
- **Sharing UI:** Reusable `<SharePanel>` component (`src/components/SharePanel.tsx`) handles list, recipe, and meal plan sharing. Props: `endpoint` (GET shares), `postEndpoint` (POST new share), `resourceId`, `resourceKey` (`'listId' | 'recipeId' | 'mealPlanId'`), `isOwner`. Non-owners see a read-only message; owners see current shares with remove buttons and a family dropdown to add new shares.
- **Email-to-userId lookup:** Adding a family member requires two calls — `GET /api/auth/lookup?email=X` to resolve the email to a userId, then `POST /api/familymembers` with the userId. The lookup endpoint is auth-gated and returns only `{ userId, email }`.
- **Meal Planner page:** `MealPlannerPage.tsx` — weekly calendar grid (Mon–Sun) with plan selector, week navigation, per-day add-entry form (recipe + meal type), per-entry delete and "Add to list" inline flow. The `MealPlanEntriesController` `addtolist` endpoint reuses `IClassificationService` and SignalR — same pattern as `RecipesController.AddIngredientsToList`.
- **Image import:** `RecipesPage.tsx` has an "Import from Image" tab. Uses `<input type="file" accept="image/*" capture="environment">` — on mobile this opens the rear camera directly. The selected file is read as base64 and POSTed to `POST /api/recipes/import-image`, which forwards to the scraper's `/scrape-image`. Requires `LLM_VISION_MODEL` (or `LLM_MODEL`) to be configured; returns 503 otherwise.
- **PWA:** The app is a Progressive Web App via `vite-plugin-pwa` (`injectManifest` strategy). The custom service worker is at `src/sw.ts` (Workbox precaching + push event handling). App icons live in `public/` (64/192/512px, maskable, apple-touch). `<InstallPrompt>` (`src/components/InstallPrompt.tsx`) handles Android add-to-home-screen; iOS shows a manual hint. `<NotificationBanner>` (`src/components/NotificationBanner.tsx`) requests push permission once per user — dismissal (both "Not now" and "Enable") is persisted to `localStorage` so the banner never re-appears. The `usePushNotifications` hook (`src/hooks/usePushNotifications.ts`) subscribes via `PushManager` and POSTs the subscription to `POST /api/pushsubscriptions`.
- **Item autocomplete:** `<ItemNameAutocomplete>` (`src/components/ItemNameAutocomplete.tsx`) wraps `AsyncCreatableSelect` with a controlled `inputValue` state. Typed text is propagated to the parent on every keystroke via `onInputChange` (action `"input-change"` only — other actions like `menu-close` are ignored to avoid wiping a confirmed selection). This means items can be submitted with just typed text without requiring a dropdown selection. A `useEffect` on `value` clears the internal input when the parent resets to `""` after a successful add. The component is a `forwardRef` exposing a `focus()` handle so the parent can return focus to the input after submission.
- **Ingredient scaling:** `src/utils/ingredientScaling.ts` — `parseQuantity` tries patterns in priority order: range → mixed number (`1 1/2`) → pure fraction (`3/2`) → whole+unicode → unicode → decimal/whole. Pure fraction is tried before whole number so `"3/2 cups"` parses as `1.5` cups, not `3` with unit `"/2 cups"`. `transformQuantity` always runs at any scale factor, normalising stored improper fractions to unicode mixed-number display. Tests in `src/utils/ingredientScaling.test.ts`.
- **Recipe image search:** `<ImageSearchModal>` (`src/components/ImageSearchModal.tsx`) — modal with auto-search on open, search bar, 3-column thumbnail grid, Escape/backdrop close. Opened from a "🔍 Find image" button in `RecipeDetailPage` edit mode; selecting a thumbnail fills the image URL field. Requires a Brave Search API key configured in Admin → LLM Settings (`ImageSearchApiKey` column). Backend: `GET /api/imagesearch?q=...` (`ImageSearchController`) proxies to `https://api.search.brave.com/res/v1/images/search` with `X-Subscription-Token` header; returns 503 if key not set.
- **Families page:** Non-owner members can now see (read-only) families they belong to. `FamiliesController.GetFamilies` and `GetFamily` include families where the user is a member via `f.Members.Any(m => m.UserId == userId)`. Mutation endpoints (`PUT`, `DELETE`) remain owner-only.
- **Recipe tags:** User-scoped tags with a 10-colour palette (`red`, `orange`, `yellow`, `green`, `teal`, `blue`, `indigo`, `purple`, `pink`, `gray`). `RecipesPage` has a collapsible "Manage Tags" panel (CRUD) and OR-logic filter chips — a recipe matches if it has *any* of the selected tags. `RecipeDetailPage` shows tags in view mode and a colour-toggle picker in edit mode; `handleSave` syncs tags with parallel POST/DELETE calls after the recipe PUT, then derives `newTags` from local state to avoid an extra round-trip. `TAG_COLORS` constant and `getTagColor`/`TagChip` helpers are defined in both page files.
- **List templates:** `ShoppingList.IsTemplate` separates templates from active lists. The lists page shows a "Templates" section below active lists. "Use Template" opens a modal with two tabs — "Create new list" (POST `createfrom`) and "Add to existing list" (POST `appendto/{id}`). A toggle button on each template converts it to/from an active list via PUT. All recipe/meal-planner "add to list" dropdowns use `activeLists = lists.filter(l => !l.isTemplate)` to exclude templates.
- **Duplicate detection:** When `ItemsController.PostItem` succeeds, the response includes a duplicate flag if an item with the same name already exists on the list. The UI surfaces a warning to the user without blocking the add.

## Backend conventions

- **DTO pattern:** Controllers return DTOs (e.g., `ItemWithCategoryDto`), not EF models directly. Never return `ApplicationUser`-related navigation properties directly — they expose `PasswordHash`, `SecurityStamp`, etc.
- **JSON cycles:** `ReferenceHandler.IgnoreCycles` is applied to both MVC and SignalR serialization.
- **Migrations auto-apply:** `dbContext.Database.Migrate()` runs at startup, wrapped in a PostgreSQL session advisory lock (`pg_advisory_lock(887236419)`) so that multi-replica deployments serialise migrations without duplicate-run errors. The lock connection is separate from the EF context and is released in a `finally` block.
- **Classification config:** `ClassificationService:PredictUrl` in `appsettings.json`. Classification logic lives in `Services/ClassificationService` (injected as `IClassificationService`) — not inline in controllers.
- **Recipe scraper config:** `RecipeScraperService:ScrapeUrl`, `RecipeScraperService:ScrapeImageUrl`, and `RecipeScraperService:ParseUrl` in `appsettings.json`. `ParseUrl` is called by `RecipesController.ParseIngredientsAsync` during recipe edits to re-parse any ingredients whose text changed (identified by null `ParsedName`). `ScrapeImageUrl` is called by `POST /api/recipes/import-image`; returns 503 if not configured (i.e. LLM not set up).
- **Cookie persistence:** Google OAuth sign-in uses `isPersistent: true` in `ExternalLogin.cshtml.cs` (both returning-user and first-registration paths) — overrides the scaffolded default of `false`.
- **Admin controller:** `AdminController` is gated with `[Authorize(Roles = "Admin")]`. Endpoints: `GET/PUT /api/admin/llm-settings` (includes `ImageSearchApiKey` — masked in GET responses, only overwritten when a non-masked value is sent); `GET /api/admin/users`, `PUT /api/admin/users/{id}/role`, `DELETE /api/admin/users/{id}`; `GET /api/admin/families`, `DELETE /api/admin/families/{familyId}/members/{memberId}`, `DELETE /api/admin/families/{familyId}`; `GET /api/admin/classification-feedback`, `DELETE /api/admin/classification-feedback/{id:guid}`. A user cannot change or delete their own account via these endpoints (guarded by current-user check).
- **Image search:** `ImageSearchController` (`GET /api/imagesearch?q=...`) is `[Authorize]` (any logged-in user). Reads `LlmSettings.ImageSearchApiKey` from the DB; returns 503 if absent. Calls Brave Search API v1 images endpoint with `X-Subscription-Token` header, maps `results[].properties.url` → `ImageUrl` and `results[].thumbnail.src` → `ThumbnailUrl`. Free tier: 2,000 calls/month at [api.search.brave.com](https://api.search.brave.com).
- **Push notifications:** `PushNotificationService` (`Services/PushNotificationService.cs`) implements `IPushNotificationService` with three methods: `NotifyItemAddedAsync` (fired from `ItemsController.PostItem` as a background task), `NotifyListSharedAsync` (fired from `ListSharesController.PostListShare`), and `NotifyRecipeSharedAsync` (fired from `RecipeSharesController.PostRecipeShare`). For family shares, each family member receives an individual notification. VAPID keys are **nullable** — the service silently no-ops when `VapidSettings:PublicKey` / `VapidSettings:PrivateKey` are empty; this prevents startup crashes in dev environments without keys configured. Stale subscriptions (HTTP 410/404 from the push service) are auto-removed. VAPID config: `VapidSettings:Subject`, `VapidSettings:PublicKey`, `VapidSettings:PrivateKey` in `appsettings.json`; use `VapidSettings__PublicKey` / `VapidSettings__PrivateKey` env vars in Docker. Rotate keys for production. **Do not notify on item check/uncheck** — this was explicitly rejected; only item-added and share events should trigger notifications.
- **Shared resource access:** `GetAccessibleXxxIdsAsync(userId)` (implemented per-controller) unions own IDs + user-share IDs + family-share IDs into a `HashSet<Guid>`. This pattern is used by `ItemsController`, `RecipesController`, and `MealPlansController`; apply it to any new shareable resource. Read/add/update endpoints use this set; delete remains owner-only. `ItemsController` uses this so shared users can fully interact with list items — `GetItems`, `GetItem`, `PostItem`, `PutItem`, and `DeleteItem` all filter via `GetAccessibleListIdsAsync`. `PutItem` also validates that the *target* list (when moving an item) is in the accessible set. **Note:** `ListShare.ListId` is the FK property name (not `ShoppingListId`) — use `s.ListId` not `s.ShoppingListId` when querying `ListShares`.
- **`IsOwned` in DTOs:** When a resource can be shared, include `bool IsOwned` in its detail and summary DTOs (computed as `entity.UserId == userId` in the controller). The frontend uses this to conditionally show owner-only controls (delete, share management).
- **Ingredient parsing:** `RecipeIngredient` stores `ParsedName` (nullable, max 300) and `ParsedQuantity` (nullable, max 100). The scraper service uses `ingredient-parser-nlp` to populate these; quantities are stored as mixed-number strings (`"1 1/2"`) via `_format_quantity()` — never as improper fractions. When adding ingredients to a shopping list, `ParsedName ?? Text` is used for classification and the item name; `ParsedQuantity` maps to `Item.Quantity`. `RecipeDetailPage` always passes `parsedQuantity` through `transformQuantity` (at scale 1×) so any legacy improper-fraction values normalise on display.
- **`DateOnly` in EF/API:** `MealPlanEntry.PlannedDate` uses `DateOnly`, which EF Core 9 + Npgsql maps to PostgreSQL `date`. System.Text.Json in .NET 8 serialises `DateOnly` as `"YYYY-MM-DD"` without a custom converter.
- **Share controllers:** `ListSharesController`, `RecipeSharesController`, and `MealPlanSharesController` all use a private `ApplyShareTargetAsync` helper to set `UserId` or `FamilyId` on the share entity (including self-share guard and existence checks). Apply this pattern to any future share controller to keep cognitive complexity within SonarCloud limits.
- **`QuantityHelper`** (`Services/QuantityHelper.cs`) — internal static class for merging quantity strings when deduplicating items added from recipes. `Merge(existing, incoming)` parses both quantities, adds amounts if units match, otherwise concatenates with ` + `. The regex uses a `(?!/)` negative lookahead so slash fractions like `"1/4 tsp"` parse as `0.25` (not `whole=1, unit="/4 tsp"`). `[assembly: InternalsVisibleTo("Nimblist.test")]` in `nimblist.api/AssemblyInfo.cs` exposes it to the test project.
- **Account management:** `DELETE /api/auth/account` cancels any active PayPal subscription, signs the user out, then hard-deletes the `ApplicationUser` (cascading to all owned data). `GET /api/auth/export` returns a JSON file attachment containing the user's lists, items, recipes, meal plans, and subscription status — used for GDPR data portability.
- **Subscription emails:** `ISubscriptionEmailService` / `SubscriptionEmailService` (`Services/SubscriptionEmailService.cs`) wraps `IEmailSender` (Resend) with four methods: `SendWelcomeAsync`, `SendSubscriptionActivatedAsync`, `SendPaymentFailedAsync`, `SendSubscriptionCancelledAsync`. All sends are fire-and-forget (failures are logged, not thrown). Called from `SubscriptionController` (activate/cancel) and `PayPalWebhookController` (webhook events). Resend config: `Resend:ApiKey` and `Resend:FromAddress` in `appsettings.json`.
- **Legal pages:** `/privacy` (`PrivacyPolicyPage.tsx`) and `/terms` (`TermsOfServicePage.tsx`) are public routes (no `<ProtectedRoute>`). Links to both appear at the bottom of `BillingPage.tsx`.
- **Cookie security:** The application cookie explicitly sets `Cookie.SecurePolicy = CookieSecurePolicy.Always` and `Cookie.SameSite = SameSiteMode.Strict` in `Program.cs` — do not remove these; they ensure the `Secure` flag is set regardless of request scheme.
- **Image search:** Brave Search API only accepts `safesearch=off` or `safesearch=strict` — `safesearch=moderate` returns 422. `ImageSearchController` uses `safesearch=strict`.
- **SonarCloud:** Project is `tmnrtn/nimblist` on SonarCloud. An MCP server is configured on `lxc-mcp.lan:8086/mcp` (add via `claude mcp add --transport http sonarcloud http://lxc-mcp.lan:8086/mcp`).
- **Homelab repo:** Infrastructure and MCP server documentation lives at `C:\Users\mail\source\repos\homelab` (GitHub: `tmnrtn/homelab`, private). MCP server connection details and tokens are documented in `docs/mcp-servers.md`.

---

## Classification service

### Inference (app.py)

`clean_text()` and `_lemmatize_word()` in `app.py` define the **shared preprocessing contract** — they must stay byte-for-byte identical to the copies in `scripts/retrain.py`. Any change to preprocessing requires updating both files and retraining the models.

Preprocessing pipeline (order matters):
1. Lowercase
2. Strip size/quantity tokens before punctuation removal (so word boundaries work): `500g`, `2l`, `750ml`, `6 pack`, `pack of 12`, `x4`, `4 x 500g`
3. Remove punctuation
4. Normalise whitespace
5. Lemmatize: `eggs→egg`, `tomatoes→tomato`, `berries→berry`, `loaves→loaf`; skips `ss`/`us`/`is` endings and words ≤ 2 chars — no external dependencies

**Confidence threshold:** The service uses `predict_proba` rather than `predict`. If the top class probability is below `PRIMARY_CONFIDENCE_THRESHOLD` (default `0.35`, configurable via env var), `predicted_primary_category` is returned as `null` — same for `SUB_CONFIDENCE_THRESHOLD`. This prevents confidently-wrong classifications for short/ambiguous inputs. The C# `ClassificationService` already handles null via `string.IsNullOrEmpty` — no sentinel strings (`"Unknown"`, `"N/A"`, `"No Sub-Model"`) are returned any more.

### Retraining (scripts/)

**`scripts/retrain.py`** — retrains all models and saves `.joblib` files directly into `src/nimblist/Nimblist.classification/`. Commit and push the updated `.joblib` files to deploy improved models (they bake into the Docker image at build time).

Improvements over the original notebooks:
- `sublinear_tf=True` and `max_features=15000` (primary); `max_features=5000` (sub-models, less data)
- **Data augmentation:** for each cleaned product name, generates up to 2 left-truncated shorter versions (`organic whole milk` → `whole milk`, `milk`) so the model trains on user-input-length examples
- **Two-pass training:** evaluation pass (vectorizer fit on 80% train split → honest accuracy metrics); deployment pass (vectorizer fit on all data → maximum vocabulary coverage)
- **Feedback integration:** rows from `GET /api/classificationfeedback/export` are merged with the base CSV and oversampled (default 5×) as verified ground truth

```bash
# Retrain on base data only
python scripts/retrain.py

# Fetch feedback first, then retrain with it
python scripts/fetch_feedback.py --api-url https://nimblist.tmnrtn.com --cookie <session-cookie-value>
python scripts/retrain.py --feedback feedback.jsonl

# After retraining, commit the updated .joblib files and push to deploy
git add src/nimblist/Nimblist.classification/*.joblib src/nimblist/Nimblist.classification/sub_category_models/*.joblib
git commit -m "Retrain classification models"
git push
```

**`scripts/fetch_feedback.py`** — fetches `/api/classificationfeedback/export` using a browser session cookie (copy `.AspNetCore.Identity.Application` from DevTools → Application → Cookies after logging in) and writes `feedback.jsonl`.

**`combined_cleaned.csv`** lives at `scripts/ClassificationModel/combined_cleaned.csv` (tracked in git, ~9 MB). The retrain script reads it but never writes to it — feedback data is merged in memory only.

---

## Production Hosting Plan

The intended production hosting target is **DigitalOcean** using a **Docker Swarm** deployment, following a phased approach:

### Stage 1 — Single Droplet (starting point)
- One DO Droplet (2 vCPU / 4GB, ~$24/mo) running Swarm in single-manager mode
- All services deployed as a Swarm stack, including Postgres and Redis on named volumes
- Postgres pinned to the manager node via a placement constraint to avoid data loss if workers are added later
- Existing Docker Compose files adapted to a stack file; existing webhook auto-deploy updated to use `docker service update --image ...` instead of `docker compose pull`
- Secrets (VAPID keys, OAuth credentials, LLM API keys) stored as Docker secrets, not env files

### Stage 2 — Multi-node + managed services
- Add a second Droplet and join it to the Swarm (`docker swarm join`)
- Migrate Postgres to DO Managed Database and Redis to DO Managed Redis before adding the second node — this removes the stateful-volume problem
- Add a DO Load Balancer ($12/mo) in front; enable sticky sessions or rely on the existing Redis SignalR backplane (already in place)
- Scale replicas: `nimblist-api` × 2, `Nimblist.recipescraper` × 3 (the bottleneck service), `Nimblist.classification` × 2

### Stage 3 — Further scaling (if needed)
- Add more Droplets as workers; Swarm handles placement automatically
- If operational complexity grows beyond what Swarm can handle cleanly, migrate to DO Managed Kubernetes (DOKS)

### Key production config changes required
- `CorsSettings:AllowedOrigins` must be updated to the production domain
- VAPID keys should be rotated for production (`VapidSettings:PublicKey` / `VapidSettings:PrivateKey`)
- `Cookie.SecurePolicy = Always` and `SameSite = Strict` are already set — do not remove them
- A DO Container Registry (~$5/mo) should hold production images; CI pushes on merge to main

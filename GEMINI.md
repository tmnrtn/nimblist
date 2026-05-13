# Nimblist: Project Instructions

Nimblist is a collaborative shopping list application built with a polyglot architecture. It features a React frontend, an ASP.NET Core backend, and specialized Python services for machine learning-based item classification and recipe scraping.

## Architecture Overview

The project is structured as a monorepo under `src/nimblist/`:

- **`nimblist.api`**: ASP.NET Core 8 REST API and Razor Pages (for Identity UI). This is the main backend entry point.
- **`Nimblist.data`**: EF Core 9 data access layer, including models, migrations, and seed data.
- **`Nimblist.Frontend`**: React 19 + TypeScript SPA powered by Vite and Tailwind CSS 4.
- **`Nimblist.classification`**: Python Flask service that classifies item names using pre-trained Logistic Regression models.
- **`Nimblist.recipescraper`**: Python Flask service that scrapes recipes from URLs and parses ingredients using NLP.
- **`Nimblist.test`**: Backend test suite using xUnit and Moq.

### Data Flow
`React SPA (:5173)` -> `ASP.NET API (:64212)` -> `PostgreSQL (:5433)`
- Real-time updates: SignalR via Redis pub/sub.
- Classification: API calls Python service (`:5000`) on item creation/update.
- Scraper: API calls Python service (`:5001`) for recipe imports.

## Building and Running

### Full Stack (Docker)
Run the entire stack using Docker Compose from the repository root:
```powershell
docker compose -f src/nimblist/docker-compose.yml -f src/nimblist/docker-compose.override.yml up
```

### Backend (.NET)
- **Build**: `dotnet build src/nimblist/nimblist.sln`
- **Test**: `dotnet test src/nimblist/nimblist.sln`
- **Migrations**: (Run from `nimblist.api`)
  ```powershell
  dotnet ef migrations add <Name> --project "../nimblist.data/Nimblist.data.csproj"
  dotnet ef database update
  ```

### Frontend (React)
Navigate to `src/nimblist/Nimblist.Frontend/`:
- **Dev**: `npm run dev` (Default: `https://localhost:5173`)
- **Build**: `npm run build`
- **Lint**: `npm run lint`
- **Test**: `npm run test`

### Python Services
Navigate to the respective service directory (`Nimblist.classification` or `Nimblist.recipescraper`):
- **Install**: `pip install -r requirements.txt`
- **Run**: `python app.py`
- **Update Dependencies**: Edit `requirements.txt` and regenerate `requirements.lock` via:
  ```powershell
  pip-compile requirements.txt --output-file requirements.lock --generate-hashes
  ```

## Development Conventions

### Backend
- **DTO Pattern**: Always return DTOs from controllers; never expose EF models or `ApplicationUser` directly.
- **Data Integrity**: Use `DateOnly` for dates (mapped to Postgres `date`).
- **Shared Access**: Use `GetAccessibleXxxIdsAsync` pattern for resources shareable with families/users.
- **Auth**: Secured via ASP.NET Core Identity with cookie-based authentication. API routes return 401/403 instead of redirects.

### Frontend
- **State Management**: Use Zustand for global auth state; local state for components.
- **HTTP**: Use `authenticatedFetch` wrapper for all API calls (ensures credentials and base URL).
- **Styling**: Tailwind CSS 4 (via `@tailwindcss/vite`).
- **Real-time**: Utilize the `useShoppingListHub` hook for SignalR lifecycle management.

### Python
- **Environment**: Python 3.11-slim.
- **Lockfiles**: Use `requirements.lock` with hashes for Docker builds to ensure reproducible environments.

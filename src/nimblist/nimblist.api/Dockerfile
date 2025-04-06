# Dockerfile located in nimblist.api/
# Build Context should be the SOLUTION ROOT ('..')

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# --- Build Stage ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Set encoding (optional but good practice)
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8

# Copy solution file and project files first to leverage layer cache
# --- Corrected Paths Relative to Build Context Root ---
COPY ["src/nimblist/nimblist.sln", "src/nimblist/"]
COPY ["src/nimblist/nimblist.api/Nimblist.api.csproj", "src/nimblist/nimblist.api/"]
COPY ["src/nimblist/nimblist.data/Nimblist.data.csproj", "src/nimblist/nimblist.data/"]
# --- End Corrected Paths ---

# Restore dependencies for the solution, using its path inside the container
# Note: WORKDIR is /src, so the path becomes relative to that
RUN dotnet restore "src/nimblist/nimblist.sln"

# Copy the rest of the source code from the context root into the container's /src
# This replicates your repo structure inside /src (e.g., /src/src/nimblist/...)
COPY . .

# Publish the API project, using its full path within the container relative to WORKDIR (/src)
RUN dotnet publish "./src/nimblist/nimblist.api/Nimblist.api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# --- Final Stage ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Optional: USER instruction might be here or inherited from 'base' stage
# USER app
EXPOSE 8080  # Likely around line 41
EXPOSE 8081  # Likely around line 42
ENTRYPOINT ["dotnet", "Nimblist.api.dll"] # Likely around line 43
# Dockerfile located in nimblist.api/
# Build Context should be the SOLUTION ROOT ('..')

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Dockerfile located in nimblist.api/
# Build Context = Solution Root (.)

# --- Build Stage ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8

# Copy solution and ALL necessary project files first
# Paths are relative TO THE BUILD CONTEXT (solution root)
# Destination paths are relative TO THE WORKDIR (/src)
COPY ["src/nimblist/nimblist.sln", "src/nimblist/"]
COPY ["src/nimblist/nimblist.api/Nimblist.api.csproj", "src/nimblist/nimblist.api/"]
COPY ["src/nimblist/nimblist.data/Nimblist.data.csproj", "src/nimblist/nimblist.data/"]
# --- ADD COPY FOR TEST PROJECT ---
COPY ["src/nimblist/Nimblist.test/Nimblist.test.csproj", "src/nimblist/Nimblist.test/"]
# --- (Optional: Copy dcproj if restore *really* needs it, but likely not) ---
COPY ["src/nimblist/docker-compose.dcproj", "src/nimblist/"]

# Restore dependencies for the solution, using its path inside the container
RUN dotnet restore "src/nimblist/nimblist.sln"

# Copy the rest of the source code
COPY . .

# Publish the API project
# Path is relative to WORKDIR /src
RUN dotnet publish "./src/nimblist/nimblist.api/Nimblist.api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# --- Final Stage ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
EXPOSE 8081
ENTRYPOINT ["dotnet", "Nimblist.api.dll"]
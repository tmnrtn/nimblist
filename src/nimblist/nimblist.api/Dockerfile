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
# Paths below are relative TO THE BUILD CONTEXT (which should be the solution root)
COPY ["nimblist.sln", "./"]
COPY ["nimblist.api/Nimblist.api.csproj", "nimblist.api/"]
COPY ["nimblist.data/Nimblist.data.csproj", "nimblist.data/"]
# Add COPY lines for any other dependent project files (e.g., test projects aren't usually needed for build/publish)

# Restore dependencies for the *entire solution* using the .sln file
RUN dotnet restore "./nimblist.sln"

# Copy the rest of the source code from the build context (solution root) into /src
# This ensures all source code (api, data, etc.) is available for the build/publish step
COPY . .

# Publish the API project, specifying the project file path within /src
WORKDIR "/src/nimblist.api" # Change context to where the API project is
RUN dotnet publish "./Nimblist.api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# --- Final Stage ---
# Use the base image alias from the top (or directly use aspnet image)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Optional: Set non-root user if needed AFTER files are copied
# USER $APP_UID
EXPOSE 8080
EXPOSE 8081
ENTRYPOINT ["dotnet", "Nimblist.api.dll"]

# Note: Removed intermediate 'publish' stage from your original file as it's redundant.
# Removed the initial 'base' stage definition unless you need it for multi-stage debugging setup in VS.
# Using 'final' alias for clarity.
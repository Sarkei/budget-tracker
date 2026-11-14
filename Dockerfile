# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
ARG BUILD_CONFIGURATION=Release
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV NUGET_XMLDOC_MODE=skip

# Copy project file and restore dependencies first (layer cache optimization)
COPY ["BudgetTracker.csproj", "."]
RUN dotnet restore "BudgetTracker.csproj" --disable-parallel

# Copy everything else and publish (includes wwwroot + _content static assets)
COPY . .
# NAS-safe publish with reduced memory pressure.
RUN dotnet publish "BudgetTracker.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore -v minimal "-clp:ErrorsOnly;Summary" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -p:UseRazorBuildServer=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app
COPY --from=build /app/publish .

# Data directory for SQLite database (mount as Docker volume)
RUN mkdir -p /app/data

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:11111
ENV DATA_PATH=/app/data

EXPOSE 11111

ENTRYPOINT ["dotnet", "BudgetTracker.dll"]

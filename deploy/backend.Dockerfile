# One build, three images: the API, the worker and the sandbox runner share the same code.
# Build context is the repository root, e.g.:
#   docker build -f deploy/backend.Dockerfile --target api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/ src/
RUN dotnet publish src/FileForge.Api/FileForge.Api.csproj -c Release -o /out/api \
 && dotnet publish src/FileForge.Worker/FileForge.Worker.csproj -c Release -o /out/worker \
 && dotnet publish src/FileForge.SandboxRunner/FileForge.SandboxRunner.csproj -c Release -o /out/runner

# The storage volume is mounted here. Creating the folder in the image with the right owner
# makes Docker initialise the (empty) volume with that owner, so the non-root app user can write.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS api
WORKDIR /app
RUN mkdir -p /data/storage && chown "$APP_UID" /data/storage
COPY --from=build /out/api .
USER $APP_UID
ENTRYPOINT ["dotnet", "FileForge.Api.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS worker
WORKDIR /app
RUN mkdir -p /data/storage && chown "$APP_UID" /data/storage
COPY --from=build /out/worker .
USER $APP_UID
ENTRYPOINT ["dotnet", "FileForge.Worker.dll"]

# Runs as root: it needs the Docker socket. That is exactly why it is a separate, tiny service
# with no database access and no public port (see docs/decisions.md).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runner
WORKDIR /app
RUN apk add --no-cache docker-cli \
 && mkdir -p /data/storage /data/work \
 && chown "$APP_UID" /data/storage
COPY --from=build /out/runner .
ENTRYPOINT ["dotnet", "FileForge.SandboxRunner.dll"]

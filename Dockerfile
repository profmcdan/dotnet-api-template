# syntax=docker/dockerfile:1.7

# One multi-stage Dockerfile builds all three services. PROJECT selects which one, so the
# restore and build layers are shared across images instead of repeated per service.
ARG DOTNET_VERSION=10.0

# ---------------------------------------------------------------------------------------------
# Restore - only the project files are copied first, so a source-only change does not invalidate
# the (slow) NuGet restore layer.
# ---------------------------------------------------------------------------------------------
# Debian-based SDK, not Alpine: Grpc.Tools ships a glibc-linked protoc with no musl build, so
# an Alpine SDK cannot compile .proto files. The publish output is portable IL either way, so
# the runtime stage below is still Alpine.
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS restore
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/CleanArchTemplate.Domain/CleanArchTemplate.Domain.csproj             src/CleanArchTemplate.Domain/
COPY src/CleanArchTemplate.Contracts/CleanArchTemplate.Contracts.csproj       src/CleanArchTemplate.Contracts/
COPY src/CleanArchTemplate.Application/CleanArchTemplate.Application.csproj   src/CleanArchTemplate.Application/
COPY src/CleanArchTemplate.Infrastructure/CleanArchTemplate.Infrastructure.csproj src/CleanArchTemplate.Infrastructure/
COPY src/CleanArchTemplate.Api/CleanArchTemplate.Api.csproj                   src/CleanArchTemplate.Api/
COPY src/CleanArchTemplate.Migrator/CleanArchTemplate.Migrator.csproj         src/CleanArchTemplate.Migrator/
COPY src/CleanArchTemplate.Worker/CleanArchTemplate.Worker.csproj             src/CleanArchTemplate.Worker/

ARG PROJECT=src/CleanArchTemplate.Api/CleanArchTemplate.Api.csproj
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "${PROJECT}"

# ---------------------------------------------------------------------------------------------
# Publish
# ---------------------------------------------------------------------------------------------
FROM restore AS publish
WORKDIR /src

COPY src/ src/

ARG PROJECT=src/CleanArchTemplate.Api/CleanArchTemplate.Api.csproj
ARG BUILD_CONFIGURATION=Release
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "${PROJECT}" \
        --configuration "${BUILD_CONFIGURATION}" \
        --no-restore \
        --output /app/publish \
        /p:UseAppHost=false

# ---------------------------------------------------------------------------------------------
# Runtime - Alpine keeps a shell available for container health checks while staying small.
# ---------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine AS final

# The .NET images already ship an unprivileged `app` user; curl is needed by the compose
# health checks, icu-libs by anything that opts out of invariant globalization.
RUN apk add --no-cache curl icu-libs

WORKDIR /app

# Owned by root and read-only to `app`: the process can execute its own code but not rewrite it.
COPY --from=publish --chown=root:root --chmod=555 /app/publish ./

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_gcServer=1

EXPOSE 8080

# Never root. Nothing under /app is writable, which is what makes `read_only: true` workable
# in compose - see the tmpfs mount there for scratch space.
USER app

ARG ENTRY_DLL=CleanArchTemplate.Api.dll
ENV ENTRY_DLL=${ENTRY_DLL}

ENTRYPOINT ["/bin/sh", "-c", "exec dotnet /app/${ENTRY_DLL} \"$@\"", "--"]

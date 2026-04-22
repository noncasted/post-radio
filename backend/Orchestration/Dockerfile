# syntax=docker/dockerfile:1.7-labs
#
# Shared multi-stage Dockerfile for the post-radio production .NET services.
# Optimised for Coolify/BuildKit:
#   - restore stage only copies .csproj / .slnx / Directory.*.props so that a code edit
#     does not invalidate the NuGet cache layer.
#   - publish-all stage publishes every service sequentially inside one stage — msbuild
#     incremental output is reused for shared projects, so each subsequent publish only
#     compiles its own entry-point csproj.
#   - runtime stages just copy their service's publish output, selected via ASSEMBLY_NAME.

ARG DOTNET_VERSION=10.0
# SDK 10.0.202 in the latest tag was producing publish output without
# wwwroot/_framework/blazor.web.js for Web SDK projects after the first one
# in publish-all (Blazor console got 404 on its own framework JS).
# Pin a known-good tag until the regression is bisected.
ARG DOTNET_SDK_TAG=10.0.201

# Stage 1: restore. Cache key = project/graph metadata only.
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_TAG} AS restore
WORKDIR /src

# `--parents` preserves source directory layout so restore paths match the full repo.
COPY --parents \
    backend/post-radio.slnx \
    **/Directory.*.props \
    **/*.csproj \
    ./

RUN dotnet restore backend/Orchestration/Silo/Silo.csproj \
 && dotnet restore backend/Orchestration/Coordinator/Coordinator.csproj \
 && dotnet restore backend/Orchestration/MetaGateway/MetaGateway.csproj \
 && dotnet restore backend/Frontend/Server/Server.csproj \
 && dotnet restore backend/Orchestration/ConsoleGateway/ConsoleGateway.csproj \
 && dotnet restore backend/Tools/DeploySetup/DeploySetup.csproj

# Stage 2: publish every service inside a single RUN so msbuild's incremental compilation
# reuses shared project outputs across services.
FROM restore AS publish-all
ARG CONFIGURATION=Release

COPY . .

RUN set -e; \
    for pair in \
      "backend/Orchestration/Silo/Silo.csproj:Silo" \
      "backend/Orchestration/Coordinator/Coordinator.csproj:Coordinator" \
      "backend/Orchestration/MetaGateway/MetaGateway.csproj:MetaGateway" \
      "backend/Frontend/Server/Server.csproj:Frontend.Server" \
      "backend/Orchestration/ConsoleGateway/ConsoleGateway.csproj:ConsoleGateway" \
      "backend/Tools/DeploySetup/DeploySetup.csproj:DeploySetup"; do \
      proj=${pair%:*}; name=${pair##*:}; \
      # Wipe StaticWebAssets incremental tracking so each publish actually
      # copies wwwroot/_framework/* into its output. Without this, only the
      # first Web SDK service in the loop gets blazor.web.js/dotnet.js on disk
      # and every later one ends up with a manifest pointing at missing files
      # (404 at runtime in the Blazor console).
      find /src -path "*/obj/*" \( -name "staticwebassets*.json" -o -name "*.StaticWebAssets.Up2Date" \) -delete 2>/dev/null || true; \
      dotnet publish "$proj" \
        -c "${CONFIGURATION}" \
        -o "/app/out/${name}" \
        --no-restore \
        /p:UseAppHost=false; \
    done

# Stage 3: lean runtime. Compose selects service via build-arg ASSEMBLY_NAME.
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
ARG ASSEMBLY_NAME

RUN apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app

RUN test -n "${ASSEMBLY_NAME}" || (echo "ASSEMBLY_NAME build-arg is required" && exit 1)

COPY --from=publish-all /app/out/${ASSEMBLY_NAME}/ .
COPY --from=publish-all /src/tools/metadata/ ./tools/metadata/

RUN test -f ./tools/metadata/songs.json || (echo "tools/metadata/songs.json is required in runtime image" && exit 1)

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

ENV TARGET_DLL=${ASSEMBLY_NAME}.dll
ENTRYPOINT ["sh", "-c", "exec dotnet $TARGET_DLL"]

#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/../.."

rm -rf publish
mkdir -p publish

publish() {
    local name=$1
    local csproj=$2
    echo "==> Publishing $name"
    dotnet publish "$csproj" -c Release -o "publish/$name" /p:UseAppHost=false --nologo -v q
}

publish Silo           backend/Orchestration/Silo/Silo.csproj
publish Coordinator    backend/Orchestration/Coordinator/Coordinator.csproj
publish MetaGateway    backend/Orchestration/MetaGateway/MetaGateway.csproj
publish GameGateway    backend/Orchestration/GameGateway/GameGateway.csproj
publish ConsoleGateway backend/Orchestration/ConsoleGateway/ConsoleGateway.csproj
publish DeploySetup    backend/Tools/DeploySetup/DeploySetup.csproj

echo
echo "Done. Artefacts in ./publish/"
du -sh publish/*

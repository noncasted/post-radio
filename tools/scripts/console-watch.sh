#!/bin/bash
# Start ConsoleGateway with hot reload (dotnet watch)
# Requires: Aspire cluster already running (aspire-start.sh)
# Reloads on .razor, .cs, .css changes in Console and ConsoleGateway projects
dotnet watch run --project backend/Orchestration/ConsoleGateway/ConsoleGateway.csproj --launch-profile http

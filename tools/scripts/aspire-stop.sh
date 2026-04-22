#!/bin/bash
# Stop all Aspire/dotnet processes
pkill -f "dotnet.*Aspire" 2>/dev/null
pkill -f "ConsoleGateway" 2>/dev/null
pkill -f "MetaGateway" 2>/dev/null
pkill -f "GameGateway" 2>/dev/null
pkill -f "Coordinator" 2>/dev/null
pkill -f "Silo" 2>/dev/null
echo "Aspire stopped"

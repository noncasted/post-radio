#!/bin/bash
# Run a benchmark or group
# Usage:
#   tools/benchmark-run.sh group Messaging        -- run entire group
#   tools/benchmark-run.sh single "benchmark name" -- run single benchmark

MODE="${1:-group}"
TARGET="${2:-Messaging}"

if [ "$MODE" = "group" ]; then
  curl -s -X POST "http://localhost:5000/api/benchmarks/group/${TARGET}/run" --max-time 600
elif [ "$MODE" = "single" ]; then
  ENCODED=$(python3 -c "import urllib.parse; print(urllib.parse.quote('${TARGET}'))")
  curl -s -X POST "http://localhost:5000/api/benchmarks/${ENCODED}/run" --max-time 120
fi

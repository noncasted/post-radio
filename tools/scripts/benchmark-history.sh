#!/bin/bash
# Get benchmark history
# Usage:
#   tools/benchmark-history.sh group Messaging        -- group history
#   tools/benchmark-history.sh single "benchmark name" -- single benchmark history

MODE="${1:-group}"
TARGET="${2:-Messaging}"

if [ "$MODE" = "group" ]; then
  curl -s "http://localhost:5000/api/benchmarks/group/${TARGET}/history"
elif [ "$MODE" = "single" ]; then
  ENCODED=$(python3 -c "import urllib.parse; print(urllib.parse.quote('${TARGET}'))")
  curl -s "http://localhost:5000/api/benchmarks/${ENCODED}/history"
fi

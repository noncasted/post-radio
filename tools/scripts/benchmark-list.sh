#!/bin/bash
# List all benchmarks or by group
# Usage:
#   tools/benchmark-list.sh            -- list all
#   tools/benchmark-list.sh Messaging  -- list group

GROUP="$1"

if [ -z "$GROUP" ]; then
  curl -s http://localhost:5000/api/benchmarks
else
  curl -s "http://localhost:5000/api/benchmarks/group/${GROUP}"
fi

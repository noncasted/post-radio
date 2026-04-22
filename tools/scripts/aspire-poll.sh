#!/bin/bash
# Poll until ConsoleGateway API is ready (up to 2 minutes)
for i in $(seq 1 24); do
  code=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/api/benchmarks 2>/dev/null)
  if [ "$code" = "200" ]; then
    echo "READY at attempt $i"
    exit 0
  fi
  sleep 5
done
echo "TIMEOUT"
exit 1

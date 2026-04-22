#!/bin/bash
# Converts ALL TestResults/*.log files from UTF-16LE to UTF-8.
# Creates sibling *.utf8.log files readable by Claude Code.
# xUnit v3 writes logs in UTF-16LE — Read tool cannot parse them directly.

echo "Searching for test log files..."

find . -name "*.log" -path "*/TestResults/*" -type f ! -name "*.utf8.log" | while read -r log_file; do
    encoding=$(file -bi "$log_file" | grep -o "charset=[^ ]*" | cut -d= -f2)

    if [[ "$encoding" == "utf-16le" ]]; then
        utf8_file="${log_file%.log}.utf8.log"

        if [ ! -f "$utf8_file" ] || [ "$log_file" -nt "$utf8_file" ]; then
            echo "Converting: $log_file -> ${utf8_file##*/}"
            iconv -f UTF-16LE -t UTF-8 -c "$log_file" > "$utf8_file" 2>/dev/null
        fi
    fi
done

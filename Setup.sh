#!/usr/bin/env bash
set -eu
script_dir="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
exec python3 "$script_dir/scripts/setup_proton.py" "$@"

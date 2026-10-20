#!/usr/bin/env bash
# Compares the 12.3.0 release (MarkView123.Benchmarks, NuGet) with the current sources (MarkView.Benchmarks), then
# measures what each opt-in or opt-out feature of the current sources costs. One process per scenario; the two
# builds alternate, and swap order on every repeat, so that a noisy machine affects both alike.
#
#   benchmarks/compare-releases.sh [out.tsv] [repeats] [part]   part: all (default), release or features
#
# Build both runners in Release first.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="${1:-compare.tsv}"
repeats="${2:-3}"
part="${3:-all}"
current="$here/MarkView.Benchmarks/bin/Release/net10.0/MarkView.Benchmarks"
release="$here/MarkView123.Benchmarks/bin/Release/net10.0/MarkView123.Benchmarks"

shared="Plain Supported Default Default+TextMate Default+Mermaid Default+All"
features="Default Default-Html Default-CodeToolbar Default-Rtl Lean Default+ExtAlerts Default+FoldHeadings Default+Tasks Default+LineNumbers Default+Thinking Plain Plain-CodeToolbar Plain-Rtl Plain-ContextMenu Plain-Lean"

echo -e "library\tscenario\tconfiguration\tsize\tsamples\tmedian_ms\tp95_ms\tmin_ms\tmedian_alloc_kb\textra\trepeat" > "$out"

run() { # runner scenario configuration size iterations repeat
  local line
  if line="$("$1" scenario "$2" "$3" "$4" "$5" 2>/dev/null)"; then
    echo -e "$line\t$6" >> "$out"
  else
    echo -e "$(basename "$1")\t$2\t$3\t$4\tFAILED\t\t\t\t\t\t$6" >> "$out"
  fi
}

both() { # scenario configuration size iterations repeat
  if (( $5 % 2 )); then run "$release" "$@"; run "$current" "$@"; else run "$current" "$@"; run "$release" "$@"; fi
}

for r in $(seq 1 "$repeats"); do
  if [[ "$part" != features ]]; then
    for c in $shared; do
      both load "$c" tiny 20 "$r"
      both load "$c" medium 10 "$r"
      both load "$c" long 6 "$r"
      both replace "$c" medium 10 "$r"
      both theme "$c" long 6 "$r"
      both memory "$c" tiny 1 "$r"
      both memory "$c" long 1 "$r"
    done
    for c in Default Default+All; do both scroll "$c" long 2 "$r"; done
    for c in Default Default+TextMate Default+All; do both stream "$c" medium 2 "$r"; done
  fi
  if [[ "$part" != release ]]; then
    for c in $features; do
      run "$current" load "$c" tiny 20 "$r"
      run "$current" load "$c" long 6 "$r"
      run "$current" memory "$c" long 1 "$r"
    done
    for c in Default Lean Default+FoldHeadings; do run "$current" theme "$c" long 6 "$r"; done
  fi
  echo "repeat $r done" >&2
done

#!/usr/bin/env bash
# Compares MarkView (current sources, and optionally an older commit) with the other Avalonia markdown viewers on the
# configurations that match (see the analysis): one process per scenario, the libraries' order rotating on every
# repeat so that a noisy machine affects them alike. Then the first document of a fresh process (probe).
#
#   benchmarks/compare-libraries.sh [out-prefix] [repeats]
#
# Build every runner in Release first (MarkViewBaseline.Benchmarks is skipped when it isn't built).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="${1:-libraries}"
repeats="${2:-3}"
bin() { echo "$here/$1/bin/Release/net10.0/$(basename "$1")"; }

# runner|matched configuration (code highlighting on)|other configurations
libraries=(
  "$(bin MarkView.Benchmarks)|Default+TextMate|Plain Default Default+All"
  "$(bin Competitors/LiveMarkdown.Benchmarks)|Default|Plain Default+All"
  "$(bin Competitors/MarkdownAvalonia.Benchmarks)|Default|"
)
if [[ -x "$(bin MarkViewBaseline.Benchmarks)" ]]; then
  libraries+=("$(bin MarkViewBaseline.Benchmarks)|Default+TextMate|Plain Default Default+All")
fi

echo -e "library\tscenario\tconfiguration\tsize\tsamples\tmedian_ms\tp95_ms\tmin_ms\tmedian_alloc_kb\textra\trepeat" > "$out.tsv"
run() { # runner scenario configuration size iterations repeat
  local line
  if line="$("$1" scenario "$2" "$3" "$4" "$5" 2>/dev/null)"; then
    echo -e "$line\t$6" >> "$out.tsv"
  else
    echo -e "$(basename "$1")\t$2\t$3\t$4\tFAILED\t\t\t\t\t\t$6" >> "$out.tsv"
  fi
}

count=${#libraries[@]}
for r in $(seq 1 "$repeats"); do
  for i in $(seq 0 $((count - 1))); do
    IFS='|' read -r runner matched others <<< "${libraries[$(( (i + r) % count ))]}"
    for c in $matched $others; do
      run "$runner" load "$c" tiny 20 "$r"
      run "$runner" load "$c" medium 10 "$r"
      run "$runner" load "$c" long 6 "$r"
      run "$runner" replace "$c" medium 10 "$r"
      run "$runner" replace "$c" long 4 "$r"
      run "$runner" theme "$c" long 6 "$r"
      run "$runner" scroll "$c" long 2 "$r"
      run "$runner" stream "$c" medium 2 "$r"
      run "$runner" memory "$c" medium 1 "$r"
      run "$runner" memory "$c" long 1 "$r"
    done
    run "$runner" load "$matched" large 2 "$r"
    run "$runner" memory "$matched" large 1 "$r"
  done
  echo "repeat $r done" >&2
done

: > "$out-probe.tsv"
for lib in "${libraries[@]}"; do
  IFS='|' read -r runner matched others <<< "$lib"
  "$runner" probe "$repeats" "$matched" tiny,medium,long >> "$out-probe.tsv" 2>/dev/null || echo "$(basename "$runner") probe FAILED" >> "$out-probe.tsv"
done
echo done >&2

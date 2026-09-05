#!/usr/bin/env bash
# Stop hook: blocks completion if the solution doesn't build, or if any C# file
# deviates from .editorconfig (dotnet format, cslint).
#
# The slow part (three `dotnet` invocations, ~12s) only runs when a file that
# actually affects the build or style has changed AND that exact tree state has
# not already passed. Docs-only turns, no-op turns, and re-runs on an unchanged
# tree short-circuit in well under a second.
set -o pipefail

cd "$(git rev-parse --show-toplevel)" || exit 2

[ -f Anywhere.slnx ] || exit 0

# Files whose contents can change a build or a style verdict.
globs=('*.cs' '*.csproj' '*.props' '*.targets' '.editorconfig' 'Anywhere.slnx' '.config/dotnet-tools.json')

# Fast path 1: nothing in that surface is dirty -> nothing to verify.
if [ -z "$(git status --porcelain -- "${globs[@]}" 2>/dev/null)" ]; then
  exit 0
fi

# Fast path 2: this exact tree state already passed on a previous Stop.
sig=$( { git diff HEAD -- "${globs[@]}" 2>/dev/null;
         git ls-files --others --exclude-standard -- "${globs[@]}" 2>/dev/null | xargs -r cat; } | sha1sum | cut -d' ' -f1)
cache="${TMPDIR:-/tmp}/anywhere-verify-before-stop.pass"
[ -f "$cache" ] && [ "$sig" = "$(cat "$cache" 2>/dev/null)" ] && exit 0

build_output=$(dotnet build Anywhere.slnx -clp:ErrorsOnly 2>&1 </dev/null)
build_status=$?
# `dotnet watch` (the user's live dev loop) holds the output exe/dll. MSB3021/
# MSB3026/MSB3027 copy locks are environmental, not a code defect -- if those are
# the only errors, compilation succeeded and the hook must not block on them.
if [ $build_status -ne 0 ]; then
  real=$(printf '%s\n' "$build_output" \
    | grep -E ': (error|Error) ' \
    | grep -Ev 'MSB302[1267]|being used by another process|locked by:')
  [ -z "$real" ] && build_status=0
fi

format_output=$(dotnet format Anywhere.slnx --verify-no-changes 2>&1 </dev/null)
format_status=$?

cslint_output=$(dotnet tool run cslint -- src --severity warning --exclude '**/Migrations/*.cs' 2>&1 </dev/null)
cslint_status=$?

if [ $build_status -ne 0 ] || [ $format_status -ne 0 ] || [ $cslint_status -ne 0 ]; then
  echo "Cannot report work as finished: verification failed." >&2
  if [ $build_status -ne 0 ]; then
    echo "--- dotnet build errors ---" >&2
    echo "$build_output" >&2
  fi
  if [ $format_status -ne 0 ]; then
    echo "--- dotnet format violations (.editorconfig) ---" >&2
    echo "$format_output" >&2
  fi
  if [ $cslint_status -ne 0 ]; then
    echo "--- cslint violations (.editorconfig) ---" >&2
    echo "$cslint_output" >&2
  fi
  exit 2
fi

printf '%s' "$sig" > "$cache"
exit 0

#!/usr/bin/env bash
# Stop hook: blocks completion if the solution doesn't build, or if any C# file
# deviates from .editorconfig (dotnet format, cslint).
set -o pipefail

cd "$(git rev-parse --show-toplevel)" || exit 2

if [ ! -f Anywhere.slnx ]; then
  exit 0
fi

build_output=$(dotnet build Anywhere.slnx -clp:ErrorsOnly 2>&1 </dev/null)
build_status=$?

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

exit 0

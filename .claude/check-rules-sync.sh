#!/usr/bin/env bash
# Claude's behavioural rules, guard hook and permission matrix live in ~/.claude
# (the reference copy, loaded in every repository) and are mirrored here so that
# cloud sessions, which have no ~/.claude of their own, load them too.
# Runs as a SessionStart hook: prints a warning when the copies differ.
set -u
home="$HOME/.claude"
repo="$(cd "$(dirname "$0")" && pwd)"
[ -d "$home/rules" ] || exit 0
problems=""
if ! out=$(diff -r "$home/rules" "$repo/rules" 2>&1); then
  problems+=$'\n'"rules/ differ:"$'\n'"$(echo "$out" | head -20)"
fi
if ! diff -q "$home/hooks/guard.py" "$repo/hooks/guard.py" >/dev/null 2>&1; then
  problems+=$'\n'"hooks/guard.py differs"
fi
if ! diff <(jq -S .permissions "$home/settings.json" 2>/dev/null) \
          <(jq -S .permissions "$repo/settings.json" 2>/dev/null) >/dev/null; then
  problems+=$'\n'"the permissions block in settings.json differs"
fi
if [ -n "$problems" ]; then
  echo "WARNING: Claude's rules in ~/.claude and in this repository's .claude/ differ. ~/.claude is the reference copy; the repository copy is stale or was edited directly.$problems"
fi

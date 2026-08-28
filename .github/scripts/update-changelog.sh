#!/usr/bin/env bash
set -euo pipefail

# Prepends a release entry to CHANGELOG.md (same content as the GitHub release notes).
# Usage: update-changelog.sh <version> <subject> <notes-file> [changelog-file]

VERSION="${1:?version required}"
SUBJECT="${2:?subject required}"
NOTES_FILE="${3:?notes file required}"
CHANGELOG="${4:-CHANGELOG.md}"

{
  printf '## [%s] - %s\n' "$VERSION" "$SUBJECT"
  echo '### Notes'
  while IFS= read -r line || [ -n "$line" ]; do
    line="${line#"${line%%[![:space:]]*}"}"
    line="${line%"${line##*[![:space:]]}"}"
    [ -z "$line" ] && continue
    case "$line" in
      -*) printf '%s\n' "$line" ;;
      *) printf -- '- %s\n' "$line" ;;
    esac
  done < "$NOTES_FILE"
  echo
} > /tmp/changelog-entry.txt

head -n 4 "$CHANGELOG" > /tmp/changelog-new.md
cat /tmp/changelog-entry.txt >> /tmp/changelog-new.md
tail -n +5 "$CHANGELOG" >> /tmp/changelog-new.md
mv /tmp/changelog-new.md "$CHANGELOG"

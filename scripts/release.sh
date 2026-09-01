#!/usr/bin/env bash
#
# Cuts a release: bumps the version, commits, tags.
#
#   ./scripts/release.sh 1.1.0            # prepare, then push the tag yourself
#   ./scripts/release.sh 1.1.0 --push     # prepare and push in one go
#
# The version lives in exactly one place and the tag is derived from it, so the release
# workflow's tag-matches-repository check can never fail the way it does when the two are
# bumped by hand in separate steps.

set -euo pipefail

PROPS="Directory.Packaging.props"

info() { printf '\033[0;36m==>\033[0m %s\n' "$1"; }
die()  { printf '\033[0;31merror\033[0m %s\n' "$1" >&2; exit 1; }

VERSION="${1:-}"
PUSH="${2:-}"

[ -n "$VERSION" ] || die "Usage: ./scripts/release.sh <version> [--push]   e.g. ./scripts/release.sh 1.1.0"

# Strip a leading v so both `1.1.0` and `v1.1.0` work.
VERSION="${VERSION#v}"

if ! echo "$VERSION" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$'; then
  die "'$VERSION' is not a valid semantic version (1.2.3, or 1.2.3-rc.1)."
fi

cd "$(dirname "${BASH_SOURCE[0]}")/.."

[ -f "$PROPS" ] || die "$PROPS not found. Run this from the repository."
git rev-parse --git-dir >/dev/null 2>&1 || die "Not a git repository."

[ -z "$(git status --porcelain)" ] || die "The working tree has uncommitted changes. Commit or stash them first."

if git rev-parse "v$VERSION" >/dev/null 2>&1; then
  die "Tag v$VERSION already exists.

  A tag-triggered workflow runs the workflow file as it exists in the tagged commit, so fixing
  the workflow on a branch does nothing until the tag moves. If nothing was published, deleting
  and re-cutting the tag is safe:

    git tag -d v$VERSION && git push --delete origin v$VERSION
    ./scripts/release.sh $VERSION"
fi

CURRENT=$(dotnet msbuild tools/DotnetApiTemplate.Cli/DotnetApiTemplate.Cli.csproj -getProperty:Version -nologo | tr -d '[:space:]')
info "Current version: $CURRENT  ->  $VERSION"

# Rewrite only the real element, never a mention of it in a comment. Editing by offset rather
# than re-serialising keeps the file byte-identical apart from the version itself.
python3 - "$PROPS" "$VERSION" <<'REWRITE'
import re, sys

path, version = sys.argv[1], sys.argv[2]
with open(path) as handle:
    text = handle.read()

comments = [(m.start(), m.end()) for m in re.finditer(r"<!--.*?-->", text, re.DOTALL)]


def inside_comment(index):
    return any(start <= index < end for start, end in comments)


matches = [m for m in re.finditer(r"(<Version>)([^<]*)(</Version>)", text)
           if not inside_comment(m.start())]

if len(matches) != 1:
    raise SystemExit(
        f"Expected exactly one Version element outside comments in {path}, found {len(matches)}.")

match = matches[0]
with open(path, "w") as handle:
    handle.write(text[:match.start()] + match.group(1) + version + match.group(3) + text[match.end():])
REWRITE

APPLIED=$(dotnet msbuild tools/DotnetApiTemplate.Cli/DotnetApiTemplate.Cli.csproj -getProperty:Version -nologo | tr -d '[:space:]')
[ "$APPLIED" = "$VERSION" ] || die "The version did not apply cleanly: MSBuild still reports '$APPLIED'."

info "Running tests"
dotnet test CleanArchTemplate.slnx --nologo --verbosity quiet
dotnet test tools/DotnetApiTemplate.slnx --nologo --verbosity quiet

# Re-tagging the same version is a normal thing to do after fixing a CI bug: the version file
# is already correct, so there is nothing to commit and we just move the tag onto the fix.
if git diff --quiet -- "$PROPS"; then
  info "Version is already $VERSION; tagging the current commit"
else
  info "Committing"
  git add "$PROPS"
  git commit --quiet --message "chore: release $VERSION"
fi

info "Tagging v$VERSION at $(git rev-parse --short HEAD)"
git tag --annotate "v$VERSION" --message "Release $VERSION"

echo
printf '\033[0;32mPrepared v%s.\033[0m\n' "$VERSION"
echo

if [ "$PUSH" = "--push" ]; then
  info "Pushing"
  git push origin HEAD --follow-tags
  echo
  echo "  The release workflow is now running. Watch it, then check nuget.org a few minutes later."
  echo
else
  echo "  Nothing has been pushed. Publishing to nuget.org is permanent, so review first:"
  echo
  echo "    git show --stat HEAD"
  echo "    git push origin HEAD --follow-tags"
  echo
fi

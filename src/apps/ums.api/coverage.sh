#!/usr/bin/env bash
# =============================================================================
# coverage.sh — UMS business-layer coverage runner
#
# Uses coverlet CLI (static instrumentation — macOS/arm64 compatible).
# Instruments each test assembly in-place, runs tests, produces Cobertura XML.
# Merges both reports with ReportGenerator for the final HTML view.
#
# Usage:
#   ./coverage.sh          Run all tests + generate HTML report
#   ./coverage.sh --ci     Same, exit non-zero if combined < 80% line coverage;
#                          writes coverage/coverage-summary.txt and GITHUB_OUTPUT
#   ./coverage.sh --print  Print existing combined percentage without re-running tests
#
# First time:
#   dotnet tool restore    (installs coverlet.console + reportgenerator)
#
# Output:
#   coverage/domain.cobertura.xml
#   coverage/application.cobertura.xml
#   coverage/report/index.html      ← open this in a browser
# =============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"   # dotnet tool run requires the manifest directory to be CWD
COVERAGE_DIR="$SCRIPT_DIR/coverage"
REPORT_DIR="$COVERAGE_DIR/report"
CI_MODE=false
PRINT_ONLY=false

for arg in "$@"; do
  [[ "$arg" == "--ci" ]]    && CI_MODE=true
  [[ "$arg" == "--print" ]] && PRINT_ONLY=true
done

# ---------------------------------------------------------------------------
# --print: read an existing merged report and exit without re-running tests
# ---------------------------------------------------------------------------
if $PRINT_ONLY; then
  SUMMARY="$REPORT_DIR/Cobertura.xml"
  if [[ ! -f "$SUMMARY" ]]; then
    echo "No coverage report found at $SUMMARY — run ./coverage.sh first." >&2
    exit 1
  fi
  COVERAGE=$(grep -oE 'line-rate="[0-9.]+"' "$SUMMARY" | head -1 | grep -oE '[0-9.]+')
  COVERAGE_PCT=$(printf "%.1f" "$(echo "$COVERAGE * 100" | bc -l)")
  echo "${COVERAGE_PCT}"
  exit 0
fi

DOMAIN_PROJECT="$SCRIPT_DIR/Evolith.Ums.Domain.Test/Evolith.Ums.Domain.Test.csproj"
APPLICATION_PROJECT="$SCRIPT_DIR/Evolith.Ums.Application.Test/Evolith.Ums.Application.Test.csproj"
DOMAIN_DLL="$SCRIPT_DIR/Evolith.Ums.Domain.Test/bin/Release/net10.0/Evolith.Ums.Domain.Test.dll"
APPLICATION_DLL="$SCRIPT_DIR/Evolith.Ums.Application.Test/bin/Release/net10.0/Evolith.Ums.Application.Test.dll"

# ---------------------------------------------------------------------------
# Build (coverlet instruments compiled assemblies)
# ---------------------------------------------------------------------------
echo "▶  Building test projects..."
dotnet build "$DOMAIN_PROJECT"      --configuration Release
dotnet build "$APPLICATION_PROJECT" --configuration Release

# ---------------------------------------------------------------------------
# Clean previous results
# ---------------------------------------------------------------------------
rm -rf "$COVERAGE_DIR"
mkdir -p "$COVERAGE_DIR"

# ---------------------------------------------------------------------------
# Run Domain tests via coverlet CLI
# ---------------------------------------------------------------------------
echo "▶  Running Evolith.Ums.Domain.Test (with coverage)..."
dotnet tool run coverlet "$DOMAIN_DLL" \
  --target dotnet \
  --targetargs "test \"$DOMAIN_PROJECT\" --configuration Release --no-build" \
  --format cobertura \
  --output "$COVERAGE_DIR/domain.cobertura.xml" \
  --include "[Evolith.Ums.Domain]*" \
  --exclude "[Evolith.Ums.Domain]*.Props" \
  --exclude "[Evolith.Ums.Domain]*Event" \
  --exclude "[Evolith.Ums.Domain]*DomainErrors" \
  --exclude "[Evolith.Ums.Domain]*Regex*" \
  --exclude "[Evolith.Ums.Domain]*Generated*" \
  --exclude "[Evolith.Ums.Domain]*RuleValidator" \
  --exclude "[Evolith.Ums.Infrastructure]*" \
  --exclude "[Evolith.Ums.Shell.*]*" \
  --exclude "[Evolith.Ums.Globalization]*" \
  --verbosity Minimal

# ---------------------------------------------------------------------------
# Run Application tests via coverlet CLI
# ---------------------------------------------------------------------------
echo "▶  Running Evolith.Ums.Application.Test (with coverage)..."
dotnet tool run coverlet "$APPLICATION_DLL" \
  --target dotnet \
  --targetargs "test \"$APPLICATION_PROJECT\" --configuration Release --no-build" \
  --format cobertura \
  --output "$COVERAGE_DIR/application.cobertura.xml" \
  --include "[Evolith.Ums.Domain]*" \
  --include "[Evolith.Ums.Application]*" \
  --exclude "[Evolith.Ums.Domain]*.Props" \
  --exclude "[Evolith.Ums.Domain]*Event" \
  --exclude "[Evolith.Ums.Domain]*DomainErrors" \
  --exclude "[Evolith.Ums.Domain]*Regex*" \
  --exclude "[Evolith.Ums.Domain]*Generated*" \
  --exclude "[Evolith.Ums.Domain]*RuleValidator" \
  --exclude "[Evolith.Ums.Application]*.DTOs.*" \
  --exclude "[Evolith.Ums.Application]*Validator" \
  --exclude "[Evolith.Ums.Infrastructure]*" \
  --exclude "[Evolith.Ums.Presentation]*" \
  --exclude "[Evolith.Ums.Shell.*]*" \
  --exclude "[Evolith.Ums.Globalization]*" \
  --verbosity Minimal

# ---------------------------------------------------------------------------
# Merge + generate HTML report
# ---------------------------------------------------------------------------
echo "▶  Generating HTML report..."
dotnet tool run reportgenerator \
  "-reports:$COVERAGE_DIR/domain.cobertura.xml;$COVERAGE_DIR/application.cobertura.xml" \
  "-targetdir:$REPORT_DIR" \
  "-reporttypes:Html;Cobertura;Badges" \
  "-assemblyfilters:+Evolith.Ums.Domain;+Evolith.Ums.Application;-Evolith.Ums.Infrastructure;-Evolith.Ums.Presentation;-Evolith.Ums.Shell.*" \
  "-classfilters:-*Props;-*Event;-*DomainErrors;-*Validator;-*DTOs.*;-*.Props;-*Generated*;-RegexGenerator*" \
  "-title:UMS Business Layer Coverage"

echo ""
echo "✅  Report → $REPORT_DIR/index.html"
echo ""

# ---------------------------------------------------------------------------
# Threshold enforcement (--ci mode only)
# Combined threshold: ≥ 80% line coverage
# ---------------------------------------------------------------------------
if $CI_MODE; then
  echo "▶  Enforcing thresholds (CI mode)..."

  SUMMARY="$REPORT_DIR/Cobertura.xml"
  COVERAGE=$(grep -oE 'line-rate="[0-9.]+"' "$SUMMARY" | head -1 | grep -oE '[0-9.]+')
  COVERAGE_PCT=$(printf "%.1f" "$(echo "$COVERAGE * 100" | bc -l)")

  # Write a machine-readable summary for CI steps that need just the number
  echo "${COVERAGE_PCT}" > "$COVERAGE_DIR/coverage-summary.txt"
  # GitHub Actions output variable (no-op outside Actions)
  if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
    echo "coverage_pct=${COVERAGE_PCT}" >> "$GITHUB_OUTPUT"
  fi

  echo "  Combined line coverage: ${COVERAGE_PCT}%"

  THRESHOLD=80
  if (( $(echo "$COVERAGE_PCT < $THRESHOLD" | bc -l) )); then
    echo "❌  Coverage ${COVERAGE_PCT}% is below required threshold ${THRESHOLD}%."
    exit 1
  else
    echo "✅  Coverage ${COVERAGE_PCT}% meets the ${THRESHOLD}% threshold."
  fi
fi

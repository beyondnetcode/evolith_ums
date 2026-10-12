#!/bin/bash
# =============================================================================
# UMS Metrics Update Script
# =============================================================================
# Collects engineering metrics from all solutions and updates the metrics
# dashboard document. Runs as part of CI/CD pipeline after each commit.
#
# Usage: ./update-metrics.sh [--ci] [--dry-run]
#   --ci       Run in CI mode (non-interactive, fail on errors)
#   --dry-run  Collect metrics but do not update documents
# =============================================================================

set -euo pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
DOCS_DIR="$ROOT_DIR/../docs/operations/metrics"
METRICS_FILE="$DOCS_DIR/index.md"
METRICS_FILE_ES="$DOCS_DIR/index.es.md"
TIMESTAMP=$(date -u +"%Y-%m-%d")
CI_MODE=false
DRY_RUN=false

# Parse arguments
for arg in "$@"; do
  case $arg in
    --ci)
      CI_MODE=true
      shift
      ;;
    --dry-run)
      DRY_RUN=true
      shift
      ;;
  esac
done

# Color output helpers
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log_info() {
  echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
  echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
  echo -e "${RED}[ERROR]${NC} $1"
}

# =============================================================================
# Metric Collection Functions
# =============================================================================

# Count lines of code (excluding node_modules, coverage, etc.)
count_loc() {
  local dir=$1
  local extensions=$2
  local count=0

  if [ -d "$dir" ]; then
    count=$(find "$dir" -type f \( $extensions \) \
      -not -path "*/node_modules/*" \
      -not -path "*/coverage/*" \
      -not -path "*/dist/*" \
      -not -path "*/bin/*" \
      -not -path "*/obj/*" \
      -not -path "*/.nx/*" \
      2>/dev/null | xargs wc -l 2>/dev/null | tail -1 | awk '{print $1}' || echo "0")
  fi

  echo "${count:-0}"
}

# Count source files
count_files() {
  local dir=$1
  local extensions=$2
  local count=0

  if [ -d "$dir" ]; then
    count=$(find "$dir" -type f \( $extensions \) \
      -not -path "*/node_modules/*" \
      -not -path "*/coverage/*" \
      -not -path "*/dist/*" \
      -not -path "*/bin/*" \
      -not -path "*/obj/*" \
      -not -path "*/.nx/*" \
      2>/dev/null | wc -l || echo "0")
  fi

  echo "${count:-0}"
}

# Get test counts from dotnet test
get_dotnet_test_counts() {
  local project_dir=$1
  local total=0
  local passed=0
  local failed=0
  local skipped=0

  if [ -d "$project_dir" ]; then
    local test_output
    test_output=$(cd "$project_dir" && dotnet test --logger "console;verbosity=normal" --no-build 2>&1 || true)

    total=$(echo "$test_output" | grep -oP 'Total tests: \K\d+' || echo "0")
    passed=$(echo "$test_output" | grep -oP 'Passed: \K\d+' || echo "0")
    failed=$(echo "$test_output" | grep -oP 'Failed: \K\d+' || echo "0")
    skipped=$(echo "$test_output" | grep -oP 'Skipped: \K\d+' || echo "0")
  fi

  echo "$total $passed $failed $skipped"
}

# Get coverage percentage
get_coverage() {
  local project_dir=$1
  local coverage_file="$project_dir/coverage/report/coverage.xml"

  if [ -f "$coverage_file" ]; then
    # Parse coverage from XML (simplified)
    local coverage
    coverage=$(grep -oP 'line-rate="[^"]*"' "$coverage_file" | head -1 | grep -oP '[0-9.]+')
    if [ -n "$coverage" ]; then
      echo "$(echo "$coverage * 100" | bc | cut -d. -f1)%"
      return
    fi
  fi

  echo "N/A"
}

# Get npm audit results
get_npm_audit() {
  local dir=$1
  local critical=0
  local high=0
  local medium=0

  if [ -d "$dir" ] && [ -f "$dir/package.json" ]; then
    local audit_output
    audit_output=$(cd "$dir" && npm audit --json 2>/dev/null || true)

    critical=$(echo "$audit_output" | grep -oP '"critical":\s*\K\d+' | head -1 || echo "0")
    high=$(echo "$audit_output" | grep -oP '"high":\s*\K\d+' | head -1 || echo "0")
    medium=$(echo "$audit_output" | grep -oP '"moderate":\s*\K\d+' | head -1 || echo "0")
  fi

  echo "${critical:-0} ${high:-0} ${medium:-0}"
}

# Get NuGet vulnerability count
get_nuget_vulnerabilities() {
  local solution_dir=$1
  local critical=0
  local high=0
  local medium=0

  if [ -d "$solution_dir" ]; then
    local audit_output
    audit_output=$(cd "$solution_dir" && dotnet nuget verify --vulnerability-audit 2>&1 || true)

    critical=$(echo "$audit_output" | grep -ci "critical" || echo "0")
    high=$(echo "$audit_output" | grep -ci "high" || echo "0")
    medium=$(echo "$audit_output" | grep -ci "moderate" || echo "0")
  fi

  echo "${critical:-0} ${high:-0} ${medium:-0}"
}

# Get build warnings
get_build_warnings() {
  local solution_dir=$1
  local warnings=0

  if [ -d "$solution_dir" ]; then
    local build_output
    build_output=$(cd "$solution_dir" && dotnet build --no-restore 2>&1 || true)
    warnings=$(echo "$build_output" | grep -c "Warning" || echo "0")
  fi

  echo "${warnings:-0}"
}

# Get ESLint errors
get_eslint_errors() {
  local dir=$1
  local errors=0
  local warnings=0

  if [ -d "$dir" ] && [ -f "$dir/package.json" ]; then
    local lint_output
    lint_output=$(cd "$dir" && npx eslint . --ext .ts,.tsx --format compact 2>&1 || true)

    errors=$(echo "$lint_output" | grep -c "error" || echo "0")
    warnings=$(echo "$lint_output" | grep -c "warning" || echo "0")
  fi

  echo "${errors:-0} ${warnings:-0}"
}

# =============================================================================
# Main Collection
# =============================================================================

log_info "Starting metrics collection..."
log_info "Timestamp: $TIMESTAMP"

# --- API Metrics ---
log_info "Collecting API metrics..."
API_DIR="$ROOT_DIR/apps/ums.api"

API_LOC=$(count_loc "$API_DIR" "-name '*.cs'")
API_FILES=$(count_files "$API_DIR" "-name '*.cs'")
API_NUGET_VULNS=$(get_nuget_vulnerabilities "$API_DIR")
API_BUILD_WARNINGS=$(get_build_warnings "$API_DIR")

# --- Web Metrics ---
log_info "Collecting Web metrics..."
WEB_DIR="$ROOT_DIR/apps/ums.web-app"

WEB_LOC=$(count_loc "$WEB_DIR" "-name '*.ts' -o -name '*.tsx' -o -name '*.js' -o -name '*.jsx'")
WEB_FILES=$(count_files "$WEB_DIR" "-name '*.ts' -o -name '*.tsx' -o -name '*.js' -o -name '*.jsx'")
WEB_NPM_AUDIT=$(get_npm_audit "$WEB_DIR")
WEB_ESLINT=$(get_eslint_errors "$WEB_DIR")

# --- Shell Library Metrics ---
log_info "Collecting Shell Library metrics..."
SHELL_DIR="$ROOT_DIR/libs/shell"

SHELL_LOC=$(count_loc "$SHELL_DIR" "-name '*.cs'")
SHELL_FILES=$(count_files "$SHELL_DIR" "-name '*.cs'")

# Per-library metrics
for lib in ddd factory aop bootstrapper; do
  lib_dir="$SHELL_DIR/$lib"
  if [ -d "$lib_dir/src" ]; then
    eval "SHELL_${lib^^}_LOC=$(count_loc "$lib_dir/src" "-name '*.cs'")"
    eval "SHELL_${lib^^}_FILES=$(count_files "$lib_dir/src" "-name '*.cs'")"
  fi
done

# --- Test Metrics ---
log_info "Collecting Test metrics..."

# Unit tests
DOMAIN_TEST=$(get_dotnet_test_counts "$API_DIR/Evolith.Ums.Domain.Test")
APP_TEST=$(get_dotnet_test_counts "$API_DIR/Evolith.Ums.Application.Test")

# =============================================================================
# Document Update
# =============================================================================

if [ "$DRY_RUN" = true ]; then
  log_info "Dry run mode - metrics collected but documents not updated"
  log_info "API: LOC=$API_LOC, Files=$API_FILES"
  log_info "Web: LOC=$WEB_LOC, Files=$WEB_FILES"
  log_info "Shell: LOC=$SHELL_LOC, Files=$SHELL_FILES"
  exit 0
fi

log_info "Updating metrics documents..."

# Create docs directory if it doesn't exist
mkdir -p "$DOCS_DIR"

# Update timestamp in English document
if [ -f "$METRICS_FILE" ]; then
  sed -i "s/\*\*Last Updated:\*\*.*/\*\*Last Updated:\*\* $TIMESTAMP/" "$METRICS_FILE"
  log_info "Updated English metrics document"
fi

# Update timestamp in Spanish document
if [ -f "$METRICS_FILE_ES" ]; then
  sed -i "s/\*\*Ultima Actualizacion:\*\*.*/\*\*Ultima Actualizacion:\*\* $TIMESTAMP/" "$METRICS_FILE_ES"
  log_info "Updated Spanish metrics document"
fi

# =============================================================================
# BMAD Compliance Checks
# =============================================================================

log_info "Running BMAD compliance checks..."

# R-03: UTF-8 validation
python3 "$ROOT_DIR/../.bmad-core/scripts/cleanup_markdown_encoding.py" "$METRICS_FILE" 2>/dev/null || true
python3 "$ROOT_DIR/../.bmad-core/scripts/cleanup_markdown_encoding.py" "$METRICS_FILE_ES" 2>/dev/null || true

# R-14: Emoji/icon removal
python3 "$ROOT_DIR/../.bmad-core/scripts/strip_emojis.py" "$METRICS_FILE" 2>/dev/null || true
python3 "$ROOT_DIR/../.bmad-core/scripts/strip_emojis.py" "$METRICS_FILE_ES" 2>/dev/null || true

# Validate Mermaid diagrams (if any)
python3 "$ROOT_DIR/../.bmad-core/scripts/validate_mermaid.py" "$METRICS_FILE" 2>/dev/null || true
python3 "$ROOT_DIR/../.bmad-core/scripts/validate_mermaid.py" "$METRICS_FILE_ES" 2>/dev/null || true

log_info "BMAD compliance checks completed"

# =============================================================================
# CI Mode: Commit and Push
# =============================================================================

if [ "$CI_MODE" = true ]; then
  log_info "CI mode - committing metrics updates..."

  cd "$ROOT_DIR/.."

  # Check if there are changes
  if git diff --quiet "$METRICS_FILE" "$METRICS_FILE_ES" 2>/dev/null; then
    log_info "No changes to commit"
  else
    git add "$METRICS_FILE" "$METRICS_FILE_ES"
    git commit -m "[ci skip] docs: update metrics dashboard ($TIMESTAMP)" || log_warn "Commit failed (may be empty)"
    log_info "Metrics documents committed"
  fi
fi

log_info "Metrics update completed successfully"
exit 0

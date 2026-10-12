#!/usr/bin/env bash
# =============================================================================
# OPS-03: Dev DB anonymization pipeline wrapper
# =============================================================================
# Usage:
#   ./scripts/anonymize-dev-db.sh --source ums --target ums_dev_YYYYMMDD \
#                                 --server localhost --user sa --password <pwd>
#
# What it does:
#   1. Validates that the target DB name is NOT a production/staging name.
#   2. Restores a backup of --source into --target  (BAK file or live copy).
#   3. Runs anonymize-dev-db.sql against --target.
#   4. Prints a summary line.
#
# Requirements:
#   - sqlcmd (mssql-tools or sqlcmd v18+) must be on PATH.
#   - The SQL Server user must have db_owner on --target and VIEW DATABASE STATE.
#   - For the backup restore step the user needs sysadmin or ALTER ANY DATABASE.
#
# Environment variables (alternative to flags):
#   UMS_SQL_SERVER   SQL Server hostname  (default: localhost)
#   UMS_SQL_USER     SQL login            (default: sa)
#   UMS_SQL_PASSWORD SQL password         (required unless using Windows auth)
#   UMS_SOURCE_DB    Source database name (default: ums)
#   UMS_TARGET_DB    Target database name (required)
# =============================================================================

set -euo pipefail

# ── Defaults from environment ────────────────────────────────────────────────
SERVER="${UMS_SQL_SERVER:-localhost}"
SQL_USER="${UMS_SQL_USER:-sa}"
SQL_PASSWORD="${UMS_SQL_PASSWORD:-}"
SOURCE_DB="${UMS_SOURCE_DB:-ums}"
TARGET_DB="${UMS_TARGET_DB:-}"
SKIP_RESTORE=false
BACKUP_FILE=""

# ── Parse arguments ──────────────────────────────────────────────────────────
while [[ $# -gt 0 ]]; do
    case "$1" in
        --server)      SERVER="$2";        shift 2 ;;
        --user)        SQL_USER="$2";      shift 2 ;;
        --password)    SQL_PASSWORD="$2";  shift 2 ;;
        --source)      SOURCE_DB="$2";     shift 2 ;;
        --target)      TARGET_DB="$2";     shift 2 ;;
        --backup-file) BACKUP_FILE="$2";   shift 2 ;;
        --skip-restore)SKIP_RESTORE=true;  shift   ;;
        -h|--help)
            sed -n '3,40p' "$0"   # Print the header comment block as help
            exit 0 ;;
        *) echo "Unknown argument: $1" >&2; exit 1 ;;
    esac
done

# ── Validate required inputs ──────────────────────────────────────────────────
if [[ -z "$TARGET_DB" ]]; then
    echo "ERROR: --target database name is required." >&2
    exit 1
fi

PROD_NAMES=(ums ums_prod ums_staging ums_preprod)
for name in "${PROD_NAMES[@]}"; do
    if [[ "$TARGET_DB" == "$name" ]]; then
        echo "SAFETY ABORT: target '$TARGET_DB' matches a production/staging name." >&2
        exit 2
    fi
done

if [[ -z "$SQL_PASSWORD" ]]; then
    echo "ERROR: SQL password is required (--password or \$UMS_SQL_PASSWORD)." >&2
    exit 1
fi

SQLCMD_ARGS=(-S "$SERVER" -U "$SQL_USER" -P "$SQL_PASSWORD" -b -r1)

echo "========================================================="
echo " UMS Dev DB Anonymization Pipeline"
echo " Server : $SERVER"
echo " Source : $SOURCE_DB"
echo " Target : $TARGET_DB"
echo " Date   : $(date -u '+%Y-%m-%d %H:%M:%S UTC')"
echo "========================================================="

# ── Step 1: Restore / copy ────────────────────────────────────────────────────
if [[ "$SKIP_RESTORE" == "false" ]]; then
    echo ""
    echo "[1/3] Restoring database copy..."

    if [[ -n "$BACKUP_FILE" ]]; then
        # Restore from a .bak file.
        echo "      Source: backup file $BACKUP_FILE"
        sqlcmd "${SQLCMD_ARGS[@]}" -Q "
            IF DB_ID(N'${TARGET_DB}') IS NOT NULL
            BEGIN
                ALTER DATABASE [${TARGET_DB}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [${TARGET_DB}];
            END;
            RESTORE DATABASE [${TARGET_DB}] FROM DISK = N'${BACKUP_FILE}'
            WITH MOVE N'${SOURCE_DB}' TO N'/var/opt/mssql/data/${TARGET_DB}.mdf',
                 MOVE N'${SOURCE_DB}_log' TO N'/var/opt/mssql/data/${TARGET_DB}_log.ldf',
                 REPLACE, STATS = 10;
        "
    else
        # Copy-only restore via DBCC CLONEDATABASE (SQL Server 2014+, schema only)
        # For a full data copy use backup/restore above.
        echo "      Source: live DB $SOURCE_DB (DBCC CLONEDATABASE — schema+stats only)"
        echo "      NOTE: for a full data copy provide --backup-file path/to/backup.bak"
        sqlcmd "${SQLCMD_ARGS[@]}" -Q "
            USE [master];
            IF DB_ID(N'${TARGET_DB}') IS NOT NULL
            BEGIN
                ALTER DATABASE [${TARGET_DB}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [${TARGET_DB}];
            END;
            DBCC CLONEDATABASE ([${SOURCE_DB}], [${TARGET_DB}]) WITH NO_STATISTICS, NO_QUERYSTORE, VERIFY_CLONEDB;
        "
    fi
    echo "      ✔  Database $TARGET_DB ready."
else
    echo "[1/3] Skipping restore (--skip-restore)."
fi

# ── Step 2: Run anonymization SQL ─────────────────────────────────────────────
echo ""
echo "[2/3] Running anonymization script..."
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
sqlcmd "${SQLCMD_ARGS[@]}" \
    -v "TargetDatabase=${TARGET_DB}" \
    -i "${SCRIPT_DIR}/anonymize-dev-db.sql"
echo "      ✔  Anonymization complete."

# ── Step 3: Verify ────────────────────────────────────────────────────────────
echo ""
echo "[3/3] Verification — checking for real email patterns..."
REAL_EMAILS=$(sqlcmd "${SQLCMD_ARGS[@]}" -h -1 -Q "
    USE [${TARGET_DB}];
    SELECT COUNT(*)
    FROM [ums_identity].[UserAccounts]
    WHERE [Email] NOT LIKE '%@dev.invalid'
      AND [Email] NOT LIKE 'gdpr_del_%@anonymized.invalid'
      AND [IsDeleted] = 0;
" 2>/dev/null | tr -d '[:space:]')

if [[ "$REAL_EMAILS" -gt 0 ]]; then
    echo "      ✘  WARNING: $REAL_EMAILS rows still contain non-anonymized email addresses!" >&2
    exit 3
fi
echo "      ✔  All active user accounts use anonymized email addresses."

echo ""
echo "========================================================="
echo " Done. Use connection string:"
echo "   Server=${SERVER};Database=${TARGET_DB};User Id=${SQL_USER};..."
echo "========================================================="

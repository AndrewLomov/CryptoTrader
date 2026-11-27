#!/usr/bin/env bash
# Restore a custom-format pg_dump into the Compose PostgreSQL service.
# Usage: bash deploy/scripts/restore.sh <backup-file>
# Stops the app while restore runs, then starts it again if it was running.

set -euo pipefail

if [[ $# -ne 1 || -z "${1//[[:space:]]/}" ]]; then
    echo "error: target file path is empty" >&2
    echo "usage: restore.sh <backup-file>" >&2
    exit 1
fi

target="$1"

if [[ ! -f "${target}" ]]; then
    echo "error: backup file not found: ${target}" >&2
    exit 1
fi

if [[ ! -s "${target}" ]]; then
    echo "error: backup file is empty: ${target}" >&2
    exit 1
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
deploy_dir="$(cd "${script_dir}/.." && pwd)"
env_file="${deploy_dir}/.env"

if [[ ! -f "${env_file}" ]]; then
    echo "error: ${env_file} is missing. Copy deploy/.env.example to deploy/.env" >&2
    exit 1
fi

read_env_value() {
    local key="$1"
    local line value
    line="$(grep -E "^${key}=" "${env_file}" | tail -n 1 || true)"
    if [[ -z "${line}" ]]; then
        echo "error: ${key} is missing in ${env_file}" >&2
        exit 1
    fi
    value="${line#"${key}"=}"
    value="${value%$'\r'}"
    value="${value#\"}"
    value="${value%\"}"
    value="${value#\'}"
    value="${value%\'}"
    if [[ -z "${value}" ]]; then
        echo "error: ${key} is empty in ${env_file}" >&2
        exit 1
    fi
    printf '%s' "${value}"
}

postgres_user="$(read_env_value POSTGRES_USER)"
postgres_db="$(read_env_value POSTGRES_DB)"

compose() {
    docker compose \
        --project-directory "${deploy_dir}" \
        --env-file "${env_file}" \
        -f "${deploy_dir}/docker-compose.yml" \
        "$@"
}

app_was_running=0
if compose ps --status running --services | grep -qx "cryptotrader"; then
    app_was_running=1
    echo "Stopping cryptotrader so it releases database connections"
    compose stop cryptotrader
fi

echo "Restoring ${target}"
compose exec -T postgres \
    pg_restore \
    --username="${postgres_user}" \
    --dbname="${postgres_db}" \
    --format=custom \
    --clean \
    --if-exists \
    --no-owner \
    --no-acl \
    --exit-on-error \
    < "${target}"

if [[ "${app_was_running}" -eq 1 ]]; then
    echo "Starting cryptotrader"
    compose start cryptotrader
fi

echo "Restore finished from ${target}"

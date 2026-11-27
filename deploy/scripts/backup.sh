#!/usr/bin/env bash
# Custom-format backup of the Compose PostgreSQL service.
# Usage: bash deploy/scripts/backup.sh <target-file>
# Run with bash. PowerShell redirection corrupts the binary dump.

set -euo pipefail

if [[ $# -ne 1 || -z "${1//[[:space:]]/}" ]]; then
    echo "error: target file path is empty" >&2
    echo "usage: backup.sh <target-file>" >&2
    exit 1
fi

target="$1"

if [[ -d "${target}" ]]; then
    echo "error: target file path is a directory: ${target}" >&2
    exit 1
fi

parent="$(dirname "${target}")"
if [[ ! -d "${parent}" ]]; then
    echo "error: directory does not exist: ${parent}" >&2
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

partial="${target}.partial"
rm -f -- "${partial}"
trap 'rm -f -- "${partial}"' EXIT

echo "Writing custom-format backup to ${target}"
umask 077
compose exec -T postgres \
    pg_dump \
    --username="${postgres_user}" \
    --dbname="${postgres_db}" \
    --format=custom \
    --no-owner \
    --no-acl \
    > "${partial}"

mv -- "${partial}" "${target}"

if [[ ! -s "${target}" ]]; then
    echo "error: backup file is empty: ${target}" >&2
    exit 1
fi

echo "Backup written to ${target}"

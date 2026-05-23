#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="${SCRIPT_DIR}/build"
EXECUTABLE="${BUILD_DIR}/Server_build.x86_64"
PORT="${1:-7770}"
LOG_FILE="${SCRIPT_DIR}/server.log"

if [[ ! -f "${EXECUTABLE}" ]]; then
  echo "Server build not found: ${EXECUTABLE}"
  echo "Copy your Linux Dedicated Server build into Server/build/"
  exit 1
fi

chmod +x "${EXECUTABLE}"

echo "Starting dedicated server on port ${PORT}"
echo "Log file: ${LOG_FILE}"

"${EXECUTABLE}" -batchmode -nographics -port "${PORT}" -logFile "${LOG_FILE}"

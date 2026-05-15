#!/bin/sh
set -eu

COMPOSE_FILE=/compose/docker-compose.yml
PROJECT=nimblist

# Services that receive new images on every CI push.
# db and redis use upstream images and are never rebuilt, so exclude them.
SERVICES="Nimblist.api Nimblist.classification Nimblist.recipescraper"

echo "[deploy] $(date -u '+%Y-%m-%dT%H:%M:%SZ') — starting"

echo "[deploy] Pulling updated images..."
# shellcheck disable=SC2086
docker compose -p "$PROJECT" -f "$COMPOSE_FILE" pull $SERVICES

echo "[deploy] Restarting updated services..."
# shellcheck disable=SC2086
docker compose -p "$PROJECT" -f "$COMPOSE_FILE" up -d --no-deps $SERVICES

echo "[deploy] Done."

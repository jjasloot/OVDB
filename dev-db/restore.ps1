# Restores a production dump (ovdb-dump.sql in this folder) into the local dev database.
# Usage:  ./restore.ps1   (from the dev-db folder; container must be running)
$ErrorActionPreference = "Stop"
$dump = Join-Path $PSScriptRoot "ovdb-dump.sql"
if (-not (Test-Path $dump)) {
    Write-Error "No dump found at $dump - create one first (see dev-db/README.md)"
}

Write-Host "Dropping and recreating local ovdb database..."
docker exec ovdb-dev-db mariadb -uroot -povdb-dev-root -e "DROP DATABASE IF EXISTS ovdb; CREATE DATABASE ovdb CHARACTER SET utf8mb4; GRANT ALL PRIVILEGES ON ovdb.* TO 'ovdb'@'%';"

# The dump is copied into the container and restored there: piping it through
# PowerShell re-encodes the text and corrupts non-ASCII characters (ü, é, ...)
Write-Host "Copying dump into container ($([Math]::Round((Get-Item $dump).Length / 1MB, 1)) MB)..."
docker cp $dump ovdb-dev-db:/tmp/ovdb-dump.sql

Write-Host "Restoring (this can take a few minutes)..."
docker exec ovdb-dev-db sh -c "mariadb -uroot -povdb-dev-root --default-character-set=utf8mb4 ovdb < /tmp/ovdb-dump.sql && rm -f /tmp/ovdb-dump.sql"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Restore failed"
}
Write-Host "Done. Local dev database is a byte-faithful copy of the dump."

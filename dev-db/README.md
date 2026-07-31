# Local development database

Local MariaDB in Docker so development never touches the production database on the
NAS. `appsettings.Development.json` points here (`localhost:3307`), so `dotnet run`
uses this database automatically.

## One-time setup / refresh from production

```powershell
# 1. Start the container (from this folder)
docker compose up -d

# 2. Dump production (read-only; runs the client in a throwaway container).
#    The redirect happens INSIDE the container: piping SQL through the PowerShell
#    console can corrupt non-ASCII characters depending on the console codepage.
docker run --rm -v ${PWD}:/dump --env MYSQL_PWD='<prod password from appsettings.json>' mariadb:11.4 `
  sh -c "mariadb-dump -h 192.168.178.30 -P 3307 -u ovdb --single-transaction --no-tablespaces ovdb > /dump/ovdb-dump.sql"

# 3. Restore into the local container
./restore.ps1
```

Re-run steps 2–3 whenever you want fresh production data. The dump file is
gitignored — never commit it.

Logos: `LogoLocation` for development points at `dev-db/logos` (gitignored).
To see production logos locally, copy them once:

```powershell
robocopy \\192.168.178.30\OVDB\Logos .\logos /E
```

## Notes

- Migrations apply automatically when the app starts — against THIS database, not
  production. That is the point: you can rehearse schema migrations on a copy.
- Rollback rehearsal: `dotnet ef database update <previous-migration> --project OVDB_database --startup-project OV_DB --connection "server=localhost;port=3307;database=ovdb;user=root;password=ovdb-dev-root"`

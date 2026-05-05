# Database Setup

## Local SQL Server with Docker

RogueNet requires SQL Server for local development. The easiest way to run it is with Docker.

### Start SQL Server

```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong@Passw0rd" \
  -p 1433:1433 --name roguenet-sql \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

### Verify Connection

```bash
docker exec -it roguenet-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "YourStrong@Passw0rd" -C \
  -Q "SELECT @@VERSION"
```

### Apply Migrations

From the solution root:

```bash
dotnet ef database update --project src/RogueNet.Infrastructure --startup-project src/RogueNet.Api
```

This creates the `RogueNetLiveServices_Dev` database (in Development) or `RogueNetLiveServices` (in Production) with all tables.

### Verify Schema

```bash
docker exec -it roguenet-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "YourStrong@Passw0rd" -C \
  -d RogueNetLiveServices_Dev \
  -Q "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'"
```

Expected tables:
- `Players`
- `PlayerProfiles`
- `InventoryItems`
- `InventoryTransactions`
- `MissionCompletions`
- `IdempotencyKeys`
- `OutboxEvents`
- `CloudSaveSlots`
- `LeaderboardScores`
- `__EFMigrationsHistory`

## Connection Strings

**Development:** `appsettings.Development.json`
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost,1433;Database=RogueNetLiveServices_Dev;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

**Production:** Set environment variable or use Azure Key Vault/AWS Secrets Manager.

## Stopping/Starting

```bash
# Stop
docker stop roguenet-sql

# Start
docker start roguenet-sql

# Remove (destroys data)
docker rm -f roguenet-sql
```

## Resetting the Database

```bash
dotnet ef database drop --project src/RogueNet.Infrastructure --startup-project src/RogueNet.Api
dotnet ef database update --project src/RogueNet.Infrastructure --startup-project src/RogueNet.Api
```

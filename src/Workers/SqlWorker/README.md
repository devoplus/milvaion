# SQL Worker

Built-in worker for executing SQL queries/stored procedures from Milvaion scheduler.

## Job: SqlExecutorJob

Executes SQL commands against configured database (PostgreSQL, SQL Server, MySQL).

**Configuration:**

```json
"SqlWorkerOptions": {
  "DefaultConnectionString": "Host=localhost;Database=mydb;Username=user;Password=pass",
  "CommandTimeout": 300
}
```

**Job Data:**
```json
{
  "ConnectionString": "Host=localhost;Database=mydb;...",
  "CommandText": "DELETE FROM Logs WHERE CreatedAt < @CutoffDate",
  "CommandType": "Text",
  "Parameters": {
    "@CutoffDate": "2024-01-01"
  },
  "TimeoutSeconds": 60
}
```

**Command Types:**
- `Text` - Raw SQL query
- `StoredProcedure` - Execute stored procedure

## Running

### Docker
```bash
docker run -d --name sql-worker \
  --network milvaion_milvaion-network \
  -e SqlWorkerOptions__DefaultConnectionString="Host=postgres;Database=mydb;..." \
  milvaion-sql-worker
```

### Development
```bash
cd src/Workers/SqlWorker
dotnet run
```

## Features

- PostgreSQL, SQL Server, MySQL support
- Parameterized queries (SQL injection safe)
- Stored procedure execution
- Transaction support
- Configurable timeout
- Row count logging

## Use Cases

- Database cleanup jobs
- Report generation
- Data aggregation
- Batch updates
- ETL processes

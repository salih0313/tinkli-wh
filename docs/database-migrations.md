# Database migrations and integrity

EF Core migrations are the only schema change path. Never use EnsureCreated, drop/recreate, or edit an already applied migration. Back up the target database before applying a reviewed migration. Development user creation is idempotent and never resets passwords.

```sh
dotnet tool restore
dotnet ef migrations list --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
dotnet ef database update --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
dotnet ef migrations has-pending-model-changes --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
```

Future authoring (only after an approved model change):

```sh
dotnet ef migrations add DescriptiveName --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
dotnet ef migrations script --idempotent --project src/Wros.Api --startup-project src/Wros.Api --output migration-review.sql -- --environment Development
```

Commands work in macOS zsh and Windows PowerShell. Use `-- --environment Production` with explicitly configured production secrets **only after separate production authorization**. This delivery does not deploy or execute production migrations. Development launch profiles do not automatically select the environment for `dotnet ef`; pass it explicitly.

New additive migrations:

- `20261018134244_Phase6CompletedStockMovements`: completed stock transaction headers/lines, references on ledger, timestamp/reference indexes and extended signed movement constraint.
- `20261018134809_Phase7Integration`: client registry, mappings, assertion replay keys, immutable snapshot headers/rows, external order deduction headers/selections and integration client on ledger; extends allowed signed stock-out types.

Phase 5 is read-only; Phase 8 needs no additional schema change. All earlier migrations are preserved. No demo master/stock records are inserted. Existing role seeds and grants remain unchanged; all required operation permissions already existed. Database table names for these new entity sets are singular (`StockTransaction`, `ProductMapping`, etc.) because no plural DbSet properties were introduced for them.

Rollback of stock history is not a normal workflow. Earlier signed movement constraints cannot accept newer transaction types; don't run migration Down after posting such records. Restore a verified backup/recovery plan under owner supervision instead. Immutable movements and receipt originals must never be deleted to make a downgrade work.

## Automatic startup migrations

The API applies pending EF Core migrations on every startup, in all environments, before accepting HTTP requests. An up-to-date database requires no schema changes. A failure stops startup; logs report the failure without raw connection details. Migration commands have a three-minute timeout; normal API commands retain their existing timeout. No new migration or configuration flag is required for this change.

Configure `ConnectionStrings:DefaultConnection` for the target database. The API SQL identity now needs permissions to apply schema changes (and create the database if it does not exist). Back up the database before deploying schema changes. Stop the old API during deployment and restart after all files are uploaded. Production never runs Development demo-user seeding; in Development, the existing EF seeding hook also runs during startup and skips existing users.

Manual verification on a Development database:

1. Run `dotnet run --project src/Wros.Api -- --environment Development` against a database with pending migrations. Expect migration-start/completion logs before the API listens, and pending migrations recorded in `__EFMigrationsHistory`.
2. Restart against the same database. Expect successful startup with no repeated schema changes or password resets.
3. Request `/api/v1/health/ready`. Expect Healthy after successful startup.
4. Temporarily configure an unreachable SQL endpoint or an account lacking required schema permissions with a pending migration. Expect startup to fail and no HTTP traffic accepted. Restore the valid connection/permissions and restart; expect success.

Database-backed checks require an accessible SQL Server; a successful build alone does not verify migration execution. The manual EF commands above remain available for inspection and controlled updates.

## Reconciliation (read only)

```sql
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;
SELECT * FROM InventoryBalances WHERE Quantity<0;
WITH Ledger AS (
 SELECT StockLotId, WarehouseId, RackId, LocationId, SUM(Quantity) AS Qty
 FROM StockMovements GROUP BY StockLotId, WarehouseId, RackId, LocationId
)
SELECT b.Id, b.Quantity, COALESCE(l.Qty,0) AS LedgerQuantity
FROM InventoryBalances b LEFT JOIN Ledger l ON l.StockLotId=b.StockLotId
 AND l.WarehouseId=b.WarehouseId AND l.RackId=b.RackId
 AND (l.LocationId=b.LocationId OR (l.LocationId IS NULL AND b.LocationId IS NULL))
WHERE b.Quantity<>COALESCE(l.Qty,0);
SELECT ReferenceNumber, SUM(Quantity) AS NetUnits FROM StockMovements
WHERE ReferenceType='Transfer' GROUP BY ReferenceNumber HAVING SUM(Quantity)<>0;
```

Expected: no negative/mismatch/net-transfer rows. Original receipt allocations describe the received amount, not current availability after stock-out. Zero balances remain for history and concurrency. Snapshot rows describe immutable capture-time availability, not live balances.

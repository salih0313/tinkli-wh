using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wros.Api.Shared.Persistence.Migrations;

public partial class AllowRepeatedSupplierImports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_SupplierStockImports_SupplierId_FileHash", table: "SupplierStockImports");
        migrationBuilder.CreateIndex(name: "IX_SupplierStockImports_SupplierId_FileHash", table: "SupplierStockImports", columns: new[] { "SupplierId", "FileHash" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve import history: rollback must fail rather than delete repeated imports.
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [SupplierStockImports] WHERE [ConfirmedAt] IS NOT NULL GROUP BY [SupplierId], [FileHash] HAVING COUNT(*) > 1) THROW 50000, 'Repeated confirmed imports exist; this migration cannot be rolled back without resolving their history.', 1;");
        migrationBuilder.DropIndex(name: "IX_SupplierStockImports_SupplierId_FileHash", table: "SupplierStockImports");
        migrationBuilder.CreateIndex(name: "IX_SupplierStockImports_SupplierId_FileHash", table: "SupplierStockImports", columns: new[] { "SupplierId", "FileHash" }, unique: true, filter: "[ConfirmedAt] IS NOT NULL");
    }
}

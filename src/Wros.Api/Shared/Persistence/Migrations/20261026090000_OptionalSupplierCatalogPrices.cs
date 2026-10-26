using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wros.Api.Shared.Persistence.Migrations;

public partial class OptionalSupplierCatalogPrices : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<decimal>(
            name: "SupplierPrice", table: "SupplierProducts", type: "decimal(18,3)",
            precision: 18, scale: 3, nullable: true,
            oldClrType: typeof(decimal), oldType: "decimal(18,3)", oldPrecision: 18, oldScale: 3);
        migrationBuilder.AlterColumn<decimal>(
            name: "NetUnitPrice", table: "SupplierProducts", type: "decimal(18,3)",
            precision: 18, scale: 3, nullable: true,
            oldClrType: typeof(decimal), oldType: "decimal(18,3)", oldPrecision: 18, oldScale: 3);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Refuse rollback while unpriced entries exist rather than replacing unknown prices with zero.
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [SupplierProducts] WHERE [SupplierPrice] IS NULL OR [NetUnitPrice] IS NULL) THROW 50000, 'Fill missing supplier prices before rolling back this migration.', 1;");
        migrationBuilder.AlterColumn<decimal>(
            name: "SupplierPrice", table: "SupplierProducts", type: "decimal(18,3)",
            precision: 18, scale: 3, nullable: false,
            oldClrType: typeof(decimal), oldType: "decimal(18,3)", oldPrecision: 18, oldScale: 3, oldNullable: true);
        migrationBuilder.AlterColumn<decimal>(
            name: "NetUnitPrice", table: "SupplierProducts", type: "decimal(18,3)",
            precision: 18, scale: 3, nullable: false,
            oldClrType: typeof(decimal), oldType: "decimal(18,3)", oldPrecision: 18, oldScale: 3, oldNullable: true);
    }
}

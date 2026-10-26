using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wros.Api.Shared.Persistence.Migrations;

public partial class OptionalPurchaseOrderProductMapping : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(name: "ProductId", table: "PurchaseOrderLines", type: "uniqueidentifier", nullable: true,
            oldClrType: typeof(Guid), oldType: "uniqueidentifier");
        migrationBuilder.AddCheckConstraint(name: "CK_PurchaseOrderLine_Identity", table: "PurchaseOrderLines",
            sql: "[ProductId] IS NOT NULL OR ([SupplierProductId] IS NOT NULL AND [PricingJson] IS NOT NULL)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [PurchaseOrderLines] WHERE [ProductId] IS NULL) THROW 50000, 'PO lines without a WROS product exist. Resolve them before rolling back.', 1;");
        migrationBuilder.DropCheckConstraint(name: "CK_PurchaseOrderLine_Identity", table: "PurchaseOrderLines");
        migrationBuilder.AlterColumn<Guid>(name: "ProductId", table: "PurchaseOrderLines", type: "uniqueidentifier", nullable: false,
            oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wros.Api.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase31SupplierCatalogPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PricingJson",
                table: "PurchaseOrderLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierProductId",
                table: "PurchaseOrderLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SupplierStockImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    HeadersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MappingJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierStockImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierStockImports_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Rsp = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    SupplierPrice = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DiscountType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    NetUnitPrice = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AvailableQuantity = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: true),
                    LatestImportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierProducts", x => x.Id);
                    table.CheckConstraint("CK_SupplierProduct_Price", "[SupplierPrice]>=0 AND [NetUnitPrice]>=0 AND ([Rsp] IS NULL OR [Rsp]>=0) AND [DiscountValue]>=0");
                    table.ForeignKey(
                        name: "FK_SupplierProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierProducts_SupplierStockImports_LatestImportId",
                        column: x => x.LatestImportId,
                        principalTable: "SupplierStockImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierProducts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierStockImportRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    CellsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierStockImportRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierStockImportRows_SupplierProducts_SupplierProductId",
                        column: x => x.SupplierProductId,
                        principalTable: "SupplierProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierStockImportRows_SupplierStockImports_ImportId",
                        column: x => x.ImportId,
                        principalTable: "SupplierStockImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_SupplierProductId",
                table: "PurchaseOrderLines",
                column: "SupplierProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProducts_LatestImportId",
                table: "SupplierProducts",
                column: "LatestImportId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProducts_ProductId",
                table: "SupplierProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProducts_SupplierId_Code",
                table: "SupplierProducts",
                columns: new[] { "SupplierId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProducts_SupplierId_LatestImportId",
                table: "SupplierProducts",
                columns: new[] { "SupplierId", "LatestImportId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierStockImportRows_ImportId_RowNumber",
                table: "SupplierStockImportRows",
                columns: new[] { "ImportId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierStockImportRows_SupplierProductId",
                table: "SupplierStockImportRows",
                column: "SupplierProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierStockImports_SupplierId_CreatedAt",
                table: "SupplierStockImports",
                columns: new[] { "SupplierId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierStockImports_SupplierId_FileHash",
                table: "SupplierStockImports",
                columns: new[] { "SupplierId", "FileHash" },
                unique: true,
                filter: "[ConfirmedAt] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderLines_SupplierProducts_SupplierProductId",
                table: "PurchaseOrderLines",
                column: "SupplierProductId",
                principalTable: "SupplierProducts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderLines_SupplierProducts_SupplierProductId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropTable(
                name: "SupplierStockImportRows");

            migrationBuilder.DropTable(
                name: "SupplierProducts");

            migrationBuilder.DropTable(
                name: "SupplierStockImports");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLines_SupplierProductId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "PricingJson",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "SupplierProductId",
                table: "PurchaseOrderLines");
        }
    }
}

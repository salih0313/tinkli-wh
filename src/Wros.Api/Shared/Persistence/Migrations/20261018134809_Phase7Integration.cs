using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wros.Api.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7Integration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovement_Sign",
                table: "StockMovements");

            migrationBuilder.AddColumn<string>(
                name: "IntegrationClientId",
                table: "StockMovements",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IntegrationClient",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationClient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssertionReplay",
                columns: table => new
                {
                    ClientId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TokenIdHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssertionReplay", x => new { x.ClientId, x.TokenIdHash });
                    table.ForeignKey(
                        name: "FK_AssertionReplay_IntegrationClient_ClientId",
                        column: x => x.ClientId,
                        principalTable: "IntegrationClient",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalOrderDeduction",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExternalOrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalOrderDeduction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalOrderDeduction_IntegrationClient_ClientId",
                        column: x => x.ClientId,
                        principalTable: "IntegrationClient",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventorySyncSnapshot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventorySyncSnapshot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventorySyncSnapshot_IntegrationClient_ClientId",
                        column: x => x.ClientId,
                        principalTable: "IntegrationClient",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductMapping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ExternalProductId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductMapping", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductMapping_IntegrationClient_ClientId",
                        column: x => x.ClientId,
                        principalTable: "IntegrationClient",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductMapping_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalOrderDeductionLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalOrderDeductionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalLineId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExternalProductId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BalanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalOrderDeductionLine", x => x.Id);
                    table.CheckConstraint("CK_ExternalDeductionLine_Positive", "[Quantity]>0");
                    table.ForeignKey(
                        name: "FK_ExternalOrderDeductionLine_ExternalOrderDeduction_ExternalOrderDeductionId",
                        column: x => x.ExternalOrderDeductionId,
                        principalTable: "ExternalOrderDeduction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalOrderDeductionLine_InventoryBalances_BalanceId",
                        column: x => x.BalanceId,
                        principalTable: "InventoryBalances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalOrderDeductionLine_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventorySyncSnapshotLine",
                columns: table => new
                {
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventorySyncSnapshotLine", x => new { x.SnapshotId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_InventorySyncSnapshotLine_InventorySyncSnapshot_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "InventorySyncSnapshot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovement_Sign",
                table: "StockMovements",
                sql: "([Type] IN ('Receipt','RelocationIn','TransferIn') AND [Quantity]>0) OR ([Type] IN ('RelocationOut','SupplierReturn','Disposal','TransferOut','ExternalOrder') AND [Quantity]<0)");

            migrationBuilder.CreateIndex(
                name: "IX_AssertionReplay_ExpiresAt",
                table: "AssertionReplay",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrderDeduction_ClientId_EventId",
                table: "ExternalOrderDeduction",
                columns: new[] { "ClientId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrderDeduction_ClientId_ExternalOrderId",
                table: "ExternalOrderDeduction",
                columns: new[] { "ClientId", "ExternalOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrderDeduction_CompletedAt",
                table: "ExternalOrderDeduction",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrderDeductionLine_BalanceId",
                table: "ExternalOrderDeductionLine",
                column: "BalanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrderDeductionLine_ExternalOrderDeductionId_ExternalLineId",
                table: "ExternalOrderDeductionLine",
                columns: new[] { "ExternalOrderDeductionId", "ExternalLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrderDeductionLine_ProductId",
                table: "ExternalOrderDeductionLine",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySyncSnapshot_ClientId_CreatedAt",
                table: "InventorySyncSnapshot",
                columns: new[] { "ClientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventorySyncSnapshot_ExpiresAt",
                table: "InventorySyncSnapshot",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_InventorySyncSnapshot_Version",
                table: "InventorySyncSnapshot",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductMapping_ClientId_ExternalProductId",
                table: "ProductMapping",
                columns: new[] { "ClientId", "ExternalProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductMapping_ProductId_ClientId",
                table: "ProductMapping",
                columns: new[] { "ProductId", "ClientId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssertionReplay");

            migrationBuilder.DropTable(
                name: "ExternalOrderDeductionLine");

            migrationBuilder.DropTable(
                name: "InventorySyncSnapshotLine");

            migrationBuilder.DropTable(
                name: "ProductMapping");

            migrationBuilder.DropTable(
                name: "ExternalOrderDeduction");

            migrationBuilder.DropTable(
                name: "InventorySyncSnapshot");

            migrationBuilder.DropTable(
                name: "IntegrationClient");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovement_Sign",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "IntegrationClientId",
                table: "StockMovements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovement_Sign",
                table: "StockMovements",
                sql: "([Type] IN ('Receipt','RelocationIn','TransferIn') AND [Quantity]>0) OR ([Type] IN ('RelocationOut','SupplierReturn','Disposal','TransferOut') AND [Quantity]<0)");
        }
    }
}

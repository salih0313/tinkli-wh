using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wros.Api.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase6CompletedStockMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovement_Sign",
                table: "StockMovements");

            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceId",
                table: "StockMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                table: "StockMovements",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceType",
                table: "StockMovements",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StockTransaction",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeliveryNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransaction", x => x.Id);
                    table.CheckConstraint("CK_StockTransaction_Kind", "[Kind] IN ('SupplierReturn','Disposal','Transfer')");
                    table.ForeignKey(
                        name: "FK_StockTransaction_DeliveryNotes_DeliveryNoteId",
                        column: x => x.DeliveryNoteId,
                        principalTable: "DeliveryNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransaction_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransaction_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTransactionLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceBalanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    DestinationRackId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DestinationLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransactionLine", x => x.Id);
                    table.CheckConstraint("CK_StockTransactionLine_Positive", "[Quantity]>0");
                    table.ForeignKey(
                        name: "FK_StockTransactionLine_InventoryBalances_SourceBalanceId",
                        column: x => x.SourceBalanceId,
                        principalTable: "InventoryBalances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransactionLine_StockTransaction_StockTransactionId",
                        column: x => x.StockTransactionId,
                        principalTable: "StockTransaction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransactionLine_StorageLocations_DestinationLocationId",
                        column: x => x.DestinationLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransactionLine_StorageRacks_DestinationRackId",
                        column: x => x.DestinationRackId,
                        principalTable: "StorageRacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ReferenceType_ReferenceId",
                table: "StockMovements",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_Timestamp_Id",
                table: "StockMovements",
                columns: new[] { "Timestamp", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovement_Sign",
                table: "StockMovements",
                sql: "([Type] IN ('Receipt','RelocationIn','TransferIn') AND [Quantity]>0) OR ([Type] IN ('RelocationOut','SupplierReturn','Disposal','TransferOut') AND [Quantity]<0)");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransaction_DeliveryNoteId",
                table: "StockTransaction",
                column: "DeliveryNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransaction_IdempotencyKey",
                table: "StockTransaction",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransaction_Kind_CompletedAt_Id",
                table: "StockTransaction",
                columns: new[] { "Kind", "CompletedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransaction_Number",
                table: "StockTransaction",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransaction_PurchaseOrderId",
                table: "StockTransaction",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransaction_SupplierId",
                table: "StockTransaction",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactionLine_DestinationLocationId",
                table: "StockTransactionLine",
                column: "DestinationLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactionLine_DestinationRackId",
                table: "StockTransactionLine",
                column: "DestinationRackId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactionLine_SourceBalanceId",
                table: "StockTransactionLine",
                column: "SourceBalanceId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactionLine_StockTransactionId",
                table: "StockTransactionLine",
                column: "StockTransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockTransactionLine");

            migrationBuilder.DropTable(
                name: "StockTransaction");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ReferenceType_ReferenceId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_Timestamp_Id",
                table: "StockMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovement_Sign",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "ReferenceType",
                table: "StockMovements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovement_Sign",
                table: "StockMovements",
                sql: "([Type] IN ('Receipt','RelocationIn') AND [Quantity]>0) OR ([Type]='RelocationOut' AND [Quantity]<0)");
        }
    }
}

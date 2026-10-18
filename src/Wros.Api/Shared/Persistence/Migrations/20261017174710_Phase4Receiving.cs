using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wros.Api.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4Receiving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeliveryNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    PostedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryNotes_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryNotes_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryNoteLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryNoteLines", x => x.Id);
                    table.CheckConstraint("CK_DeliveryNoteLine_Quantity", "[Quantity]>=0");
                    table.ForeignKey(
                        name: "FK_DeliveryNoteLines_DeliveryNotes_DeliveryNoteId",
                        column: x => x.DeliveryNoteId,
                        principalTable: "DeliveryNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryNoteLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryNoteLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierBatch = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Expiry = table.Column<DateOnly>(type: "date", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockLots", x => x.Id);
                    table.CheckConstraint("CK_StockLot_Quantity", "[Quantity]>=0");
                    table.ForeignKey(
                        name: "FK_StockLots_DeliveryNoteLines_DeliveryNoteLineId",
                        column: x => x.DeliveryNoteLineId,
                        principalTable: "DeliveryNoteLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockLots_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryNoteAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockLotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryNoteAllocations", x => x.Id);
                    table.CheckConstraint("CK_ReceiptAllocation_Quantity", "[Quantity]>0");
                    table.ForeignKey(
                        name: "FK_DeliveryNoteAllocations_StockLots_StockLotId",
                        column: x => x.StockLotId,
                        principalTable: "StockLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryNoteAllocations_StorageLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeliveryNoteAllocations_StorageRacks_RackId",
                        column: x => x.RackId,
                        principalTable: "StorageRacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockLotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryBalances", x => x.Id);
                    table.CheckConstraint("CK_InventoryBalance_Nonnegative", "[Quantity]>=0");
                    table.ForeignKey(
                        name: "FK_InventoryBalances_StockLots_StockLotId",
                        column: x => x.StockLotId,
                        principalTable: "StockLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryBalances_StorageLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryBalances_StorageRacks_RackId",
                        column: x => x.RackId,
                        principalTable: "StorageRacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryBalances_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LotPlacements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockLotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotPlacements", x => x.Id);
                    table.CheckConstraint("CK_LotPlacement_Quantity", "[Quantity]>0");
                    table.ForeignKey(
                        name: "FK_LotPlacements_StockLots_StockLotId",
                        column: x => x.StockLotId,
                        principalTable: "StockLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotPlacements_StorageLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotPlacements_StorageRacks_RackId",
                        column: x => x.RackId,
                        principalTable: "StorageRacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockLotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CorrectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.CheckConstraint("CK_StockMovement_Sign", "([Type] IN ('Receipt','RelocationIn') AND [Quantity]>0) OR ([Type]='RelocationOut' AND [Quantity]<0)");
                    table.ForeignKey(
                        name: "FK_StockMovements_DeliveryNotes_DeliveryNoteId",
                        column: x => x.DeliveryNoteId,
                        principalTable: "DeliveryNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_StockLots_StockLotId",
                        column: x => x.StockLotId,
                        principalTable: "StockLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_StorageLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_StorageRacks_RackId",
                        column: x => x.RackId,
                        principalTable: "StorageRacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteAllocations_LocationId",
                table: "DeliveryNoteAllocations",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteAllocations_RackId",
                table: "DeliveryNoteAllocations",
                column: "RackId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteAllocations_StockLotId_RackId_LocationId",
                table: "DeliveryNoteAllocations",
                columns: new[] { "StockLotId", "RackId", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteLines_DeliveryNoteId_Position",
                table: "DeliveryNoteLines",
                columns: new[] { "DeliveryNoteId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteLines_ProductId",
                table: "DeliveryNoteLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_Number",
                table: "DeliveryNotes",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_PurchaseOrderId",
                table: "DeliveryNotes",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_ReceivedAt_Id",
                table: "DeliveryNotes",
                columns: new[] { "ReceivedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_Status_ReceivedAt",
                table: "DeliveryNotes",
                columns: new[] { "Status", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_SupplierId",
                table: "DeliveryNotes",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_LocationId",
                table: "InventoryBalances",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_RackId",
                table: "InventoryBalances",
                column: "RackId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_StockLotId_RackId_LocationId",
                table: "InventoryBalances",
                columns: new[] { "StockLotId", "RackId", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_WarehouseId_RackId_LocationId",
                table: "InventoryBalances",
                columns: new[] { "WarehouseId", "RackId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_LotPlacements_LocationId",
                table: "LotPlacements",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LotPlacements_RackId",
                table: "LotPlacements",
                column: "RackId");

            migrationBuilder.CreateIndex(
                name: "IX_LotPlacements_StockLotId_RackId_LocationId",
                table: "LotPlacements",
                columns: new[] { "StockLotId", "RackId", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockLots_DeliveryNoteLineId",
                table: "StockLots",
                column: "DeliveryNoteLineId");

            migrationBuilder.CreateIndex(
                name: "IX_StockLots_Expiry",
                table: "StockLots",
                column: "Expiry");

            migrationBuilder.CreateIndex(
                name: "IX_StockLots_ProductId_ReceivedAt",
                table: "StockLots",
                columns: new[] { "ProductId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_CorrectionId",
                table: "StockMovements",
                column: "CorrectionId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_DeliveryNoteId_Timestamp_Id",
                table: "StockMovements",
                columns: new[] { "DeliveryNoteId", "Timestamp", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_LocationId",
                table: "StockMovements",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_RackId",
                table: "StockMovements",
                column: "RackId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_StockLotId_Timestamp_Id",
                table: "StockMovements",
                columns: new[] { "StockLotId", "Timestamp", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_WarehouseId",
                table: "StockMovements",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryNoteAllocations");

            migrationBuilder.DropTable(
                name: "InventoryBalances");

            migrationBuilder.DropTable(
                name: "LotPlacements");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "StockLots");

            migrationBuilder.DropTable(
                name: "DeliveryNoteLines");

            migrationBuilder.DropTable(
                name: "DeliveryNotes");
        }
    }
}

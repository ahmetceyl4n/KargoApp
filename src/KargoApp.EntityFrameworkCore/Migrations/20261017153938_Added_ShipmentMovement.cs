using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KargoApp.Migrations
{
    /// <inheritdoc />
    public partial class Added_ShipmentMovement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppShipmentMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    MovementType = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppShipmentMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppShipmentMovements_AppShipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "AppShipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppShipmentMovements_ScanId",
                table: "AppShipmentMovements",
                column: "ScanId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppShipmentMovements_ShipmentId_OccurredAt",
                table: "AppShipmentMovements",
                columns: new[] { "ShipmentId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppShipmentMovements");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KargoApp.Migrations
{
    /// <inheritdoc />
    public partial class Added_Shipment_And_Branch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppBranches",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    City = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBranches", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "AppShipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackingNumber = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    SenderName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReceiverName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReceiverCity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WeightKg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LastBranchCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    LastMovementAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppShipments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppShipments_TrackingNumber",
                table: "AppShipments",
                column: "TrackingNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppBranches");

            migrationBuilder.DropTable(
                name: "AppShipments");
        }
    }
}

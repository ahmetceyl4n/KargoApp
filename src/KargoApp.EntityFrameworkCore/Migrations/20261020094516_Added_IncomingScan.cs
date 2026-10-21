using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KargoApp.Migrations
{
    /// <inheritdoc />
    public partial class Added_IncomingScan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppIncomingScans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackingNumber = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    BranchCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    MovementType = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Error = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppIncomingScans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppIncomingScans_TrackingNumber",
                table: "AppIncomingScans",
                column: "TrackingNumber",
                filter: "\"ProcessedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppIncomingScans");
        }
    }
}

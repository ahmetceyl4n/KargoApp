using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KargoApp.Migrations
{
    /// <inheritdoc />
    public partial class Added_IncomingScan_Retry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "AppIncomingScans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                table: "AppIncomingScans",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "AppIncomingScans");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "AppIncomingScans");
        }
    }
}

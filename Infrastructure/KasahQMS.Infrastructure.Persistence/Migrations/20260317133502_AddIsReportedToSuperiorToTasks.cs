using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasahQMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsReportedToSuperiorToTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReportedToSuperior",
                table: "QmsTasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ReportedToUserId",
                table: "QmsTasks",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReportedToSuperior",
                table: "QmsTasks");

            migrationBuilder.DropColumn(
                name: "ReportedToUserId",
                table: "QmsTasks");
        }
    }
}

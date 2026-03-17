using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasahQMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskApprovalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalRemarks",
                table: "QmsTasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "QmsTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedById",
                table: "QmsTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "QmsTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedById",
                table: "QmsTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionRemarks",
                table: "QmsTasks",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalRemarks",
                table: "QmsTasks");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "QmsTasks");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "QmsTasks");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "QmsTasks");

            migrationBuilder.DropColumn(
                name: "RejectedById",
                table: "QmsTasks");

            migrationBuilder.DropColumn(
                name: "RejectionRemarks",
                table: "QmsTasks");
        }
    }
}

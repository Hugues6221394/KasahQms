using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasahQMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLoginActivityAndReviewerRemarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewerRemarks",
                table: "QmsTasks",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserLoginActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    DeviceInfo = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedById = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLoginActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLoginActivities_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documents_CreatedById",
                table: "documents",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_UserLoginActivities_TenantId_Timestamp",
                table: "UserLoginActivities",
                columns: new[] { "TenantId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_UserLoginActivities_UserId",
                table: "UserLoginActivities",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_documents_users_CreatedById",
                table: "documents",
                column: "CreatedById",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documents_users_CreatedById",
                table: "documents");

            migrationBuilder.DropTable(
                name: "UserLoginActivities");

            migrationBuilder.DropIndex(
                name: "IX_documents_CreatedById",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "ReviewerRemarks",
                table: "QmsTasks");
        }
    }
}

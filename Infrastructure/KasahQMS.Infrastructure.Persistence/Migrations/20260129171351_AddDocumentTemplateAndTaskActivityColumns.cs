using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasahQMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTemplateAndTaskActivityColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "document_attachments");

            migrationBuilder.AddColumn<string>(
                name: "AuthorizedDepartmentIds",
                table: "documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTemplate",
                table: "documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceTemplateId",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetUserId",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TaskActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AttachmentPath = table.Column<string>(type: "text", nullable: true),
                    AttachmentName = table.Column<string>(type: "text", nullable: true),
                    ProgressPercentage = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskActivities_QmsTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "QmsTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskActivities_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QmsTasks_LinkedDocumentId",
                table: "QmsTasks",
                column: "LinkedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_TargetDepartmentId",
                table: "documents",
                column: "TargetDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_TargetUserId",
                table: "documents",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskActivities_TaskId",
                table: "TaskActivities",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskActivities_UserId",
                table: "TaskActivities",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_documents_organization_units_TargetDepartmentId",
                table: "documents",
                column: "TargetDepartmentId",
                principalTable: "organization_units",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_documents_users_TargetUserId",
                table: "documents",
                column: "TargetUserId",
                principalTable: "users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QmsTasks_documents_LinkedDocumentId",
                table: "QmsTasks",
                column: "LinkedDocumentId",
                principalTable: "documents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documents_organization_units_TargetDepartmentId",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "FK_documents_users_TargetUserId",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "FK_QmsTasks_documents_LinkedDocumentId",
                table: "QmsTasks");

            migrationBuilder.DropTable(
                name: "TaskActivities");

            migrationBuilder.DropIndex(
                name: "IX_QmsTasks_LinkedDocumentId",
                table: "QmsTasks");

            migrationBuilder.DropIndex(
                name: "IX_documents_TargetDepartmentId",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_TargetUserId",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "AuthorizedDepartmentIds",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "IsTemplate",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "SourceTemplateId",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "TargetUserId",
                table: "documents");

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "document_attachments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}

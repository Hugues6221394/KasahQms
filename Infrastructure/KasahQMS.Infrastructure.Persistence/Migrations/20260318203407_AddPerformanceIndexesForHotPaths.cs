using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasahQMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexesForHotPaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TrainingRecords_TenantId_Status_CreatedById",
                table: "TrainingRecords",
                columns: new[] { "TenantId", "Status", "CreatedById" });

            migrationBuilder.CreateIndex(
                name: "IX_TrainingRecords_TenantId_TrainerId_Status",
                table: "TrainingRecords",
                columns: new[] { "TenantId", "TrainerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TrainingRecords_TenantId_UserId_Status",
                table: "TrainingRecords",
                columns: new[] { "TenantId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_QmsTasks_TenantId_AssignedToId_Status",
                table: "QmsTasks",
                columns: new[] { "TenantId", "AssignedToId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_QmsTasks_TenantId_Status_CreatedById",
                table: "QmsTasks",
                columns: new[] { "TenantId", "Status", "CreatedById" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_TenantId_CreatedById_Status",
                table: "documents",
                columns: new[] { "TenantId", "CreatedById", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_TenantId_Status_CurrentApproverId",
                table: "documents",
                columns: new[] { "TenantId", "Status", "CurrentApproverId" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_TenantId_TargetUserId_Status",
                table: "documents",
                columns: new[] { "TenantId", "TargetUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_document_approvals_DocumentId_IsApproved",
                table: "document_approvals",
                columns: new[] { "DocumentId", "IsApproved" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_ThreadId_CreatedAt",
                table: "chat_messages",
                columns: new[] { "ThreadId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrainingRecords_TenantId_Status_CreatedById",
                table: "TrainingRecords");

            migrationBuilder.DropIndex(
                name: "IX_TrainingRecords_TenantId_TrainerId_Status",
                table: "TrainingRecords");

            migrationBuilder.DropIndex(
                name: "IX_TrainingRecords_TenantId_UserId_Status",
                table: "TrainingRecords");

            migrationBuilder.DropIndex(
                name: "IX_QmsTasks_TenantId_AssignedToId_Status",
                table: "QmsTasks");

            migrationBuilder.DropIndex(
                name: "IX_QmsTasks_TenantId_Status_CreatedById",
                table: "QmsTasks");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_documents_TenantId_CreatedById_Status",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_TenantId_Status_CurrentApproverId",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_TenantId_TargetUserId_Status",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_document_approvals_DocumentId_IsApproved",
                table: "document_approvals");

            migrationBuilder.DropIndex(
                name: "IX_chat_messages_ThreadId_CreatedAt",
                table: "chat_messages");
        }
    }
}

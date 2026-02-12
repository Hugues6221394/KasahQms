using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasahQMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentAttachmentsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_document_attachments_users_UploadedById",
                table: "document_attachments");

            migrationBuilder.DropIndex(
                name: "IX_document_attachments_UploadedById",
                table: "document_attachments");

            migrationBuilder.DropColumn(
                name: "Checksum",
                table: "document_attachments");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "document_attachments");

            migrationBuilder.DropColumn(
                name: "IsEncrypted",
                table: "document_attachments");

            migrationBuilder.DropColumn(
                name: "StoragePath",
                table: "document_attachments");

            migrationBuilder.DropColumn(
                name: "UploadedAt",
                table: "document_attachments");

            migrationBuilder.DropColumn(
                name: "UploadedById",
                table: "document_attachments");

            migrationBuilder.RenameColumn(
                name: "FileName",
                table: "document_attachments",
                newName: "FilePath");

            migrationBuilder.AlterColumn<string>(
                name: "OriginalFileName",
                table: "document_attachments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "document_attachments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceDocumentId",
                table: "document_attachments",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceDocumentId",
                table: "document_attachments");

            migrationBuilder.RenameColumn(
                name: "FilePath",
                table: "document_attachments",
                newName: "FileName");

            migrationBuilder.AlterColumn<string>(
                name: "OriginalFileName",
                table: "document_attachments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "document_attachments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Checksum",
                table: "document_attachments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "document_attachments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEncrypted",
                table: "document_attachments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StoragePath",
                table: "document_attachments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadedAt",
                table: "document_attachments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "UploadedById",
                table: "document_attachments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_document_attachments_UploadedById",
                table: "document_attachments",
                column: "UploadedById");

            migrationBuilder.AddForeignKey(
                name: "FK_document_attachments_users_UploadedById",
                table: "document_attachments",
                column: "UploadedById",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

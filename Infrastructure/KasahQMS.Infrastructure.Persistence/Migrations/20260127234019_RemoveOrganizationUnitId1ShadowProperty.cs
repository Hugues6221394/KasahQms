using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasahQMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOrganizationUnitId1ShadowProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_organization_units_OrganizationUnitId1",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_OrganizationUnitId1",
                table: "users");

            migrationBuilder.DropColumn(
                name: "OrganizationUnitId1",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationUnitId1",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_OrganizationUnitId1",
                table: "users",
                column: "OrganizationUnitId1");

            migrationBuilder.AddForeignKey(
                name: "FK_users_organization_units_OrganizationUnitId1",
                table: "users",
                column: "OrganizationUnitId1",
                principalTable: "organization_units",
                principalColumn: "Id");
        }
    }
}

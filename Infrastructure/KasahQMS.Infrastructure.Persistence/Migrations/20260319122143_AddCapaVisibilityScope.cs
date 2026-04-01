using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasahQMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCapaVisibilityScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGlobal",
                table: "Capas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetDepartmentId",
                table: "Capas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Capas_TargetDepartmentId",
                table: "Capas",
                column: "TargetDepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Capas_organization_units_TargetDepartmentId",
                table: "Capas",
                column: "TargetDepartmentId",
                principalTable: "organization_units",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Capas_organization_units_TargetDepartmentId",
                table: "Capas");

            migrationBuilder.DropIndex(
                name: "IX_Capas_TargetDepartmentId",
                table: "Capas");

            migrationBuilder.DropColumn(
                name: "IsGlobal",
                table: "Capas");

            migrationBuilder.DropColumn(
                name: "TargetDepartmentId",
                table: "Capas");
        }
    }
}

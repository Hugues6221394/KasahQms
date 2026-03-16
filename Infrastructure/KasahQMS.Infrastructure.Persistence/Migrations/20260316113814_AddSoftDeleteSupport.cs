using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasahQMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "UserLoginActivities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                table: "UserLoginActivities",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "UserLoginActivities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "user_permission_delegations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                table: "user_permission_delegations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "user_permission_delegations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "system_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                table: "system_settings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "system_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "StockReservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                table: "StockReservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "StockReservations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "StockMovements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "StockMovements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "StockLocations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                table: "StockLocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "StockLocations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "StockItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                table: "StockItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "StockItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "roles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                table: "roles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "QmsTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                table: "QmsTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "QmsTasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "organization_units",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                table: "organization_units",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "organization_units",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Capas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                table: "Capas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Capas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "audits",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                table: "audits",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "audits",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "access_policies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                table: "access_policies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "access_policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "users");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "UserLoginActivities");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "UserLoginActivities");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "UserLoginActivities");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "user_permission_delegations");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "user_permission_delegations");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "user_permission_delegations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "StockReservations");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "StockReservations");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "StockReservations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "StockLocations");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "StockLocations");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "StockLocations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "QmsTasks");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "QmsTasks");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "QmsTasks");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "organization_units");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "organization_units");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "organization_units");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Capas");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "Capas");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Capas");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "audits");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "audits");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "audits");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "access_policies");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "access_policies");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "access_policies");
        }
    }
}

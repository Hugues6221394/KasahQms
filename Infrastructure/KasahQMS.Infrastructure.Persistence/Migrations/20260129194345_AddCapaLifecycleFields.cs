using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasahQMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCapaLifecycleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrectiveActions",
                table: "Capas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImplementationNotes",
                table: "Capas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreventiveActions",
                table: "Capas",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorrectiveActions",
                table: "Capas");

            migrationBuilder.DropColumn(
                name: "ImplementationNotes",
                table: "Capas");

            migrationBuilder.DropColumn(
                name: "PreventiveActions",
                table: "Capas");
        }
    }
}

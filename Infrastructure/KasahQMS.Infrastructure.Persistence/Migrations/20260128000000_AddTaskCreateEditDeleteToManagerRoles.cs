using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KasahQMS.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds TaskCreate, TaskEdit, TaskDelete (and TaskAssign for TMD) to TMD, Deputy, Department Manager roles
/// so they can create, edit, and delete tasks. Existing DBs only; seeder already updated for new installs.
/// </summary>
public class AddTaskCreateEditDeleteToManagerRoles : Migration
{
    private const string TmdNew = "1,16,64,1024,32768,65536,131072,262144,524288,1048576,33554432";
    private const string DeputyNew = "1,16,64,1024,32768,65536,131072,262144,524288,1048576";
    private const string DeptManagerNew = "1,4,16,64,1024,32768,65536,131072,262144,524288";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($@"
            UPDATE roles SET ""Permissions"" = '{TmdNew}' WHERE ""Name"" = 'TMD';
            UPDATE roles SET ""Permissions"" = '{DeputyNew}' WHERE ""Name"" = 'Deputy Country Manager';
            UPDATE roles SET ""Permissions"" = '{DeptManagerNew}' WHERE ""Name"" = 'Department Manager';
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            UPDATE roles SET ""Permissions"" = '1,16,64,1024,32768,1048576,33554432' WHERE ""Name"" = 'TMD';
            UPDATE roles SET ""Permissions"" = '1,16,64,1024,32768,524288,1048576' WHERE ""Name"" = 'Deputy Country Manager';
            UPDATE roles SET ""Permissions"" = '1,4,16,64,1024,32768,524288' WHERE ""Name"" = 'Department Manager';
        ");
    }
}

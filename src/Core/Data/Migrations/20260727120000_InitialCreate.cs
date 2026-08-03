using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Ditak.Attendance.Core.Data;

#nullable disable

namespace Ditak.Attendance.Core.Data.Migrations;

[DbContext(typeof(AttendanceDbContext))]
[Migration("20260727120000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "companies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                LogoPath = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_companies", x => x.Id));

        migrationBuilder.CreateTable(
            name: "roles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_roles", x => x.Id));

        migrationBuilder.CreateTable(
            name: "sites",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                AllowedCidrs = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_sites", x => x.Id));

        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                PreferredLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "en"),
                ThemePreference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "system"),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_users", x => x.Id));

        migrationBuilder.CreateTable(
            name: "user_roles",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_roles", x => new { x.UserId, x.RoleId });
                table.ForeignKey(name: "FK_user_roles_roles_RoleId", column: x => x.RoleId, principalTable: "roles", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey(name: "FK_user_roles_users_UserId", column: x => x.UserId, principalTable: "users", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "employees",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                EmployeeCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Department = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_employees", x => x.Id);
                table.ForeignKey(name: "FK_employees_sites_SiteId", column: x => x.SiteId, principalTable: "sites", principalColumn: "Id");
                table.ForeignKey(name: "FK_employees_users_UserId", column: x => x.UserId, principalTable: "users", principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "attendance_day_summaries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                WorkedMinutes = table.Column<int>(type: "integer", nullable: false),
                HasOpenShift = table.Column<bool>(type: "boolean", nullable: false),
                CalculatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_attendance_day_summaries", x => x.Id);
                table.ForeignKey(name: "FK_attendance_day_summaries_employees_EmployeeId", column: x => x.EmployeeId, principalTable: "employees", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "attendance_events",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                EventType = table.Column<int>(type: "integer", nullable: false),
                Source = table.Column<int>(type: "integer", nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ClientIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                DeviceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_attendance_events", x => x.Id);
                table.ForeignKey(name: "FK_attendance_events_employees_EmployeeId", column: x => x.EmployeeId, principalTable: "employees", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey(name: "FK_attendance_events_sites_SiteId", column: x => x.SiteId, principalTable: "sites", principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "presence_hints",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                ClientIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_presence_hints", x => x.Id);
                table.ForeignKey(name: "FK_presence_hints_employees_EmployeeId", column: x => x.EmployeeId, principalTable: "employees", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey(name: "FK_presence_hints_sites_SiteId", column: x => x.SiteId, principalTable: "sites", principalColumn: "Id");
            });

        migrationBuilder.CreateIndex(name: "IX_attendance_day_summaries_EmployeeId_WorkDate", table: "attendance_day_summaries", columns: new[] { "EmployeeId", "WorkDate" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_attendance_events_EmployeeId_OccurredAtUtc", table: "attendance_events", columns: new[] { "EmployeeId", "OccurredAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_attendance_events_IdempotencyKey", table: "attendance_events", column: "IdempotencyKey", unique: true);
        migrationBuilder.CreateIndex(name: "IX_attendance_events_SiteId", table: "attendance_events", column: "SiteId");
        migrationBuilder.CreateIndex(name: "IX_employees_EmployeeCode", table: "employees", column: "EmployeeCode", unique: true);
        migrationBuilder.CreateIndex(name: "IX_employees_SiteId", table: "employees", column: "SiteId");
        migrationBuilder.CreateIndex(name: "IX_employees_UserId", table: "employees", column: "UserId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_presence_hints_EmployeeId", table: "presence_hints", column: "EmployeeId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_presence_hints_SiteId", table: "presence_hints", column: "SiteId");
        migrationBuilder.CreateIndex(name: "IX_roles_Name", table: "roles", column: "Name", unique: true);
        migrationBuilder.CreateIndex(name: "IX_user_roles_RoleId", table: "user_roles", column: "RoleId");
        migrationBuilder.CreateIndex(name: "IX_users_Email", table: "users", column: "Email", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "attendance_day_summaries");
        migrationBuilder.DropTable(name: "attendance_events");
        migrationBuilder.DropTable(name: "presence_hints");
        migrationBuilder.DropTable(name: "user_roles");
        migrationBuilder.DropTable(name: "employees");
        migrationBuilder.DropTable(name: "roles");
        migrationBuilder.DropTable(name: "sites");
        migrationBuilder.DropTable(name: "users");
        migrationBuilder.DropTable(name: "companies");
    }
}

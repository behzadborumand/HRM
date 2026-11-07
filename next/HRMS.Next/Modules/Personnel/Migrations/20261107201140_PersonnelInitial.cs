using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Next.Modules.Personnel.Migrations
{
    /// <inheritdoc />
    public partial class PersonnelInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "personnel");

            migrationBuilder.CreateTable(
                name: "EmployeeAudits",
                schema: "personnel",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    AtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "personnel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    HireDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TerminationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAudits_EmployeeId_AtUtc",
                schema: "personnel",
                table: "EmployeeAudits",
                columns: new[] { "EmployeeId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId_DepartmentId_LastName",
                schema: "personnel",
                table: "Employees",
                columns: new[] { "CompanyId", "DepartmentId", "LastName" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId_EmployeeNumber",
                schema: "personnel",
                table: "Employees",
                columns: new[] { "CompanyId", "EmployeeNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeAudits",
                schema: "personnel");

            migrationBuilder.DropTable(
                name: "Employees",
                schema: "personnel");
        }
    }
}

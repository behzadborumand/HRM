using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Next.Modules.Access.Migrations
{
    /// <inheritdoc />
    public partial class AccountAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                schema: "access",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "access",
                table: "Users",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "Audits",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    TargetUserId = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    AtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_CompanyId_EmployeeId",
                schema: "access",
                table: "Users",
                columns: new[] { "CompanyId", "EmployeeId" },
                unique: true,
                filter: "[EmployeeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Audits_CompanyId_TargetUserId_AtUtc",
                schema: "access",
                table: "Audits",
                columns: new[] { "CompanyId", "TargetUserId", "AtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Audits",
                schema: "access");

            migrationBuilder.DropIndex(
                name: "IX_Users_CompanyId_EmployeeId",
                schema: "access",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                schema: "access",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "access",
                table: "Users");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Next.Modules.Access.Migrations
{
    /// <inheritdoc />
    public partial class ScopedRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoleId",
                schema: "access",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    CanReadEmployees = table.Column<bool>(type: "bit", nullable: false),
                    CanWriteEmployees = table.Column<bool>(type: "bit", nullable: false),
                    CanManageTeam = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                    table.UniqueConstraint("AK_Roles_Id_CompanyId", x => new { x.Id, x.CompanyId });
                });

            migrationBuilder.CreateTable(
                name: "RoleAudits",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    AtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleAudits_Roles_RoleId_CompanyId",
                        columns: x => new { x.RoleId, x.CompanyId },
                        principalSchema: "access",
                        principalTable: "Roles",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId_CompanyId",
                schema: "access",
                table: "Users",
                columns: new[] { "RoleId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleAudits_CompanyId_RoleId_AtUtc",
                schema: "access",
                table: "RoleAudits",
                columns: new[] { "CompanyId", "RoleId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleAudits_RoleId_CompanyId",
                schema: "access",
                table: "RoleAudits",
                columns: new[] { "RoleId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_CompanyId_Name",
                schema: "access",
                table: "Roles",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_RoleId_CompanyId",
                schema: "access",
                table: "Users",
                columns: new[] { "RoleId", "CompanyId" },
                principalSchema: "access",
                principalTable: "Roles",
                principalColumns: new[] { "Id", "CompanyId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_RoleId_CompanyId",
                schema: "access",
                table: "Users");

            migrationBuilder.DropTable(
                name: "RoleAudits",
                schema: "access");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "access");

            migrationBuilder.DropIndex(
                name: "IX_Users_RoleId_CompanyId",
                schema: "access",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RoleId",
                schema: "access",
                table: "Users");
        }
    }
}

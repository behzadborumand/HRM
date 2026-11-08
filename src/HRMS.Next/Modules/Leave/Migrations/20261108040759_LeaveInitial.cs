using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Next.Modules.Leave.Migrations
{
    /// <inheritdoc />
    public partial class LeaveInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "leave");

            migrationBuilder.CreateTable(
                name: "Policies",
                schema: "leave",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AnnualHours = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Policies", x => x.Id);
                    table.UniqueConstraint("AK_Policies_Id_CompanyId", x => new { x.Id, x.CompanyId });
                });

            migrationBuilder.CreateTable(
                name: "Balances",
                schema: "leave",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    PolicyId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    AccruedHours = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    UsedHours = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Balances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Balances_Policies_PolicyId_CompanyId",
                        columns: x => new { x.PolicyId, x.CompanyId },
                        principalSchema: "leave",
                        principalTable: "Policies",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Requests",
                schema: "leave",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    PolicyId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Hours = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Requests_Policies_PolicyId_CompanyId",
                        columns: x => new { x.PolicyId, x.CompanyId },
                        principalSchema: "leave",
                        principalTable: "Policies",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Balances_CompanyId_EmployeeId_PolicyId_Year",
                schema: "leave",
                table: "Balances",
                columns: new[] { "CompanyId", "EmployeeId", "PolicyId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Balances_PolicyId_CompanyId",
                schema: "leave",
                table: "Balances",
                columns: new[] { "PolicyId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Policies_CompanyId_Name",
                schema: "leave",
                table: "Policies",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requests_CompanyId_EmployeeId_Status_StartDate",
                schema: "leave",
                table: "Requests",
                columns: new[] { "CompanyId", "EmployeeId", "Status", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_PolicyId_CompanyId",
                schema: "leave",
                table: "Requests",
                columns: new[] { "PolicyId", "CompanyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Balances",
                schema: "leave");

            migrationBuilder.DropTable(
                name: "Requests",
                schema: "leave");

            migrationBuilder.DropTable(
                name: "Policies",
                schema: "leave");
        }
    }
}

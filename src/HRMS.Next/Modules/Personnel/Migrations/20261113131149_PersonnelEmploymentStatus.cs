using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Next.Modules.Personnel.Migrations
{
    /// <inheritdoc />
    public partial class PersonnelEmploymentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOnLeave",
                schema: "personnel",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "EmploymentStatusChanges",
                schema: "personnel",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: true),
                    AtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmploymentStatusChanges", x => x.Id);
                    table.CheckConstraint("CK_EmploymentStatusChanges_Baseline", "([EffectiveDate] IS NULL AND [FromStatus] IS NULL AND [ActorUserId] IS NULL) OR ([EffectiveDate] IS NOT NULL AND [FromStatus] IS NOT NULL AND [ActorUserId] IS NOT NULL)");
                    table.CheckConstraint("CK_EmploymentStatusChanges_Status", "[ToStatus] IN (N'Active',N'OnLeave') AND ([FromStatus] IS NULL OR ([FromStatus] IN (N'Active',N'OnLeave') AND [FromStatus]<>[ToStatus]))");
                    table.ForeignKey(
                        name: "FK_EmploymentStatusChanges_Employees_EmployeeId_CompanyId",
                        columns: x => new { x.EmployeeId, x.CompanyId },
                        principalSchema: "personnel",
                        principalTable: "Employees",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentStatusChanges_CompanyId_EmployeeId_EffectiveDate_Id",
                schema: "personnel",
                table: "EmploymentStatusChanges",
                columns: new[] { "CompanyId", "EmployeeId", "EffectiveDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentStatusChanges_EmployeeId_CompanyId",
                schema: "personnel",
                table: "EmploymentStatusChanges",
                columns: new[] { "EmployeeId", "CompanyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmploymentStatusChanges",
                schema: "personnel");

            migrationBuilder.DropColumn(
                name: "IsOnLeave",
                schema: "personnel",
                table: "Employees");
        }
    }
}

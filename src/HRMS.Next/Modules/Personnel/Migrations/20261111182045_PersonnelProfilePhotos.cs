using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Next.Modules.Personnel.Migrations
{
    /// <inheritdoc />
    public partial class PersonnelProfilePhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeePhotos",
                schema: "personnel",
                columns: table => new
                {
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Bytes = table.Column<byte[]>(type: "varbinary(max)", maxLength: 2097152, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePhotos", x => x.EmployeeId);
                    table.ForeignKey(
                        name: "FK_EmployeePhotos_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "personnel",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId_LastName_FirstName_Id",
                schema: "personnel",
                table: "Employees",
                columns: new[] { "CompanyId", "LastName", "FirstName", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePhotos_CompanyId",
                schema: "personnel",
                table: "EmployeePhotos",
                column: "CompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeePhotos",
                schema: "personnel");

            migrationBuilder.DropIndex(
                name: "IX_Employees_CompanyId_LastName_FirstName_Id",
                schema: "personnel",
                table: "Employees");
        }
    }
}

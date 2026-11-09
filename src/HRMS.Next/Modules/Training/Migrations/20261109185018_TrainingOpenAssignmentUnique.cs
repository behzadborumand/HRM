using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Next.Modules.Training.Migrations
{
    /// <inheritdoc />
    public partial class TrainingOpenAssignmentUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Assignments_CompanyId_EmployeeId_CourseId",
                schema: "training",
                table: "Assignments",
                columns: new[] { "CompanyId", "EmployeeId", "CourseId" },
                unique: true,
                filter: "[CompletedDate] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assignments_CompanyId_EmployeeId_CourseId",
                schema: "training",
                table: "Assignments");
        }
    }
}

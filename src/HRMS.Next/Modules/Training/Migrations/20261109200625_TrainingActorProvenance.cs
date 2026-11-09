using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Next.Modules.Training.Migrations
{
    /// <inheritdoc />
    public partial class TrainingActorProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "AssignedByUserId",
                schema: "training",
                table: "Assignments",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "AssignedByEmployeeId",
                schema: "training",
                table: "Assignments",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM training.Assignments WHERE AssignedByUserId IS NULL OR AssignedByEmployeeId IS NOT NULL) THROW 51320, 'Cannot downgrade Training actor provenance while legacy or unknown actors exist.', 1;");

            migrationBuilder.DropColumn(
                name: "AssignedByEmployeeId",
                schema: "training",
                table: "Assignments");

            migrationBuilder.AlterColumn<int>(
                name: "AssignedByUserId",
                schema: "training",
                table: "Assignments",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}

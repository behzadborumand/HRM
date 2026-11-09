using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Next.Modules.Leave.Migrations
{
    /// <inheritdoc />
    public partial class LeaveActorProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "RequestedByUserId",
                schema: "leave",
                table: "Requests",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "DecidedByEmployeeId",
                schema: "leave",
                table: "Requests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequestedByEmployeeId",
                schema: "leave",
                table: "Requests",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [leave].[Requests] WHERE [RequestedByUserId] IS NULL OR [RequestedByEmployeeId] IS NOT NULL OR [DecidedByEmployeeId] IS NOT NULL) BEGIN THROW 51210, 'Cannot roll back Leave actor provenance while employee-attributed requests exist.', 1; END;");
            migrationBuilder.DropColumn(
                name: "DecidedByEmployeeId",
                schema: "leave",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "RequestedByEmployeeId",
                schema: "leave",
                table: "Requests");

            migrationBuilder.AlterColumn<int>(
                name: "RequestedByUserId",
                schema: "leave",
                table: "Requests",
                type: "int",
                nullable: false,

                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}

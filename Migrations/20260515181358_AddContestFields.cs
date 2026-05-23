using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMvcApp.Migrations
{
    /// <inheritdoc />
    public partial class AddContestFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_registered",
                table: "contests");

            migrationBuilder.DropColumn(
                name: "participant_count",
                table: "contests");

            migrationBuilder.DropColumn(
                name: "problem_count",
                table: "contests");

            migrationBuilder.DropColumn(
                name: "status",
                table: "contests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_registered",
                table: "contests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "participant_count",
                table: "contests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "problem_count",
                table: "contests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "contests",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}

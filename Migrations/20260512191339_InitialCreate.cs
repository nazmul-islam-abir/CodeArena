using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyMvcApp.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contest_registrations",
                columns: table => new
                {
                    contest_id = table.Column<int>(type: "integer", nullable: false),
                    user_name = table.Column<string>(type: "text", nullable: false),
                    registration_time = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contest_registrations", x => new { x.contest_id, x.user_name });
                });

            migrationBuilder.CreateTable(
                name: "contests",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    organizer = table.Column<string>(type: "text", nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    duration = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    participant_count = table.Column<int>(type: "integer", nullable: false),
                    problem_count = table.Column<int>(type: "integer", nullable: false),
                    difficulty = table.Column<string>(type: "text", nullable: false),
                    is_registered = table.Column<bool>(type: "boolean", nullable: false),
                    contest_link = table.Column<string>(type: "text", nullable: false),
                    requires_approval = table.Column<bool>(type: "boolean", nullable: false),
                    approval_status = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_frozen = table.Column<bool>(type: "boolean", nullable: false),
                    freeze_time = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "problems",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    input_format = table.Column<string>(type: "text", nullable: true),
                    output_format = table.Column<string>(type: "text", nullable: true),
                    sample_input = table.Column<string>(type: "text", nullable: true),
                    sample_output = table.Column<string>(type: "text", nullable: true),
                    constraints = table.Column<string>(type: "text", nullable: true),
                    difficulty = table.Column<int>(type: "integer", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<string>(type: "text", nullable: true),
                    time_limit = table.Column<int>(type: "integer", nullable: false),
                    memory_limit = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    solved_count = table.Column<int>(type: "integer", nullable: false),
                    submission_count = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    source_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_problems", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "submissions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    problem_id = table.Column<int>(type: "integer", nullable: false),
                    language_id = table.Column<int>(type: "integer", nullable: false),
                    language_name = table.Column<string>(type: "text", nullable: false),
                    source_code = table.Column<string>(type: "text", nullable: false),
                    verdict = table.Column<string>(type: "text", nullable: false),
                    execution_time = table.Column<double>(type: "double precision", nullable: true),
                    memory_used = table.Column<int>(type: "integer", nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    contest_id = table.Column<int>(type: "integer", nullable: true),
                    test_cases_passed = table.Column<int>(type: "integer", nullable: true),
                    total_test_cases = table.Column<int>(type: "integer", nullable: true),
                    unique_id = table.Column<string>(type: "text", nullable: false),
                    problem_title = table.Column<string>(type: "text", nullable: false),
                    user_name = table.Column<string>(type: "text", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "test_cases",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    problem_id = table.Column<int>(type: "integer", nullable: false),
                    input = table.Column<string>(type: "text", nullable: false),
                    expected_output = table.Column<string>(type: "text", nullable: false),
                    is_sample = table.Column<bool>(type: "boolean", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_test_cases", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    first_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    student_id = table.Column<string>(type: "text", nullable: true),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    problems_solved = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    contests_participated = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    total_points = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contest_problems",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    contest_id = table.Column<int>(type: "integer", nullable: false),
                    problem_id = table.Column<int>(type: "integer", nullable: false),
                    custom_title = table.Column<string>(type: "text", nullable: true),
                    order = table.Column<int>(type: "integer", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contest_problems", x => x.id);
                    table.ForeignKey(
                        name: "fk_contest_problems_contests_contest_id",
                        column: x => x.contest_id,
                        principalTable: "contests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_contest_problems_problems_problem_id",
                        column: x => x.problem_id,
                        principalTable: "problems",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contest_problems_contest_id",
                table: "contest_problems",
                column: "contest_id");

            migrationBuilder.CreateIndex(
                name: "ix_contest_problems_problem_id",
                table: "contest_problems",
                column: "problem_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contest_problems");

            migrationBuilder.DropTable(
                name: "contest_registrations");

            migrationBuilder.DropTable(
                name: "submissions");

            migrationBuilder.DropTable(
                name: "test_cases");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "contests");

            migrationBuilder.DropTable(
                name: "problems");
        }
    }
}

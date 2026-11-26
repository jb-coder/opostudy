using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpoStudy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMockExams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "mock_exam_duration",
                table: "quiz_attempts",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "mock_exam_exercise_number",
                table: "quiz_attempts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "mock_exam_max_score",
                table: "quiz_attempts",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "mock_exam_pass_score",
                table: "quiz_attempts",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "mock_exam_penalty_ratio",
                table: "quiz_attempts",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "mock_exam_question_count",
                table: "quiz_attempts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mock_exam_title",
                table: "quiz_attempts",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "score",
                table: "quiz_attempts",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "exam_exercises",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    question_count = table.Column<int>(type: "integer", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    max_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    penalty_ratio = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    sections = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exam_exercises", x => x.id);
                    table.ForeignKey(
                        name: "fk_exam_exercises_exams_exam_id",
                        column: x => x.exam_id,
                        principalTable: "exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_exam_exercises_exam_id_number",
                table: "exam_exercises",
                columns: new[] { "exam_id", "number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exam_exercises");

            migrationBuilder.DropColumn(
                name: "mock_exam_duration",
                table: "quiz_attempts");

            migrationBuilder.DropColumn(
                name: "mock_exam_exercise_number",
                table: "quiz_attempts");

            migrationBuilder.DropColumn(
                name: "mock_exam_max_score",
                table: "quiz_attempts");

            migrationBuilder.DropColumn(
                name: "mock_exam_pass_score",
                table: "quiz_attempts");

            migrationBuilder.DropColumn(
                name: "mock_exam_penalty_ratio",
                table: "quiz_attempts");

            migrationBuilder.DropColumn(
                name: "mock_exam_question_count",
                table: "quiz_attempts");

            migrationBuilder.DropColumn(
                name: "mock_exam_title",
                table: "quiz_attempts");

            migrationBuilder.DropColumn(
                name: "score",
                table: "quiz_attempts");
        }
    }
}

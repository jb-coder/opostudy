using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpoStudy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "review_schedules",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    box = table.Column<int>(type: "integer", nullable: false),
                    due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    last_answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_schedules", x => new { x.user_id, x.question_id });
                    table.ForeignKey(
                        name: "fk_review_schedules_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_review_schedules_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_review_schedules_question_id",
                table: "review_schedules",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_schedules_user_id_due_on",
                table: "review_schedules",
                columns: new[] { "user_id", "due_on" });

            // Backfill: the schedule of everything already answered, from the latest answer of each question
            // (same tie-break as the pending mistakes: greatest answered_at, then greatest quiz id).
            // Correct -> box 1 (due in 2 days); wrong -> box 0 (due in 1 day). users.time_zone is validated by .NET,
            // which also accepts Windows ids that Postgres does not know: those fall back to the default zone
            // instead of failing the whole migration.
            migrationBuilder.Sql(@"
INSERT INTO review_schedules (user_id, question_id, box, due_on, last_answered_at)
SELECT l.user_id,
       l.question_id,
       CASE WHEN l.is_correct THEN 1 ELSE 0 END,
       (l.answered_at AT TIME ZONE
           CASE WHEN EXISTS (SELECT 1 FROM pg_timezone_names z WHERE z.name = u.time_zone)
                THEN u.time_zone ELSE 'Europe/Madrid' END)::date
           + CASE WHEN l.is_correct THEN 2 ELSE 1 END,
       l.answered_at
FROM (
    SELECT DISTINCT ON (a.user_id, x.question_id)
           a.user_id, x.question_id, x.is_correct, x.answered_at
    FROM quiz_answers x
    JOIN quiz_attempts a ON a.id = x.quiz_attempt_id
    WHERE x.is_correct IS NOT NULL
    ORDER BY a.user_id, x.question_id, x.answered_at DESC, a.id DESC
) l
JOIN users u ON u.id = l.user_id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "review_schedules");
        }
    }
}

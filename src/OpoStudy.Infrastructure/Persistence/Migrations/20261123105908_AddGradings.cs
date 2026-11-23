using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpoStudy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGradings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gradings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    practical_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    max_points = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    feedback = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    corrector = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    input_tokens = table.Column<int>(type: "integer", nullable: true),
                    output_tokens = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gradings", x => x.id);
                    table.ForeignKey(
                        name: "fk_gradings_practical_cases_practical_case_id",
                        column: x => x.practical_case_id,
                        principalTable: "practical_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_gradings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grading_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grading_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    max_points = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    feedback = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    criteria = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grading_answers", x => x.id);
                    table.ForeignKey(
                        name: "fk_grading_answers_gradings_grading_id",
                        column: x => x.grading_id,
                        principalTable: "gradings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_grading_answers_grading_id_order",
                table: "grading_answers",
                columns: new[] { "grading_id", "order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_gradings_practical_case_id",
                table: "gradings",
                column: "practical_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_gradings_status_next_attempt_at",
                table: "gradings",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_gradings_user_id_submitted_at",
                table: "gradings",
                columns: new[] { "user_id", "submitted_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ux_gradings_one_in_progress_per_user",
                table: "gradings",
                column: "user_id",
                unique: true,
                filter: "status IN ('Pending', 'Processing')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grading_answers");

            migrationBuilder.DropTable(
                name: "gradings");
        }
    }
}

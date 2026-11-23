using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpoStudy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPracticalCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "practical_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    statement = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    estimated_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_practical_cases", x => x.id);
                    table.ForeignKey(
                        name: "fk_practical_cases_exams_exam_id",
                        column: x => x.exam_id,
                        principalTable: "exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "practical_case_parts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    practical_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    statement = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    model_answer = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    max_points = table.Column<int>(type: "integer", nullable: false),
                    criteria = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_practical_case_parts", x => x.id);
                    table.ForeignKey(
                        name: "fk_practical_case_parts_practical_cases_practical_case_id",
                        column: x => x.practical_case_id,
                        principalTable: "practical_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_practical_case_parts_practical_case_id_order",
                table: "practical_case_parts",
                columns: new[] { "practical_case_id", "order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_practical_cases_exam_id",
                table: "practical_cases",
                column: "exam_id");

            migrationBuilder.CreateIndex(
                name: "ix_practical_cases_key",
                table: "practical_cases",
                column: "key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "practical_case_parts");

            migrationBuilder.DropTable(
                name: "practical_cases");
        }
    }
}

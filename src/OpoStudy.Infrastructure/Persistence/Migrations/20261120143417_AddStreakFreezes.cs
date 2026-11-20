using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpoStudy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStreakFreezes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "streak_freezes",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "streak_protected_through",
                table: "users",
                type: "date",
                nullable: true);

            // xmin is a PostgreSQL system column: it already exists on users, so no column is added.

            migrationBuilder.AddColumn<int>(
                name: "xp_spent",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "freeze_used_at",
                table: "daily_activities",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "streak_freezes",
                table: "users");

            migrationBuilder.DropColumn(
                name: "streak_protected_through",
                table: "users");

            migrationBuilder.DropColumn(
                name: "xp_spent",
                table: "users");

            migrationBuilder.DropColumn(
                name: "freeze_used_at",
                table: "daily_activities");
        }
    }
}

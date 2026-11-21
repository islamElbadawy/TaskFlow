using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "position",
                table: "tasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill: number each status column from 0, soonest due date first (no due date last), then oldest
            // first; the id only makes ties deterministic.
            migrationBuilder.Sql("""
                UPDATE tasks AS t
                SET position = ranked.row_number - 1
                FROM (
                    SELECT id, ROW_NUMBER() OVER (
                        PARTITION BY status
                        ORDER BY due_date ASC NULLS LAST, created_at ASC, id ASC) AS row_number
                    FROM tasks
                ) AS ranked
                WHERE t.id = ranked.id;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_tasks_status_position",
                table: "tasks",
                columns: new[] { "status", "position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tasks_status_position",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "position",
                table: "tasks");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkNotificationsToTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_notifications_related_task_id",
                table: "notifications",
                column: "related_task_id");

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_tasks_related_task_id",
                table: "notifications",
                column: "related_task_id",
                principalTable: "tasks",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notifications_tasks_related_task_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_related_task_id",
                table: "notifications");
        }
    }
}

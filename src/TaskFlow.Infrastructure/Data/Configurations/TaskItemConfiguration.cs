using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Data.Configurations;

public class TaskItemConfiguration: IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(t => t.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.Priority)
            .HasConversion<string>()
            .HasMaxLength(20);

        // "Tasks assigned to me (by status)" and "tasks I created" are the main access paths.
        builder.HasIndex(t => new { t.AssignedToUserId, t.Status });
        builder.HasIndex(t => t.CreatedByUserId);

        // The board reads a column in order and appends to its end (highest position in a status).
        builder.Property(t => t.Position).HasDefaultValue(0);
        builder.HasIndex(t => new { t.Status, t.Position });

        builder.HasMany(t => t.Comments)
            .WithOne(t => t.Task)
            .HasForeignKey(t => t.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
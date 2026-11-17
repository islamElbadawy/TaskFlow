using System;

namespace TaskFlow.Domain.Common;

public abstract class BaseEntity
{
    // Version 7 GUIDs are time-ordered, so inserts append to the primary-key index instead of fragmenting it.
    public Guid Id { get; set; } = Guid.CreateVersion7();

    // Set by ApplicationDbContext on insert when left at default (seed data may supply its own value). Always UTC.
    public DateTime CreatedAt { get; set; }

    // Null until the entity is first modified.
    public DateTime? UpdatedAt { get; set; }
}

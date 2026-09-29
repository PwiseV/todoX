namespace TodoX.Api.Entities;

public class TaskEntity
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>"active" or "complete" (enforced by a DB check constraint).</summary>
    public string Status { get; set; } = "active";

    /// <summary>Client-managed; the server never sets or validates it against Status.</summary>
    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

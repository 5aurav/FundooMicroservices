namespace Fundoo.Notes.Domain.Entity;

public class NoteCollaborator
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NoteId { get; set; }
    public string CollaboratorEmail { get; set; } = string.Empty;
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}

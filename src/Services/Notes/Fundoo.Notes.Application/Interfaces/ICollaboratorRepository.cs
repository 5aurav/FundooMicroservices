using Fundoo.Notes.Domain.Entity;

namespace Fundoo.Notes.Application.Interfaces
{
    public interface ICollaboratorRepository
    {
        Task<IReadOnlyList<NoteCollaborator>> GetCollaboratorsByNoteIdAsync(
            Guid noteId,
            CancellationToken cancellationToken = default);

        Task<bool> AddCollaboratorAsync(
            NoteCollaborator collaborator,
            CancellationToken cancellationToken = default);

        Task<bool> RemoveCollaboratorAsync(
            Guid noteId,
            string collaboratorEmail,
            CancellationToken cancellationToken = default);

        Task<bool> IsCollaboratorAsync(
            Guid noteId,
            string userEmail,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Note>> GetCollaboratedNotesAsync(
            string userEmail,
            CancellationToken cancellationToken = default);
    }
}

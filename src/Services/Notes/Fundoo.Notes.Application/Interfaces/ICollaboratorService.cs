using Fundoo.Notes.Domain.Entity;

namespace Fundoo.Notes.Application.Interfaces
{
    public interface ICollaboratorService
    {
        Task<IReadOnlyList<NoteCollaborator>> GetCollaboratorsAsync(
            Guid noteId,
            Guid userId,
            string userEmail,
            CancellationToken cancellationToken = default);

        Task<bool> AddCollaboratorAsync(
            Guid noteId,
            Guid ownerId,
            string collaboratorEmail,
            CancellationToken cancellationToken = default);

        Task<bool> RemoveCollaboratorAsync(
            Guid noteId,
            Guid ownerId,
            string collaboratorEmail,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Note>> GetCollaboratedNotesAsync(
            string userEmail,
            CancellationToken cancellationToken = default);
    }
}

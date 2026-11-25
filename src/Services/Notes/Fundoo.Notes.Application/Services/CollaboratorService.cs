using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Domain.Entity;

namespace Fundoo.Notes.Application.Services
{
    public class CollaboratorService : ICollaboratorService
    {
        private readonly ICollaboratorRepository _collaboratorRepository;
        private readonly INotesRepository _notesRepository;

        public CollaboratorService(
            ICollaboratorRepository collaboratorRepository,
            INotesRepository notesRepository)
        {
            _collaboratorRepository = collaboratorRepository;
            _notesRepository = notesRepository;
        }

        public async Task<IReadOnlyList<NoteCollaborator>> GetCollaboratorsAsync(
            Guid noteId,
            Guid userId,
            string userEmail,
            CancellationToken cancellationToken = default)
        {
            var note = await _notesRepository.GetByIdAsync(noteId, userId, cancellationToken);
            var isCollab = note != null || await _collaboratorRepository.IsCollaboratorAsync(noteId, userEmail, cancellationToken);

            if (!isCollab) return Array.Empty<NoteCollaborator>();

            return await _collaboratorRepository.GetCollaboratorsByNoteIdAsync(noteId, cancellationToken);
        }

        public async Task<bool> AddCollaboratorAsync(
            Guid noteId,
            Guid ownerId,
            string collaboratorEmail,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(collaboratorEmail)) return false;

            // Only the owner can add collaborators
            var note = await _notesRepository.GetByIdAsync(noteId, ownerId, cancellationToken);
            if (note == null || note.IsTrashed) return false;

            var collaborator = new NoteCollaborator
            {
                Id = Guid.NewGuid(),
                NoteId = noteId,
                CollaboratorEmail = collaboratorEmail.Trim().ToLowerInvariant(),
                AddedAtUtc = DateTime.UtcNow
            };

            return await _collaboratorRepository.AddCollaboratorAsync(collaborator, cancellationToken);
        }

        public async Task<bool> RemoveCollaboratorAsync(
            Guid noteId,
            Guid ownerId,
            string collaboratorEmail,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(collaboratorEmail)) return false;

            // Only the owner can remove collaborators
            var note = await _notesRepository.GetByIdAsync(noteId, ownerId, cancellationToken);
            if (note == null) return false;

            return await _collaboratorRepository.RemoveCollaboratorAsync(noteId, collaboratorEmail, cancellationToken);
        }

        public async Task<IReadOnlyList<Note>> GetCollaboratedNotesAsync(
            string userEmail,
            CancellationToken cancellationToken = default)
        {
            return await _collaboratorRepository.GetCollaboratedNotesAsync(userEmail, cancellationToken);
        }
    }
}

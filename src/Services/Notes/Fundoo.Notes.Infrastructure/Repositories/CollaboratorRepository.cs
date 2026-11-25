using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Domain.Entity;
using Fundoo.Notes.Infrastructure.Data;
using Npgsql;

namespace Fundoo.Notes.Infrastructure.Repositories
{
    public class CollaboratorRepository : ICollaboratorRepository
    {
        private readonly NotesDbConnectionFactory _connectionFactory;

        public CollaboratorRepository(NotesDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IReadOnlyList<NoteCollaborator>> GetCollaboratorsByNoteIdAsync(
            Guid noteId,
            CancellationToken cancellationToken = default)
        {
            const string query = """
                SELECT id, note_id, collaborator_email, added_at_utc
                FROM note_collaborators
                WHERE note_id = @noteId
                ORDER BY added_at_utc ASC;
                """;

            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("noteId", noteId);

            var list = new List<NoteCollaborator>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(new NoteCollaborator
                {
                    Id = reader.GetGuid(0),
                    NoteId = reader.GetGuid(1),
                    CollaboratorEmail = reader.GetString(2),
                    AddedAtUtc = reader.GetDateTime(3)
                });
            }

            return list;
        }

        public async Task<bool> AddCollaboratorAsync(
            NoteCollaborator collaborator,
            CancellationToken cancellationToken = default)
        {
            const string query = """
                INSERT INTO note_collaborators (id, note_id, collaborator_email, added_at_utc)
                VALUES (@id, @noteId, @email, @addedAtUtc)
                ON CONFLICT (note_id, collaborator_email) DO NOTHING;
                """;

            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("id", collaborator.Id);
            command.Parameters.AddWithValue("noteId", collaborator.NoteId);
            command.Parameters.AddWithValue("email", collaborator.CollaboratorEmail.Trim().ToLowerInvariant());
            command.Parameters.AddWithValue("addedAtUtc", collaborator.AddedAtUtc);

            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            return affected > 0;
        }

        public async Task<bool> RemoveCollaboratorAsync(
            Guid noteId,
            string collaboratorEmail,
            CancellationToken cancellationToken = default)
        {
            const string query = """
                DELETE FROM note_collaborators
                WHERE note_id = @noteId
                  AND collaborator_email = @email;
                """;

            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("noteId", noteId);
            command.Parameters.AddWithValue("email", collaboratorEmail.Trim().ToLowerInvariant());

            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            return affected > 0;
        }

        public async Task<bool> IsCollaboratorAsync(
            Guid noteId,
            string userEmail,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userEmail)) return false;

            const string query = """
                SELECT 1
                FROM note_collaborators
                WHERE note_id = @noteId
                  AND collaborator_email = @email
                LIMIT 1;
                """;

            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("noteId", noteId);
            command.Parameters.AddWithValue("email", userEmail.Trim().ToLowerInvariant());

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result != null;
        }

        public async Task<IReadOnlyList<Note>> GetCollaboratedNotesAsync(
            string userEmail,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userEmail)) return Array.Empty<Note>();

            const string query = """
                SELECT n.id, n.user_id, n.title, n.content, n.color,
                       n.is_pinned, n.is_archived, n.is_trashed,
                       n.created_at_utc, n.updated_at_utc
                FROM notes n
                INNER JOIN note_collaborators c ON n.id = c.note_id
                WHERE c.collaborator_email = @email
                  AND n.is_trashed = FALSE
                ORDER BY n.updated_at_utc DESC;
                """;

            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("email", userEmail.Trim().ToLowerInvariant());

            var notes = new List<Note>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                notes.Add(new Note
                {
                    Id = reader.GetGuid(0),
                    UserId = reader.GetGuid(1),
                    Title = reader.GetString(2),
                    Content = reader.GetString(3),
                    Color = reader.IsDBNull(4) ? null : reader.GetString(4),
                    IsPinned = reader.GetBoolean(5),
                    IsArchived = reader.GetBoolean(6),
                    IsTrashed = reader.GetBoolean(7),
                    CreatedAtUtc = reader.GetDateTime(8),
                    UpdatedAtUtc = reader.GetDateTime(9)
                });
            }

            return notes;
        }
    }
}

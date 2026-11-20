namespace Fundoo.Notes.Api.Contracts.Requests
{
    public class CreateNoteRequest
    {
        public string Title { get; set; } = string.Empty;

        public string? Content { get; set; }

        public string? Color { get; set; }

        public bool IsPinned { get; set; }
    }
}

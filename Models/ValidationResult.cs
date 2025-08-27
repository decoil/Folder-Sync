namespace FolderSync.Models
{
    public record ValidationResult(
        bool IsValid,
        string? ErrorMessage = null,
        SyncConfiguration? Configuration = null
    );
}
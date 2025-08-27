using System.Threading.Tasks;

namespace FolderSync.Infrastructure
{
    public interface ILogger
    {
        Task LogAsync(string message);
        Task LogErrorAsync(string message);
        Task LogWarningAsync(string message);
    }
}
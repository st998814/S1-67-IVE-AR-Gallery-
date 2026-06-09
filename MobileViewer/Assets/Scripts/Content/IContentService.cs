using System.Threading;
using System.Threading.Tasks;

namespace MobileViewer.Content
{
    /// <summary>
    /// Provides content metadata for a recognized AR target.
    /// Implementations may fetch data from HTTP APIs, local storage, or other runtime sources.
    /// </summary>
    public interface IContentService
    {
        Task<ContentData> GetContentForTargetAsync(string targetName, CancellationToken cancellationToken = default);
    }
}

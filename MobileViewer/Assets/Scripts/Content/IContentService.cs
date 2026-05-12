using System.Threading;
using System.Threading.Tasks;

namespace MobileViewer.Content
{
    public interface IContentService
    {
        Task<ContentData> GetContentForTargetAsync(string targetName, CancellationToken cancellationToken = default);
    }
}

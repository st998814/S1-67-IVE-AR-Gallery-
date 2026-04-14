/// <summary>
/// The interface for the API request handle.
/// </summary>
public interface IApiRequestHandle
{
    bool IsDone { get; }
    bool IsCancelled { get; }
    void Cancel();
}

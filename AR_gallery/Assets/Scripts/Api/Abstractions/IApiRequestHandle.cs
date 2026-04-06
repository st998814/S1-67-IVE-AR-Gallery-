/// <summary>
/// The interface for the API request handle.(interface : kind of abstract class in python)
/// </summary>
public interface IApiRequestHandle
{
    bool IsDone { get; }
    bool IsCancelled { get; }
    void Cancel();
}

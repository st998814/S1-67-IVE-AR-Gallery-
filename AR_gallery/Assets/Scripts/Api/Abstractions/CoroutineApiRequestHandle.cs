using UnityEngine;

/// <summary>
/// The handler for the API request.
/// </summary>
public class CoroutineApiRequestHandle : IApiRequestHandle
{   // fields
    private readonly MonoBehaviour owner;
    private readonly Coroutine coroutine;
    private bool done;
    private bool cancelled;
    // constructor
    public CoroutineApiRequestHandle(MonoBehaviour owner, Coroutine coroutine)
    {
        this.owner = owner; // the owner of the handler
        this.coroutine = coroutine; // the actual coroutine that will be executed
    }
    // properties
    public bool IsDone => done;
    public bool IsCancelled => cancelled;

    public void MarkDone()
    {
        done = true;
    }

    public void Cancel()
    {
        if (done || cancelled)
            return;

        cancelled = true;
        if (owner != null && coroutine != null)
            owner.StopCoroutine(coroutine); // stop the coroutine
        done = true; // mark as done
    }
}

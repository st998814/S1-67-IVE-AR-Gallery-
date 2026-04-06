using UnityEngine;

/// <summary>
/// The handler for the API request.
/// </summary>
public class CoroutineApiRequestHandle : IApiRequestHandle
{
    private readonly MonoBehaviour owner;
    private Coroutine coroutine;
    private bool done;
    private bool cancelled;

    public CoroutineApiRequestHandle(MonoBehaviour owner)
    {
        this.owner = owner;
    }

    public bool IsDone => done;
    public bool IsCancelled => cancelled;

    public void BindCoroutine(Coroutine coroutineToBind)
    {
        coroutine = coroutineToBind;
    }

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
            owner.StopCoroutine(coroutine);
        done = true;
    }
}

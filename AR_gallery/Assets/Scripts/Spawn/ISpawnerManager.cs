namespace ARGallery.Spawning
{
    /// <summary>
    /// Unified entry point for runtime target/content creation.
    /// Implementations route requests to concrete creation workflows.
    /// </summary>
    public interface ISpawnerManager
    {
        SpawnContentResult CreateContent(SpawnRequest request);
        SpawnTargetResult CreateTarget(SpawnTargetRequest request);
    }
}

namespace ARGallery.Content
{
    /// <summary>
    /// How content is rendered in the scene: flat surfaces vs volumetric meshes.
    /// Used at creation time to branch spawning and loading logic.
    /// </summary>
    public enum ContentRenderKind
    {
        /// <summary>Quad / canvas / video plane / text — prefab + material or TMP.</summary>
        Surface,

        /// <summary>Imported 3D hierarchy under a container (future: glTF, etc.).</summary>
        Volumetric
    }

    /// <summary>
    /// Semantic media category for authoring and API sync, independent of render implementation.
    /// </summary>
    public enum ContentMediaKind
    {
        Image,
        Video,
        Text,
        Model
    }

    /// <summary>
    /// Pool key semantics are based on runtime shell structure, not semantic media type.
    /// </summary>
    public enum RuntimeContentShellType
    {
        SurfaceShell,
        TextShell,
        ModelShell
    }
}

namespace ARGallery.Workspace
{
    /// <summary>
    /// Frontend-side composition root for workspace draft access.
    /// Replace Provider with HttpWorkspaceProvider when backend is ready.
    /// </summary>
    public static class WorkspaceDataServices
    {
        private static IWorkspaceProvider provider = new MockWorkspaceProvider();
        private static readonly LocalWorkspaceStore localStore = new LocalWorkspaceStore();

        public static IWorkspaceProvider Provider => provider;
        public static LocalWorkspaceStore LocalStore => localStore;

        public static void SetProvider(IWorkspaceProvider nextProvider)
        {
            if (nextProvider != null)
                provider = nextProvider;
        }

        /// <summary>Drops in-memory draft for <paramref name="workspaceId"/> from <see cref="LocalWorkspaceStore"/>.</summary>
        public static bool RemoveCachedWorkspaceDraft(string workspaceId)
        {
            return localStore.TryRemoveFromCache(workspaceId);
        }
    }
}

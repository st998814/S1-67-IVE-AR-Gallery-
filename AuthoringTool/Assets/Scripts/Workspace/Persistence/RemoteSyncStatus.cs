namespace ARGallery.Workspace.Persistence
{
    /// <summary>
    /// Serialized <see cref="WorkspaceSnapshot.remoteSyncStatus"/> string values (Layer 3).
    /// </summary>
    public static class RemoteSyncStatus
    {
        public const string LocalOnly = "LocalOnly";
        public const string SyncPending = "SyncPending";
        public const string Syncing = "Syncing";
        public const string Synced = "Synced";
        public const string Failed = "Failed";
    }
}

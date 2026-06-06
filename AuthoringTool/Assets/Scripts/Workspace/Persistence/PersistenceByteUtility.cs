namespace ARGallery.Workspace.Persistence
{
    /// <summary>Helpers for in-memory asset payloads (WebGL-safe; no persistentDataPath copies).</summary>
    internal static class PersistenceByteUtility
    {
        public static byte[] CloneBytes(byte[] source)
        {
            if (source == null || source.Length == 0)
                return null;
            return (byte[])source.Clone();
        }

        public static bool HasBytes(byte[] source) => source != null && source.Length > 0;
    }
}

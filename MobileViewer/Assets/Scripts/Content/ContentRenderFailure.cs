namespace MobileViewer.Content
{
    public enum ContentRenderFailureReason
    {
        None,
        UnsupportedContentType,
        MissingMediaUrl,
        InvalidMediaUrl,
        UnsupportedStreamingUrl,
        NetworkError,
        MediaLoadFailed,
        MediaPlaybackFailed,
        ModelImportFailed
    }

    public static class ContentRenderFailureMessages
    {
        public static string ToastFor(ContentRenderFailureReason reason)
        {
            return reason switch
            {
                ContentRenderFailureReason.UnsupportedContentType => "Unsupported content type",
                ContentRenderFailureReason.MissingMediaUrl => "Content media URL missing",
                ContentRenderFailureReason.InvalidMediaUrl => "Invalid content media URL",
                ContentRenderFailureReason.UnsupportedStreamingUrl => "Streaming URL not supported",
                ContentRenderFailureReason.NetworkError => "Content download failed",
                ContentRenderFailureReason.MediaLoadFailed => "Content failed to load",
                ContentRenderFailureReason.MediaPlaybackFailed => "Video failed to play",
                ContentRenderFailureReason.ModelImportFailed => "3D model failed to load",
                _ => "Content render failed"
            };
        }

        public static string PanelSuffixFor(ContentRenderFailureReason reason)
        {
            return reason switch
            {
                ContentRenderFailureReason.UnsupportedContentType => "[Unsupported type]",
                ContentRenderFailureReason.MissingMediaUrl => "[Missing media URL]",
                ContentRenderFailureReason.InvalidMediaUrl => "[Invalid media URL]",
                ContentRenderFailureReason.UnsupportedStreamingUrl => "[Streaming not supported]",
                ContentRenderFailureReason.NetworkError => "[Download failed]",
                ContentRenderFailureReason.MediaLoadFailed => "[Media load failed]",
                ContentRenderFailureReason.MediaPlaybackFailed => "[Video playback failed]",
                ContentRenderFailureReason.ModelImportFailed => "[Model load failed]",
                _ => "[Render failed]"
            };
        }
    }
}

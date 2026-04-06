using System;
using FrostweepGames.Plugins.WebGLFileBrowser;

/// <summary>
/// Shared upload workflow service.
/// Builds upload request DTO and delegates transport to IApiClient.
/// </summary>
public class UploadWorkflowService
{
    public IApiRequestHandle UploadSelectedFile(
        File selectedFile,
        IApiClient apiClient,
        Action<ApiResult<UploadFileResponseDto>> onCompleted,
        float timeoutSeconds = 20f)
    {
        if (apiClient == null)
        {
            onCompleted?.Invoke(ApiResult<UploadFileResponseDto>.Fail(
                ApiErrorCodes.Unknown,
                "Upload API client is not available."));
            return null;
        }

        var request = new UploadFileRequestDto
        {
            fileName = GetSanitizedUploadFileName(selectedFile),
            mimeType = GuessMimeType(selectedFile),
            fileBytes = selectedFile != null ? selectedFile.data : null,
            meta = new ApiSyncMetaDto
            {
                schemaVersion = "v1",
                clientRequestId = Guid.NewGuid().ToString("N"),
                createdAtUtc = DateTime.UtcNow.ToString("o")
            }
        };

        return apiClient.UploadFile(request, onCompleted, timeoutSeconds);
    }
    // helper methods for dealing with file name and extention
    private static string GetSanitizedUploadFileName(File file)
    {
        if (file?.fileInfo == null)
            return "upload.bin";

        if (!string.IsNullOrEmpty(file.fileInfo.fullName))
            return System.IO.Path.GetFileName(file.fileInfo.fullName.Trim());

        string baseName = string.IsNullOrEmpty(file.fileInfo.name) ? "image" : file.fileInfo.name.TrimEnd('.');
        string ext = file.fileInfo.extension ?? "";
        if (string.IsNullOrEmpty(ext))
            return baseName;
        return ext.StartsWith(".") ? baseName + ext : baseName + "." + ext;
    }

    private static string GuessMimeType(File file)
    {
        string ext = file?.fileInfo != null ? (file.fileInfo.extension ?? "") : "";
        string lower = ext.ToLowerInvariant();
        if (lower == ".png" || lower == "png")
            return "image/png";
        if (lower == ".jpg" || lower == "jpg" || lower == ".jpeg" || lower == "jpeg")
            return "image/jpeg";
        if (lower == ".mp4" || lower == "mp4")
            return "video/mp4";
        return "application/octet-stream";
    }
}

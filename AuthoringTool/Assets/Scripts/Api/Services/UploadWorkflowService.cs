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
        float timeoutSeconds = 20f,
        string contentId = null)
    {
        if (apiClient == null)
        {
            onCompleted?.Invoke(ApiResult<UploadFileResponseDto>.Fail(
                ApiErrorCodes.Unknown,
                "Upload API client is not available."));
            return null;
        }

        string extension = GetExtension(selectedFile);
        var request = new UploadFileRequestDto
        {
            fileName = GetSanitizedUploadFileName(selectedFile, contentId),
            mimeType = GuessMimeTypeFromExtension(extension),
            fileBytes = selectedFile != null ? selectedFile.data : null,
            uploadCategory = "content",
            contentId = string.IsNullOrWhiteSpace(contentId) ? "" : contentId.Trim(),
            meta = new ApiSyncMetaDto
            {
                schemaVersion = "v1",
                clientRequestId = Guid.NewGuid().ToString("N"),
                createdAtUtc = DateTime.UtcNow.ToString("o")
            }
        };

        return apiClient.UploadFile(request, onCompleted, timeoutSeconds);
    }

    public static string GuessMimeTypeFromExtension(string extension)
    {
        string lower = NormalizeExtension(extension);
        if (lower == ".png")
            return "image/png";
        if (lower == ".jpg" || lower == ".jpeg")
            return "image/jpeg";
        if (lower == ".mp4")
            return "video/mp4";
        if (lower == ".mov")
            return "video/quicktime";
        if (lower == ".webm")
            return "video/webm";
        if (lower == ".glb")
            return "model/gltf-binary";
        if (lower == ".gltf")
            return "model/gltf+json";
        return "application/octet-stream";
    }

    private static string GetSanitizedUploadFileName(File file, string contentId = null)
    {
        string extension = GetExtension(file);

        if (!string.IsNullOrWhiteSpace(contentId))
        {
            string stem = contentId.Trim();
            if (!string.IsNullOrEmpty(extension))
                return extension.StartsWith(".") ? stem + extension : stem + "." + extension;
            return stem + ".bin";
        }

        if (file?.fileInfo == null)
            return DefaultUploadFileNameForExtension(extension);

        if (!string.IsNullOrEmpty(file.fileInfo.fullName))
            return System.IO.Path.GetFileName(file.fileInfo.fullName.Trim());

        string baseName = string.IsNullOrEmpty(file.fileInfo.name)
            ? DefaultBaseNameForExtension(extension)
            : file.fileInfo.name.TrimEnd('.');
        if (string.IsNullOrEmpty(extension))
            return baseName;
        return extension.StartsWith(".") ? baseName + extension : baseName + "." + extension;
    }

    private static string GetExtension(File file)
    {
        return file?.fileInfo != null ? NormalizeExtension(file.fileInfo.extension ?? "") : "";
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return "";
        string lower = extension.Trim().ToLowerInvariant();
        return lower.StartsWith(".") ? lower : "." + lower;
    }

    private static string DefaultBaseNameForExtension(string extension)
    {
        string lower = NormalizeExtension(extension);
        if (lower == ".mp4" || lower == ".mov" || lower == ".webm")
            return "upload";
        if (lower == ".glb" || lower == ".gltf")
            return "model";
        return "upload";
    }

    private static string DefaultUploadFileNameForExtension(string extension)
    {
        string baseName = DefaultBaseNameForExtension(extension);
        if (string.IsNullOrEmpty(extension))
            return baseName + ".bin";
        return extension.StartsWith(".") ? baseName + extension : baseName + "." + extension;
    }
}

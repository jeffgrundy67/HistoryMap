namespace HistoryMap.Api.Configuration;

/// <summary>
/// Base URL for the public Blob (or CDN) container that holds place photos.
/// Photo rows store relative BlobPath; responses concatenate BaseUrl + BlobPath.
/// </summary>
public class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    /// <summary>
    /// Container base URL without trailing slash, e.g.
    /// https://historymap.blob.core.windows.net/photos
    /// </summary>
    public string ContainerBaseUrl { get; set; } = string.Empty;

    public string BuildUrl(string blobPath)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
            return string.Empty;

        var baseUrl = (ContainerBaseUrl ?? string.Empty).TrimEnd('/');
        var path = blobPath.TrimStart('/');
        return string.IsNullOrEmpty(baseUrl) ? path : $"{baseUrl}/{path}";
    }
}

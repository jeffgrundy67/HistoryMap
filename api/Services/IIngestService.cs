using HistoryMap.Api.Models;

namespace HistoryMap.Api.Services;

public interface IIngestService
{
    Task<UpsertPlaceResult> UpsertPlaceAsync(
        UpsertPlaceRequest request,
        CancellationToken cancellationToken = default);

    Task<UpsertPhotoResult> UpsertPhotoAsync(
        UpsertPhotoRequest request,
        CancellationToken cancellationToken = default);

    Task<IngestBatchResult> UpsertBatchAsync(
        IngestBatchRequest request,
        CancellationToken cancellationToken = default);
}

using Microsoft.AspNetCore.Http;

namespace BookingSaas_backend.Application.Interfaces;

public interface IImageUploadService
{
    Task<ImageUploadResponse> UploadAsync(
        IFormFile file,
        string folder,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string publicId,
        CancellationToken cancellationToken = default);
}
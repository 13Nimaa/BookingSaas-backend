using BookingSaas_backend.Application.Exceptions;
using BookingSaas_backend.Application.Interfaces;
using BookingSaas_backend.Infrastructure.Configuration;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BookingSaas_backend.Infrastructure.Services;

public class CloudinaryImageUploadService : IImageUploadService
{
    private readonly Cloudinary _cloudinary;

    private const long MaxFileSize = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    private static readonly HashSet<string> AllowedExtensions =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    ];

    public CloudinaryImageUploadService(
        IOptions<CloudinarySettings> options)
    {
        var settings = options.Value;

        var account = new Account(
            settings.CloudName,
            settings.ApiKey,
            settings.ApiSecret);

        _cloudinary = new Cloudinary(account);
    }

public async Task<ImageUploadResponse> UploadAsync(
    IFormFile file,
    string folder,
    CancellationToken cancellationToken = default)
{
    ValidateFile(file);

    cancellationToken.ThrowIfCancellationRequested();

    await using var stream = file.OpenReadStream();

    var uploadParams = new ImageUploadParams
    {
        File = new FileDescription(file.FileName, stream),
        Folder = folder
    };

    var result = await _cloudinary.UploadAsync(uploadParams);

    cancellationToken.ThrowIfCancellationRequested();

    if (result.Error != null)
    {
        throw new InvalidOperationException(
            $"خطا در آپلود تصویر: {result.Error.Message}");
    }

    if (string.IsNullOrWhiteSpace(result.PublicId) ||
        result.SecureUrl is null)
    {
        throw new InvalidOperationException(
            "اطلاعات تصویر آپلودشده از Cloudinary معتبر نیست.");
    }

    return new ImageUploadResponse(
        result.SecureUrl.ToString(),
        result.PublicId);
}
public async Task DeleteAsync(
    string publicId,
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(publicId))
    {
        throw new ImageUploadException(
            "شناسه تصویر برای حذف مشخص نشده است.");
    }

    cancellationToken.ThrowIfCancellationRequested();

    var deleteParams = new DeletionParams(publicId);

    var result = await _cloudinary.DestroyAsync(deleteParams);

    cancellationToken.ThrowIfCancellationRequested();

    if (result.Error != null)
    {
        throw new InvalidOperationException(
            $"خطا در حذف تصویر: {result.Error.Message}");
    }

    if (result.Result != "ok" && result.Result != "not found")
    {
        throw new InvalidOperationException(
            "حذف تصویر از Cloudinary با موفقیت انجام نشد.");
    }
}

    private static void ValidateFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new ImageUploadException(
                "لطفاً یک تصویر انتخاب کنید.");
        }

        if (file.Length > MaxFileSize)
        {
            throw new ImageUploadException(
                "حجم تصویر نباید بیشتر از ۵ مگابایت باشد.");
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            throw new ImageUploadException(
                "فرمت تصویر باید JPG، PNG یا WEBP باشد.");
        }

        var extension = Path
            .GetExtension(file.FileName)
            .ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
        {
            throw new ImageUploadException(
                "پسوند تصویر مجاز نیست.");
        }
    }
}
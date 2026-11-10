using Application.Dtos.Customer;
using BookingSaas_backend.Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BookingSaas_backend.Application.Services;

public sealed class BusinessFavoriteService : IBusinessFavoriteService
{
    private readonly AppDbContext _db;

    public BusinessFavoriteService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<BusinessFavoriteDto>> ListAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.BusinessFavorites
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ThenByDescending(f => f.Id)
            .Select(f => new BusinessFavoriteDto(
                f.BusinessId,
                f.Business.Name,
                f.Business.City,
                f.Business.ProfileImageUrl,
                f.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<BusinessFavoriteDto> AddAsync(
        string userId,
        int businessId,
        CancellationToken cancellationToken = default)
    {
        var business = await _db.Businesses
            .AsNoTracking()
            .Where(b => b.Id == businessId)
            .Select(b => new { b.Id, b.Name, b.City, b.ProfileImageUrl })
            .FirstOrDefaultAsync(cancellationToken);

        if (business is null)
            throw new KeyNotFoundException("کسب‌وکار پیدا نشد.");

        var existing = await _db.BusinessFavorites
            .AsNoTracking()
            .FirstOrDefaultAsync(
                f => f.UserId == userId && f.BusinessId == businessId,
                cancellationToken);

        if (existing is not null)
        {
            return new BusinessFavoriteDto(
                business.Id,
                business.Name,
                business.City,
                business.ProfileImageUrl,
                existing.CreatedAt);
        }

        var favorite = new BusinessFavorite
        {
            UserId = userId,
            BusinessId = businessId,
            CreatedAt = DateTime.UtcNow
        };

        _db.BusinessFavorites.Add(favorite);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Concurrent duplicate insert — return current row.
            var raced = await _db.BusinessFavorites
                .AsNoTracking()
                .FirstAsync(
                    f => f.UserId == userId && f.BusinessId == businessId,
                    cancellationToken);

            return new BusinessFavoriteDto(
                business.Id,
                business.Name,
                business.City,
                business.ProfileImageUrl,
                raced.CreatedAt);
        }

        return new BusinessFavoriteDto(
            business.Id,
            business.Name,
            business.City,
            business.ProfileImageUrl,
            favorite.CreatedAt);
    }

    public async Task RemoveAsync(
        string userId,
        int businessId,
        CancellationToken cancellationToken = default)
    {
        var favorite = await _db.BusinessFavorites
            .FirstOrDefaultAsync(
                f => f.UserId == userId && f.BusinessId == businessId,
                cancellationToken);

        if (favorite is null)
            throw new KeyNotFoundException("علاقه‌مندی یافت نشد.");

        _db.BusinessFavorites.Remove(favorite);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        _db.BusinessFavorites
            .AsNoTracking()
            .CountAsync(f => f.UserId == userId, cancellationToken);
}

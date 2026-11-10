using System.Security.Claims;
using Application.Dtos.Customer;
using BookingSaas_backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingSaas_backend.Controllers;

[ApiController]
[Authorize]
[Route("api/customer")]
public sealed class CustomerDashboardController : ControllerBase
{
    private readonly ICustomerDashboardService _dashboardService;
    private readonly IBusinessFavoriteService _favoriteService;

    public CustomerDashboardController(
        ICustomerDashboardService dashboardService,
        IBusinessFavoriteService favoriteService)
    {
        _dashboardService = dashboardService;
        _favoriteService = favoriteService;
    }

    /// <summary>
    /// Customer home dashboard: summary cards, upcoming widget, recent list.
    /// Scoped by Customer.UserId / favorites UserId from JWT — never client-supplied ids.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<CustomerDashboardDto>> GetDashboard(
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var dashboard = await _dashboardService.GetAsync(userId, cancellationToken);
        return Ok(dashboard);
    }

    [HttpGet("favorites")]
    public async Task<ActionResult<IReadOnlyList<BusinessFavoriteDto>>> ListFavorites(
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var items = await _favoriteService.ListAsync(userId, cancellationToken);
        return Ok(items);
    }

    [HttpPost("favorites")]
    public async Task<ActionResult<BusinessFavoriteDto>> AddFavorite(
        AddBusinessFavoriteDto dto,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        try
        {
            var item = await _favoriteService.AddAsync(userId, dto.BusinessId, cancellationToken);
            return Ok(item);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("favorites/{businessId:int}")]
    public async Task<IActionResult> RemoveFavorite(
        int businessId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        try
        {
            await _favoriteService.RemoveAsync(userId, businessId, cancellationToken);
            return Ok(new { message = "علاقه‌مندی حذف شد." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}

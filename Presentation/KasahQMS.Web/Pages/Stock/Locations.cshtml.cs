using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.Stock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace KasahQMS.Web.Pages.Stock;

/// <summary>
/// Stock locations management page.
/// All authenticated users can view locations.
/// Only TMD, Deputy, and Tender roles can create locations.
/// </summary>
[Authorize]
public class LocationsModel : PageModel
{
    private readonly IStockService _stockService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<LocationsModel> _logger;

    public LocationsModel(
        IStockService stockService,
        ICurrentUserService currentUserService,
        ILogger<LocationsModel> logger)
    {
        _stockService = stockService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [BindProperty]
    [Required(ErrorMessage = "Code is required")]
    [StringLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
    public string Code { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Name is required")]
    [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    [BindProperty]
    [StringLength(500, ErrorMessage = "Address cannot exceed 500 characters")]
    public string? Address { get; set; }

    [BindProperty]
    public bool IsVirtual { get; set; }

    public bool CanManageStock { get; set; }
    public List<LocationRow> Locations { get; set; } = new();
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return;

        CanManageStock = await _stockService.CanManageStockAsync(userId.Value);
        await LoadLocationsAsync();

        _logger.LogInformation("Stock locations viewed by user {UserId}", userId);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Unauthorized();

        CanManageStock = await _stockService.CanManageStockAsync(userId.Value);

        if (!CanManageStock)
        {
            ErrorMessage = "You do not have permission to create stock locations.";
            await LoadLocationsAsync();
            return Page();
        }

        if (!ModelState.IsValid)
        {
            await LoadLocationsAsync();
            return Page();
        }

        try
        {
            await _stockService.CreateLocationAsync(
                Code,
                Name,
                Description,
                Address,
                IsVirtual);

            _logger.LogInformation("Stock location {Code} created by user {UserId}", Code, userId);
            SuccessMessage = $"Location '{Name}' created successfully.";
            
            // Reset form
            Code = string.Empty;
            Name = string.Empty;
            Description = null;
            Address = null;
            IsVirtual = false;
            ModelState.Clear();
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating stock location {Code}", Code);
            ErrorMessage = "An error occurred while creating the location. Please try again.";
        }

        await LoadLocationsAsync();
        return Page();
    }

    private async Task LoadLocationsAsync()
    {
        var locations = await _stockService.GetLocationsAsync(activeOnly: false);
        Locations = locations.Select(l => new LocationRow(
            l.Id,
            l.Code,
            l.Name,
            l.Description,
            l.Address,
            l.IsVirtual,
            l.IsActive)).ToList();
    }

    public record LocationRow(
        Guid Id,
        string Code,
        string Name,
        string? Description,
        string? Address,
        bool IsVirtual,
        bool IsActive);
}

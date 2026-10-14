using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AcxiomCRM.Controllers;

[Authorize]
public class ActivitiesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public ActivitiesController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? type, string? status, string? search)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        bool isSalesExecutive = !User.IsInRole("Admin") && !User.IsInRole("Manager");

        IQueryable<Activity> query = _context.Activities
            .Include(a => a.Customer)
            .Include(a => a.Lead)
            .Include(a => a.AssignedUser)
            .AsNoTracking();

        if (isSalesExecutive)
        {
            query = query.Where(a => a.AssignedTo == currentUserId);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(a => a.ActivityType == type);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim().ToLower();
            query = query.Where(a =>
                a.Subject.ToLower().Contains(search) ||
                (a.Description != null && a.Description.ToLower().Contains(search)));
        }

        var activities = await query.OrderByDescending(a => a.ActivityDate).ToListAsync();

        ViewBag.Type = type;
        ViewBag.Status = status;
        ViewBag.Search = search;

        return View(activities);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
    {
        await PopulateDropdownsAsync(customerId, leadId);

        return View(new Activity
        {
            CustomerId = customerId,
            LeadId = leadId,
            ActivityType = "Call",
            ActivityDate = DateTime.UtcNow,
            Status = "Completed"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Activity activity)
    {
        if (string.IsNullOrWhiteSpace(activity.Subject))
        {
            ModelState.AddModelError("Subject", "Subject is required.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(activity.CustomerId, activity.LeadId, activity.AssignedTo);
            return View(activity);
        }

        if (string.IsNullOrEmpty(activity.AssignedTo))
        {
            activity.AssignedTo = User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        activity.CreatedDate = DateTime.UtcNow;

        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Create", "Activity", activity.ActivityId.ToString(),
            null,
            new { activity.ActivityType, activity.Subject, activity.ActivityDate },
            "Success",
            $"Logged Activity: '{activity.Subject}' ({activity.ActivityType})");

        TempData["SuccessMessage"] = "Activity logged successfully.";

        if (activity.CustomerId.HasValue)
        {
            return RedirectToAction("Details", "Customers", new { id = activity.CustomerId });
        }
        if (activity.LeadId.HasValue)
        {
            return RedirectToAction("Details", "Leads", new { id = activity.LeadId });
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync(int? customerId = null, int? leadId = null, string? assignedTo = null)
    {
        var customers = await _context.Customers
            .OrderBy(c => c.CustomerName)
            .Select(c => new { c.CustomerId, c.CustomerName })
            .ToListAsync();

        var leads = await _context.Leads
            .OrderBy(l => l.LeadName)
            .Select(l => new { l.LeadId, l.LeadName })
            .ToListAsync();

        var users = await _userManager.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, DisplayName = $"{u.FullName} ({u.UserRole})" })
            .ToListAsync();

        ViewBag.Customers = new SelectList(customers, "CustomerId", "CustomerName", customerId);
        ViewBag.Leads = new SelectList(leads, "LeadId", "LeadName", leadId);
        ViewBag.Users = new SelectList(users, "Id", "DisplayName", assignedTo);
    }
}

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
public class FollowUpsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public FollowUpsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? search, string? status, string? type, string? filter = "all")
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        bool isSalesExecutive = !User.IsInRole("Admin") && !User.IsInRole("Manager");

        IQueryable<FollowUp> query = _context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.Lead)
            .Include(f => f.Opportunity)
            .Include(f => f.AssignedUser)
            .AsNoTracking();

        if (isSalesExecutive)
        {
            query = query.Where(f => f.AssignedTo == currentUserId);
        }

        DateTime now = DateTime.UtcNow;

        if (filter == "upcoming")
        {
            query = query.Where(f => f.Status == "Planned" && f.FollowUpDate >= now);
        }
        else if (filter == "overdue")
        {
            query = query.Where(f => f.Status == "Planned" && f.FollowUpDate < now);
        }
        else if (filter == "today")
        {
            query = query.Where(f => f.Status == "Planned" && f.FollowUpDate.Date == now.Date);
        }
        else if (filter == "completed")
        {
            query = query.Where(f => f.Status == "Completed");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim().ToLower();
            query = query.Where(f =>
                f.Subject.ToLower().Contains(search) ||
                (f.Remarks != null && f.Remarks.ToLower().Contains(search)) ||
                (f.Customer != null && f.Customer.CustomerName.ToLower().Contains(search)) ||
                (f.Lead != null && f.Lead.LeadName.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(f => f.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(f => f.FollowUpType == type);
        }

        var followUps = await query.OrderBy(f => f.FollowUpDate).ToListAsync();

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.Type = type;
        ViewBag.Filter = filter;

        return View(followUps);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId = null, int? leadId = null, int? opportunityId = null)
    {
        await PopulateDropdownsAsync(customerId, leadId, opportunityId);

        var followUp = new FollowUp
        {
            CustomerId = customerId,
            LeadId = leadId,
            OpportunityId = opportunityId,
            FollowUpDate = DateTime.UtcNow.AddHours(4),
            Status = "Planned",
            FollowUpType = "Call"
        };

        return View(followUp);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FollowUp followUp)
    {
        // Mandatory Business Validation: Follow-up date cannot be earlier than today for a new/planned follow-up
        if (followUp.Status == "Planned" && followUp.FollowUpDate.Date < DateTime.UtcNow.Date)
        {
            ModelState.AddModelError("FollowUpDate", "Follow-up date cannot be earlier than today.");
        }

        if (string.IsNullOrWhiteSpace(followUp.Subject))
        {
            ModelState.AddModelError("Subject", "Subject is required.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(followUp.CustomerId, followUp.LeadId, followUp.OpportunityId, followUp.AssignedTo);
            return View(followUp);
        }

        if (string.IsNullOrEmpty(followUp.AssignedTo))
        {
            followUp.AssignedTo = User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        followUp.CreatedDate = DateTime.UtcNow;

        _context.FollowUps.Add(followUp);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Create", "FollowUp", followUp.FollowUpId.ToString(),
            null,
            new { followUp.Subject, followUp.FollowUpType, followUp.FollowUpDate, followUp.Status },
            "Success",
            $"Scheduled Follow-up: '{followUp.Subject}' for {followUp.FollowUpDate:g}");

        TempData["SuccessMessage"] = "Follow-up scheduled successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, string? remarks)
    {
        var followUp = await _context.FollowUps.FindAsync(id);
        if (followUp == null) return NotFound();

        var oldStatus = followUp.Status;
        followUp.Status = "Completed";
        if (!string.IsNullOrWhiteSpace(remarks))
        {
            followUp.Remarks = string.IsNullOrEmpty(followUp.Remarks)
                ? $"[Completed]: {remarks}"
                : $"{followUp.Remarks}\n[Completed]: {remarks}";
        }

        _context.Update(followUp);

        // Also add an Activity entry automatically for completed follow-up
        var activity = new Activity
        {
            ActivityType = followUp.FollowUpType,
            Subject = $"Completed Follow-up: {followUp.Subject}",
            Description = remarks ?? followUp.Remarks ?? "Follow-up marked as completed.",
            ActivityDate = DateTime.UtcNow,
            CustomerId = followUp.CustomerId,
            LeadId = followUp.LeadId,
            AssignedTo = followUp.AssignedTo,
            Status = "Completed",
            CreatedDate = DateTime.UtcNow
        };
        _context.Activities.Add(activity);

        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Update", "FollowUp", id.ToString(),
            new { Status = oldStatus },
            new { Status = "Completed", Remarks = remarks },
            "Success",
            $"Completed Follow-up '{followUp.Subject}'");

        TempData["SuccessMessage"] = "Follow-up marked as completed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(int id, DateTime newDate, string? remarks)
    {
        var followUp = await _context.FollowUps.FindAsync(id);
        if (followUp == null) return NotFound();

        if (newDate.Date < DateTime.UtcNow.Date)
        {
            TempData["ErrorMessage"] = "New follow-up date cannot be earlier than today.";
            return RedirectToAction(nameof(Index));
        }

        var oldDate = followUp.FollowUpDate;
        followUp.FollowUpDate = newDate;
        followUp.Status = "Planned";
        if (!string.IsNullOrWhiteSpace(remarks))
        {
            followUp.Remarks = string.IsNullOrEmpty(followUp.Remarks)
                ? $"[Rescheduled to {newDate:g}]: {remarks}"
                : $"{followUp.Remarks}\n[Rescheduled to {newDate:g}]: {remarks}";
        }

        _context.Update(followUp);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Update", "FollowUp", id.ToString(),
            new { FollowUpDate = oldDate },
            new { FollowUpDate = newDate, Remarks = remarks },
            "Success",
            $"Rescheduled Follow-up '{followUp.Subject}' from {oldDate:g} to {newDate:g}");

        TempData["SuccessMessage"] = "Follow-up rescheduled successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var followUp = await _context.FollowUps.FindAsync(id);
        if (followUp == null) return NotFound();

        var subject = followUp.Subject;
        _context.FollowUps.Remove(followUp);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Delete", "FollowUp", id.ToString(),
            new { followUp.Subject },
            null,
            "Success",
            $"Deleted Follow-up '{subject}'");

        TempData["SuccessMessage"] = "Follow-up deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync(int? customerId = null, int? leadId = null, int? opportunityId = null, string? assignedTo = null)
    {
        var customers = await _context.Customers
            .OrderBy(c => c.CustomerName)
            .Select(c => new { c.CustomerId, c.CustomerName })
            .ToListAsync();

        var leads = await _context.Leads
            .OrderBy(l => l.LeadName)
            .Select(l => new { l.LeadId, l.LeadName })
            .ToListAsync();

        var opportunities = await _context.Opportunities
            .OrderBy(o => o.OpportunityName)
            .Select(o => new { o.OpportunityId, o.OpportunityName })
            .ToListAsync();

        var users = await _userManager.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, DisplayName = $"{u.FullName} ({u.UserRole})" })
            .ToListAsync();

        ViewBag.Customers = new SelectList(customers, "CustomerId", "CustomerName", customerId);
        ViewBag.Leads = new SelectList(leads, "LeadId", "LeadName", leadId);
        ViewBag.Opportunities = new SelectList(opportunities, "OpportunityId", "OpportunityName", opportunityId);
        ViewBag.Users = new SelectList(users, "Id", "DisplayName", assignedTo);
    }
}

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
public class OpportunitiesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public OpportunitiesController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? search, string? stage, string? status, string? viewMode = "pipeline")
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        bool isSalesExecutive = !User.IsInRole("Admin") && !User.IsInRole("Manager");

        IQueryable<Opportunity> query = _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.AssignedUser)
            .AsNoTracking();

        if (isSalesExecutive)
        {
            query = query.Where(o => o.AssignedTo == currentUserId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim().ToLower();
            query = query.Where(o =>
                o.OpportunityName.ToLower().Contains(search) ||
                (o.Customer != null && o.Customer.CustomerName.ToLower().Contains(search)) ||
                (o.Lead != null && o.Lead.LeadName.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(stage))
        {
            query = query.Where(o => o.Stage == stage);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(o => o.Status == status);
        }

        var opportunities = await query.OrderByDescending(o => o.CreatedDate).ToListAsync();

        ViewBag.Search = search;
        ViewBag.Stage = stage;
        ViewBag.Status = status;
        ViewBag.ViewMode = viewMode;

        // Pipeline summary totals
        ViewBag.TotalCount = opportunities.Count;
        ViewBag.TotalPipeline = opportunities.Where(o => o.Status == "Open").Sum(o => o.Amount);
        ViewBag.WeightedPipeline = opportunities.Where(o => o.Status == "Open").Sum(o => (o.Amount * o.Probability) / 100m);

        return View(opportunities);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var opportunity = await _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.AssignedUser)
            .Include(o => o.FollowUps.OrderByDescending(f => f.FollowUpDate))
            .FirstOrDefaultAsync(m => m.OpportunityId == id);

        if (opportunity == null) return NotFound();

        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (opportunity.AssignedTo != currentUserId) return Forbid();
        }

        return View(opportunity);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
    {
        await PopulateDropdownsAsync(customerId, leadId);

        var opp = new Opportunity
        {
            CustomerId = customerId,
            LeadId = leadId,
            Stage = "Qualification",
            Probability = 20,
            Status = "Open",
            ExpectedCloseDate = DateTime.UtcNow.AddDays(30)
        };

        if (customerId.HasValue)
        {
            var cust = await _context.Customers.FindAsync(customerId.Value);
            if (cust != null)
            {
                opp.OpportunityName = $"{cust.CustomerName} - New Opportunity";
            }
        }

        return View(opp);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Opportunity opportunity)
    {
        // Section 5.3 & 17.7 Business Validation Rules
        if (opportunity.Amount <= 0)
        {
            ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
        }

        if (opportunity.Probability < 0 || opportunity.Probability > 100)
        {
            ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
        }

        // Expected Close Date cannot be in the past for active Opportunity
        if (opportunity.Status == "Open" && opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
        {
            ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(opportunity.CustomerId, opportunity.LeadId, opportunity.AssignedTo);
            return View(opportunity);
        }

        if (string.IsNullOrEmpty(opportunity.AssignedTo))
        {
            opportunity.AssignedTo = User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        // Auto status alignment based on stage
        opportunity.Status = opportunity.Stage switch
        {
            "Won" => "Won",
            "Lost" => "Lost",
            _ => "Open"
        };

        opportunity.CreatedDate = DateTime.UtcNow;

        _context.Opportunities.Add(opportunity);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Create", "Opportunity", opportunity.OpportunityId.ToString(),
            null,
            new { opportunity.OpportunityName, opportunity.Amount, opportunity.Stage, opportunity.Probability },
            "Success",
            $"Created Opportunity {opportunity.OpportunityName} valued at {opportunity.Amount:C}");

        TempData["SuccessMessage"] = $"Opportunity '{opportunity.OpportunityName}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var opportunity = await _context.Opportunities.FindAsync(id);
        if (opportunity == null) return NotFound();

        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (opportunity.AssignedTo != currentUserId) return Forbid();
        }

        await PopulateDropdownsAsync(opportunity.CustomerId, opportunity.LeadId, opportunity.AssignedTo);
        return View(opportunity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Opportunity opportunity)
    {
        if (id != opportunity.OpportunityId) return NotFound();

        var existingOpp = await _context.Opportunities.FindAsync(id);
        if (existingOpp == null) return NotFound();

        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (existingOpp.AssignedTo != currentUserId) return Forbid();
        }

        // Section 5.3 & 17.7 Business Validation Rules
        if (opportunity.Amount <= 0)
        {
            ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
        }

        if (opportunity.Probability < 0 || opportunity.Probability > 100)
        {
            ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
        }

        if (opportunity.Status == "Open" && opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
        {
            ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(opportunity.CustomerId, opportunity.LeadId, opportunity.AssignedTo);
            return View(opportunity);
        }

        var oldSnapshot = new { existingOpp.OpportunityName, existingOpp.Amount, existingOpp.Stage, existingOpp.Probability, existingOpp.Status };

        existingOpp.OpportunityName = opportunity.OpportunityName;
        existingOpp.CustomerId = opportunity.CustomerId;
        existingOpp.LeadId = opportunity.LeadId;
        existingOpp.Amount = opportunity.Amount;
        existingOpp.Stage = opportunity.Stage;
        existingOpp.Probability = opportunity.Probability;
        existingOpp.ExpectedCloseDate = opportunity.ExpectedCloseDate;
        existingOpp.AssignedTo = opportunity.AssignedTo;
        existingOpp.Notes = opportunity.Notes;

        existingOpp.Status = opportunity.Stage switch
        {
            "Won" => "Won",
            "Lost" => "Lost",
            _ => "Open"
        };

        _context.Update(existingOpp);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Update", "Opportunity", id.ToString(),
            oldSnapshot,
            new { existingOpp.OpportunityName, existingOpp.Amount, existingOpp.Stage, existingOpp.Probability, existingOpp.Status },
            "Success",
            $"Updated Opportunity {existingOpp.OpportunityName} to stage {existingOpp.Stage}");

        TempData["SuccessMessage"] = $"Opportunity '{existingOpp.OpportunityName}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStage(int id, string stage)
    {
        var opp = await _context.Opportunities.FindAsync(id);
        if (opp == null) return NotFound();

        var oldStage = opp.Stage;
        opp.Stage = stage;
        opp.Status = stage switch
        {
            "Won" => "Won",
            "Lost" => "Lost",
            _ => "Open"
        };

        if (stage == "Won") opp.Probability = 100;
        if (stage == "Lost") opp.Probability = 0;

        _context.Update(opp);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Update", "Opportunity", id.ToString(),
            new { Stage = oldStage },
            new { Stage = stage, opp.Status },
            "Success",
            $"Transitioned Opportunity {opp.OpportunityName} from {oldStage} to {stage}");

        return Json(new { success = true, newStage = stage, newStatus = opp.Status });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var opp = await _context.Opportunities.FindAsync(id);
        if (opp == null) return NotFound();

        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
        {
            return Forbid();
        }

        var oppInfo = opp.OpportunityName;
        _context.Opportunities.Remove(opp);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Delete", "Opportunity", id.ToString(),
            new { opp.OpportunityName, opp.Amount },
            null,
            "Success",
            $"Deleted Opportunity {oppInfo}");

        TempData["SuccessMessage"] = $"Opportunity '{oppInfo}' deleted successfully.";
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

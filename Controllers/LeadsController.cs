using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AcxiomCRM.Controllers;

[Authorize]
public class LeadsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public LeadsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? search, string? status, string? priority, string? assignedTo, int page = 1)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        bool isSalesExecutive = !User.IsInRole("Admin") && !User.IsInRole("Manager");

        IQueryable<Lead> query = _context.Leads
            .Include(l => l.AssignedUser)
            .Include(l => l.Customer)
            .AsNoTracking();

        if (isSalesExecutive)
        {
            query = query.Where(l => l.AssignedTo == currentUserId);
        }
        else if (!string.IsNullOrEmpty(assignedTo))
        {
            query = query.Where(l => l.AssignedTo == assignedTo);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim().ToLower();
            query = query.Where(l =>
                l.LeadName.ToLower().Contains(search) ||
                l.Email.ToLower().Contains(search) ||
                l.Phone.Contains(search) ||
                (l.CompanyName != null && l.CompanyName.ToLower().Contains(search)) ||
                l.LeadCode.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            query = query.Where(l => l.Priority == priority);
        }

        const int pageSize = 10;
        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var leads = await query
            .OrderByDescending(l => l.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.Priority = priority;
        ViewBag.AssignedTo = assignedTo;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;

        await PopulateUsersDropdownAsync(assignedTo);
        return View(leads);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var lead = await _context.Leads
            .Include(l => l.AssignedUser)
            .Include(l => l.Customer)
            .Include(l => l.FollowUps.OrderByDescending(f => f.FollowUpDate))
            .Include(l => l.Activities.OrderByDescending(a => a.ActivityDate))
            .FirstOrDefaultAsync(m => m.LeadId == id);

        if (lead == null) return NotFound();

        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (lead.AssignedTo != currentUserId) return Forbid();
        }

        return View(lead);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateUsersDropdownAsync();
        return View(new Lead
        {
            LeadCode = await GenerateLeadCodeAsync(),
            Status = "New",
            Priority = "Medium",
            ExpectedValue = 0m
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Lead lead)
    {
        // Mandatory Validations (Section 17.6)
        if (string.IsNullOrWhiteSpace(lead.LeadName))
        {
            ModelState.AddModelError("LeadName", "Lead Name is required.");
        }

        if (string.IsNullOrWhiteSpace(lead.Email))
        {
            ModelState.AddModelError("Email", "Enter a valid email address.");
        }

        if (string.IsNullOrWhiteSpace(lead.Phone) || !System.Text.RegularExpressions.Regex.IsMatch(lead.Phone.Trim(), @"^[0-9]{10}$"))
        {
            ModelState.AddModelError("Phone", "Enter a valid 10-digit phone number.");
        }

        if (lead.ExpectedValue < 0)
        {
            ModelState.AddModelError("ExpectedValue", "Expected Value must be 0 or greater.");
        }

        var validStatuses = new[] { "New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost" };
        if (!validStatuses.Contains(lead.Status))
        {
            ModelState.AddModelError("Status", "Select a valid lead status.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateUsersDropdownAsync(lead.AssignedTo);
            return View(lead);
        }

        if (string.IsNullOrEmpty(lead.AssignedTo))
        {
            lead.AssignedTo = User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        lead.LeadCode = await GenerateLeadCodeAsync();
        lead.CreatedDate = DateTime.UtcNow;
        lead.Email = lead.Email.Trim().ToLower();
        lead.Phone = lead.Phone.Trim();

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Create", "Lead", lead.LeadId.ToString(),
            null,
            new { lead.LeadCode, lead.LeadName, lead.Email, lead.Status, lead.ExpectedValue },
            "Success",
            $"Created Lead {lead.LeadName} ({lead.LeadCode})");

        TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var lead = await _context.Leads.FindAsync(id);
        if (lead == null) return NotFound();

        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (lead.AssignedTo != currentUserId) return Forbid();
        }

        await PopulateUsersDropdownAsync(lead.AssignedTo);
        return View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Lead lead)
    {
        if (id != lead.LeadId) return NotFound();

        var existingLead = await _context.Leads.FindAsync(id);
        if (existingLead == null) return NotFound();

        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (existingLead.AssignedTo != currentUserId) return Forbid();
        }

        if (string.IsNullOrWhiteSpace(lead.LeadName))
        {
            ModelState.AddModelError("LeadName", "Lead Name is required.");
        }

        if (string.IsNullOrWhiteSpace(lead.Phone) || !System.Text.RegularExpressions.Regex.IsMatch(lead.Phone.Trim(), @"^[0-9]{10}$"))
        {
            ModelState.AddModelError("Phone", "Enter a valid 10-digit phone number.");
        }

        if (lead.ExpectedValue < 0)
        {
            ModelState.AddModelError("ExpectedValue", "Expected Value must be 0 or greater.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateUsersDropdownAsync(lead.AssignedTo);
            return View(lead);
        }

        var oldSnapshot = new { existingLead.LeadName, existingLead.Status, existingLead.Priority, existingLead.ExpectedValue };

        existingLead.LeadName = lead.LeadName;
        existingLead.Email = lead.Email.Trim().ToLower();
        existingLead.Phone = lead.Phone.Trim();
        existingLead.CompanyName = lead.CompanyName;
        existingLead.Source = lead.Source;
        existingLead.Status = lead.Status;
        existingLead.Priority = lead.Priority;
        existingLead.ExpectedValue = lead.ExpectedValue;
        existingLead.AssignedTo = lead.AssignedTo;

        _context.Update(existingLead);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Update", "Lead", id.ToString(),
            oldSnapshot,
            new { existingLead.LeadName, existingLead.Status, existingLead.Priority, existingLead.ExpectedValue },
            "Success",
            $"Updated Lead {existingLead.LeadName} ({existingLead.LeadCode})");

        TempData["SuccessMessage"] = $"Lead '{existingLead.LeadName}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    // Lead Conversion Workflow (Section 4.5 & 8)
    [HttpGet]
    public async Task<IActionResult> Convert(int? id)
    {
        if (id == null) return NotFound();

        var lead = await _context.Leads.FindAsync(id);
        if (lead == null) return NotFound();

        if (lead.Status == "Converted")
        {
            TempData["ErrorMessage"] = "This lead has already been converted.";
            return RedirectToAction(nameof(Details), new { id = lead.LeadId });
        }

        var model = new LeadConvertViewModel
        {
            LeadId = lead.LeadId,
            LeadCode = lead.LeadCode,
            LeadName = lead.LeadName,
            CompanyName = lead.CompanyName,
            Email = lead.Email,
            Phone = lead.Phone,
            CreateOpportunity = true,
            OpportunityName = $"{lead.LeadName} - Expansion Deal",
            OpportunityAmount = lead.ExpectedValue > 0 ? lead.ExpectedValue : 10000m,
            Stage = "Qualification",
            Probability = 20,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(30)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(LeadConvertViewModel model)
    {
        var lead = await _context.Leads.FindAsync(model.LeadId);
        if (lead == null) return NotFound();

        if (model.CreateOpportunity)
        {
            if (string.IsNullOrWhiteSpace(model.OpportunityName))
            {
                ModelState.AddModelError("OpportunityName", "Opportunity Name is required.");
            }
            if (!model.OpportunityAmount.HasValue || model.OpportunityAmount <= 0)
            {
                ModelState.AddModelError("OpportunityAmount", "Opportunity Amount must be greater than 0.");
            }
            if (model.Probability < 0 || model.Probability > 100)
            {
                ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
            }
            if (model.ExpectedCloseDate.HasValue && model.ExpectedCloseDate.Value.Date < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
            }
        }

        // Check if customer email already exists
        var existingCust = await _context.Customers.FirstOrDefaultAsync(c => c.Email.ToLower() == model.Email.Trim().ToLower());
        Customer customer;

        if (existingCust == null)
        {
            // Generate Customer Code
            var lastCustId = await _context.Customers.MaxAsync(c => (int?)c.CustomerId) ?? 1000;
            var custCode = $"CUST-{lastCustId + 1}";

            customer = new Customer
            {
                CustomerCode = custCode,
                CustomerName = model.LeadName,
                Email = model.Email.Trim().ToLower(),
                Phone = model.Phone.Trim(),
                CompanyName = model.CompanyName,
                Status = "Active",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "System",
                OwnerId = lead.AssignedTo ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
        }
        else
        {
            customer = existingCust;
        }

        // Link customer to lead
        lead.CustomerId = customer.CustomerId;
        lead.Status = "Converted";
        _context.Update(lead);

        Opportunity? opportunity = null;
        if (model.CreateOpportunity && ModelState.IsValid)
        {
            opportunity = new Opportunity
            {
                OpportunityName = model.OpportunityName!,
                CustomerId = customer.CustomerId,
                LeadId = lead.LeadId,
                Amount = model.OpportunityAmount!.Value,
                Stage = model.Stage,
                Probability = model.Probability,
                ExpectedCloseDate = model.ExpectedCloseDate ?? DateTime.UtcNow.AddDays(30),
                Status = "Open",
                CreatedDate = DateTime.UtcNow,
                AssignedTo = lead.AssignedTo ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
                Notes = $"Converted from lead {lead.LeadCode}."
            };

            _context.Opportunities.Add(opportunity);
        }

        await _context.SaveChangesAsync();

        // Audit logging for conversion (Section 4.9 & 8)
        await _auditService.LogAsync("Convert", "Lead", lead.LeadId.ToString(),
            new { lead.LeadCode, OldStatus = "Qualified" },
            new { lead.LeadCode, NewStatus = "Converted", CustomerId = customer.CustomerId, OpportunityId = opportunity?.OpportunityId },
            "Success",
            $"Successfully converted lead {lead.LeadCode} into customer {customer.CustomerCode}{(opportunity != null ? $" and opportunity {opportunity.OpportunityName}" : "")}.");

        TempData["SuccessMessage"] = $"Lead successfully converted! Customer '{customer.CustomerName}' created.";
        return RedirectToAction("Details", "Customers", new { id = customer.CustomerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var lead = await _context.Leads.FindAsync(id);
        if (lead == null) return NotFound();

        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
        {
            return Forbid();
        }

        var leadInfo = $"{lead.LeadName} ({lead.LeadCode})";
        _context.Leads.Remove(lead);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Delete", "Lead", id.ToString(),
            new { lead.LeadName, lead.LeadCode },
            null,
            "Success",
            $"Deleted Lead {leadInfo}");

        TempData["SuccessMessage"] = $"Lead '{leadInfo}' was deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateUsersDropdownAsync(string? selectedId = null)
    {
        var users = await _userManager.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, DisplayName = $"{u.FullName} ({u.UserRole})" })
            .ToListAsync();

        ViewBag.Users = new SelectList(users, "Id", "DisplayName", selectedId);
    }

    private async Task<string> GenerateLeadCodeAsync()
    {
        var lastId = await _context.Leads.MaxAsync(l => (int?)l.LeadId) ?? 1000;
        return $"LEAD-{lastId + 1}";
    }
}

using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager")]
public class AuditLogsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuditLogsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(
        string? userFilter,
        string? moduleFilter,
        string? actionFilter,
        DateTime? startDate,
        DateTime? endDate,
        int page = 1)
    {
        IQueryable<AuditLog> query = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(userFilter))
        {
            query = query.Where(a => a.UserName != null && a.UserName.Contains(userFilter));
        }

        if (!string.IsNullOrWhiteSpace(moduleFilter))
        {
            query = query.Where(a => a.EntityName == moduleFilter);
        }

        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            query = query.Where(a => a.Action == actionFilter);
        }

        if (startDate.HasValue)
        {
            var startUtc = startDate.Value.ToUniversalTime();
            query = query.Where(a => a.CreatedDate >= startUtc);
        }

        if (endDate.HasValue)
        {
            var endUtc = endDate.Value.ToUniversalTime().AddDays(1).AddTicks(-1);
            query = query.Where(a => a.CreatedDate <= endUtc);
        }

        const int pageSize = 15;
        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var logs = await query
            .OrderByDescending(a => a.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.UserFilter = userFilter;
        ViewBag.ModuleFilter = moduleFilter;
        ViewBag.ActionFilter = actionFilter;
        ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;

        var modules = new[] { "Auth", "Customer", "Lead", "Opportunity", "FollowUp", "Activity", "User", "Security" };
        var actions = new[] { "Login", "Failed Login", "Logout", "Create", "Update", "Delete", "Convert", "Role Change", "Security" };

        ViewBag.Modules = new SelectList(modules, moduleFilter);
        ViewBag.Actions = new SelectList(actions, actionFilter);

        return View(logs);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(
        string? userFilter,
        string? moduleFilter,
        string? actionFilter,
        DateTime? startDate,
        DateTime? endDate)
    {
        IQueryable<AuditLog> query = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(userFilter))
        {
            query = query.Where(a => a.UserName != null && a.UserName.Contains(userFilter));
        }
        if (!string.IsNullOrWhiteSpace(moduleFilter))
        {
            query = query.Where(a => a.EntityName == moduleFilter);
        }
        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            query = query.Where(a => a.Action == actionFilter);
        }
        if (startDate.HasValue)
        {
            query = query.Where(a => a.CreatedDate >= startDate.Value.ToUniversalTime());
        }
        if (endDate.HasValue)
        {
            query = query.Where(a => a.CreatedDate <= endDate.Value.ToUniversalTime().AddDays(1).AddTicks(-1));
        }

        var logs = await query.OrderByDescending(a => a.CreatedDate).Take(2000).ToListAsync();

        var builder = new StringBuilder();
        builder.AppendLine("AuditLogId,TimestampUtc,User,Action,Module,RecordId,Result,IpAddress,Details");

        foreach (var log in logs)
        {
            string cleanDetails = (log.Details ?? string.Empty).Replace("\"", "\"\"");
            builder.AppendLine($"{log.AuditLogId},\"{log.CreatedDate:yyyy-MM-dd HH:mm:ss}\",\"{log.UserName}\",\"{log.Action}\",\"{log.EntityName}\",\"{log.RecordId}\",\"{log.Result}\",\"{log.IpAddress}\",\"{cleanDetails}\"");
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return File(bytes, "text/csv", $"AcxiomCRM_AuditLogs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
    }
}

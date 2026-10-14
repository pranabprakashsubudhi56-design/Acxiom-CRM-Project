using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

namespace AcxiomCRM.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReportsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(
        string report = "Pipeline",
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? userId = null,
        string? status = null)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        bool isSalesExecutive = !User.IsInRole("Admin") && !User.IsInRole("Manager");

        var viewModel = new ReportsViewModel
        {
            ActiveReport = report,
            StartDate = startDate,
            EndDate = endDate,
            SelectedUserId = userId,
            StatusFilter = status
        };

        // Populate users dropdown
        var users = await _userManager.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync();
        ViewBag.Users = new SelectList(users, "Id", "FullName", userId);

        // Date range helper
        DateTime? startUtc = startDate?.ToUniversalTime();
        DateTime? endUtc = endDate?.ToUniversalTime().AddDays(1).AddTicks(-1);

        switch (report)
        {
            case "Pipeline":
                IQueryable<Opportunity> oppQuery = _context.Opportunities
                    .Include(o => o.Customer)
                    .Include(o => o.AssignedUser)
                    .AsNoTracking();

                if (isSalesExecutive) oppQuery = oppQuery.Where(o => o.AssignedTo == currentUserId);
                else if (!string.IsNullOrEmpty(userId)) oppQuery = oppQuery.Where(o => o.AssignedTo == userId);
                if (!string.IsNullOrEmpty(status)) oppQuery = oppQuery.Where(o => o.Status == status);
                if (startUtc.HasValue) oppQuery = oppQuery.Where(o => o.CreatedDate >= startUtc.Value);
                if (endUtc.HasValue) oppQuery = oppQuery.Where(o => o.CreatedDate <= endUtc.Value);

                var opps = await oppQuery.ToListAsync();
                viewModel.Opportunities = opps;
                viewModel.TotalRecords = opps.Count;
                viewModel.TotalAmount = opps.Sum(o => o.Amount);
                viewModel.WeightedAmount = opps.Sum(o => (o.Amount * o.Probability) / 100m);

                // Stage-wise pipeline
                viewModel.PipelineByStage = opps
                    .GroupBy(o => o.Stage)
                    .Select(g => new PipelineSummaryItem
                    {
                        GroupKey = g.Key,
                        Count = g.Count(),
                        TotalAmount = g.Sum(x => x.Amount),
                        WeightedAmount = g.Sum(x => (x.Amount * x.Probability) / 100m)
                    })
                    .ToList();

                // Owner-wise pipeline
                viewModel.PipelineByOwner = opps
                    .GroupBy(o => o.AssignedUser?.FullName ?? "Unassigned")
                    .Select(g => new PipelineSummaryItem
                    {
                        GroupKey = g.Key,
                        Count = g.Count(),
                        TotalAmount = g.Sum(x => x.Amount),
                        WeightedAmount = g.Sum(x => (x.Amount * x.Probability) / 100m)
                    })
                    .ToList();
                break;

            case "Customer":
                IQueryable<Customer> custQuery = _context.Customers
                    .Include(c => c.Owner)
                    .AsNoTracking();

                if (isSalesExecutive) custQuery = custQuery.Where(c => c.OwnerId == currentUserId);
                else if (!string.IsNullOrEmpty(userId)) custQuery = custQuery.Where(c => c.OwnerId == userId);
                if (!string.IsNullOrEmpty(status)) custQuery = custQuery.Where(c => c.Status == status);
                if (startUtc.HasValue) custQuery = custQuery.Where(c => c.CreatedDate >= startUtc.Value);
                if (endUtc.HasValue) custQuery = custQuery.Where(c => c.CreatedDate <= endUtc.Value);

                viewModel.Customers = await custQuery.OrderByDescending(c => c.CreatedDate).ToListAsync();
                viewModel.TotalRecords = viewModel.Customers.Count;
                break;

            case "Lead":
                IQueryable<Lead> leadQuery = _context.Leads
                    .Include(l => l.AssignedUser)
                    .Include(l => l.Customer)
                    .AsNoTracking();

                if (isSalesExecutive) leadQuery = leadQuery.Where(l => l.AssignedTo == currentUserId);
                else if (!string.IsNullOrEmpty(userId)) leadQuery = leadQuery.Where(l => l.AssignedTo == userId);
                if (!string.IsNullOrEmpty(status)) leadQuery = leadQuery.Where(l => l.Status == status);
                if (startUtc.HasValue) leadQuery = leadQuery.Where(l => l.CreatedDate >= startUtc.Value);
                if (endUtc.HasValue) leadQuery = leadQuery.Where(l => l.CreatedDate <= endUtc.Value);

                viewModel.Leads = await leadQuery.OrderByDescending(l => l.CreatedDate).ToListAsync();
                viewModel.TotalRecords = viewModel.Leads.Count;
                viewModel.TotalAmount = viewModel.Leads.Sum(l => l.ExpectedValue);
                break;

            case "FollowUp":
                IQueryable<FollowUp> followUpQuery = _context.FollowUps
                    .Include(f => f.Customer)
                    .Include(f => f.Lead)
                    .Include(f => f.AssignedUser)
                    .AsNoTracking();

                if (isSalesExecutive) followUpQuery = followUpQuery.Where(f => f.AssignedTo == currentUserId);
                else if (!string.IsNullOrEmpty(userId)) followUpQuery = followUpQuery.Where(f => f.AssignedTo == userId);
                if (!string.IsNullOrEmpty(status)) followUpQuery = followUpQuery.Where(f => f.Status == status);
                if (startUtc.HasValue) followUpQuery = followUpQuery.Where(f => f.FollowUpDate >= startUtc.Value);
                if (endUtc.HasValue) followUpQuery = followUpQuery.Where(f => f.FollowUpDate <= endUtc.Value);

                viewModel.FollowUps = await followUpQuery.OrderBy(f => f.FollowUpDate).ToListAsync();
                viewModel.TotalRecords = viewModel.FollowUps.Count;
                break;

            case "Conversion":
                IQueryable<Lead> convLeadQuery = _context.Leads.AsNoTracking();
                IQueryable<Opportunity> convOppQuery = _context.Opportunities.AsNoTracking();

                if (isSalesExecutive)
                {
                    convLeadQuery = convLeadQuery.Where(l => l.AssignedTo == currentUserId);
                    convOppQuery = convOppQuery.Where(o => o.AssignedTo == currentUserId);
                }

                var allLeads = await convLeadQuery.ToListAsync();
                var allOpps = await convOppQuery.ToListAsync();

                viewModel.TotalLeadsCount = allLeads.Count;
                viewModel.ConvertedLeadsCount = allLeads.Count(l => l.Status == "Converted");
                viewModel.LostLeadsCount = allLeads.Count(l => l.Status == "Lost");
                viewModel.LeadConversionRate = viewModel.TotalLeadsCount > 0
                    ? Math.Round((decimal)viewModel.ConvertedLeadsCount / viewModel.TotalLeadsCount * 100, 1)
                    : 0;

                viewModel.WonDealsCount = allOpps.Count(o => o.Status == "Won");
                viewModel.LostDealsCount = allOpps.Count(o => o.Status == "Lost");
                int decidedDeals = viewModel.WonDealsCount + viewModel.LostDealsCount;
                viewModel.DealWinRate = decidedDeals > 0
                    ? Math.Round((decimal)viewModel.WonDealsCount / decidedDeals * 100, 1)
                    : 0;
                break;

            case "UserActivity":
                if (isSalesExecutive) return Forbid();

                var appUsers = await _userManager.Users.Where(u => u.IsActive).ToListAsync();
                var userStats = new List<UserActivitySummary>();

                foreach (var u in appUsers)
                {
                    int cCount = await _context.Customers.CountAsync(c => c.OwnerId == u.Id);
                    int lCount = await _context.Leads.CountAsync(l => l.AssignedTo == u.Id);
                    int oCount = await _context.Opportunities.CountAsync(o => o.AssignedTo == u.Id);
                    int fCount = await _context.FollowUps.CountAsync(f => f.AssignedTo == u.Id);

                    userStats.Add(new UserActivitySummary
                    {
                        UserName = u.FullName,
                        Role = u.UserRole,
                        CustomersCount = cCount,
                        LeadsCount = lCount,
                        OpportunitiesCount = oCount,
                        FollowUpsCount = fCount,
                        TotalActionsCount = cCount + lCount + oCount + fCount
                    });
                }

                viewModel.UserActivities = userStats.OrderByDescending(s => s.TotalActionsCount).ToList();
                break;

            case "Audit":
                if (isSalesExecutive) return Forbid();

                IQueryable<AuditLog> auditQ = _context.AuditLogs.AsNoTracking();
                if (startUtc.HasValue) auditQ = auditQ.Where(a => a.CreatedDate >= startUtc.Value);
                if (endUtc.HasValue) auditQ = auditQ.Where(a => a.CreatedDate <= endUtc.Value);

                viewModel.AuditLogs = await auditQ.OrderByDescending(a => a.CreatedDate).Take(200).ToListAsync();
                viewModel.TotalRecords = viewModel.AuditLogs.Count;
                break;
        }

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(string report = "Pipeline")
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        bool isSalesExecutive = !User.IsInRole("Admin") && !User.IsInRole("Manager");

        var sb = new StringBuilder();

        if (report == "Pipeline")
        {
            sb.AppendLine("OpportunityId,Name,Customer,Amount,Stage,Probability,ExpectedCloseDate,Status,AssignedTo");
            var query = _context.Opportunities.Include(o => o.Customer).Include(o => o.AssignedUser).AsQueryable();
            if (isSalesExecutive) query = query.Where(o => o.AssignedTo == currentUserId);
            var list = await query.ToListAsync();
            foreach (var o in list)
            {
                sb.AppendLine($"{o.OpportunityId},\"{o.OpportunityName}\",\"{o.Customer?.CustomerName}\",{o.Amount},\"{o.Stage}\",{o.Probability},\"{o.ExpectedCloseDate:yyyy-MM-dd}\",\"{o.Status}\",\"{o.AssignedUser?.FullName}\"");
            }
        }
        else if (report == "Customer")
        {
            sb.AppendLine("CustomerId,Code,Name,Email,Phone,Company,Status,CreatedDate,Owner");
            var query = _context.Customers.Include(c => c.Owner).AsQueryable();
            if (isSalesExecutive) query = query.Where(c => c.OwnerId == currentUserId);
            var list = await query.ToListAsync();
            foreach (var c in list)
            {
                sb.AppendLine($"{c.CustomerId},\"{c.CustomerCode}\",\"{c.CustomerName}\",\"{c.Email}\",\"{c.Phone}\",\"{c.CompanyName}\",\"{c.Status}\",\"{c.CreatedDate:yyyy-MM-dd}\",\"{c.Owner?.FullName}\"");
            }
        }
        else if (report == "Lead")
        {
            sb.AppendLine("LeadId,Code,Name,Email,Phone,Company,Source,Status,Priority,ExpectedValue,AssignedTo");
            var query = _context.Leads.Include(l => l.AssignedUser).AsQueryable();
            if (isSalesExecutive) query = query.Where(l => l.AssignedTo == currentUserId);
            var list = await query.ToListAsync();
            foreach (var l in list)
            {
                sb.AppendLine($"{l.LeadId},\"{l.LeadCode}\",\"{l.LeadName}\",\"{l.Email}\",\"{l.Phone}\",\"{l.CompanyName}\",\"{l.Source}\",\"{l.Status}\",\"{l.Priority}\",{l.ExpectedValue},\"{l.AssignedUser?.FullName}\"");
            }
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"AcxiomCRM_{report}Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
    }
}

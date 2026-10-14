using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AcxiomCRM.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string dateFilter = "ThisMonth", DateTime? startDate = null, DateTime? endDate = null)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var currentUser = await _userManager.GetUserAsync(User);
        bool isAdmin = User.IsInRole("Admin");
        bool isManager = User.IsInRole("Manager");
        bool isSalesExecutive = !isAdmin && !isManager;

        // Base scoped queries
        IQueryable<Customer> customerQuery = _context.Customers.AsNoTracking();
        IQueryable<Lead> leadQuery = _context.Leads.AsNoTracking();
        IQueryable<Opportunity> opportunityQuery = _context.Opportunities.Include(o => o.Customer).AsNoTracking();
        IQueryable<FollowUp> followUpQuery = _context.FollowUps.Include(f => f.Customer).Include(f => f.Lead).AsNoTracking();
        IQueryable<Activity> activityQuery = _context.Activities.Include(a => a.Customer).Include(a => a.Lead).AsNoTracking();

        // Scope data for Sales Executive
        if (isSalesExecutive)
        {
            customerQuery = customerQuery.Where(c => c.OwnerId == currentUserId);
            leadQuery = leadQuery.Where(l => l.AssignedTo == currentUserId);
            opportunityQuery = opportunityQuery.Where(o => o.AssignedTo == currentUserId);
            followUpQuery = followUpQuery.Where(f => f.AssignedTo == currentUserId);
            activityQuery = activityQuery.Where(a => a.AssignedTo == currentUserId);
        }

        // Apply Date Filters
        DateTime now = DateTime.UtcNow;
        DateTime? filterStart = null;
        DateTime? filterEnd = null;

        switch (dateFilter)
        {
            case "Today":
                filterStart = now.Date;
                filterEnd = now.Date.AddDays(1).AddTicks(-1);
                break;
            case "ThisWeek":
                int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
                filterStart = now.Date.AddDays(-1 * diff);
                filterEnd = filterStart.Value.AddDays(7).AddTicks(-1);
                break;
            case "ThisMonth":
                filterStart = new DateTime(now.Year, now.Month, 1);
                filterEnd = filterStart.Value.AddMonths(1).AddTicks(-1);
                break;
            case "Custom":
                filterStart = startDate?.ToUniversalTime();
                filterEnd = endDate?.ToUniversalTime().AddDays(1).AddTicks(-1);
                break;
            case "AllTime":
            default:
                break;
        }

        // Count queries
        var totalCustomers = await customerQuery.CountAsync();
        var totalLeads = await leadQuery.CountAsync();
        var openLeads = await leadQuery.Where(l => l.Status != "Converted" && l.Status != "Lost").CountAsync();

        var totalOpportunities = await opportunityQuery.CountAsync();
        var openOpportunities = await opportunityQuery.Where(o => o.Status == "Open").CountAsync();
        var wonOpportunities = await opportunityQuery.Where(o => o.Status == "Won").CountAsync();
        var lostOpportunities = await opportunityQuery.Where(o => o.Status == "Lost").CountAsync();

        var openOppsList = await opportunityQuery.Where(o => o.Status == "Open").ToListAsync();
        var totalPipelineValue = openOppsList.Sum(o => o.Amount);
        var weightedPipelineValue = openOppsList.Sum(o => (o.Amount * o.Probability) / 100m);

        // Follow-up statistics
        var pendingFollowUpsCount = await followUpQuery.Where(f => f.Status == "Planned").CountAsync();
        var overdueFollowUpsCount = await followUpQuery.Where(f => f.Status == "Planned" && f.FollowUpDate < now).CountAsync();
        var todayFollowUpsCount = await followUpQuery.Where(f => f.Status == "Planned" && f.FollowUpDate.Date == now.Date).CountAsync();

        // 1. Chart: Lead Status Breakdown
        var allLeads = await leadQuery.ToListAsync();
        var leadStatuses = new[] { "New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost" };
        var leadStatusLabels = leadStatuses.ToList();
        var leadStatusCounts = leadStatuses.Select(s => allLeads.Count(l => l.Status == s)).ToList();

        // 2. Chart: Opportunity Pipeline Breakdown
        var allOpportunities = await opportunityQuery.ToListAsync();
        var pipelineStages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
        var pipelineLabels = pipelineStages.ToList();
        var pipelineAmounts = pipelineStages.Select(stage => allOpportunities.Where(o => o.Stage == stage).Sum(o => o.Amount)).ToList();

        // 3. Chart: Monthly Sales (Won opportunities by month for past 6 months)
        var monthlySalesLabels = new List<string>();
        var monthlySalesAmounts = new List<decimal>();

        for (int i = 5; i >= 0; i--)
        {
            var targetMonth = now.AddMonths(-i);
            var mStart = new DateTime(targetMonth.Year, targetMonth.Month, 1);
            var mEnd = mStart.AddMonths(1).AddTicks(-1);

            monthlySalesLabels.Add(targetMonth.ToString("MMM yyyy"));
            var mAmount = allOpportunities
                .Where(o => o.Status == "Won" && o.CreatedDate >= mStart && o.CreatedDate <= mEnd)
                .Sum(o => o.Amount);
            monthlySalesAmounts.Add(mAmount);
        }

        // Upcoming followups
        var upcomingFollowUps = await followUpQuery
            .Where(f => f.Status == "Planned")
            .OrderBy(f => f.FollowUpDate)
            .Take(5)
            .ToListAsync();

        // Top opportunities
        var openOppsForTop = await opportunityQuery
            .Where(o => o.Status == "Open")
            .ToListAsync();
        var topOpportunities = openOppsForTop
            .OrderByDescending(o => o.Amount)
            .Take(5)
            .ToList();

        // Recent activities
        var recentActivities = await activityQuery
            .OrderByDescending(a => a.ActivityDate)
            .Take(5)
            .ToListAsync();

        var viewModel = new DashboardViewModel
        {
            UserRole = currentUser?.UserRole ?? "SalesExecutive",
            UserName = currentUser?.FullName ?? User.Identity?.Name ?? "User",
            DateFilter = dateFilter,
            StartDate = startDate,
            EndDate = endDate,
            TotalCustomers = totalCustomers,
            TotalLeads = totalLeads,
            OpenLeads = openLeads,
            TotalOpportunities = totalOpportunities,
            OpenOpportunities = openOpportunities,
            WonOpportunities = wonOpportunities,
            LostOpportunities = lostOpportunities,
            TotalPipelineValue = totalPipelineValue,
            WeightedPipelineValue = weightedPipelineValue,
            PendingFollowUpsCount = pendingFollowUpsCount,
            OverdueFollowUpsCount = overdueFollowUpsCount,
            TodayFollowUpsCount = todayFollowUpsCount,
            LeadStatusLabels = leadStatusLabels,
            LeadStatusCounts = leadStatusCounts,
            OpportunityPipelineLabels = pipelineLabels,
            OpportunityPipelineAmounts = pipelineAmounts,
            MonthlySalesLabels = monthlySalesLabels,
            MonthlySalesAmounts = monthlySalesAmounts,
            UpcomingFollowUps = upcomingFollowUps,
            TopOpportunities = topOpportunities,
            RecentActivities = recentActivities
        };

        return View(viewModel);
    }
}

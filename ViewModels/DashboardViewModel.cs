using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels;

public class DashboardViewModel
{
    // Role & Context
    public string UserRole { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DateFilter { get; set; } = "ThisMonth"; // Today, ThisWeek, ThisMonth, AllTime, Custom
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    // Mandatory KPI Cards (Section 17.11)
    public int TotalCustomers { get; set; }
    public int TotalLeads { get; set; }
    public int OpenLeads { get; set; }
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public decimal TotalPipelineValue { get; set; }
    public decimal WeightedPipelineValue { get; set; }

    // Follow-ups & Activities
    public int PendingFollowUpsCount { get; set; }
    public int OverdueFollowUpsCount { get; set; }
    public int TodayFollowUpsCount { get; set; }

    // Chart.js Data (Section 17.12)
    public List<string> LeadStatusLabels { get; set; } = new();
    public List<int> LeadStatusCounts { get; set; } = new();

    public List<string> OpportunityPipelineLabels { get; set; } = new();
    public List<decimal> OpportunityPipelineAmounts { get; set; } = new();

    public List<string> MonthlySalesLabels { get; set; } = new();
    public List<decimal> MonthlySalesAmounts { get; set; } = new();

    // Lists for Quick Action Panels
    public List<FollowUp> UpcomingFollowUps { get; set; } = new();
    public List<Opportunity> TopOpportunities { get; set; } = new();
    public List<Activity> RecentActivities { get; set; } = new();
}

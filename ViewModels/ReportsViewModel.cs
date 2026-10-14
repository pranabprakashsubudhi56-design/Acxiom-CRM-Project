using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels;

public class ReportsViewModel
{
    public string ActiveReport { get; set; } = "Pipeline";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? SelectedUserId { get; set; }
    public string? StatusFilter { get; set; }

    // Summary KPIs for current report view
    public decimal TotalAmount { get; set; }
    public decimal WeightedAmount { get; set; }
    public int TotalRecords { get; set; }

    // Aggregated data collections
    public List<Customer> Customers { get; set; } = new();
    public List<Lead> Leads { get; set; } = new();
    public List<Opportunity> Opportunities { get; set; } = new();
    public List<FollowUp> FollowUps { get; set; } = new();
    public List<AuditLog> AuditLogs { get; set; } = new();

    // Pipeline report grouped summaries
    public List<PipelineSummaryItem> PipelineByStage { get; set; } = new();
    public List<PipelineSummaryItem> PipelineByOwner { get; set; } = new();

    // Conversion report stats
    public int TotalLeadsCount { get; set; }
    public int ConvertedLeadsCount { get; set; }
    public int LostLeadsCount { get; set; }
    public decimal LeadConversionRate { get; set; }
    public int WonDealsCount { get; set; }
    public int LostDealsCount { get; set; }
    public decimal DealWinRate { get; set; }

    // User activity report stats
    public List<UserActivitySummary> UserActivities { get; set; } = new();
}

public class PipelineSummaryItem
{
    public string GroupKey { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal WeightedAmount { get; set; }
}

public class UserActivitySummary
{
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int CustomersCount { get; set; }
    public int LeadsCount { get; set; }
    public int OpportunitiesCount { get; set; }
    public int FollowUpsCount { get; set; }
    public int TotalActionsCount { get; set; }
}

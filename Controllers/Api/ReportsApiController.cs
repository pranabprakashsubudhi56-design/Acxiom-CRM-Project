using AcxiomCRM.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/reports")]
[Produces("application/json")]
public class ReportsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ReportsApiController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("pipeline")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPipelineReport()
    {
        var openOpportunities = await _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.AssignedUser)
            .Where(o => o.Status == "Open")
            .AsNoTracking()
            .ToListAsync();

        var stageSummary = openOpportunities
            .GroupBy(o => o.Stage)
            .Select(g => new
            {
                Stage = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(x => x.Amount),
                WeightedAmount = g.Sum(x => (x.Amount * x.Probability) / 100m)
            })
            .ToList();

        var ownerSummary = openOpportunities
            .GroupBy(o => o.AssignedUser?.FullName ?? "Unassigned")
            .Select(g => new
            {
                Owner = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(x => x.Amount),
                WeightedAmount = g.Sum(x => (x.Amount * x.Probability) / 100m)
            })
            .ToList();

        return Ok(new
        {
            TotalPipeline = openOpportunities.Sum(o => o.Amount),
            WeightedPipeline = openOpportunities.Sum(o => (o.Amount * o.Probability) / 100m),
            TotalOpenOpportunities = openOpportunities.Count,
            Stages = stageSummary,
            Owners = ownerSummary
        });
    }
}

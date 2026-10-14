using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/opportunities")]
[Produces("application/json")]
public class OpportunitiesApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public OpportunitiesApiController(ApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<OpportunityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOpportunities([FromQuery] string? stage, [FromQuery] string? status)
    {
        IQueryable<Opportunity> query = _context.Opportunities
            .Include(o => o.Customer)
            .Include(o => o.AssignedUser)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(stage)) query = query.Where(o => o.Stage == stage);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(o => o.Status == status);

        var list = await query.OrderByDescending(o => o.CreatedDate)
            .Select(o => new OpportunityDto
            {
                OpportunityId = o.OpportunityId,
                OpportunityName = o.OpportunityName,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer != null ? o.Customer.CustomerName : null,
                LeadId = o.LeadId,
                Amount = o.Amount,
                Stage = o.Stage,
                Probability = o.Probability,
                ExpectedCloseDate = o.ExpectedCloseDate,
                WeightedValue = (o.Amount * o.Probability) / 100m,
                Status = o.Status,
                CreatedDate = o.CreatedDate,
                AssignedTo = o.AssignedTo,
                AssignedUserName = o.AssignedUser != null ? o.AssignedUser.FullName : null,
                Notes = o.Notes
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOpportunity(int id)
    {
        var o = await _context.Opportunities
            .Include(x => x.Customer)
            .Include(x => x.AssignedUser)
            .FirstOrDefaultAsync(x => x.OpportunityId == id);

        if (o == null) return NotFound(new { message = $"Opportunity with ID {id} not found." });

        return Ok(new OpportunityDto
        {
            OpportunityId = o.OpportunityId,
            OpportunityName = o.OpportunityName,
            CustomerId = o.CustomerId,
            CustomerName = o.Customer?.CustomerName,
            LeadId = o.LeadId,
            Amount = o.Amount,
            Stage = o.Stage,
            Probability = o.Probability,
            ExpectedCloseDate = o.ExpectedCloseDate,
            WeightedValue = (o.Amount * o.Probability) / 100m,
            Status = o.Status,
            CreatedDate = o.CreatedDate,
            AssignedTo = o.AssignedTo,
            AssignedUserName = o.AssignedUser?.FullName,
            Notes = o.Notes
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOpportunity([FromBody] OpportunityCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Mandatory Business Validations (Section 5.3 & 17.7)
        if (dto.Amount <= 0)
        {
            return BadRequest(new { message = "Opportunity Amount must be greater than 0." });
        }

        if (dto.Probability < 0 || dto.Probability > 100)
        {
            return BadRequest(new { message = "Probability must be between 0 and 100." });
        }

        if (dto.Status == "Open" && dto.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
        {
            return BadRequest(new { message = "Expected Close Date cannot be in the past for an active opportunity." });
        }

        var opp = new Opportunity
        {
            OpportunityName = dto.OpportunityName.Trim(),
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            Amount = dto.Amount,
            Stage = dto.Stage,
            Probability = dto.Probability,
            ExpectedCloseDate = dto.ExpectedCloseDate,
            Status = dto.Stage == "Won" ? "Won" : (dto.Stage == "Lost" ? "Lost" : "Open"),
            AssignedTo = dto.AssignedTo,
            Notes = dto.Notes,
            CreatedDate = DateTime.UtcNow
        };

        _context.Opportunities.Add(opp);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Create", "Opportunity", opp.OpportunityId.ToString(),
            null, new { opp.OpportunityName, opp.Amount, opp.Stage },
            "Success", "Created opportunity via REST API.");

        var resultDto = new OpportunityDto
        {
            OpportunityId = opp.OpportunityId,
            OpportunityName = opp.OpportunityName,
            CustomerId = opp.CustomerId,
            LeadId = opp.LeadId,
            Amount = opp.Amount,
            Stage = opp.Stage,
            Probability = opp.Probability,
            ExpectedCloseDate = opp.ExpectedCloseDate,
            WeightedValue = (opp.Amount * opp.Probability) / 100m,
            Status = opp.Status,
            CreatedDate = opp.CreatedDate,
            AssignedTo = opp.AssignedTo,
            Notes = opp.Notes
        };

        return CreatedAtAction(nameof(GetOpportunity), new { id = opp.OpportunityId }, resultDto);
    }
}

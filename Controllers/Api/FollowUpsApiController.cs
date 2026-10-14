using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/followups")]
[Produces("application/json")]
public class FollowUpsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public FollowUpsApiController(ApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<FollowUpDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFollowUps([FromQuery] string? status)
    {
        IQueryable<FollowUp> query = _context.FollowUps
            .Include(f => f.Customer)
            .Include(f => f.AssignedUser)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(f => f.Status == status);

        var list = await query.OrderBy(f => f.FollowUpDate)
            .Select(f => new FollowUpDto
            {
                FollowUpId = f.FollowUpId,
                CustomerId = f.CustomerId,
                CustomerName = f.Customer != null ? f.Customer.CustomerName : null,
                LeadId = f.LeadId,
                OpportunityId = f.OpportunityId,
                FollowUpDate = f.FollowUpDate,
                FollowUpType = f.FollowUpType,
                Subject = f.Subject,
                Remarks = f.Remarks,
                Status = f.Status,
                AssignedTo = f.AssignedTo,
                AssignedUserName = f.AssignedUser != null ? f.AssignedUser.FullName : null
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpPost]
    [ProducesResponseType(typeof(FollowUpDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateFollowUp([FromBody] FollowUp model)
    {
        if (model.Status == "Planned" && model.FollowUpDate.Date < DateTime.UtcNow.Date)
        {
            return BadRequest(new { message = "Follow-up date cannot be earlier than today." });
        }

        if (string.IsNullOrWhiteSpace(model.Subject))
        {
            return BadRequest(new { message = "Subject is required." });
        }

        model.CreatedDate = DateTime.UtcNow;
        _context.FollowUps.Add(model);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Create", "FollowUp", model.FollowUpId.ToString(),
            null, new { model.Subject, model.FollowUpDate },
            "Success", "Created follow-up via REST API.");

        var dto = new FollowUpDto
        {
            FollowUpId = model.FollowUpId,
            CustomerId = model.CustomerId,
            LeadId = model.LeadId,
            OpportunityId = model.OpportunityId,
            FollowUpDate = model.FollowUpDate,
            FollowUpType = model.FollowUpType,
            Subject = model.Subject,
            Remarks = model.Remarks,
            Status = model.Status,
            AssignedTo = model.AssignedTo
        };

        return CreatedAtAction(nameof(GetFollowUps), new { id = model.FollowUpId }, dto);
    }
}

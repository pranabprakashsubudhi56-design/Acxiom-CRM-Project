using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/leads")]
[Produces("application/json")]
public class LeadsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public LeadsApiController(ApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LeadDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLeads([FromQuery] string? status, [FromQuery] string? priority)
    {
        IQueryable<Lead> query = _context.Leads.Include(l => l.AssignedUser).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(l => l.Status == status);
        if (!string.IsNullOrWhiteSpace(priority)) query = query.Where(l => l.Priority == priority);

        var list = await query.OrderByDescending(l => l.CreatedDate)
            .Select(l => new LeadDto
            {
                LeadId = l.LeadId,
                LeadCode = l.LeadCode,
                LeadName = l.LeadName,
                Email = l.Email,
                Phone = l.Phone,
                CompanyName = l.CompanyName,
                Source = l.Source,
                Status = l.Status,
                Priority = l.Priority,
                ExpectedValue = l.ExpectedValue,
                CreatedDate = l.CreatedDate,
                AssignedTo = l.AssignedTo,
                AssignedUserName = l.AssignedUser != null ? l.AssignedUser.FullName : null
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLead(int id)
    {
        var l = await _context.Leads.Include(x => x.AssignedUser).FirstOrDefaultAsync(x => x.LeadId == id);
        if (l == null) return NotFound(new { message = $"Lead with ID {id} not found." });

        return Ok(new LeadDto
        {
            LeadId = l.LeadId,
            LeadCode = l.LeadCode,
            LeadName = l.LeadName,
            Email = l.Email,
            Phone = l.Phone,
            CompanyName = l.CompanyName,
            Source = l.Source,
            Status = l.Status,
            Priority = l.Priority,
            ExpectedValue = l.ExpectedValue,
            CreatedDate = l.CreatedDate,
            AssignedTo = l.AssignedTo,
            AssignedUserName = l.AssignedUser?.FullName
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateLead([FromBody] LeadCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (dto.ExpectedValue < 0)
        {
            return BadRequest(new { message = "Expected value must be 0 or greater." });
        }

        var lastId = await _context.Leads.MaxAsync(l => (int?)l.LeadId) ?? 1000;
        var leadCode = $"LEAD-{lastId + 1}";

        var lead = new Lead
        {
            LeadCode = leadCode,
            LeadName = dto.LeadName.Trim(),
            Email = dto.Email.Trim().ToLower(),
            Phone = dto.Phone.Trim(),
            CompanyName = dto.CompanyName,
            Source = string.IsNullOrWhiteSpace(dto.Source) ? "Website" : dto.Source,
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "New" : dto.Status,
            Priority = string.IsNullOrWhiteSpace(dto.Priority) ? "Medium" : dto.Priority,
            ExpectedValue = dto.ExpectedValue,
            AssignedTo = dto.AssignedTo,
            CreatedDate = DateTime.UtcNow
        };

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Create", "Lead", lead.LeadId.ToString(),
            null, new { lead.LeadCode, lead.LeadName, lead.ExpectedValue },
            "Success", "Created lead via REST API.");

        var resultDto = new LeadDto
        {
            LeadId = lead.LeadId,
            LeadCode = lead.LeadCode,
            LeadName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            Source = lead.Source,
            Status = lead.Status,
            Priority = lead.Priority,
            ExpectedValue = lead.ExpectedValue,
            CreatedDate = lead.CreatedDate,
            AssignedTo = lead.AssignedTo
        };

        return CreatedAtAction(nameof(GetLead), new { id = lead.LeadId }, resultDto);
    }
}

using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/customers")]
[Produces("application/json")]
public class CustomersApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public CustomersApiController(ApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomers([FromQuery] string? search, [FromQuery] string? status)
    {
        IQueryable<Customer> query = _context.Customers.Include(c => c.Owner).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim().ToLower();
            query = query.Where(c => c.CustomerName.ToLower().Contains(search) ||
                                     c.Email.ToLower().Contains(search) ||
                                     c.Phone.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        var list = await query.OrderByDescending(c => c.CreatedDate)
            .Select(c => new CustomerDto
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                Address = c.Address,
                City = c.City,
                State = c.State,
                Status = c.Status,
                CreatedDate = c.CreatedDate,
                OwnerId = c.OwnerId,
                OwnerName = c.Owner != null ? c.Owner.FullName : null
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustomer(int id)
    {
        var c = await _context.Customers.Include(c => c.Owner).FirstOrDefaultAsync(c => c.CustomerId == id);
        if (c == null) return NotFound(new { message = $"Customer with ID {id} not found." });

        return Ok(new CustomerDto
        {
            CustomerId = c.CustomerId,
            CustomerCode = c.CustomerCode,
            CustomerName = c.CustomerName,
            Email = c.Email,
            Phone = c.Phone,
            CompanyName = c.CompanyName,
            Address = c.Address,
            City = c.City,
            State = c.State,
            Status = c.Status,
            CreatedDate = c.CreatedDate,
            OwnerId = c.OwnerId,
            OwnerName = c.Owner?.FullName
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCustomer([FromBody] CustomerCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Duplicate checks
        bool emailExists = await _context.Customers.AnyAsync(c => c.Email.ToLower() == dto.Email.Trim().ToLower());
        if (emailExists)
        {
            return Conflict(new { message = "A customer with this email address already exists." });
        }

        bool phoneExists = await _context.Customers.AnyAsync(c => c.Phone == dto.Phone.Trim());
        if (phoneExists)
        {
            return Conflict(new { message = "A customer with this phone number already exists." });
        }

        var lastId = await _context.Customers.MaxAsync(c => (int?)c.CustomerId) ?? 1000;
        var customerCode = $"CUST-{lastId + 1}";

        var customer = new Customer
        {
            CustomerCode = customerCode,
            CustomerName = dto.CustomerName.Trim(),
            Email = dto.Email.Trim().ToLower(),
            Phone = dto.Phone.Trim(),
            CompanyName = dto.CompanyName,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status,
            OwnerId = dto.OwnerId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = "API"
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Create", "Customer", customer.CustomerId.ToString(),
            null, new { customer.CustomerCode, customer.CustomerName, customer.Email },
            "Success", "Created customer via REST API.");

        var resultDto = new CustomerDto
        {
            CustomerId = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            Email = customer.Email,
            Phone = customer.Phone,
            CompanyName = customer.CompanyName,
            Address = customer.Address,
            City = customer.City,
            State = customer.State,
            Status = customer.Status,
            CreatedDate = customer.CreatedDate,
            OwnerId = customer.OwnerId
        };

        return CreatedAtAction(nameof(GetCustomer), new { id = customer.CustomerId }, resultDto);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCustomer(int id, [FromBody] CustomerCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound(new { message = $"Customer with ID {id} not found." });

        bool emailConflict = await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Email.ToLower() == dto.Email.Trim().ToLower());
        if (emailConflict) return Conflict(new { message = "Another customer with this email address already exists." });

        bool phoneConflict = await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Phone == dto.Phone.Trim());
        if (phoneConflict) return Conflict(new { message = "Another customer with this phone number already exists." });

        customer.CustomerName = dto.CustomerName.Trim();
        customer.Email = dto.Email.Trim().ToLower();
        customer.Phone = dto.Phone.Trim();
        customer.CompanyName = dto.CompanyName;
        customer.Address = dto.Address;
        customer.City = dto.City;
        customer.State = dto.State;
        customer.Status = dto.Status;
        customer.OwnerId = dto.OwnerId;
        customer.ModifiedDate = DateTime.UtcNow;

        _context.Update(customer);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Update", "Customer", id.ToString(),
            null, new { customer.CustomerName, customer.Email },
            "Success", "Updated customer via REST API.");

        return Ok(new { message = "Customer updated successfully." });
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound(new { message = $"Customer with ID {id} not found." });

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Delete", "Customer", id.ToString(),
            new { customer.CustomerName }, null,
            "Success", "Deleted customer via REST API.");

        return Ok(new { message = "Customer deleted successfully." });
    }
}

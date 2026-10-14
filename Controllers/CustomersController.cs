using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AcxiomCRM.Controllers;

[Authorize]
public class CustomersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public CustomersController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? search, string? status, int page = 1)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        bool isSalesExecutive = !User.IsInRole("Admin") && !User.IsInRole("Manager");

        IQueryable<Customer> query = _context.Customers
            .Include(c => c.Owner)
            .AsNoTracking();

        if (isSalesExecutive)
        {
            query = query.Where(c => c.OwnerId == currentUserId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim().ToLower();
            query = query.Where(c =>
                c.CustomerName.ToLower().Contains(search) ||
                c.Email.ToLower().Contains(search) ||
                c.Phone.Contains(search) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(search)) ||
                c.CustomerCode.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        const int pageSize = 10;
        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var customers = await query
            .OrderByDescending(c => c.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;

        return View(customers);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var customer = await _context.Customers
            .Include(c => c.Owner)
            .Include(c => c.Opportunities)
            .Include(c => c.FollowUps.OrderByDescending(f => f.FollowUpDate))
            .Include(c => c.Activities.OrderByDescending(a => a.ActivityDate))
            .FirstOrDefaultAsync(m => m.CustomerId == id);

        if (customer == null) return NotFound();

        // Scope check for SalesExecutive
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (customer.OwnerId != currentUserId)
            {
                return Forbid();
            }
        }

        return View(customer);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateUsersDropdownAsync();
        return View(new Customer
        {
            Status = "Active",
            CustomerCode = await GenerateCustomerCodeAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer customer)
    {
        // 1. Mandatory Business Validations (Section 5.3 & 17.5)
        if (string.IsNullOrWhiteSpace(customer.CustomerName))
        {
            ModelState.AddModelError("CustomerName", "Customer Name is required.");
        }

        if (string.IsNullOrWhiteSpace(customer.Email))
        {
            ModelState.AddModelError("Email", "Enter a valid email address.");
        }
        else
        {
            // Uniqueness check for Email
            bool emailExists = await _context.Customers.AnyAsync(c => c.Email.ToLower() == customer.Email.Trim().ToLower());
            if (emailExists)
            {
                ModelState.AddModelError("Email", "A customer with this email address already exists.");
            }
        }

        if (string.IsNullOrWhiteSpace(customer.Phone))
        {
            ModelState.AddModelError("Phone", "Enter a valid phone number.");
        }
        else
        {
            // Phone pattern & length check (10-digit)
            if (!System.Text.RegularExpressions.Regex.IsMatch(customer.Phone.Trim(), @"^[0-9]{10}$"))
            {
                ModelState.AddModelError("Phone", "Enter a valid 10-digit phone number.");
            }
            else
            {
                // Uniqueness check for Phone
                bool phoneExists = await _context.Customers.AnyAsync(c => c.Phone == customer.Phone.Trim());
                if (phoneExists)
                {
                    ModelState.AddModelError("Phone", "A customer with this phone number already exists.");
                }
            }
        }

        // Duplicate customer check (Name & Company)
        if (!string.IsNullOrWhiteSpace(customer.CustomerName) && !string.IsNullOrWhiteSpace(customer.CompanyName))
        {
            bool duplicateCustomer = await _context.Customers.AnyAsync(c =>
                c.CustomerName.ToLower() == customer.CustomerName.Trim().ToLower() &&
                c.CompanyName != null && c.CompanyName.ToLower() == customer.CompanyName.Trim().ToLower());

            if (duplicateCustomer)
            {
                ModelState.AddModelError(string.Empty, "A duplicate customer with the same name and company already exists.");
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateUsersDropdownAsync(customer.OwnerId);
            return View(customer);
        }

        // Auto-assign owner if not selected and user is sales executive
        if (string.IsNullOrEmpty(customer.OwnerId))
        {
            customer.OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        customer.CustomerCode = await GenerateCustomerCodeAsync();
        customer.CreatedDate = DateTime.UtcNow;
        customer.CreatedBy = User.Identity?.Name ?? "System";
        customer.Email = customer.Email.Trim().ToLower();
        customer.Phone = customer.Phone.Trim();

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Create", "Customer", customer.CustomerId.ToString(),
            null,
            new { customer.CustomerCode, customer.CustomerName, customer.Email, customer.Phone },
            "Success",
            $"Created Customer {customer.CustomerName} ({customer.CustomerCode})");

        TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound();

        // Scope check
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (customer.OwnerId != currentUserId) return Forbid();
        }

        await PopulateUsersDropdownAsync(customer.OwnerId);
        return View(customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Customer customer)
    {
        if (id != customer.CustomerId) return NotFound();

        var existingCustomer = await _context.Customers.FindAsync(id);
        if (existingCustomer == null) return NotFound();

        // Scope check
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (existingCustomer.OwnerId != currentUserId) return Forbid();
        }

        // Validate Email uniqueness excluding this record
        if (string.IsNullOrWhiteSpace(customer.Email))
        {
            ModelState.AddModelError("Email", "Enter a valid email address.");
        }
        else
        {
            bool emailExists = await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Email.ToLower() == customer.Email.Trim().ToLower());
            if (emailExists)
            {
                ModelState.AddModelError("Email", "Another customer with this email address already exists.");
            }
        }

        // Validate Phone uniqueness excluding this record
        if (string.IsNullOrWhiteSpace(customer.Phone) || !System.Text.RegularExpressions.Regex.IsMatch(customer.Phone.Trim(), @"^[0-9]{10}$"))
        {
            ModelState.AddModelError("Phone", "Enter a valid 10-digit phone number.");
        }
        else
        {
            bool phoneExists = await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Phone == customer.Phone.Trim());
            if (phoneExists)
            {
                ModelState.AddModelError("Phone", "Another customer with this phone number already exists.");
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateUsersDropdownAsync(customer.OwnerId);
            return View(customer);
        }

        var oldSnapshot = new { existingCustomer.CustomerName, existingCustomer.Email, existingCustomer.Phone, existingCustomer.Status };

        existingCustomer.CustomerName = customer.CustomerName;
        existingCustomer.Email = customer.Email.Trim().ToLower();
        existingCustomer.Phone = customer.Phone.Trim();
        existingCustomer.CompanyName = customer.CompanyName;
        existingCustomer.Address = customer.Address;
        existingCustomer.City = customer.City;
        existingCustomer.State = customer.State;
        existingCustomer.Status = customer.Status;
        existingCustomer.OwnerId = customer.OwnerId;
        existingCustomer.ModifiedDate = DateTime.UtcNow;

        _context.Update(existingCustomer);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Update", "Customer", id.ToString(),
            oldSnapshot,
            new { existingCustomer.CustomerName, existingCustomer.Email, existingCustomer.Phone, existingCustomer.Status },
            "Success",
            $"Updated Customer {existingCustomer.CustomerName} ({existingCustomer.CustomerCode})");

        TempData["SuccessMessage"] = $"Customer '{existingCustomer.CustomerName}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null) return NotFound();

        // Scope check
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
        {
            return Forbid();
        }

        var customerInfo = $"{customer.CustomerName} ({customer.CustomerCode})";

        // Deactivate or remove
        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync("Delete", "Customer", id.ToString(),
            new { customer.CustomerName, customer.CustomerCode },
            null,
            "Success",
            $"Deleted Customer {customerInfo}");

        TempData["SuccessMessage"] = $"Customer '{customerInfo}' was deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateUsersDropdownAsync(string? selectedId = null)
    {
        var users = await _userManager.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, DisplayName = $"{u.FullName} ({u.UserRole})" })
            .ToListAsync();

        ViewBag.Users = new SelectList(users, "Id", "DisplayName", selectedId);
    }

    private async Task<string> GenerateCustomerCodeAsync()
    {
        var lastId = await _context.Customers.MaxAsync(c => (int?)c.CustomerId) ?? 1000;
        return $"CUST-{lastId + 1}";
    }
}

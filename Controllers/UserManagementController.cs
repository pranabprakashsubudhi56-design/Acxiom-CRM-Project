using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin,Manager")]
public class UserManagementController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IAuditService _auditService;

    public UserManagementController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IAuditService auditService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(string? search, string? role)
    {
        IQueryable<ApplicationUser> query = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(search) || u.Email!.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.UserRole == role);
        }

        var users = await query.OrderBy(u => u.FullName).ToListAsync();
        var model = new List<UserListViewModel>();

        foreach (var user in users)
        {
            bool isLockedOut = await _userManager.IsLockedOutAsync(user);
            model.Add(new UserListViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Role = user.UserRole,
                IsActive = user.IsActive,
                IsLockedOut = isLockedOut,
                CreatedDate = user.CreatedDate
            });
        }

        ViewBag.Search = search;
        ViewBag.Role = role;
        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        return View(new CreateUserViewModel());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "A user with this email already exists.");
            return View(model);
        }

        var validRoles = new[] { "Admin", "Manager", "SalesExecutive" };
        var assignedRole = validRoles.Contains(model.Role) ? model.Role : "SalesExecutive";

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            UserRole = assignedRole,
            IsActive = true,
            EmailConfirmed = true,
            CreatedDate = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            if (!await _roleManager.RoleExistsAsync(assignedRole))
            {
                await _roleManager.CreateAsync(new IdentityRole(assignedRole));
            }
            await _userManager.AddToRoleAsync(user, assignedRole);

            await _auditService.LogAsync("Create", "User", user.Id,
                null,
                new { user.Email, user.FullName, Role = assignedRole },
                "Success",
                $"Admin created user '{user.FullName}' with role '{assignedRole}'");

            TempData["SuccessMessage"] = $"User '{user.FullName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        return View(new EditUserViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Role = user.UserRole,
            IsActive = user.IsActive
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null) return NotFound();

        var oldSnapshot = new { user.FullName, user.UserRole, user.IsActive };

        user.FullName = model.FullName;
        user.IsActive = model.IsActive;

        // Role update
        if (user.UserRole != model.Role)
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            if (!await _roleManager.RoleExistsAsync(model.Role))
            {
                await _roleManager.CreateAsync(new IdentityRole(model.Role));
            }
            await _userManager.AddToRoleAsync(user, model.Role);
            user.UserRole = model.Role;

            await _auditService.LogAsync("Role Change", "User", user.Id,
                new { OldRole = oldSnapshot.UserRole },
                new { NewRole = model.Role },
                "Success",
                $"Role of user '{user.Email}' changed to '{model.Role}'");
        }

        // Optional password reset
        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
            if (!resetResult.Succeeded)
            {
                foreach (var err in resetResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, err.Description);
                }
                return View(model);
            }

            await _auditService.LogAsync("Security", "User", user.Id,
                null, null, "Success",
                $"Password reset performed for user '{user.Email}'");
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (updateResult.Succeeded)
        {
            await _auditService.LogAsync("Update", "User", user.Id,
                oldSnapshot,
                new { user.FullName, user.UserRole, user.IsActive },
                "Success",
                $"Updated user profile for '{user.Email}'");

            TempData["SuccessMessage"] = $"User '{user.FullName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var error in updateResult.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLockout(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        bool isCurrentlyLocked = await _userManager.IsLockedOutAsync(user);

        if (isCurrentlyLocked)
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);

            await _auditService.LogAsync("Security", "User", user.Id,
                new { Lockout = "Locked" },
                new { Lockout = "Unlocked" },
                "Success",
                $"Unlocked account for '{user.Email}'");

            TempData["SuccessMessage"] = $"Account '{user.Email}' has been unlocked.";
        }
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));

            await _auditService.LogAsync("Security", "User", user.Id,
                new { Lockout = "Active" },
                new { Lockout = "Locked" },
                "Success",
                $"Manually locked out account for '{user.Email}'");

            TempData["SuccessMessage"] = $"Account '{user.Email}' has been locked.";
        }

        return RedirectToAction(nameof(Index));
    }
}

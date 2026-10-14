using AcxiomCRM.Data;
using AcxiomCRM.Models;
using System.Security.Claims;
using System.Text.Json;

namespace AcxiomCRM.Services;

public interface IAuditService
{
    Task LogAsync(
        string action,
        string entityName,
        string? recordId = null,
        object? oldValue = null,
        object? newValue = null,
        string result = "Success",
        string? details = null);
}

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        string action,
        string entityName,
        string? recordId = null,
        object? oldValue = null,
        object? newValue = null,
        string result = "Success",
        string? details = null)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var userId = httpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = httpContext?.User?.Identity?.Name ?? "System";
            var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();

            string? oldValJson = oldValue switch
            {
                null => null,
                string s => s,
                _ => JsonSerializer.Serialize(oldValue)
            };

            string? newValJson = newValue switch
            {
                null => null,
                string s => s,
                _ => JsonSerializer.Serialize(newValue)
            };

            var auditLog = new AuditLog
            {
                UserId = userId,
                UserName = userName,
                Action = action,
                EntityName = entityName,
                RecordId = recordId,
                OldValue = oldValJson,
                NewValue = newValJson,
                Result = result,
                Details = details,
                IpAddress = ipAddress,
                CreatedDate = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }
        catch
        {
            // Fail-safe to avoid breaking application workflows if audit logging fails
        }
    }
}

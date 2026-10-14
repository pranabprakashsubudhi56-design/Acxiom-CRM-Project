using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class AuditLog
{
    [Key]
    public long AuditLogId { get; set; }

    [StringLength(450)]
    public string? UserId { get; set; }

    [StringLength(256)]
    [Display(Name = "User Name")]
    public string? UserName { get; set; }

    [Required]
    [StringLength(100)]
    public string Action { get; set; } = string.Empty; // Login, Failed Login, Logout, Create, Update, Delete, Convert, Role Change, Security

    [Required]
    [StringLength(100)]
    [Display(Name = "Entity / Module")]
    public string EntityName { get; set; } = string.Empty; // Customer, Lead, Opportunity, FollowUp, User, Auth, System

    [StringLength(100)]
    [Display(Name = "Record ID")]
    public string? RecordId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    [Display(Name = "Timestamp")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    [Display(Name = "IP Address")]
    public string? IpAddress { get; set; }

    [StringLength(50)]
    public string Result { get; set; } = "Success"; // Success, Failed, Warning

    [StringLength(2000)]
    public string? Details { get; set; }
}

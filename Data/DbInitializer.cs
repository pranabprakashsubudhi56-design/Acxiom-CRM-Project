using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Ensure database is created with schema
        await context.Database.EnsureCreatedAsync();

        // 1. Seed Roles
        string[] roles = new[] { "Admin", "Manager", "SalesExecutive" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // 2. Seed Users
        var usersToSeed = new[]
        {
            new
            {
                Email = "admin@acxiomcrm.com",
                UserName = "admin@acxiomcrm.com",
                FullName = "System Administrator",
                Role = "Admin",
                Password = "Admin@123"
            },
            new
            {
                Email = "manager@acxiomcrm.com",
                UserName = "manager@acxiomcrm.com",
                FullName = "Morgan Taylor",
                Role = "Manager",
                Password = "Manager@123"
            },
            new
            {
                Email = "sales@acxiomcrm.com",
                UserName = "sales@acxiomcrm.com",
                FullName = "Alex Mercer",
                Role = "SalesExecutive",
                Password = "Sales@123"
            },
            new
            {
                Email = "sarah@acxiomcrm.com",
                UserName = "sarah@acxiomcrm.com",
                FullName = "Sarah Connor",
                Role = "SalesExecutive",
                Password = "Sales@123"
            }
        };

        var createdUsers = new Dictionary<string, ApplicationUser>();

        foreach (var u in usersToSeed)
        {
            var existingUser = await userManager.FindByEmailAsync(u.Email);
            if (existingUser == null)
            {
                var user = new ApplicationUser
                {
                    UserName = u.UserName,
                    Email = u.Email,
                    FullName = u.FullName,
                    UserRole = u.Role,
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(user, u.Password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, u.Role);
                    createdUsers[u.Role] = user;
                }
            }
            else
            {
                createdUsers[u.Role] = existingUser;
            }
        }

        // 3. Seed Sample CRM Data if empty
        if (!context.Customers.Any())
        {
            var alexUser = createdUsers.ContainsKey("SalesExecutive") ? createdUsers["SalesExecutive"] : null;
            var sarahUser = await userManager.FindByEmailAsync("sarah@acxiomcrm.com");

            var c1 = new Customer
            {
                CustomerCode = "CUST-1001",
                CustomerName = "Nexus Technologies Inc",
                Email = "contact@nexustech.io",
                Phone = "9876543210",
                CompanyName = "Nexus Technologies",
                Address = "Plot 42, Silicon Valley Park",
                City = "Bengaluru",
                State = "Karnataka",
                Status = "Active",
                CreatedDate = DateTime.UtcNow.AddDays(-60),
                CreatedBy = "System",
                OwnerId = alexUser?.Id
            };

            var c2 = new Customer
            {
                CustomerCode = "CUST-1002",
                CustomerName = "Apex Logistics Ltd",
                Email = "procurement@apexlogistics.com",
                Phone = "9876543211",
                CompanyName = "Apex Logistics",
                Address = "Harbor Tower 5th Floor",
                City = "Mumbai",
                State = "Maharashtra",
                Status = "Active",
                CreatedDate = DateTime.UtcNow.AddDays(-45),
                CreatedBy = "System",
                OwnerId = sarahUser?.Id
            };

            var c3 = new Customer
            {
                CustomerCode = "CUST-1003",
                CustomerName = "Horizon Healthcare Systems",
                Email = "info@horizonhealth.org",
                Phone = "9876543212",
                CompanyName = "Horizon Healthcare",
                Address = "Medical Enclave Sector 18",
                City = "Gurugram",
                State = "Haryana",
                Status = "Active",
                CreatedDate = DateTime.UtcNow.AddDays(-20),
                CreatedBy = "System",
                OwnerId = alexUser?.Id
            };

            context.Customers.AddRange(c1, c2, c3);
            await context.SaveChangesAsync();

            // Seed Leads
            var l1 = new Lead
            {
                LeadCode = "LEAD-1001",
                LeadName = "Fintech Horizon Global",
                Email = "partner@fintechhorizon.com",
                Phone = "9876543213",
                CompanyName = "Fintech Horizon",
                Source = "Website",
                Status = "Qualified",
                Priority = "High",
                ExpectedValue = 45000.00m,
                CreatedDate = DateTime.UtcNow.AddDays(-15),
                AssignedTo = alexUser?.Id
            };

            var l2 = new Lead
            {
                LeadCode = "LEAD-1002",
                LeadName = "CyberSafe Cloud Labs",
                Email = "dev@cybersafelabs.io",
                Phone = "9876543214",
                CompanyName = "CyberSafe Labs",
                Source = "Referral",
                Status = "New",
                Priority = "Medium",
                ExpectedValue = 28000.00m,
                CreatedDate = DateTime.UtcNow.AddDays(-10),
                AssignedTo = sarahUser?.Id
            };

            var l3 = new Lead
            {
                LeadCode = "LEAD-1003",
                LeadName = "Quantum Retail Group",
                Email = "buying@quantumretail.in",
                Phone = "9876543215",
                CompanyName = "Quantum Retail",
                Source = "Cold Call",
                Status = "Contacted",
                Priority = "Low",
                ExpectedValue = 15000.00m,
                CreatedDate = DateTime.UtcNow.AddDays(-5),
                AssignedTo = alexUser?.Id
            };

            var l4 = new Lead
            {
                LeadCode = "LEAD-1004",
                LeadName = "Starlight Media",
                Email = "biz@starlightmedia.com",
                Phone = "9876543216",
                CompanyName = "Starlight Media",
                Source = "Advertisement",
                Status = "Converted",
                Priority = "High",
                ExpectedValue = 60000.00m,
                CreatedDate = DateTime.UtcNow.AddDays(-25),
                AssignedTo = sarahUser?.Id,
                CustomerId = c1.CustomerId
            };

            context.Leads.AddRange(l1, l2, l3, l4);
            await context.SaveChangesAsync();

            // Seed Opportunities
            var o1 = new Opportunity
            {
                OpportunityName = "Nexus Cloud Migration Enterprise Suite",
                CustomerId = c1.CustomerId,
                Amount = 75000.00m,
                Stage = "Proposal",
                Probability = 70,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(20),
                Status = "Open",
                CreatedDate = DateTime.UtcNow.AddDays(-14),
                AssignedTo = alexUser?.Id,
                Notes = "Client requested detailed SLA and migration milestones."
            };

            var o2 = new Opportunity
            {
                OpportunityName = "Apex Fleet GPS Integration",
                CustomerId = c2.CustomerId,
                Amount = 35000.00m,
                Stage = "Negotiation",
                Probability = 85,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(10),
                Status = "Open",
                CreatedDate = DateTime.UtcNow.AddDays(-18),
                AssignedTo = sarahUser?.Id,
                Notes = "Final contract revision under review with legal team."
            };

            var o3 = new Opportunity
            {
                OpportunityName = "Horizon Patient Portal Modernization",
                CustomerId = c3.CustomerId,
                Amount = 120000.00m,
                Stage = "Won",
                Probability = 100,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(-2),
                Status = "Won",
                CreatedDate = DateTime.UtcNow.AddDays(-30),
                AssignedTo = alexUser?.Id,
                Notes = "Deal closed successfully! Implementation scheduled."
            };

            var o4 = new Opportunity
            {
                OpportunityName = "Legacy POS Migration",
                CustomerId = c2.CustomerId,
                Amount = 20000.00m,
                Stage = "Lost",
                Probability = 0,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(-10),
                Status = "Lost",
                CreatedDate = DateTime.UtcNow.AddDays(-40),
                AssignedTo = sarahUser?.Id,
                Notes = "Prospect postponed budget to next fiscal year."
            };

            context.Opportunities.AddRange(o1, o2, o3, o4);
            await context.SaveChangesAsync();

            // Seed Follow-Ups
            var f1 = new FollowUp
            {
                CustomerId = c1.CustomerId,
                OpportunityId = o1.OpportunityId,
                FollowUpDate = DateTime.UtcNow.AddDays(2),
                FollowUpType = "Meeting",
                Subject = "Discuss Cloud Architecture & Security Proposal",
                Remarks = "Prepare architectural topology diagram for CTO.",
                Status = "Planned",
                AssignedTo = alexUser?.Id,
                CreatedDate = DateTime.UtcNow.AddDays(-2)
            };

            var f2 = new FollowUp
            {
                CustomerId = c2.CustomerId,
                OpportunityId = o2.OpportunityId,
                FollowUpDate = DateTime.UtcNow.AddHours(5),
                FollowUpType = "Call",
                Subject = "Follow up on Legal contract review",
                Remarks = "Check if procurement head has signed off.",
                Status = "Planned",
                AssignedTo = sarahUser?.Id,
                CreatedDate = DateTime.UtcNow.AddDays(-1)
            };

            var f3 = new FollowUp
            {
                LeadId = l2.LeadId,
                FollowUpDate = DateTime.UtcNow.AddDays(-1),
                FollowUpType = "Email",
                Subject = "Introductory Product Demo Link",
                Remarks = "Sent demo recording and overview deck.",
                Status = "Completed",
                AssignedTo = sarahUser?.Id,
                CreatedDate = DateTime.UtcNow.AddDays(-3)
            };

            context.FollowUps.AddRange(f1, f2, f3);
            await context.SaveChangesAsync();

            // Seed Activities
            var a1 = new Activity
            {
                ActivityType = "Meeting",
                Subject = "Discovery call with Nexus IT Director",
                Description = "Discussed infrastructure requirements and timeline.",
                ActivityDate = DateTime.UtcNow.AddDays(-12),
                CustomerId = c1.CustomerId,
                AssignedTo = alexUser?.Id,
                Status = "Completed"
            };

            var a2 = new Activity
            {
                ActivityType = "Call",
                Subject = "Contract negotiation sync with Apex",
                Description = "Agreed on commercial terms and billing cycles.",
                ActivityDate = DateTime.UtcNow.AddDays(-3),
                CustomerId = c2.CustomerId,
                AssignedTo = sarahUser?.Id,
                Status = "Completed"
            };

            context.Activities.AddRange(a1, a2);
            await context.SaveChangesAsync();

            // Seed Audit Logs
            var auditEntries = new[]
            {
                new AuditLog
                {
                    UserId = "System",
                    UserName = "System Administrator",
                    Action = "Security",
                    EntityName = "Auth",
                    Details = "Initial system roles and administrator accounts seeded successfully.",
                    Result = "Success",
                    CreatedDate = DateTime.UtcNow.AddDays(-60),
                    IpAddress = "127.0.0.1"
                },
                new AuditLog
                {
                    UserId = alexUser?.Id,
                    UserName = alexUser?.UserName ?? "alex@acxiomcrm.com",
                    Action = "Create",
                    EntityName = "Customer",
                    RecordId = c1.CustomerId.ToString(),
                    Details = "Created customer Nexus Technologies Inc (CUST-1001)",
                    Result = "Success",
                    CreatedDate = DateTime.UtcNow.AddDays(-60),
                    IpAddress = "127.0.0.1"
                },
                new AuditLog
                {
                    UserId = sarahUser?.Id,
                    UserName = sarahUser?.UserName ?? "sarah@acxiomcrm.com",
                    Action = "Convert",
                    EntityName = "Lead",
                    RecordId = l4.LeadId.ToString(),
                    Details = "Converted Lead LEAD-1004 (Starlight Media) to Customer account.",
                    Result = "Success",
                    CreatedDate = DateTime.UtcNow.AddDays(-25),
                    IpAddress = "127.0.0.1"
                }
            };

            context.AuditLogs.AddRange(auditEntries);
            await context.SaveChangesAsync();
        }
    }
}

# AcxiomCRM - Enterprise Customer Relationship Management System

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-MVC%20%26%20WebAPI-purple)](https://learn.microsoft.com/aspnet/core)
[![EF Core](https://img.shields.io/badge/Entity%20Framework%20Core-SQLite-blue)](https://learn.microsoft.com/ef/core/)
[![Bootstrap](https://img.shields.io/badge/Bootstrap-5.3-7952B3?logo=bootstrap)](https://getbootstrap.com/)
[![Swagger](https://img.shields.io/badge/Swagger-OpenAPI%203.0-85EA2D?logo=swagger)](https://swagger.io/)

A modern, full-featured enterprise Customer Relationship Management (CRM) application built according to Acxiom CRM functional and technical specifications. It combines an ASP.NET Core MVC web application with integrated RESTful Web APIs, Entity Framework Core ORM, ASP.NET Core Identity with Role-Based Access Control (RBAC), and interactive analytics dashboards.

---

## 🚀 Key Modules & Features

### 1. 👥 Lead Management
- Capture leads with contact information, acquisition sources (Website, Referral, Cold Call, Advertisement), priorities, and estimated deal value.
- Automated status progression: `New` &rarr; `Contacted` &rarr; `Qualified` &rarr; `Lost` &rarr; `Converted`.
- Seamless **Lead Conversion Workflow**: Instantly transforms qualified leads into formal Customer accounts and generates corresponding Opportunities while preserving complete history.

### 2. 💼 Opportunity & Sales Pipeline
- Track sales pipelines across predefined deal stages: `Discovery` &rarr; `Proposal` &rarr; `Negotiation` &rarr; `Won` &rarr; `Lost`.
- Automatic win probability calculation, expected revenue forecasting, and close date management.
- Dynamic pipeline status badges and visual health indicators.

### 3. 🏢 Customer 360 View
- Centralized customer profiles with full contact details, billing addresses, account status, and assigned sales executive.
- Consolidated Customer 360 overview linking active opportunities, past activities, and upcoming follow-ups.

### 4. 📅 Follow-Ups & Activity Tracker
- Schedule meetings, discovery calls, demos, and email reminders.
- Status tracking (`Planned`, `Completed`, `Cancelled`) with automated overdue warnings.
- Complete chronological audit log of all customer touchpoints.

### 5. 📊 Interactive Analytics & Dashboard
- Real-time KPI summaries: Total Active Customers, Open Pipeline Value, Converted Leads, and Pending Follow-Ups.
- Interactive Chart.js visualizations:
  - **Sales Pipeline by Stage** (Doughnut / Bar charts).
  - **Lead Status & Conversion Distribution**.
- Tabulated lists for high-priority leads, top open opportunities, and urgent pending follow-ups.

### 6. 🔒 Security & Role-Based Access Control (RBAC)
- Built on ASP.NET Core Identity with secure password hashing (PBKDF2) and account lockout policies.
- Three distinct authorization levels:
  - **Admin**: Full system access, user administration, security audit logs, and configuration.
  - **Manager**: Team oversight, cross-representative pipeline management, and performance reporting.
  - **Sales Executive**: Focus on assigned leads, accounts, opportunities, and daily action items.
- Full tamper-evident **Audit Log** recording user actions (`Create`, `Edit`, `Delete`, `Convert`, `Security`) with timestamps and client IP tracking.

### 7. ⚡ RESTful Web API & Swagger Documentation
- Fully exposed REST APIs under `/api/` for enterprise integration:
  - `/api/Customers`
  - `/api/Leads`
  - `/api/Opportunities`
  - `/api/FollowUps`
- Interactive Swagger UI documentation and live testing sandbox accessible at `/swagger`.

---

## 🔑 Demo Credentials (Pre-seeded)

The database automatically initializes and seeds standard accounts on first run:

| Role | Username / Email | Default Password | Access Scope |
| :--- | :--- | :--- | :--- |
| **System Admin** | `admin@acxiomcrm.com` | `Admin@123` | Full administrative control & audit logs |
| **Sales Manager** | `manager@acxiomcrm.com` | `Manager@123` | Pipeline oversight & team management |
| **Sales Executive** | `sales@acxiomcrm.com` | `Sales@123` | Lead & opportunity sales management |
| **Sales Executive** | `sarah@acxiomcrm.com` | `Sales@123` | Lead & opportunity sales management |

---

## 🛠️ Technology Stack

- **Framework**: .NET 8.0 / ASP.NET Core MVC & Web API
- **Data Access**: Entity Framework Core with SQLite (database agnostic via EF Core providers)
- **Authentication**: ASP.NET Core Identity with cookie authentication & session management
- **Frontend**: Razor Views, HTML5, CSS3, Bootstrap 5.3, Bootstrap Icons, Chart.js
- **API Documentation**: Swashbuckle OpenAPI / Swagger UI

---

## 💻 Getting Started

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher.
- Modern web browser (Chrome, Edge, Firefox).

### Installation & Run

1. **Clone the repository**:
   ```bash
   git clone <repo-url>
   cd "AcxiomCRM Project"
   ```

2. **Restore dependencies & Build**:
   ```bash
   dotnet restore
   dotnet build
   ```

3. **Run the application**:
   ```bash
   dotnet run
   ```

4. **Access the CRM**:
   - Open your browser and navigate to: **`http://localhost:5095`** (or `https://localhost:7198`)
   - Swagger REST API documentation: **`http://localhost:5095/swagger`**
   - Log in using any of the demo accounts listed above.

---

## 📂 Project Structure

```
AcxiomCRM/
├── Controllers/            # MVC Web Controllers (Dashboard, Customers, Leads, etc.)
│   └── Api/                # REST Web API Controllers (/api/*)
├── Data/                   # EF Core DbContext and automatic DbInitializer
├── DTOs/                   # Data Transfer Objects for API requests/responses
├── Models/                 # Domain entities (Customer, Lead, Opportunity, AuditLog, etc.)
├── Services/               # Core business services (AuditService, etc.)
├── ViewModels/             # Strongly-typed models for Razor views & dashboards
├── Views/                  # Razor Views styled with Bootstrap 5
│   ├── Account/            # Authentication & Profile management
│   ├── Admin/              # User management & System Audit Logs
│   ├── Customers/          # Customer CRUD & 360 profile
│   ├── Dashboard/          # Executive KPI summary & Chart.js visualizations
│   ├── FollowUps/          # Activity tracking & follow-up calendar
│   ├── Leads/              # Lead management & Conversion modal
│   ├── Opportunities/      # Pipeline tracking & Stage progression
│   └── Shared/             # Layouts, navigation, and partials
├── wwwroot/                # Static assets (Bootstrap, custom CSS, JS)
├── appsettings.json        # Database connection strings and app configuration
└── Program.cs              # DI container, middleware pipeline, and seeding entry
```

---

## 📄 License
This project was developed for the Acxiom CRM Enterprise requirements.

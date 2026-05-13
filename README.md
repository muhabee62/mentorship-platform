# Mentorship Platform — Full‑Stack Cloud Application

A cloud‑native mentorship and education support platform built with **Azure Functions (.NET Isolated)** and **React + TypeScript (Vite)**.  
Designed for secure onboarding, role‑based access, and scalable API architecture powered by **Entra External ID (CIAM)**.

---

##  Platform Overview

The **Mentorship Platform** is a comprehensive solution enabling organizations to facilitate meaningful mentor-mentee relationships at scale. It combines enterprise-grade security with an intuitive user experience, supporting three distinct roles: Admins, Mentors, and Mentees.

### Key Value Propositions
-  **Secure by Default** - Built on Microsoft Entra External ID (CIAM) with zero hardcoded secrets
-  **Scalable Architecture** - Serverless backend using Azure Functions for cost-effective scaling
-  **Role-Based Access Control** - Fine-grained permissions for Admins, Mentors, and Mentees
-  **Full-Stack TypeScript & C#** - Modern development stack with strong typing throughout
-  **Production-Ready** - Ready for deployment to Azure with CI/CD integration

---

##  Architecture Overview

This repository is a **monorepo** containing both the backend API and the frontend application.

```
mentorship-platform/
│
├── backend/                          # Azure Functions API (C#, .NET Isolated)
│   ├── Auth/                         # JWT validation, CIAM integration, role enforcement
│   ├── Endpoints/                    # Mentor, Mentee, Admin HTTP-triggered functions
│   │   ├── MentorEndpoints.cs
│   │   ├── MenteeEndpoints.cs
│   │   └── AdminEndpoints.cs
│   ├── Services/                     # Business logic and domain operations
│   │   ├── MentorService.cs
│   │   ├── MenteeService.cs
│   │   ├── SessionService.cs
│   │   └── NotificationService.cs
│   ├── Models/                       # Domain entities and database models
│   ├── Common/                       # Shared DTOs, helpers, constants
│   ├── local.settings.json           # (Git-ignored) Local environment config
│   ├── host.json                     # Azure Functions host configuration
│   └── .csproj                       # Backend project file
│
├── frontend/                         # React + TypeScript + Vite (16.4%)
│   ├── src/
│   │   ├── auth/                     # MSAL config, login flow, role guards
│   │   │   ├── msalConfig.ts
│   │   │   ├── useAuth.ts
│   │   │   └── ProtectedRoute.tsx
│   │   ├── layouts/                  # Dashboard layouts
│   │   │   ├── AdminDashboard.tsx
│   │   │   ├── MentorDashboard.tsx
│   │   │   └── MenteeDashboard.tsx
│   │   ├── pages/                    # UI screens
│   │   │   ├── Login.tsx
│   │   │   ├── Home.tsx
│   │   │   ├── Sessions.tsx
│   │   │   └── Profile.tsx
│   │   ├── components/               # Reusable UI components
│   │   │   ├── SessionCard.tsx
│   │   │   ├── MatchingWidget.tsx
│   │   │   └── ...
│   │   ├── services/                 # API client and utility functions
│   │   │   └── api.ts
│   │   ├── App.tsx
│   │   └── main.tsx
│   ├── public/                       # Static assets
│   ├── .env                          # (Git-ignored) MSAL & API configuration
│   ├── vite.config.ts
│   ├── tsconfig.json
│   └── package.json
│
├── .gitignore                        # Root-level ignore rules
├── README.md                         # This file
└── docs/                             # (Optional) Additional documentation

**Language Composition:**
- C# (Backend): 82.5%
- TypeScript (Frontend): 16.4%
- Other (Config, Tooling): 1.1%
```

---

##  Identity & Security

The platform uses **Microsoft Entra External ID (CIAM)** for authentication and authorization, providing enterprise-grade security with minimal infrastructure overhead.

### Security Features
- **User Flows** for seamless sign‑up/sign‑in with Entra ID
- **Custom Roles**: Admin, Mentor, Mentee with granular permissions
- **Role‑Based Routing** in the frontend (protected routes)
- **Role‑Based Authorization** in the backend (HTTP trigger guards)
- **JWT Validation** using Microsoft identity platform
- **Zero Secrets Committed** - GitHub push protection enabled
- **Encrypted Credentials** - All sensitive data stored in Azure Key Vault (recommended)

### Token Flow
```
User Login → Azure Entra ID → MSAL (Browser) → JWT Token → API Request → Backend JWT Validation → Role Check → Response
```

---

##  Tech Stack

### Backend (C# - 82.5%)
- **Azure Functions** (Isolated .NET 8.0+)
- **Azure SQL Database** (relational data storage)
- **Entra External ID (CIAM)** - Identity provider
- **MSAL.NET** - Token validation and Azure AD integration
- **Dependency Injection** - Built-in .NET DI container
- **Clean Architecture** - Separation of concerns (Endpoints → Services → Models)

### Frontend (TypeScript - 16.4%)
- **React 18+** with TypeScript
- **Vite** - Lightning-fast dev server and build tool
- **MSAL Browser** - Azure Entra authentication in the browser
- **React Router v7** - SPA routing and protected routes
- **TailwindCSS** (optional) - Utility-first CSS framework
- **Axios** (optional) - HTTP client for API calls

### Infrastructure & DevOps
- **Azure Static Web Apps** - Frontend hosting with automatic deployment
- **Azure Functions Consumption Plan** - Serverless backend
- **Azure SQL** - Managed relational database
- **GitHub Actions** - CI/CD automation (ready to configure)
- **GitHub Push Protection** - Secret scanning

---

##  Quick Start

### Prerequisites

**Backend:**
- .NET 8.0 or higher
- Azure Functions Core Tools (`func` CLI)
- Azure CLI (for deployment)
- Visual Studio Code or Visual Studio

**Frontend:**
- Node.js 18.x or higher
- npm or yarn package manager

**Accounts:**
- Azure subscription (free tier available for development)
- Microsoft Entra (Azure AD) tenant

### Backend Setup

1. **Navigate to backend directory:**
   ```bash
   cd backend
   ```

2. **Create local configuration file:**
   ```bash
   # Copy the template (create if not exists)
   cp local.settings.example.json local.settings.json
   ```

3. **Configure `local.settings.json`** (Git-ignored):
   ```json
   {
     "IsEncrypted": false,
     "Values": {
       "AzureWebJobsStorage": "UseDevelopmentStorage=true",
       "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
       "EntraClientId": "YOUR_ENTRA_CLIENT_ID",
       "EntraClientSecret": "YOUR_ENTRA_CLIENT_SECRET",
       "EntraTenantId": "YOUR_ENTRA_TENANT_ID",
       "SqlConnectionString": "Server=your-server.database.windows.net;Database=mentorship;User Id=admin;Password=***;"
     }
   }
   ```

4. **Restore dependencies and start the function runtime:**
   ```bash
   dotnet restore
   func start
   ```

   The backend will be available at `http://localhost:7071`.

### Frontend Setup

1. **Navigate to frontend directory:**
   ```bash
   cd frontend
   ```

2. **Install dependencies:**
   ```bash
   npm install
   ```

3. **Create environment configuration** (Git-ignored):
   ```bash
   # Create .env file in frontend root
   cat > .env << EOF
   VITE_MSAL_CLIENT_ID=YOUR_MSAL_CLIENT_ID
   VITE_MSAL_AUTHORITY=https://login.microsoftonline.com/YOUR_TENANT_ID
   VITE_MSAL_SCOPES=api://YOUR_API_SCOPE/.default
   VITE_API_URL=http://localhost:7071
   EOF
   ```

4. **Start the development server:**
   ```bash
   npm run dev
   ```

   The frontend will be available at `http://localhost:5173`.

---

##  Core Features

### For Mentees
-  Browse available mentors and their expertise
-  Request and schedule mentoring sessions
-  Track session history and take notes
-  View progress and learning outcomes
-  Receive notifications about session updates

### For Mentors
-  Manage mentee relationships
-  Set availability and session preferences
-  Schedule and track sessions
-  Leave feedback and guidance notes
-  View mentee progress insights

### For Admins
-  User and role management
-  Analytics and reporting dashboard
-  Mentor-mentee matching algorithms (future)
-  Platform configuration and settings
-  System notifications and announcements

---

##  Database Schema

The platform uses Azure SQL with the following key entities:

```
Users
├── UserId (PK)
├── Email (unique)
├── DisplayName
├── Role (enum: Admin, Mentor, Mentee)
├── EntraId (external Azure AD identifier)
└── CreatedAt

Mentors (subset of Users)
├── MentorId (FK to Users)
├── ExpertiseAreas (JSON or related table)
├── Bio
└── Availability

Mentees (subset of Users)
├── MenteeId (FK to Users)
├── LearningGoals (JSON)
└── PreferredMentorSkills

Sessions
├── SessionId (PK)
├── MentorId (FK)
├── MenteeId (FK)
├── ScheduledAt (DateTime)
├── Duration (minutes)
├── Status (scheduled, completed, cancelled)
├── Notes
└── CreatedAt

Feedback
├── FeedbackId (PK)
├── SessionId (FK)
├── FromUserId (FK)
├── ToUserId (FK)
├── Rating (1-5)
├── Comments
└── CreatedAt
```

---

##  Deployment

### Deploy Backend to Azure Functions

```bash
# Login to Azure
az login

# Create resource group
az group create --name mentorship-rg --location eastus

# Deploy using Azure Functions Core Tools
func azure functionapp publish mentorship-api --build remote

# Or use Azure CLI
az functionapp create \
  --resource-group mentorship-rg \
  --consumption-plan-location eastus \
  --runtime dotnet-isolated \
  --runtime-version 8.0 \
  --functions-version 4 \
  --name mentorship-api
```

### Deploy Frontend to Azure Static Web Apps

```bash
# Build the frontend
cd frontend
npm run build

# Deploy using Azure Static Web Apps CLI
swa deploy ./dist --deployment-token YOUR_TOKEN
```

### CI/CD with GitHub Actions

Create `.github/workflows/deploy.yml`:

```yaml
name: Deploy to Azure

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0'
      
      - name: Deploy Backend
        run: |
          cd backend
          func azure functionapp publish mentorship-api --build remote
      
      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: '18'
      
      - name: Deploy Frontend
        run: |
          cd frontend
          npm install
          npm run build
          swa deploy ./dist --deployment-token ${{ secrets.SWA_TOKEN }}
```

---

##  API Endpoints

All endpoints require valid JWT token with appropriate role claims.

### Authentication
- `POST /api/auth/login` - Initiate login flow (handled by MSAL)
- `POST /api/auth/refresh` - Refresh JWT token

### Mentor Endpoints
- `GET /api/mentors` - List all mentors
- `GET /api/mentors/{id}` - Get mentor details
- `PUT /api/mentors/{id}` - Update mentor profile
- `GET /api/mentors/{id}/availability` - Get availability slots
- `POST /api/sessions` - Create a session

### Mentee Endpoints
- `GET /api/mentees/profile` - Get current mentee profile
- `PUT /api/mentees/profile` - Update profile
- `GET /api/mentees/sessions` - List mentee's sessions
- `POST /api/mentees/sessions/{id}/feedback` - Submit session feedback

### Admin Endpoints
- `GET /api/admin/users` - List all users
- `PUT /api/admin/users/{id}/role` - Update user role
- `DELETE /api/admin/users/{id}` - Deactivate user
- `GET /api/admin/analytics` - Platform analytics
- `GET /api/admin/sessions` - View all sessions

---

##  Troubleshooting

### Backend Issues

**Issue: `Azure Functions Core Tools not found`**
```bash
# Install Azure Functions Core Tools
# macOS
brew tap azure/tap && brew install azure-functions-core-tools@4

# Windows
choco install azure-functions-core-tools
```

**Issue: `Cannot connect to local SQL database`**
- Verify connection string in `local.settings.json`
- Ensure SQL Server is running locally or Azure SQL is accessible
- Check firewall rules for Azure SQL

**Issue: `JWT validation fails`**
- Verify `EntraClientId` and `EntraTenantId` in `local.settings.json`
- Ensure token hasn't expired
- Check role claims in token payload

### Frontend Issues

**Issue: `MSAL login redirects to blank page`**
- Verify `VITE_MSAL_CLIENT_ID` and `VITE_MSAL_AUTHORITY` in `.env`
- Check Azure Entra app registration redirect URI matches `http://localhost:5173`
- Clear browser cache and localStorage

**Issue: `API calls return 401 Unauthorized`**
- Ensure JWT token is being sent in `Authorization: Bearer` header
- Check token expiration time
- Verify user has required role

---

##  Environment Variables Reference

### Backend (`local.settings.json`)
| Variable | Purpose | Example |
|----------|---------|---------|
| `EntraClientId` | Azure Entra app ID | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| `EntraClientSecret` | Entra app secret | `abc123~def456...` |
| `EntraTenantId` | Azure Entra tenant ID | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| `SqlConnectionString` | Azure SQL connection | `Server=...;Database=...` |
| `AzureWebJobsStorage` | Functions storage | `UseDevelopmentStorage=true` |

### Frontend (`.env`)
| Variable | Purpose | Example |
|----------|---------|---------|
| `VITE_MSAL_CLIENT_ID` | MSAL app ID | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| `VITE_MSAL_AUTHORITY` | Entra authority URL | `https://login.microsoftonline.com/tenant-id` |
| `VITE_MSAL_SCOPES` | API scopes | `api://your-api/.default` |
| `VITE_API_URL` | Backend API URL | `http://localhost:7071` |

---

##  Contributing

We welcome contributions! Please follow these guidelines:

### Branch Naming Convention
- Feature: `feature/description-of-feature`
- Bug fix: `bugfix/description-of-bug`
- Hotfix: `hotfix/critical-issue`

### Commit Message Format
```
<type>(<scope>): <subject>

<body>

<footer>
```

Example:
```
feat(sessions): add session cancellation with notification

- Implement cancellation endpoint
- Add email notification to both mentor and mentee
- Update session status to 'cancelled'

Closes #123
```

### Pull Request Process
1. Create a feature branch from `main`
2. Make your changes with descriptive commit messages
3. Ensure code compiles and tests pass
4. Submit PR with clear description of changes
5. Address review feedback
6. Merge once approved

### Code Style
- **C#**: Follow [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- **TypeScript**: Use ESLint configuration (run `npm run lint`)
- Both: Keep functions small and focused; aim for < 50 lines per function

---

##  Additional Resources

- [Azure Functions Documentation](https://learn.microsoft.com/en-us/azure/azure-functions/)
- [Microsoft Entra External ID Docs](https://learn.microsoft.com/en-us/entra/external-id/customers/overview-customers-ciam)
- [React + TypeScript Best Practices](https://react-typescript-cheatsheet.netlify.app/)
- [Azure SQL Security](https://learn.microsoft.com/en-us/azure/azure-sql/database/security-overview)
- [Vite Documentation](https://vitejs.dev/)

---

##  Security Notes

- **Secrets Management**: Use Azure Key Vault for production secrets, never commit to Git
- **HTTPS Only**: Always use HTTPS in production; HTTP only for local development
- **CORS Configuration**: Configure CORS in Azure Functions to allow only trusted origins
- **Rate Limiting**: Implement rate limiting on API endpoints (recommended for production)
- **Data Privacy**: Ensure GDPR/data privacy compliance for user data storage and deletion
- **Audit Logging**: Enable Azure SQL Auditing and Application Insights monitoring

---

##  Repository Notes

- Secrets are **never** committed (GitHub push protection enabled)
- `local.settings.json` and `.env` are intentionally ignored by Git
- Backend and frontend each have their own `.gitignore` files
- Root `.gitignore` provides shared ignore rules (node_modules, bin, obj, etc.)
- All configuration must be supplied at runtime via environment variables

---

##  Roadmap & Future Improvements

-  Core platform architecture
-  Authentication & authorization
-  Automated mentor-mentee matching algorithm
-  Admin analytics dashboard with charts and KPIs
-  Notification system (email, SMS, in-app)
-  Video conferencing integration (Teams/Zoom)
-  Mobile app (React Native)
-  AI-powered session recommendations
-  Automated testing suite (unit, integration, E2E)

---

##  Support & Questions

For issues, questions, or feature requests:
- Open a [GitHub Issue](https://github.com/muhabee62/mentorship-platform/issues)
- Check [existing discussions](https://github.com/muhabee62/mentorship-platform/discussions)
- Review [documentation](./docs) for additional guides

---

##  License

This project is open source. See the [LICENSE](LICENSE) file for details.

---

##  Author

**muhabee62**
- GitHub: [@muhabee62](https://github.com/muhabee62)
- Repository: [mentorship-platform](https://github.com/muhabee62/mentorship-platform)

---

**Built with love for mentorship communities**

 If you find this repository helpful, please consider giving it a star!

*Last Updated: May 2026*

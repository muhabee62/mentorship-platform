# Mentorship Platform — Full‑Stack Cloud Application

A cloud‑native mentorship and education support platform built with **Azure Functions (.NET Isolated)** and **React + TypeScript (Vite)**.  
Designed for secure onboarding, role‑based access, and scalable API architecture powered by **Entra External ID (CIAM)**.

---

##  Architecture Overview
```
This repository is a **monorepo** containing both the backend API and the frontend application.

mentorship-platform/
│
├── backend/        # Azure Functions API (C#, .NET Isolated)
│   ├── Auth        # JWT validation, CIAM integration, role enforcement
│   ├── Endpoints   # Mentor, Mentee, Admin HTTP-triggered functions
│   ├── Services    # Business logic and domain operations
│   ├── Common      # Shared models, DTOs, helpers
│   └── ...
│
└── frontend/       # React + TypeScript + Vite
├── src/
│   ├── auth/       # MSAL config, login, role guards
│   ├── layouts/    # Admin, Mentor, Mentee dashboards
│   ├── pages/      # UI screens
│   └── components/ # Shared UI elements
├── public/
└── ...
```

---

##  Identity & Security

The platform uses **Microsoft Entra External ID (CIAM)** for authentication and authorization.

### Features:
- **User flows** for sign‑up/sign‑in  
- **Custom roles**: Admin, Mentor, Mentee  
- **Role‑based routing** in the frontend  
- **Role‑based authorization** in the backend  
- **JWT validation** using Microsoft identity platform  
- **Zero secrets committed** (GitHub push protection enabled)

---

##  Tech Stack

### Backend
- Azure Functions (Isolated .NET)
- Azure SQL
- Entra External ID (CIAM)
- MSAL for token validation
- Dependency Injection
- Clean architecture principles

### Frontend
- React + TypeScript + Vite
- MSAL Browser
- React Router
- TailwindCSS (optional)
- Component‑driven UI

---

##  Local Development

### Backend
cd backend
func start


Requires a `local.settings.json` file (ignored by Git).

### Frontend
cd frontend
npm install
npm run dev


Requires a `.env` file with MSAL + API settings.

---

##  Deployment

### Backend
- Deployable to **Azure App Service** or **Azure Functions Consumption Plan**

### Frontend
- Deployable to **Azure Static Web Apps**  
  or  
- Azure Storage + CDN

### CI/CD
- GitHub Actions can be added for automated deployments

---

##  Project Purpose

This platform supports a real mentorship initiative, enabling:

- Mentor–mentee matching  
- Session scheduling and tracking  
- Secure onboarding  
- Admin oversight  
- Analytics dashboards  

Built as part of a long‑term cloud engineering and identity architecture journey.

---

##  Repository Notes

- Secrets are **never** committed (GitHub push protection enabled)
- `local.settings.json` and `.env` are intentionally ignored
- Backend and frontend each have their own `.gitignore` files
- Root `.gitignore` provides shared ignore rules

---

##  Contributions

This is an evolving project.  
Future improvements include:

- Automated CI/CD pipelines  
- Admin analytics dashboard  
- Mentor–mentee matching algorithm  
- Notification system (email/SMS)  

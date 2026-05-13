import { createBrowserRouter } from "react-router-dom";

import LandingPage from "../pages/LandingPage";
import LoginPage from "../pages/LoginPage";
import UnauthorizedPage from "../pages/UnauthorizedPage";

import AdminLayout from "../layouts/AdminLayout";
import MentorLayout from "../layouts/MentorLayout";
import MenteeLayout from "../layouts/MenteeLayout";

import AdminDashboard from "../pages/AdminDashboard";
import MentorDashboard from "../pages/MentorDashboard";
import MenteeDashboard from "../pages/MenteeDashboard";

import { RoleGuard } from "../components/RoleGuard";

import PublicLayout from "../layouts/PublicLayout";

export const router = createBrowserRouter([
    // Public routes
    {
        path: "/",
        element: <PublicLayout />,
        children: [
            { index: true, element: <LandingPage /> }
        ]
    },
    {
        path: "/login",
        element: <PublicLayout />,
        children: [
            { index: true, element: <LoginPage /> }
        ]
    },
    {
        path: "/unauthorized",
        element: <PublicLayout />,
        children: [
            { index: true, element: <UnauthorizedPage /> }
        ]
    },

    // Admin routes
    {
        path: "/admin",
        element: (
            <RoleGuard roles={["Admin"]}>
                <AdminLayout />
            </RoleGuard>
        ),
        children: [
            { index: true, element: <AdminDashboard /> },
            // Future admin pages:
            // { path: "users", element: <AdminUsersPage /> },
            // { path: "analytics", element: <AdminAnalyticsPage /> },
        ]
    },

    // Mentor routes
    {
        path: "/mentor",
        element: (
            <RoleGuard roles={["Mentor"]}>
                <MentorLayout />
            </RoleGuard>
        ),
        children: [
            { index: true, element: <MentorDashboard /> },
            // Future mentor pages:
            // { path: "sessions", element: <MentorSessionsPage /> },
            // { path: "resources", element: <MentorResourcesPage /> },
        ]
    },

    // Mentee routes
    {
        path: "/mentee",
        element: (
            <RoleGuard roles={["Mentee"]}>
                <MenteeLayout />
            </RoleGuard>
        ),
        children: [
            { index: true, element: <MenteeDashboard /> },
            // Future mentee pages:
            // { path: "goals", element: <MenteeGoalsPage /> },
            // { path: "sessions", element: <MenteeSessionsPage /> },
        ]
    }
]);

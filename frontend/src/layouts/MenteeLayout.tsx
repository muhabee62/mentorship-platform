import Navbar from "../components/Navbar";
import { Sidebar } from "../components/Sidebar";
import { Outlet } from "react-router-dom";

export default function MenteeLayout() {
    const items = [
        { label: "Overview", to: "/mentee" },
        { label: "Goals", to: "/mentee/goals" },
        { label: "Sessions", to: "/mentee/sessions" }
    ];

    return (
        <div className="min-h-screen bg-gradient-to-br from-slate-950 via-slate-900 to-slate-950 text-slate-100 relative">

            {/* Background Glow Effects */}
            <div className="pointer-events-none absolute -left-40 top-20 h-96 w-96 rounded-full bg-fuchsia-500/25 blur-3xl" />
            <div className="pointer-events-none absolute -right-40 bottom-20 h-96 w-96 rounded-full bg-amber-400/20 blur-3xl" />
            <div className="pointer-events-none absolute left-1/2 top-1/2 h-72 w-72 -translate-x-1/2 -translate-y-1/2 rounded-full bg-pink-500/10 blur-3xl" />

            {/* Navbar */}
            <Navbar />

            {/* Main Layout */}
            <div className="mx-auto flex max-w-7xl gap-6 px-6 py-8">

                {/* Sidebar */}
                <Sidebar items={items} />

                {/* Main Content */}
                <main className="
                    flex-1
                    rounded-3xl
                    border border-white/10
                    bg-white/5
                    p-8
                    shadow-2xl shadow-fuchsia-500/20
                    backdrop-blur-2xl
                ">
                    <Outlet />
                </main>
            </div>
        </div>
    );
}

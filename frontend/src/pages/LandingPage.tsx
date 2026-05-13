import { useAuthContext } from "../auth/useAuth";

export default function LandingPage() {
    const { login, account } = useAuthContext();

    return (
        <div className="min-h-screen bg-gradient-to-br from-slate-950 via-slate-900 to-slate-950 text-slate-100 relative overflow-hidden">

            {/* Floating Glow Orbs */}
            <div className="pointer-events-none absolute -left-40 top-20 h-96 w-96 rounded-full bg-sky-500/30 blur-3xl" />
            <div className="pointer-events-none absolute -right-40 bottom-20 h-96 w-96 rounded-full bg-fuchsia-500/30 blur-3xl" />
            <div className="pointer-events-none absolute left-1/2 top-1/2 h-72 w-72 -translate-x-1/2 -translate-y-1/2 rounded-full bg-indigo-500/20 blur-3xl" />

            {/* Content Container */}
            <div className="relative z-10 mx-auto flex min-h-screen max-w-6xl flex-col px-6">

                {/* Header */}
                <header className="flex items-center justify-between py-6">
                    <div className="flex items-center gap-2">
                        <div className="h-10 w-10 rounded-2xl bg-gradient-to-tr from-indigo-500 via-sky-400 to-emerald-400 shadow-lg shadow-indigo-500/40" />
                        <span className="bg-gradient-to-r from-white via-slate-100 to-slate-300 bg-clip-text text-2xl font-semibold text-transparent">
                            Mentorship
                        </span>
                    </div>
                </header>

                {/* Hero Section */}
                <main className="flex flex-1 flex-col items-center justify-center text-center">

                    <h1 className="bg-gradient-to-r from-sky-400 via-indigo-400 to-fuchsia-400 bg-clip-text text-4xl font-bold tracking-tight text-transparent md:text-6xl">
                        Grow. Connect. Transform.
                    </h1>

                    <p className="mt-4 max-w-2xl text-base text-slate-200/70 md:text-lg">
                        A modern mentorship platform where Admins, Mentors, and Mentees collaborate in a secure, role‑based environment powered by Entra External ID.
                    </p>

                    {/* CTA Buttons */}
                    <div className="mt-10 flex flex-wrap items-center justify-center gap-4">

                        {!account && (
                            <button
                                onClick={login}
                                className="rounded-full bg-gradient-to-r from-sky-500 via-indigo-500 to-fuchsia-500 px-8 py-3 text-sm font-medium text-white shadow-xl shadow-sky-500/40 transition hover:brightness-110"
                            >
                                Login to Your Space
                            </button>
                        )}

                        {account && (
                            <div className="rounded-full border border-white/20 px-6 py-3 text-sm text-slate-100 backdrop-blur-lg">
                                You’re already logged in — use the navigation above.
                            </div>
                        )}
                    </div>
                </main>

                {/* Footer */}
                <footer className="py-6 text-center text-xs text-slate-400/60">
                    © {new Date().getFullYear()} Mentorship Platform — Built with passion.
                </footer>
            </div>
        </div>
    );
}

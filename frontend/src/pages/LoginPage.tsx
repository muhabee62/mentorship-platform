import { useAuthContext } from "../auth/useAuth";

export default function LoginPage() {
    const { login } = useAuthContext();

    return (
        <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-950 via-slate-900 to-slate-950 text-slate-100 relative overflow-hidden">

            {/* Background Glow Orbs */}
            <div className="pointer-events-none absolute -left-40 top-20 h-96 w-96 rounded-full bg-sky-500/30 blur-3xl" />
            <div className="pointer-events-none absolute -right-40 bottom-20 h-96 w-96 rounded-full bg-fuchsia-500/30 blur-3xl" />
            <div className="pointer-events-none absolute left-1/2 top-1/2 h-72 w-72 -translate-x-1/2 -translate-y-1/2 rounded-full bg-indigo-500/20 blur-3xl" />

            {/* Login Card */}
            <div className="relative z-10 w-full max-w-md rounded-3xl border border-white/10 bg-white/5 p-10 backdrop-blur-2xl shadow-2xl shadow-sky-500/20">

                <h1 className="bg-gradient-to-r from-sky-400 via-indigo-400 to-fuchsia-400 
                               bg-clip-text text-4xl font-bold text-transparent text-center">
                    Sign In
                </h1>

                <p className="mt-4 text-center text-sm text-slate-300/70">
                    Sign in using your email and one‑time password via CIAM.
                </p>

                <button
                    onClick={login}
                    className="mt-8 w-full rounded-full bg-gradient-to-r from-sky-500 via-indigo-500 to-fuchsia-500 
                               px-6 py-3 text-sm font-medium text-white shadow-lg shadow-sky-500/40 
                               transition hover:brightness-110"
                >
                    Login with CIAM
                </button>

                <p className="mt-6 text-center text-xs text-slate-400/60">
                    Secure authentication powered by Entra External ID.
                </p>
            </div>
        </div>
    );
}

import { Link } from "react-router-dom";

export default function UnauthorizedPage() {
    return (
        <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-950 via-slate-900 to-slate-950 text-slate-100 relative overflow-hidden">

            {/* Background Glow Effects */}
            <div className="pointer-events-none absolute -left-40 top-20 h-96 w-96 rounded-full bg-red-500/20 blur-3xl" />
            <div className="pointer-events-none absolute -right-40 bottom-20 h-96 w-96 rounded-full bg-fuchsia-500/20 blur-3xl" />

            {/* Content */}
            <div className="relative z-10 max-w-md w-full rounded-3xl border border-white/10 bg-white/5 p-10 backdrop-blur-2xl shadow-2xl shadow-red-500/20 text-center">

                <h1 className="bg-gradient-to-r from-red-400 via-pink-400 to-fuchsia-400 
                               bg-clip-text text-4xl font-bold text-transparent">
                    Unauthorized
                </h1>

                <p className="mt-4 text-sm text-slate-300/70">
                    You do not have permission to view this page.  
                    If you believe this is a mistake, please contact an administrator.
                </p>

                <Link
                    to="/"
                    className="mt-8 inline-block rounded-full bg-gradient-to-r from-sky-500 via-indigo-500 to-fuchsia-500 
                               px-6 py-2 text-sm font-medium text-white shadow-lg shadow-fuchsia-500/40 
                               transition hover:brightness-110"
                >
                    Return Home
                </Link>
            </div>
        </div>
    );
}

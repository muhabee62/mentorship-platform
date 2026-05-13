import { useState } from "react";
import { Link } from "react-router-dom";
import { useAuthContext } from "../auth/useAuth";

export default function Navbar() {
    const { account, roles, login, logout, loading } = useAuthContext();
    const [open, setOpen] = useState(false);
    const [menuOpen, setMenuOpen] = useState(false);

    // ⭐ Wait for MSAL to finish loading
    if (loading) {
        return null; // or a spinner
    }

    const role = roles[0] || null;

    const initials = account
        ? account.name?.[0]?.toUpperCase() || "U"
        : null;

    return (
        <header className="sticky top-0 z-40 border-b border-white/10 bg-white/10 backdrop-blur-xl">
            <div className="mx-auto flex max-w-7xl items-center justify-between px-6 py-4">

                <Link to="/" className="flex items-center gap-2">
                    <div className="h-9 w-9 rounded-2xl bg-gradient-to-tr from-indigo-500 via-sky-400 to-emerald-400 shadow-lg shadow-indigo-500/40" />
                    <span className="bg-gradient-to-r from-white via-slate-100 to-slate-300 bg-clip-text text-xl font-semibold text-transparent">
                        Mentorship
                    </span>
                </Link>

                <nav className="hidden md:flex items-center gap-6 text-sm text-slate-100/80">

                    <Link to="/" className="hover:text-white transition-colors">
                        Home
                    </Link>

                    {account && role === "Admin" && (
                        <Link to="/admin" className="hover:text-white transition-colors">
                            Admin
                        </Link>
                    )}

                    {account && role === "Mentor" && (
                        <Link to="/mentor" className="hover:text-white transition-colors">
                            Mentor
                        </Link>
                    )}

                    {account && role === "Mentee" && (
                        <Link to="/mentee" className="hover:text-white transition-colors">
                            Mentee
                        </Link>
                    )}

                    {!account ? (
                        <button
                            onClick={login}
                            className="rounded-full bg-white/10 px-5 py-2 text-xs font-medium text-white shadow-lg shadow-sky-500/30 backdrop-blur hover:bg-white/20 transition"
                        >
                            Login
                        </button>
                    ) : (
                        <div className="relative">
                            <button
                                onClick={() => setOpen(!open)}
                                className="flex items-center gap-2 rounded-full bg-white/10 px-3 py-1.5 text-xs font-medium text-white shadow-md hover:bg-white/20 transition"
                            >
                                <div className="h-7 w-7 flex items-center justify-center rounded-full bg-gradient-to-br from-sky-500 to-indigo-500 text-white font-semibold">
                                    {initials}
                                </div>
                                <span>{account.name}</span>
                            </button>

                            {open && (
                                <div className="absolute right-0 mt-3 w-48 rounded-xl bg-white/10 backdrop-blur-xl border border-white/10 shadow-xl p-3 text-sm">
                                    <p className="text-slate-200 font-medium">{account.name}</p>

                                    {role && (
                                        <p className="mt-1 inline-block rounded-full bg-sky-500/20 px-3 py-1 text-xs text-sky-300">
                                            {role}
                                        </p>
                                    )}

                                    <button
                                        onClick={logout}
                                        className="mt-4 w-full rounded-lg bg-white/10 px-4 py-2 text-left text-slate-100 hover:bg-white/20 transition"
                                    >
                                        Logout
                                    </button>
                                </div>
                            )}
                        </div>
                    )}
                </nav>

                <button
                    className="md:hidden text-white"
                    onClick={() => setMenuOpen(!menuOpen)}
                >
                    ☰
                </button>
            </div>

            {menuOpen && (
                <div className="md:hidden border-t border-white/10 bg-white/10 backdrop-blur-xl px-6 py-4 space-y-4 text-sm text-slate-100/80">

                    <Link to="/" className="block hover:text-white transition-colors">
                        Home
                    </Link>

                    {account && role === "Admin" && (
                        <Link to="/admin" className="block hover:text-white transition-colors">
                            Admin
                        </Link>
                    )}

                    {account && role === "Mentor" && (
                        <Link to="/mentor" className="block hover:text-white transition-colors">
                            Mentor
                        </Link>
                    )}

                    {account && role === "Mentee" && (
                        <Link to="/mentee" className="block hover:text-white transition-colors">
                            Mentee
                        </Link>
                    )}

                    {!account ? (
                        <button
                            onClick={login}
                            className="w-full rounded-full bg-white/10 px-5 py-2 text-xs font-medium text-white shadow-lg shadow-sky-500/30 backdrop-blur hover:bg-white/20 transition"
                        >
                            Login
                        </button>
                    ) : (
                        <button
                            onClick={logout}
                            className="w-full rounded-full border border-white/20 px-5 py-2 text-xs font-medium text-slate-100 hover:bg-white/10 transition"
                        >
                            Logout
                        </button>
                    )}
                </div>
            )}
        </header>
    );
}

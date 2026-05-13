import { useState } from "react";
import { useBackend } from "../api/backend";

export default function MentorDashboard() {
    const { callApi } = useBackend();
    const [loading, setLoading] = useState(false);
    const [result, setResult] = useState<string | null>(null);

    const test = async () => {
        try {
            setLoading(true);
            const response = await callApi("rbac/mentor");
            setResult(response);
        } catch {
            setResult("Error calling mentor endpoint");
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="space-y-8">

            {/* Header */}
            <div>
                <h1 className="bg-gradient-to-r from-emerald-400 via-teal-400 to-sky-400 
                               bg-clip-text text-3xl font-semibold text-transparent">
                    Mentor Dashboard
                </h1>
                <p className="mt-2 text-sm text-slate-300/70">
                    View your mentees, manage sessions, and track their progress.
                </p>
            </div>

            {/* Stats Grid */}
            <div className="grid gap-6 md:grid-cols-3">
                <div className="rounded-2xl border border-white/10 bg-white/5 p-6 
                                shadow-lg shadow-emerald-500/20 backdrop-blur-xl">
                    <div className="text-xs uppercase tracking-wide text-slate-300/70">
                        Active Mentees
                    </div>
                    <div className="mt-2 text-3xl font-semibold">7</div>
                </div>

                <div className="rounded-2xl border border-white/10 bg-white/5 p-6 
                                shadow-lg shadow-sky-500/20 backdrop-blur-xl">
                    <div className="text-xs uppercase tracking-wide text-slate-300/70">
                        Sessions This Week
                    </div>
                    <div className="mt-2 text-3xl font-semibold">3</div>
                </div>

                <div className="rounded-2xl border border-white/10 bg-white/5 p-6 
                                shadow-lg shadow-teal-500/20 backdrop-blur-xl">
                    <div className="text-xs uppercase tracking-wide text-slate-300/70">
                        Pending Reviews
                    </div>
                    <div className="mt-2 text-3xl font-semibold">2</div>
                </div>
            </div>

            {/* Test Mentor Endpoint */}
            <div className="rounded-3xl border border-white/10 bg-white/5 p-8 
                            shadow-xl shadow-emerald-500/20 backdrop-blur-2xl">

                <h2 className="text-lg font-semibold text-white">Test Mentor Endpoint</h2>
                <p className="mt-1 text-sm text-slate-300/70">
                    This verifies that your Mentor role is correctly recognized by the backend.
                </p>

                <button
                    onClick={test}
                    disabled={loading}
                    className="mt-6 rounded-full bg-gradient-to-r from-emerald-500 via-teal-500 to-sky-500 
                               px-6 py-2 text-sm font-medium text-white shadow-lg shadow-emerald-500/40 
                               transition hover:brightness-110 disabled:opacity-50"
                >
                    {loading ? "Testing..." : "Run Test"}
                </button>

                {result && (
                    <div className="mt-6 rounded-xl border border-white/10 bg-white/10 p-4 
                                    text-sm text-slate-100 backdrop-blur-xl shadow-lg shadow-emerald-500/20">
                        {result}
                    </div>
                )}
            </div>
        </div>
    );
}

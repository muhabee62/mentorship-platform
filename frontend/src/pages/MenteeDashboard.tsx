import { useState } from "react";
import { useBackend } from "../api/backend";

export default function MenteeDashboard() {
    const { callApi } = useBackend();
    const [loading, setLoading] = useState(false);
    const [result, setResult] = useState<string | null>(null);

    const test = async () => {
        try {
            setLoading(true);
            const response = await callApi("rbac/mentee");
            setResult(response);
        } catch {
            setResult("Error calling mentee endpoint");
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="space-y-8">

            {/* Header */}
            <div>
                <h1 className="bg-gradient-to-r from-fuchsia-400 via-pink-400 to-amber-300 
                               bg-clip-text text-3xl font-semibold text-transparent">
                    Mentee Dashboard
                </h1>
                <p className="mt-2 text-sm text-slate-300/70">
                    Track your goals, sessions, and progress with your mentor.
                </p>
            </div>

            {/* Stats Grid */}
            <div className="grid gap-6 md:grid-cols-3">
                <div className="rounded-2xl border border-white/10 bg-white/5 p-6 
                                shadow-lg shadow-fuchsia-500/20 backdrop-blur-xl">
                    <div className="text-xs uppercase tracking-wide text-slate-300/70">
                        Completed Milestones
                    </div>
                    <div className="mt-2 text-3xl font-semibold">0 / 5</div>
                </div>

                <div className="rounded-2xl border border-white/10 bg-white/5 p-6 
                                shadow-lg shadow-pink-500/20 backdrop-blur-xl">
                    <div className="text-xs uppercase tracking-wide text-slate-300/70">
                        Next Session
                    </div>
                    <div className="mt-2 text-3xl font-semibold">None Scheduled</div>
                </div>

                <div className="rounded-2xl border border-white/10 bg-white/5 p-6 
                                shadow-lg shadow-amber-400/20 backdrop-blur-xl">
                    <div className="text-xs uppercase tracking-wide text-slate-300/70">
                        Mentor Rating
                    </div>
                    <div className="mt-2 text-3xl font-semibold">—</div>
                </div>
            </div>

            {/* Test Mentee Endpoint */}
            <div className="rounded-3xl border border-white/10 bg-white/5 p-8 
                            shadow-xl shadow-fuchsia-500/20 backdrop-blur-2xl">

                <h2 className="text-lg font-semibold text-white">Test Mentee Endpoint</h2>
                <p className="mt-1 text-sm text-slate-300/70">
                    This verifies that your Mentee role is correctly recognized by the backend.
                </p>

                <button
                    onClick={test}
                    disabled={loading}
                    className="mt-6 rounded-full bg-gradient-to-r from-fuchsia-500 via-pink-500 to-amber-400 
                               px-6 py-2 text-sm font-medium text-white shadow-lg shadow-fuchsia-500/40 
                               transition hover:brightness-110 disabled:opacity-50"
                >
                    {loading ? "Testing..." : "Run Test"}
                </button>

                {result && (
                    <div className="mt-6 rounded-xl border border-white/10 bg-white/10 p-4 
                                    text-sm text-slate-100 backdrop-blur-xl shadow-lg shadow-fuchsia-500/20">
                        {result}
                    </div>
                )}
            </div>
        </div>
    );
}

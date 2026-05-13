import { NavLink } from "react-router-dom";

type SidebarItem = {
    label: string;
    to: string;
    icon?: React.ReactNode;
};

export function Sidebar({ items }: { items: SidebarItem[] }) {
    return (
        <aside
            className="
                hidden md:flex
                w-64 flex-col gap-4
                border-r border-white/10
                bg-white/5
                backdrop-blur-2xl
                p-6
                rounded-3xl
                shadow-xl shadow-black/20
            "
        >
            {/* Section Label */}
            <div className="text-xs font-semibold uppercase tracking-wider text-slate-200/60">
                Dashboard
            </div>

            {/* Navigation Items */}
            <div className="flex flex-col gap-2">
                {items.map((item) => (
                    <NavLink
                        key={item.to}
                        to={item.to}
                        className={({ isActive }) =>
                            [
                                "flex items-center gap-3 px-4 py-2.5 rounded-xl text-sm transition-all",
                                "border border-transparent bg-white/5 text-slate-200/80 hover:bg-white/10 hover:text-white",
                                isActive &&
                                    "border-sky-400/60 bg-white/15 text-white shadow-lg shadow-sky-500/30"
                            ]
                                .filter(Boolean)
                                .join(" ")
                        }
                    >
                        {item.icon && (
                            <span className="text-lg opacity-80">{item.icon}</span>
                        )}
                        <span>{item.label}</span>
                    </NavLink>
                ))}
            </div>
        </aside>
    );
}

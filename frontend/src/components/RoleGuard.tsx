import { Navigate } from "react-router-dom";
import { useAuthContext } from "../auth/useAuth";

export function RoleGuard({
    roles,
    children
}: {
    roles: string[];
    children: React.ReactNode;
}) {
    const { account, roles: userRoles } = useAuthContext();

    if (!account) {
        return <Navigate to="/login" replace />;
    }

    const allowed = roles.some((r) => userRoles.includes(r));
    if (!allowed) {
        return <Navigate to="/unauthorized" replace />;
    }

    return <>{children}</>;
}

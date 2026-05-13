import {
    PublicClientApplication,
    EventType
} from "@azure/msal-browser";
import type { AuthenticationResult } from "@azure/msal-browser";

import { MsalProvider } from "@azure/msal-react";
import { createContext, useEffect, useState } from "react";
import { msalConfig, loginRequest } from "./msalConfig";

const pca = new PublicClientApplication(msalConfig);

export const AuthContext = createContext<any>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
    // ⭐ MUST start as undefined so Navbar knows it's loading
    const [account, setAccount] = useState<any>(undefined);
    const [roles, setRoles] = useState<string[]>([]);
    const [loading, setLoading] = useState(true); // ⭐ MUST exist

    useEffect(() => {
        let mounted = true;

        (async () => {
            await pca.initialize();

            const result = await pca.handleRedirectPromise();

            if (!mounted) return;

            if (result) {
                pca.setActiveAccount(result.account);
                setAccount(result.account);
                extractRoles(result.idTokenClaims);
            } else {
                const active = pca.getActiveAccount();
                if (active) {
                    setAccount(active);
                    extractRoles(active.idTokenClaims);
                } else {
                    setAccount(null);
                }
            }

            setLoading(false); // ⭐ MUST set loading false
        })();

        pca.addEventCallback((event) => {
            if (event.eventType === EventType.LOGIN_SUCCESS) {
                const result = event.payload as AuthenticationResult;
                pca.setActiveAccount(result.account);
                setAccount(result.account);
                extractRoles(result.idTokenClaims);
            }
        });

        return () => {
            mounted = false;
        };
    }, []);

    const extractRoles = (claims: any) => {
        if (!claims) {
            setRoles([]);
            return;
        }

        const roles =
            claims.roles ||
            claims.extension_Roles ||
            claims.extension_roles ||
            claims["extension_Roles"] ||
            claims["extension_roles"] ||
            Object.keys(claims)
                .filter((k) => k.toLowerCase().includes("roles"))
                .map((k) => claims[k])
                .flat() ||
            [];

        setRoles(Array.isArray(roles) ? roles : []);
    };

    const login = () => pca.loginRedirect(loginRequest);
    const logout = () => pca.logoutRedirect();

    const getToken = async () => {
        const active = pca.getActiveAccount();
        if (!active) throw new Error("No active account");

        const result = await pca.acquireTokenSilent({
            ...loginRequest,
            account: active
        });

        return result.accessToken;
    };

    // ⭐ MUST include loading in the context
    return (
        <AuthContext.Provider value={{ account, roles, login, logout, getToken, loading }}>
            <MsalProvider instance={pca}>{children}</MsalProvider>
        </AuthContext.Provider>
    );
}

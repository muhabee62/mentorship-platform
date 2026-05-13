import { useAuthContext } from "../auth/useAuth";

const API_BASE =
    "https://mentorship-backend-api-azh4b6gkc8cterbk.uksouth-01.azurewebsites.net/api/";

export function useBackend() {
    const { getToken } = useAuthContext();

    const callApi = async (
        path: string,
        options: RequestInit = {}
    ): Promise<any> => {
        const token = await getToken();

        const response = await fetch(API_BASE + path, {
            ...options,
            headers: {
                "Content-Type": "application/json",
                Authorization: `Bearer ${token}`,
                ...(options.headers || {})
            }
        });

        if (response.status === 401) {
            throw new Error("Unauthorized — token invalid or expired");
        }

        if (response.status === 403) {
            throw new Error("Forbidden — you do not have the required role");
        }

        const text = await response.text();

        try {
            return JSON.parse(text);
        } catch {
            return text;
        }
    };

    return { callApi };
}

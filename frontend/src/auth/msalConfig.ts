import type { Configuration } from "@azure/msal-browser";

export const msalConfig: Configuration = {
    auth: {
        clientId: "b1bcc706-bf27-451a-ac01-350b03fc0140",
        authority: "https://mentorshipextid.ciamlogin.com/mentorshipextid.onmicrosoft.com/v2.0",
        knownAuthorities: ["mentorshipextid.ciamlogin.com"],
        redirectUri: "http://localhost:5173"
    },
    cache: {
        cacheLocation: "localStorage"
    }
};

export const loginRequest = {
    scopes: [
        "openid",
        "profile",
        "api://d67eb2e4-faf3-4205-96df-17ba1319ac97/access_as_user"
    ],
    extraQueryParameters: {
        p: "BeeBee" // CIAM user flow
    }
};

export const apiScopes = {
    backend: ["api://d67eb2e4-faf3-4205-96df-17ba1319ac97/.default"]
};

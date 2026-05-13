/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}"
  ],
  theme: {
    extend: {
      colors: {
        // Optional: custom brand palette
        brand: {
          sky: "#38bdf8",
          indigo: "#6366f1",
          fuchsia: "#d946ef",
          emerald: "#34d399",
          amber: "#fbbf24"
        }
      },
      boxShadow: {
        // Glow shadows for glassmorphism
        "glow-sky": "0 0 25px rgba(56, 189, 248, 0.35)",
        "glow-indigo": "0 0 25px rgba(99, 102, 241, 0.35)",
        "glow-fuchsia": "0 0 25px rgba(217, 70, 239, 0.35)",
        "glow-emerald": "0 0 25px rgba(52, 211, 153, 0.35)",
        "glow-amber": "0 0 25px rgba(251, 191, 36, 0.35)"
      },
      backdropBlur: {
        xs: "2px"
      },
      borderRadius: {
        glass: "1.5rem"
      },
      animation: {
        "float": "float 6s ease-in-out infinite",
        "pulse-slow": "pulse 4s ease-in-out infinite"
      },
      keyframes: {
        float: {
          "0%, 100%": { transform: "translateY(0px)" },
          "50%": { transform: "translateY(-12px)" }
        }
      }
    }
  },
  plugins: []
};

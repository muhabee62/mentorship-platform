using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Common
{
    /// <summary>
    /// Comprehensive exception logging utility for detailed error diagnostics
    /// </summary>
    public static class ExceptionLogger
    {
        /// <summary>
        /// Logs full exception details including stack trace, inner exceptions, and data
        /// </summary>
        public static void LogFullException(
            ILogger logger,
            Exception ex,
            string contextMessage = "",
            Dictionary<string, object>? contextData = null)
        {
            if (logger == null || ex == null)
                return;

            var sb = new StringBuilder();
            sb.AppendLine("╔════════════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                        🔴 DETAILED EXCEPTION REPORT 🔴                         ║");
            sb.AppendLine("╚════════════════════════════════════════════════════════════════════════════════╝");

            // Context
            if (!string.IsNullOrWhiteSpace(contextMessage))
            {
                sb.AppendLine($"📍 CONTEXT: {contextMessage}");
                sb.AppendLine();
            }

            // Exception Chain
            int exceptionLevel = 0;
            var currentEx = ex;

            while (currentEx != null)
            {
                string levelPrefix = exceptionLevel == 0 ? "🎯 PRIMARY EXCEPTION" : $"↳ INNER EXCEPTION #{exceptionLevel}";

                sb.AppendLine($"{levelPrefix}:");
                sb.AppendLine($"  ├─ Type: {currentEx.GetType().FullName}");
                sb.AppendLine($"  ├─ Message: {currentEx.Message}");
                sb.AppendLine($"  ├─ HResult: 0x{currentEx.HResult:X8}");

                if (currentEx.Data != null && currentEx.Data.Count > 0)
                {
                    sb.AppendLine($"  ├─ Exception Data:");
                    foreach (var key in currentEx.Data.Keys)
                    {
                        sb.AppendLine($"  │   └─ {key}: {currentEx.Data[key]}");
                    }
                }

                sb.AppendLine($"  └─ StackTrace:");
                var stackLines = currentEx.StackTrace?.Split('\n') ?? new[] { "(No stack trace)" };
                foreach (var line in stackLines)
                {
                    sb.AppendLine($"      {line}");
                }

                sb.AppendLine();

                currentEx = currentEx.InnerException;
                exceptionLevel++;
            }

            // Context Data
            if (contextData != null && contextData.Count > 0)
            {
                sb.AppendLine("📋 CONTEXT DATA:");
                foreach (var kvp in contextData)
                {
                    sb.AppendLine($"  ├─ {kvp.Key}: {kvp.Value}");
                }
                sb.AppendLine();
            }

            // Environment Info
            sb.AppendLine("🖥️  ENVIRONMENT:");
            sb.AppendLine($"  ├─ Machine: {Environment.MachineName}");
            sb.AppendLine($"  ├─ Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            sb.AppendLine($"  ├─ Timestamp: {DateTime.UtcNow:O}");
            sb.AppendLine($"  └─ Process ID: {System.Diagnostics.Process.GetCurrentProcess().Id}");

            sb.AppendLine("╚════════════════════════════════════════════════════════════════════════════════╝");

            logger.LogError(sb.ToString());
        }

        /// <summary>
        /// Creates a formatted error response message with exception details
        /// </summary>
        public static string GetDetailedErrorMessage(Exception ex, string prefix = "")
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(prefix))
                sb.AppendLine($"[{prefix}]");

            sb.AppendLine($"Error: {ex.Message}");

            if (ex.InnerException != null)
                sb.AppendLine($"Inner Error: {ex.InnerException.Message}");

            if (!string.IsNullOrWhiteSpace(ex.StackTrace))
                sb.AppendLine($"Location: {ex.StackTrace.Split('\n').FirstOrDefault()?.Trim()}");

            return sb.ToString();
        }
    }
}

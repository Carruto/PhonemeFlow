using UnityEngine;

namespace PhonemeFlow
{
    /// <summary>
    /// Shared feature toggles/state that allow the PhonemeFlow runtime to respect project-level
    /// configuration without taking a hard dependency on editor-only APIs.
    /// </summary>
    public static class PhonemeFlowFeatureFlags
    {
        private static bool s_macFallbackActive;
        private static string s_macFallbackReason = null;
        private static bool s_warningLogged;

        public static bool MacFallbackActive => s_macFallbackActive;
        public static string MacFallbackReason => s_macFallbackReason;

        public static bool IsMacNativePathAllowed =>
            !s_macFallbackActive &&
            (Application.platform == RuntimePlatform.OSXPlayer ||
             Application.platform == RuntimePlatform.OSXEditor);

        public static void ActivateMacFallback(string reason, string details = null)
        {
            s_macFallbackActive = true;
            s_macFallbackReason = string.IsNullOrEmpty(reason) ? "unknown" : reason;

            if (s_warningLogged)
            {
                return;
            }

            var message = $"PhonemeX: macOS native phonemizer unavailable (reason: {s_macFallbackReason}). Falling back to Dictionary/Cloudflare.";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" Details: {details}";
            }

            Debug.LogWarning(message);
            s_warningLogged = true;
        }

        public static void ClearMacFallback()
        {
            s_macFallbackActive = false;
            s_macFallbackReason = null;
            s_warningLogged = false;
        }
    }
}

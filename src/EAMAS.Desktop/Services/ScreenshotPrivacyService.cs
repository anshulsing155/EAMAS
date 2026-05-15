using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace EAMAS.Desktop.Services
{
    public enum PrivacyBlurLevel { None, Partial, Full }

    /// <summary>
    /// Detects personally-sensitive content in a screenshot based on the active window's
    /// process name and window title, then applies a pixelation blur before storage.
    ///
    /// Full blur  → entire screen is personal (messaging, banking, healthcare, adult sites, password managers).
    /// Partial blur → browser address-bar strip (top ~8 %) is blurred (moderately sensitive page).
    /// None       → no personal content detected; image stored unmodified.
    /// </summary>
    public class ScreenshotPrivacyService
    {
        // ── Full-blur: process names that are inherently personal ─────────────────

        private static readonly HashSet<string> FullPersonalProcesses =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Personal messaging desktop apps
                "whatsapp",
                "telegram",
                "signal",
                "messenger",          // Facebook Messenger desktop
                "viber",
                "line",
                "skype",              // personal skype (not business)
                "snapchat",

                // Dating apps
                "tinder", "bumble", "hinge", "badoo",

                // Personal finance desktop apps
                "mint", "quicken", "turbotax", "taxact", "freetaxusa",

                // Password managers (avoid capturing stored passwords/secrets)
                "keepass", "keepassxc", "1password", "bitwarden", "lastpass",
                "dashlane", "roboform", "nordpass",

                // Banking/UPI apps that sometimes have desktop clients
                "phonepe", "gpay",

                // Adult content platforms (if running as a desktop app via PWA/Electron)
                "onlyfans"
            };

        // ── Full-blur: window title keywords (process name alone is not enough) ──

        private static readonly string[] FullPersonalTitleKeywords =
        {
            // ── Messaging (browser-based) ─────────────────────────────────────────
            "whatsapp",                   // web.whatsapp.com tab
            "telegram web",               // web.telegram.org tab
            "telegram — ",                // Telegram desktop title pattern
            "messenger",                  // messenger.com tab
            "facebook messenger",
            "instagram direct",
            "instagram",                  // instagram.com DMs
            "snapchat",
            "viber web",
            "line -",

            // ── Banking & financial transactions ──────────────────────────────────
            "net banking", "netbanking", "online banking", "bank account",
            "account balance", "account statement", "bank statement",
            "credit card statement", "credit card bill", "debit card statement",
            "transaction history", "fund transfer", "neft", "rtgs", "imps",
            // Indian banks & payment apps
            "sbi", "hdfc bank", "icici bank", "axis bank", "kotak mahindra",
            "pnb", "bank of baroda", "canara bank", "union bank",
            "paytm", "phonepe", "gpay", "bhim", "upi payment",
            // Global banks
            "chase online", "bank of america", "wells fargo", "citibank",
            // Payment platforms
            "paypal", "stripe payment", "checkout",

            // ── Tax / government identity ─────────────────────────────────────────
            "income tax return", "tax return", "tax filing", "irs.gov",
            "social security", "aadhaar", "pan card", "pan verification",
            "passport", "driving license", "voter id",

            // ── Healthcare ────────────────────────────────────────────────────────
            "patient portal", "medical record", "health record", "my health",
            "prescription", "lab result", "test result", "health insurance claim",
            "apollo health", "practo", "1mg",

            // ── Adult content (common site name fragments) ────────────────────────
            "pornhub", "xvideos", "xnxx", "onlyfans", "adult content",
            "redtube", "youporn", "xhamster", "brazzers", "livejasmin",

            // ── Personal legal documents ──────────────────────────────────────────
            "will and testament", "power of attorney", "divorce", "legal document",

            // ── Personal dating / matrimony ───────────────────────────────────────
            "tinder", "bumble", "hinge", "shaadi.com", "jeevansathi",
            "matrimony", "dating profile",

            // ── Private browser modes ─────────────────────────────────────────────
            "inprivate", "incognito",     // Edge / Chrome incognito tab titles contain these
            "private browsing",           // Firefox
        };

        // ── Partial-blur: browser address-bar only ────────────────────────────────

        private static readonly HashSet<string> BrowserProcesses =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "chrome", "firefox", "msedge", "opera", "brave",
                "iexplore", "safari", "vivaldi", "arc"
            };

        private static readonly string[] PartialPersonalTitleKeywords =
        {
            // Banking / auth pages in browser
            "banking", "bank login", "bank -", "- bank",
            // Payment
            "paypal.com", "stripe.com",
            // Auth / account pages
            "account login", "sign in to", "password reset", "forgot password",
            "two-factor", "2fa", "verify your identity",
            // Health
            "health insurance", "medical portal", "pharmacy",
            // Job search (personal career activity)
            "naukri", "timesjobs", "monster.com", "indeed.com", "linkedin jobs",
            // Personal webmail
            "gmail.com - gmail", "outlook.com - personal", "yahoo mail",
        };

        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Analyses the active window and returns the privacy level plus a human-readable reason.
        /// </summary>
        public (PrivacyBlurLevel Level, string? Reason) Detect(string processName, string windowTitle)
        {
            var proc  = processName.ToLowerInvariant().Trim();
            var title = windowTitle.ToLowerInvariant();

            // 1. Full blur via process name
            if (FullPersonalProcesses.Contains(proc))
                return (PrivacyBlurLevel.Full, $"Personal app detected: {processName}");

            // 2. Full blur via title keyword (covers browser-based personal apps and web apps)
            foreach (var kw in FullPersonalTitleKeywords)
            {
                if (title.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    return (PrivacyBlurLevel.Full, $"Sensitive content detected: {kw}");
            }

            // 3. Partial blur: browser with a moderately sensitive page
            if (BrowserProcesses.Contains(proc))
            {
                foreach (var kw in PartialPersonalTitleKeywords)
                {
                    if (title.Contains(kw, StringComparison.OrdinalIgnoreCase))
                        return (PrivacyBlurLevel.Partial, $"Sensitive browser page: {kw}");
                }
            }

            return (PrivacyBlurLevel.None, null);
        }

        /// <summary>
        /// Applies a pixelation blur to the JPEG bytes according to the detected privacy level.
        /// Full  → entire image pixelated (20×20 px blocks).
        /// Partial → only the top address-bar strip (≈ top 8 % of height) is pixelated.
        /// </summary>
        public byte[] ApplyBlur(byte[] jpegBytes, PrivacyBlurLevel level, int jpegQuality = 70)
        {
            if (level == PrivacyBlurLevel.None) return jpegBytes;

            using var inputMs = new MemoryStream(jpegBytes);
            using var bmp     = new Bitmap(inputMs);

            Rectangle blurRegion;
            if (level == PrivacyBlurLevel.Full)
            {
                blurRegion = new Rectangle(0, 0, bmp.Width, bmp.Height);
            }
            else
            {
                // Partial: cover the browser chrome / address-bar area.
                // Typically ~60–100 px at 1080p; use 8 % of height as a safe bound.
                int barH = Math.Max(60, (int)(bmp.Height * 0.08));
                blurRegion = new Rectangle(0, 0, bmp.Width, barH);
            }

            Pixelate(bmp, blurRegion);

            var encoder   = GetJpegEncoder();
            var encParams = new EncoderParameters(1);
            encParams.Param[0] = new EncoderParameter(
                Encoder.Quality, (long)Math.Clamp(jpegQuality, 1, 100));

            using var outMs = new MemoryStream();
            bmp.Save(outMs, encoder, encParams);
            return outMs.ToArray();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Pixelates <paramref name="region"/> in-place by scaling down to 1/<paramref name="blockSize"/>
        /// then back up using NearestNeighbor, producing clearly visible mosaic blocks.
        /// </summary>
        private static void Pixelate(Bitmap bmp, Rectangle region, int blockSize = 20)
        {
            region.Intersect(new Rectangle(0, 0, bmp.Width, bmp.Height));
            if (region.Width <= 0 || region.Height <= 0) return;

            int tinyW = Math.Max(1, region.Width  / blockSize);
            int tinyH = Math.Max(1, region.Height / blockSize);

            using var tinyBmp = new Bitmap(tinyW, tinyH);

            using (var g = Graphics.FromImage(tinyBmp))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode   = PixelOffsetMode.Half;
                g.DrawImage(bmp, new Rectangle(0, 0, tinyW, tinyH), region, GraphicsUnit.Pixel);
            }

            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode   = PixelOffsetMode.Half;
                g.DrawImage(tinyBmp, region);
            }
        }

        private static ImageCodecInfo GetJpegEncoder()
            => ImageCodecInfo.GetImageEncoders().First(e => e.MimeType == "image/jpeg");
    }
}

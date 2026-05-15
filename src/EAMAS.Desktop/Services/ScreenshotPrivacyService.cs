using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace EAMAS.Desktop.Services
{
    public enum PrivacyBlurLevel { None, Partial, Full }

    /// <summary>
    /// Detects personally-sensitive content in a screenshot (based on the active window's
    /// process name and title) and applies a pixelation blur to protect that content before
    /// the image is stored in MongoDB.
    ///
    /// Full blur  → entire screen is personal (banking, healthcare, messaging apps, adult sites).
    /// Partial blur → browser address bar strip is blurred (moderately sensitive page detected).
    /// None       → no personal content detected; image stored unmodified.
    /// </summary>
    public class ScreenshotPrivacyService
    {
        // ── Full-blur: process names that are inherently personal ─────────────────

        private static readonly HashSet<string> FullPersonalProcesses =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Personal messaging
                "whatsapp", "telegram", "signal",
                // Dating apps (desktop clients)
                "tinder", "bumble", "hinge",
                // Personal finance desktop apps
                "mint", "quicken", "turbotax", "taxact",
                // Password managers (avoid capturing stored secrets)
                "keepass", "keepassxc", "1password", "bitwarden", "lastpass"
            };

        // ── Full-blur: window title keywords that strongly indicate personal content ─

        private static readonly string[] FullPersonalTitleKeywords =
        {
            // Banking / financial transactions
            "net banking", "netbanking", "online banking", "bank account",
            "account balance", "account statement", "bank statement",
            "credit card statement", "credit card bill", "debit card statement",
            // Payment platforms
            "paypal", "paytm wallet", "google pay", "gpay", "phonepay",
            // Tax / government
            "income tax return", "tax return", "tax filing", "irs.gov",
            "social security", "aadhaar", "pan card", "pan verification",
            // Healthcare
            "patient portal", "medical record", "health record", "my health",
            "prescription", "lab result", "test result", "health insurance claim",
            // Adult content (common site name fragments)
            "pornhub", "xvideos", "xnxx", "onlyfans", "adult content",
            // Personal legal documents
            "will and testament", "power of attorney", "divorce", "legal document"
        };

        // ── Partial-blur: browser address bar only (moderate sensitivity) ─────────

        private static readonly HashSet<string> BrowserProcesses =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "chrome", "firefox", "msedge", "opera", "brave", "iexplore", "safari", "vivaldi"
            };

        private static readonly string[] PartialPersonalTitleKeywords =
        {
            // Banking words in browser title
            "banking", "bank -", "- bank", "bank login",
            // Payment
            "paypal.com", "stripe.com", "checkout",
            // Auth / account pages
            "account login", "sign in to", "password reset", "forgot password",
            // Health
            "health insurance", "medical portal", "pharmacy",
            // Job search (personal career activity)
            "naukri", "timesjobs", "monster.com", "indeed.com",
            // Personal webmail
            "gmail.com - gmail", "outlook.com - personal"
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

            // 2. Full blur via title keyword
            foreach (var kw in FullPersonalTitleKeywords)
            {
                if (title.Contains(kw, StringComparison.Ordinal))
                    return (PrivacyBlurLevel.Full, $"Sensitive content: {kw}");
            }

            // 3. Partial blur: browser with moderately sensitive page
            if (BrowserProcesses.Contains(proc))
            {
                foreach (var kw in PartialPersonalTitleKeywords)
                {
                    if (title.Contains(kw, StringComparison.Ordinal))
                        return (PrivacyBlurLevel.Partial, $"Sensitive browser page: {kw}");
                }
            }

            return (PrivacyBlurLevel.None, null);
        }

        /// <summary>
        /// Applies a pixelation blur to the JPEG image bytes according to the detected privacy level.
        ///
        /// Full  → entire image is pixelated (20×20 px blocks).
        /// Partial → only the top address-bar strip (≈top 8 % of height) is pixelated.
        ///
        /// Returns the processed JPEG bytes; if level is None the original bytes are returned
        /// unchanged (no re-encoding, no quality loss).
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
                // Typically ~60–100 px at 1080 p; use 8 % of height as a safe bound.
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
        /// Pixelates <paramref name="region"/> in-place by scaling the area down to
        /// 1/<paramref name="blockSize"/> and back up using NearestNeighbor interpolation,
        /// producing clearly visible mosaic blocks that effectively obscure the content.
        /// </summary>
        private static void Pixelate(Bitmap bmp, Rectangle region, int blockSize = 20)
        {
            region.Intersect(new Rectangle(0, 0, bmp.Width, bmp.Height));
            if (region.Width <= 0 || region.Height <= 0) return;

            int tinyW = Math.Max(1, region.Width  / blockSize);
            int tinyH = Math.Max(1, region.Height / blockSize);

            using var tinyBmp = new Bitmap(tinyW, tinyH);

            // Shrink the region to a tiny bitmap
            using (var g = Graphics.FromImage(tinyBmp))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode   = PixelOffsetMode.Half;
                g.DrawImage(bmp,
                    new Rectangle(0, 0, tinyW, tinyH),
                    region, GraphicsUnit.Pixel);
            }

            // Scale it back up — produces the mosaic effect
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

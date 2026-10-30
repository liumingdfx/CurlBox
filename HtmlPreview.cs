using System;
using System.Text.RegularExpressions;

namespace CurlBox
{
    internal static class HtmlPreview
    {
        public const int MaxPreviewChars = 2 * 1024 * 1024;

        private const string BaseStyle =
            "<style>html,body{background:#fdfeff;color:#18222d;}" +
            "body{font-family:'Microsoft YaHei UI','Segoe UI',sans-serif;font-size:14px;line-height:1.65;margin:0;padding:14px;}" +
            "a{color:#b03a0a;}img,video,iframe{max-width:100%;height:auto;}" +
            "pre{white-space:pre-wrap;word-break:break-word;background:#f6f8fb;padding:10px;border-radius:6px;}" +
            "code{font-family:Consolas,monospace;}table{border-collapse:collapse;max-width:100%;}" +
            "td,th{border:1px solid #d5dde5;padding:5px 8px;}blockquote{margin-left:0;padding-left:12px;border-left:3px solid #d5dde5;color:#495968;}</style>";

        private static readonly Regex ScriptBlock = new Regex(
            @"<script\b[^>]*>.*?</script\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex ScriptTag = new Regex(
            @"<script\b[^>]*/\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex ActiveContent = new Regex(
            @"<(iframe|frame|object|embed|applet)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex ActiveSingleTag = new Regex(
            @"<(iframe|frame|object|embed|applet)\b[^>]*/\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex MetaRefresh = new Regex(
            @"<meta\b[^>]*http-equiv\s*=\s*(?:""\s*refresh\s*""|'\s*refresh\s*'|refresh)[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex BaseTag = new Regex(
            @"<base\b[^>]*>", RegexOptions.IgnoreCase);
        private static readonly Regex EventAttribute = new Regex(
            @"\s+on[a-z]+\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase);
        private static readonly Regex DangerousUrl = new Regex(
            @"(?:javascript|vbscript|file)\s*:", RegexOptions.IgnoreCase);
        private static readonly Regex CssExpression = new Regex(
            @"expression\s*\(", RegexOptions.IgnoreCase);

        public static bool LooksLikeHtml(string contentType, string body)
        {
            string type = contentType ?? string.Empty;
            if (type.IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (type.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.IndexOf("xml", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.IndexOf("svg", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            string text = (body ?? string.Empty).TrimStart();
            if (text.Length == 0 || text[0] != '<') return false;
            return text.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("<head", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("<body", StringComparison.OrdinalIgnoreCase);
        }

        public static string BuildDocument(string html)
        {
            string safe = (html ?? string.Empty).Trim();
            safe = ScriptBlock.Replace(safe, string.Empty);
            safe = ScriptTag.Replace(safe, string.Empty);
            safe = ActiveContent.Replace(safe, string.Empty);
            safe = ActiveSingleTag.Replace(safe, string.Empty);
            safe = MetaRefresh.Replace(safe, string.Empty);
            safe = BaseTag.Replace(safe, string.Empty);
            safe = EventAttribute.Replace(safe, string.Empty);
            safe = DangerousUrl.Replace(safe, "#");
            safe = CssExpression.Replace(safe, "none(");

            string inject = "<meta charset=\"utf-8\">" +
                            "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">" +
                            BaseStyle;

            int head = safe.IndexOf("<head", StringComparison.OrdinalIgnoreCase);
            if (head >= 0)
            {
                int end = safe.IndexOf('>', head);
                if (end >= 0) return safe.Insert(end + 1, inject);
            }

            int htmlTag = safe.IndexOf("<html", StringComparison.OrdinalIgnoreCase);
            if (htmlTag >= 0)
            {
                int end = safe.IndexOf('>', htmlTag);
                if (end >= 0) return safe.Insert(end + 1, "<head>" + inject + "</head>");
            }

            return "<!DOCTYPE html><html><head>" + inject + "</head><body>" + safe + "</body></html>";
        }
    }
}

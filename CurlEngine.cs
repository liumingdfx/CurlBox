using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CurlBox
{
    internal sealed class CurlParseException : Exception
    {
        public CurlParseException(string message) : base(message) { }
    }

    internal sealed class CurlArg
    {
        public string Option { get; set; }
        public string Value { get; set; }
        public bool IsUrl { get; set; }

        public CurlArg() { }

        public CurlArg(string option, string value)
        {
            Option = option;
            Value = value;
        }
    }

    internal sealed class CurlCommand
    {
        public readonly List<CurlArg> Args = new List<CurlArg>();
        public readonly List<string> Warnings = new List<string>();
        public string Method;
        public string ExplicitMethod;
        public string Url;
        public int HeaderCount;
        public bool HasBody;
        public string BodyKind;
        public long BodyBytes;
        public bool BodyFromFile;
    }

    internal sealed class HeaderItem
    {
        public string Name;
        public string Value;

        public HeaderItem(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }

    internal sealed class HeaderBlock
    {
        public string StatusLine;
        public int Status;
        public readonly List<HeaderItem> Headers = new List<HeaderItem>();

        public string GetHeader(string name)
        {
            for (int i = Headers.Count - 1; i >= 0; i--)
            {
                if (string.Equals(Headers[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return Headers[i].Value;
            }
            return null;
        }
    }

    internal sealed class RedirectInfo
    {
        public int Status;
        public string Location;
    }

    internal sealed class CapturedText
    {
        public string Text;
        public bool Truncated;
    }

    internal sealed class CurlResult
    {
        public bool Fatal;
        public string FatalMessage;
        public bool Canceled;
        public string Method;
        public string RequestedUrl;
        public int ExitCode;
        public bool TimedOut;
        public long DurationMs;
        public int StatusCode;
        public string EffectiveUrl;
        public string ContentType;
        public long SizeDownload;
        public double TimeNamelookup;
        public double TimeConnect;
        public double TimeStarttransfer;
        public double TimeTotal;
        public readonly List<HeaderBlock> HeaderBlocks = new List<HeaderBlock>();
        public HeaderBlock FinalHeaders;
        public readonly List<RedirectInfo> Redirects = new List<RedirectInfo>();
        public string RawHeaders;
        public string BodyKind = "empty";
        public string BodyText;
        public byte[] BodyBytes;
        public string Mime;
        public long BodyTotalBytes;
        public bool BodyTruncated;
        public bool HeadOnly;
        public string Stderr;
        public bool StderrTruncated;
        public string Hint;
        public readonly List<string> Warnings = new List<string>();
    }

    internal static class CurlOptionMap
    {
        private static readonly object Sync = new object();
        private static bool _loaded;
        private static readonly Dictionary<string, bool> LongOptions =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private static readonly HashSet<char> ShortValue = new HashSet<char>();
        private static readonly HashSet<char> ShortBool = new HashSet<char>();

        static CurlOptionMap()
        {
            foreach (string name in CurlOptionData.LongValueOptions) LongOptions[name] = true;
            foreach (string name in CurlOptionData.LongBoolOptions) LongOptions[name] = false;
            foreach (char c in CurlOptionData.ShortValueOptions) ShortValue.Add(c);
            foreach (char c in CurlOptionData.ShortBoolOptions) ShortBool.Add(c);
        }

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            lock (Sync)
            {
                if (_loaded) return;

                // curlconverter's option table is the baseline. Merge the local
                // curl's help output so new options in newer curl builds work too.
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo("curl.exe", "--help all");
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                    using (Process p = Process.Start(psi))
                    {
                        if (p != null)
                        {
                            string output = p.StandardOutput.ReadToEnd();
                            p.WaitForExit(4000);
                            foreach (string rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                string line = rawLine.TrimStart();
                                Match m = Regex.Match(line, @"^(?:-([A-Za-z0-9]),\s+)?--([a-z0-9.-]+)(\s+<[^>]+>)?\s{2,}");
                                if (!m.Success) continue;
                                bool takesValue = m.Groups[3].Success;
                                LongOptions[m.Groups[2].Value] = takesValue;
                                if (m.Groups[1].Success)
                                {
                                    char shortName = m.Groups[1].Value[0];
                                    if (takesValue)
                                    {
                                        ShortValue.Add(shortName);
                                        ShortBool.Remove(shortName);
                                    }
                                    else
                                    {
                                        ShortBool.Add(shortName);
                                        ShortValue.Remove(shortName);
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // The generated curlconverter tables are still available.
                }

                _loaded = true;
            }
        }

        public static bool TryResolveLong(string name, out string canonical, out bool takesValue, out bool ambiguous)
        {
            EnsureLoaded();
            canonical = null;
            takesValue = false;
            ambiguous = false;

            if (LongOptions.TryGetValue(name, out takesValue))
            {
                canonical = name;
                return true;
            }

            List<string> matches = new List<string>();
            foreach (KeyValuePair<string, bool> pair in LongOptions)
            {
                if (pair.Key.StartsWith(name, StringComparison.Ordinal))
                    matches.Add(pair.Key);
            }
            if (matches.Count == 1)
            {
                canonical = matches[0];
                takesValue = LongOptions[canonical];
                return true;
            }
            if (matches.Count > 1)
                ambiguous = true;

            if (name.StartsWith("no-", StringComparison.Ordinal))
            {
                string positive = name.Substring(3);
                bool positiveTakesValue;
                string positiveCanonical;
                bool positiveAmbiguous;
                if (TryResolveLong(positive, out positiveCanonical, out positiveTakesValue, out positiveAmbiguous) &&
                    !positiveTakesValue)
                {
                    canonical = name;
                    takesValue = false;
                    ambiguous = false;
                    return true;
                }
            }

            return false;
        }

        public static bool TryResolveShort(char c, out bool takesValue)
        {
            EnsureLoaded();
            if (ShortValue.Contains(c))
            {
                takesValue = true;
                return true;
            }
            if (ShortBool.Contains(c))
            {
                takesValue = false;
                return true;
            }
            takesValue = false;
            return false;
        }
    }

    internal static class CurlParser
    {
        private static readonly Regex PsPrompt = new Regex(@"^\s*PS [^>\r\n]*>\s*", RegexOptions.IgnoreCase);
        private static readonly Regex Continuation = new Regex(@"(\\|\^|`)[ \t]*\n[ \t]*");
        private static readonly HashSet<string> ShellOperators = new HashSet<string>(new[]
        {
            "|", "||", "&&", ";", ">", ">>", "<", "<<"
        });

        private static readonly HashSet<string> DataOptions = new HashSet<string>(new[]
        {
            "-d", "--data", "--data-ascii", "--data-raw", "--data-binary",
            "--data-urlencode", "--json"
        }, StringComparer.Ordinal);

        private static readonly HashSet<string> FormOptions = new HashSet<string>(new[]
        {
            "-F", "--form", "--form-string"
        }, StringComparer.Ordinal);

        private static readonly HashSet<string> OutputValueOptions = new HashSet<string>(new[]
        {
            "-o", "--output", "--output-dir", "-D", "--dump-header", "-c", "--cookie-jar",
            "-w", "--write-out", "--trace", "--trace-ascii", "--trace-config", "--stderr",
            "--libcurl", "--etag-save"
        }, StringComparer.Ordinal);

        private static readonly HashSet<string> OutputBoolOptions = new HashSet<string>(new[]
        {
            "-O", "--remote-name", "--remote-name-all", "-J", "--remote-header-name"
        }, StringComparer.Ordinal);

        public static CurlCommand Parse(string raw)
        {
            CurlOptionMap.EnsureLoaded();
            string normalized = Normalize(raw);
            if (normalized.Length == 0) throw new CurlParseException("先粘贴一条 curl 命令。");

            List<string> tokens = Tokenize(normalized);
            if (tokens.Count == 0) throw new CurlParseException("先粘贴一条 curl 命令。");

            foreach (string token in tokens)
            {
                if (ShellOperators.Contains(token))
                    throw new CurlParseException("看起来包含管道或重定向，请只保留单条 curl 命令。");
            }

            string executable = tokens[0].Split('\\', '/').LastOrDefault() ?? string.Empty;
            executable = executable.ToLowerInvariant();
            if (executable != "curl" && executable != "curl.exe")
                throw new CurlParseException("请粘贴以 curl 开头的命令（支持 curl.exe）。");

            if (tokens.Count < 2) throw new CurlParseException("curl 命令中还没有 URL。");

            List<string> args = tokens.GetRange(1, tokens.Count - 1);
            List<CurlArg> groups = GroupArgs(args);
            List<string> warnings = DetectShellExpansions(normalized);
            groups = Sanitize(groups, warnings);
            return Analyze(groups, warnings);
        }

        private static List<string> DetectShellExpansions(string text)
        {
            List<string> warnings = new List<string>();
            bool inSingle = false;
            bool inDouble = false;
            bool inAnsi = false;
            bool escaped = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (inSingle)
                {
                    if (c == '\'') inSingle = false;
                    continue;
                }
                if (inAnsi)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '\'') inAnsi = false;
                    continue;
                }
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }
                if (c == '\'')
                {
                    inSingle = true;
                    continue;
                }
                if (c == '"')
                {
                    inDouble = !inDouble;
                    continue;
                }
                if (c == '$' && i + 1 < text.Length && text[i + 1] == '\'')
                {
                    inAnsi = true;
                    i++;
                    continue;
                }
                if (c == '`')
                {
                    warnings.Add("命令中含有反引号命令替换。本工具不会执行 shell 展开，会按字面发送；请先替换成实际值。");
                    return warnings;
                }
                if (c == '$' && i + 1 < text.Length)
                {
                    char next = text[i + 1];
                    if (next == '(' || next == '{' || char.IsLetter(next) || next == '_')
                    {
                        warnings.Add("命令中含有 shell 变量或命令替换（如 $TOKEN、${VAR}、$(...)）。本工具不会展开它们，会按字面发送；请先替换成实际值。");
                        return warnings;
                    }
                }
            }
            return warnings;
        }

        private static string Normalize(string raw)
        {
            if (raw == null) throw new CurlParseException("命令内容无效。");
            string text = NormalizeLineEndings(raw.TrimStart('\uFEFF'));
            text = PsPrompt.Replace(text, string.Empty, 1);
            text = Regex.Replace(text, @"^\s*\$\s+", string.Empty);
            text = Continuation.Replace(text, " ");
            return text.Trim();
        }

        private static string NormalizeLineEndings(string text)
        {
            // CRLF inside quoted payloads is request data, not formatting.
            StringBuilder result = new StringBuilder(text.Length);
            bool inSingle = false;
            bool inDouble = false;
            bool inAnsi = false;
            bool escaped = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inSingle)
                {
                    result.Append(c);
                    if (c == '\'') inSingle = false;
                    continue;
                }
                if (inAnsi || inDouble)
                {
                    result.Append(c);
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (inAnsi && c == '\'') inAnsi = false;
                    else if (inDouble && c == '"') inDouble = false;
                    continue;
                }
                if (escaped)
                {
                    escaped = false;
                    if (c == '\r')
                    {
                        result.Append('\n');
                        if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    }
                    else
                    {
                        result.Append(c);
                    }
                    continue;
                }
                if (c == '\\')
                {
                    result.Append(c);
                    escaped = true;
                    continue;
                }
                if (c == '\'')
                {
                    inSingle = true;
                    result.Append(c);
                    continue;
                }
                if (c == '"')
                {
                    inDouble = true;
                    result.Append(c);
                    continue;
                }
                if (c == '$' && i + 1 < text.Length && text[i + 1] == '\'')
                {
                    inAnsi = true;
                    result.Append("$'");
                    i++;
                    continue;
                }
                if (c == '\r')
                {
                    result.Append('\n');
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    continue;
                }
                result.Append(c);
            }
            return result.ToString();
        }

        private static List<string> Tokenize(string input)
        {
            List<string> tokens = new List<string>();
            StringBuilder current = new StringBuilder();
            bool started = false;
            string quote = null;

            Action push = delegate
            {
                if (started)
                {
                    tokens.Add(current.ToString());
                    current.Length = 0;
                    started = false;
                }
            };

            for (int i = 0; i < input.Length; i++)
            {
                char ch = input[i];

                if (quote == "'" || quote == "\"")
                {
                    if (quote == "'")
                    {
                        if (ch == '\'')
                        {
                            quote = null;
                            started = true;
                        }
                        else
                        {
                            current.Append(ch);
                            started = true;
                        }
                        continue;
                    }

                    if (ch == '"')
                    {
                        quote = null;
                        started = true;
                        continue;
                    }
                    if (ch == '\\')
                    {
                        char next = i + 1 < input.Length ? input[i + 1] : '\0';
                        if (next == '"' || next == '\\' || next == '$' || next == '`')
                        {
                            current.Append(next);
                            i++;
                        }
                        else
                        {
                            current.Append(ch);
                        }
                        started = true;
                        continue;
                    }
                    if (ch == '`')
                    {
                        char next = i + 1 < input.Length ? input[i + 1] : '\0';
                        if (next == '"' || next == '\\' || next == '`')
                        {
                            current.Append(next);
                            i++;
                            started = true;
                            continue;
                        }
                    }
                    if (ch == '^' && i + 1 < input.Length && (input[i + 1] == '"' || input[i + 1] == '^'))
                    {
                        current.Append(input[i + 1]);
                        i++;
                        started = true;
                        continue;
                    }
                    current.Append(ch);
                    started = true;
                    continue;
                }

                if (quote == "ansi")
                {
                    if (ch == '\'')
                    {
                        quote = null;
                        started = true;
                        continue;
                    }
                    if (ch == '\\')
                    {
                        int nextIndex;
                        current.Append(DecodeAnsiEscape(input, i + 1, out nextIndex));
                        i = nextIndex - 1;
                        started = true;
                        continue;
                    }
                    current.Append(ch);
                    started = true;
                    continue;
                }

                if (char.IsWhiteSpace(ch))
                {
                    push();
                    continue;
                }
                if (ch == '\'')
                {
                    quote = "'";
                    started = true;
                    continue;
                }
                if (ch == '"')
                {
                    quote = "\"";
                    started = true;
                    continue;
                }
                if (ch == '$' && i + 1 < input.Length && input[i + 1] == '\'')
                {
                    quote = "ansi";
                    i++;
                    started = true;
                    continue;
                }
                if (ch == '\\')
                {
                    char next = i + 1 < input.Length ? input[i + 1] : '\0';
                    if (next != '\0' && (char.IsWhiteSpace(next) || next == '"' || next == '\'' || next == '\\'))
                    {
                        current.Append(next);
                        i++;
                        started = true;
                        continue;
                    }
                    current.Append(ch);
                    started = true;
                    continue;
                }
                if (ch == '^')
                {
                    char next = i + 1 < input.Length ? input[i + 1] : '\0';
                    if (next != '\0' && ("\"'&|<>\t ^".IndexOf(next) >= 0))
                    {
                        current.Append(next);
                        i++;
                        started = true;
                        continue;
                    }
                    current.Append(ch);
                    started = true;
                    continue;
                }

                current.Append(ch);
                started = true;
            }

            if (quote != null)
                throw new CurlParseException("引号没有闭合，请检查复制内容是否完整。");

            push();
            return tokens;
        }

        private static string DecodeAnsiEscape(string input, int index, out int nextIndex)
        {
            if (index >= input.Length)
            {
                nextIndex = index;
                return "\\";
            }

            char ch = input[index];
            if (ch == 'x')
            {
                int cursor = index + 1;
                StringBuilder hex = new StringBuilder();
                while (cursor < input.Length && hex.Length < 2 && IsHex(input[cursor]))
                {
                    hex.Append(input[cursor]);
                    cursor++;
                }
                if (hex.Length > 0)
                {
                    nextIndex = cursor;
                    return ((char)Convert.ToInt32(hex.ToString(), 16)).ToString();
                }
                nextIndex = index + 1;
                return "\\x";
            }
            if (ch == 'u' || ch == 'U')
            {
                int maximum = ch == 'u' ? 4 : 8;
                int cursor = index + 1;
                StringBuilder hex = new StringBuilder();
                while (cursor < input.Length && hex.Length < maximum && IsHex(input[cursor]))
                {
                    hex.Append(input[cursor]);
                    cursor++;
                }
                if (hex.Length > 0)
                {
                    int codePoint = Convert.ToInt32(hex.ToString(), 16);
                    if (codePoint > 0x10FFFF || (codePoint >= 0xD800 && codePoint <= 0xDFFF))
                        throw new CurlParseException("ANSI-C 字符串中包含无效的 Unicode 码点。");
                    nextIndex = cursor;
                    return char.ConvertFromUtf32(codePoint);
                }
                nextIndex = index + 1;
                return "\\" + ch;
            }
            if (ch >= '0' && ch <= '7')
            {
                StringBuilder digits = new StringBuilder();
                digits.Append(ch);
                int cursor = index + 1;
                while (digits.Length < 3 && cursor < input.Length && input[cursor] >= '0' && input[cursor] <= '7')
                {
                    digits.Append(input[cursor]);
                    cursor++;
                }
                nextIndex = cursor;
                return ((char)(Convert.ToInt32(digits.ToString(), 8) % 256)).ToString();
            }
            if (ch == 'c')
            {
                if (index + 1 >= input.Length)
                    throw new CurlParseException("ANSI-C 转义 \\c 缺少字符。");
                char control = input[index + 1];
                if (control > 127)
                    throw new CurlParseException("ANSI-C 转义 \\c 只支持 ASCII 字符。");
                nextIndex = index + 2;
                return ((char)(control == '?' ? 0x7F : (char.ToUpperInvariant(control) & 0x1F))).ToString();
            }

            switch (ch)
            {
                case 'n': nextIndex = index + 1; return "\n";
                case 'r': nextIndex = index + 1; return "\r";
                case 't': nextIndex = index + 1; return "\t";
                case 'b': nextIndex = index + 1; return "\b";
                case 'f': nextIndex = index + 1; return "\f";
                case 'v': nextIndex = index + 1; return "\v";
                case 'a': nextIndex = index + 1; return "\a";
                case 'e':
                case 'E': nextIndex = index + 1; return "\u001b";
                case '\\': nextIndex = index + 1; return "\\";
                case '\'': nextIndex = index + 1; return "'";
                case '"': nextIndex = index + 1; return "\"";
                case '?': nextIndex = index + 1; return "?";
                default:
                    nextIndex = index + 1;
                    return "\\" + ch;
            }
        }

        private static bool IsHex(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private static List<CurlArg> GroupArgs(List<string> args)
        {
            List<CurlArg> groups = new List<CurlArg>();
            bool endOfOptions = false;

            for (int i = 0; i < args.Count; i++)
            {
                string arg = args[i];
                if (endOfOptions)
                {
                    groups.Add(new CurlArg { IsUrl = true, Value = arg });
                    continue;
                }
                if (arg == "--")
                {
                    endOfOptions = true;
                    continue;
                }

                if (arg.StartsWith("--", StringComparison.Ordinal))
                {
                    int eq = arg.IndexOf('=');
                    if (eq > 2)
                    {
                        string typedName = arg.Substring(2, eq - 2);
                        string val = arg.Substring(eq + 1);
                        string canonical;
                        bool takesValue;
                        ResolveLongOption(typedName, arg, out canonical, out takesValue);
                        groups.Add(new CurlArg("--" + canonical, val));
                        continue;
                    }

                    string longName = arg.Substring(2);
                    string resolved;
                    bool resolvedTakesValue;
                    ResolveLongOption(longName, arg, out resolved, out resolvedTakesValue);
                    if (resolvedTakesValue)
                    {
                        if (i + 1 >= args.Count)
                            throw new CurlParseException("参数 " + arg + " 缺少值。");
                        groups.Add(new CurlArg("--" + resolved, args[++i]));
                        continue;
                    }
                    groups.Add(new CurlArg("--" + resolved, null));
                    continue;
                }

                if (arg.Length > 1 && arg[0] == '-')
                {
                    if (arg.Length == 2)
                    {
                        bool takesValue;
                        if (!CurlOptionMap.TryResolveShort(arg[1], out takesValue))
                            throw new CurlParseException("未知的 curl 短选项 -" + arg[1] + "。");
                        if (takesValue)
                        {
                            if (i + 1 >= args.Count)
                                throw new CurlParseException("参数 " + arg + " 缺少值。");
                            groups.Add(new CurlArg(arg, args[++i]));
                        }
                        else
                        {
                            groups.Add(new CurlArg(arg, null));
                        }
                        continue;
                    }

                    StringBuilder booleanCluster = new StringBuilder("-");
                    bool consumed = false;
                    for (int j = 1; j < arg.Length; j++)
                    {
                        char c = arg[j];
                        bool takesValue;
                        if (!CurlOptionMap.TryResolveShort(c, out takesValue))
                            throw new CurlParseException("未知的 curl 短选项 -" + c + "。");
                        if (takesValue)
                        {
                            string value;
                            if (j + 1 < arg.Length)
                            {
                                value = arg.Substring(j + 1);
                            }
                            else
                            {
                                if (i + 1 >= args.Count)
                                    throw new CurlParseException("参数 -" + c + " 缺少值。");
                                value = args[++i];
                            }
                            if (booleanCluster.Length > 1)
                                groups.Add(new CurlArg(booleanCluster.ToString(), null));
                            groups.Add(new CurlArg("-" + c, value));
                            consumed = true;
                            break;
                        }
                        booleanCluster.Append(c);
                    }
                    if (consumed) continue;
                    groups.Add(new CurlArg(arg, null));
                    continue;
                }

                if (arg == "-")
                    throw new CurlParseException("不支持从标准输入读取数据（单独的 -）。");
                groups.Add(new CurlArg { IsUrl = true, Value = arg });
            }

            return groups;
        }

        private static void ResolveLongOption(string name, string original, out string canonical, out bool takesValue)
        {
            bool ambiguous;
            if (!CurlOptionMap.TryResolveLong(name, out canonical, out takesValue, out ambiguous))
            {
                if (ambiguous)
                    throw new CurlParseException("选项 " + original + " 有歧义，请使用完整名称。");
                throw new CurlParseException("未知的 curl 选项 " + original + "。");
            }
        }

        private static List<CurlArg> Sanitize(List<CurlArg> groups, List<string> warnings)
        {
            List<CurlArg> result = new List<CurlArg>();
            HashSet<string> warningSet = new HashSet<string>();
            bool fileWarning = false;
            bool includeWarning = false;

            foreach (CurlArg group in groups)
            {
                string opt = group.Option ?? string.Empty;
                string lower = opt.ToLowerInvariant();

                if (lower == "--next")
                    throw new CurlParseException("一条命令中包含多个请求（--next）暂不支持，请分开执行。");
                if (lower == "--parallel" || opt == "-Z" || lower == "--parallel-immediate" ||
                    lower == "--parallel-max")
                    throw new CurlParseException("暂不支持并行请求（--parallel），请一次发送一个请求。");
                if (lower == "--config" || opt == "-K" || lower.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
                    throw new CurlParseException("暂不支持读取 curl 配置文件（-K / --config）。");

                if (OutputValueOptions.Contains(opt) || OutputBoolOptions.Contains(opt) ||
                    lower == "--parallel-max")
                {
                    fileWarning = true;
                    continue;
                }
                if (opt == "-i" || lower == "--include")
                {
                    includeWarning = true;
                    continue;
                }

                if (opt.Length > 2 && opt[0] == '-' && opt[1] != '-')
                {
                    for (int j = 1; j < opt.Length; j++)
                    {
                        char c = opt[j];
                        if ("oODcwKJ".IndexOf(c) >= 0)
                            throw new CurlParseException("请将组合短参数 " + opt + " 拆开后重试。");
                    }
                }

                result.Add(group);
            }

            if (fileWarning)
                warningSet.Add("已忽略写文件相关的参数，响应会显示在窗口中。");
            if (includeWarning)
                warningSet.Add("已忽略 -i / --include，响应头会单独展示。");
            warnings.AddRange(warningSet);
            return result;
        }

        private static CurlCommand Analyze(List<CurlArg> groups, List<string> warnings)
        {
            CurlCommand command = new CurlCommand();
            command.Args.AddRange(groups);
            command.Warnings.AddRange(warnings);

            List<CurlArg> urlCandidates = new List<CurlArg>();
            string explicitMethod = null;

            foreach (CurlArg group in groups)
            {
                if (group.IsUrl && group.Value != null)
                {
                    urlCandidates.Add(group);
                }
                else if (group.Option != null &&
                         string.Equals(group.Option, "--url", StringComparison.Ordinal) &&
                         group.Value != null)
                {
                    urlCandidates.Add(group);
                }

                if (group.Option != null &&
                    (string.Equals(group.Option, "-X", StringComparison.Ordinal) ||
                     string.Equals(group.Option, "--request", StringComparison.Ordinal)))
                {
                    explicitMethod = group.Value;
                }
            }

            if (urlCandidates.Count == 0)
                throw new CurlParseException("没有找到请求 URL。");
            if (urlCandidates.Count > 1)
                throw new CurlParseException("一条命令里似乎有多个 URL，请每次只发送一个请求。");

            command.Url = urlCandidates[0].Value;
            command.ExplicitMethod = explicitMethod;

            foreach (CurlArg group in groups)
            {
                if (group.Option != null &&
                    (string.Equals(group.Option, "-H", StringComparison.Ordinal) ||
                     string.Equals(group.Option, "--header", StringComparison.Ordinal)))
                {
                    command.HeaderCount++;
                }

                if (group.Value == null) continue;

                if (DataOptions.Contains(group.Option))
                {
                    if (!command.HasBody || command.BodyKind == null)
                    {
                        if (string.Equals(group.Option, "--json", StringComparison.Ordinal))
                            command.BodyKind = "json";
                        else
                            command.BodyKind = "data";
                    }
                    command.HasBody = true;
                    if (group.Value.StartsWith("@", StringComparison.Ordinal))
                    {
                        command.BodyFromFile = true;
                        command.BodyKind = "file";
                    }
                    if (group.Value == "@-")
                        throw new CurlParseException("不支持从标准输入读取请求体（@-），请改为内联内容或本地文件路径。");
                    command.BodyBytes += Encoding.UTF8.GetByteCount(group.Value);
                }
                else if (FormOptions.Contains(group.Option))
                {
                    command.HasBody = true;
                    command.BodyKind = "form";
                    if (group.Value.StartsWith("@", StringComparison.Ordinal))
                        command.BodyFromFile = true;
                    if (Regex.IsMatch(group.Value, @"(^|=)@-$"))
                        throw new CurlParseException("不支持从标准输入读取请求体（@-），请改为内联内容或本地文件路径。");
                    command.BodyBytes += Encoding.UTF8.GetByteCount(group.Value);
                }
                else if (string.Equals(group.Option, "-T", StringComparison.Ordinal) ||
                         string.Equals(group.Option, "--upload-file", StringComparison.Ordinal))
                {
                    command.HasBody = true;
                    command.BodyKind = "file";
                    command.BodyFromFile = true;
                    if (group.Value == "-")
                        throw new CurlParseException("不支持从标准输入上传（-T -），请改为本地文件路径。");
                }
            }

            if (!command.HasBody)
            {
                foreach (CurlArg group in groups)
                {
                    if (group.Option != null &&
                        (string.Equals(group.Option, "-T", StringComparison.Ordinal) ||
                         string.Equals(group.Option, "--upload-file", StringComparison.Ordinal)))
                    {
                        command.HasBody = true;
                        command.BodyKind = "file";
                        command.BodyFromFile = true;
                    }
                }
            }

            command.Method = InferMethod(groups, explicitMethod, command.HasBody);
            return command;
        }

        private static bool HasOption(List<CurlArg> groups, params string[] options)
        {
            foreach (CurlArg group in groups)
            {
                if (group.Option == null) continue;
                foreach (string option in options)
                {
                    if (string.Equals(group.Option, option, StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }

        private static string InferMethod(List<CurlArg> groups, string explicitMethod, bool hasBody)
        {
            if (!string.IsNullOrEmpty(explicitMethod)) return explicitMethod;
            if (HasOption(groups, "-I", "--head")) return "HEAD";
            if (HasOption(groups, "-G", "--get")) return "GET";
            if (HasOption(groups, "-T", "--upload-file")) return "PUT";
            if (hasBody) return "POST";
            return "GET";
        }
    }

    internal static class CurlUrlEncoder
    {
        public static string EncodeForCurl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            try
            {
                int schemeIndex = url.IndexOf("://", StringComparison.Ordinal);
                string prefix = string.Empty;
                string rest = url;
                if (schemeIndex > 0 && schemeIndex <= 12)
                {
                    prefix = url.Substring(0, schemeIndex + 3);
                    rest = url.Substring(schemeIndex + 3);
                }

                int authEnd = rest.Length;
                for (int i = 0; i < rest.Length; i++)
                {
                    if (rest[i] == '/' || rest[i] == '?' || rest[i] == '#')
                    {
                        authEnd = i;
                        break;
                    }
                }

                string authority = rest.Substring(0, authEnd);
                string tail = rest.Substring(authEnd);

                string userInfo = string.Empty;
                int at = authority.LastIndexOf('@');
                if (at >= 0)
                {
                    userInfo = authority.Substring(0, at + 1);
                    authority = authority.Substring(at + 1);
                }

                string host = authority;
                string port = string.Empty;
                if (!authority.StartsWith("[", StringComparison.Ordinal))
                {
                    int colon = authority.LastIndexOf(':');
                    if (colon > 0 && authority.IndexOf(']') < colon)
                    {
                        host = authority.Substring(0, colon);
                        port = authority.Substring(colon);
                    }
                }

                if (!host.StartsWith("[", StringComparison.Ordinal) && ContainsNonAscii(host) && prefix.Length > 0)
                {
                    try
                    {
                        host = new IdnMapping().GetAscii(host);
                    }
                    catch
                    {
                        // Leave the original hostname; curl will report the failure.
                    }
                }

                return prefix + PercentEncodeNonAscii(userInfo) + host + port + PercentEncodeNonAscii(tail);
            }
            catch
            {
                return url;
            }
        }

        private static bool ContainsNonAscii(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] > 0x7F) return true;
            }
            return false;
        }

        private static string PercentEncodeNonAscii(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            StringBuilder result = new StringBuilder(value.Length + 16);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == ' ')
                {
                    result.Append("%20");
                }
                else if (c <= 0x7F)
                {
                    result.Append(c);
                }
                else
                {
                    int codePoint;
                    if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                    {
                        codePoint = char.ConvertToUtf32(c, value[i + 1]);
                        i++;
                    }
                    else
                    {
                        codePoint = c;
                    }
                    byte[] bytes = Encoding.UTF8.GetBytes(char.ConvertFromUtf32(codePoint));
                    foreach (byte b in bytes)
                        result.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
                }
            }
            return result.ToString();
        }
    }

    internal static class CurlConfigWriter
    {
        public static void Write(IEnumerable<CurlArg> groups, string path)
        {
            StringBuilder builder = new StringBuilder();
            foreach (CurlArg group in groups)
            {
                if (group.IsUrl)
                {
                    builder.Append("url = ").Append(Quote(CurlUrlEncoder.EncodeForCurl(group.Value))).Append('\n');
                    continue;
                }
                if (group.Option == null) continue;

                if (string.Equals(group.Option, "--url", StringComparison.Ordinal))
                {
                    builder.Append("url = ").Append(Quote(CurlUrlEncoder.EncodeForCurl(group.Value))).Append('\n');
                    continue;
                }

                builder.Append(group.Option);
                if (group.Value != null)
                {
                    builder.Append(' ').Append(Quote(group.Value));
                }
                builder.Append('\n');
            }
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        private static string Quote(string value)
        {
            StringBuilder result = new StringBuilder(value.Length + 16);
            result.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': result.Append("\\\\"); break;
                    case '"': result.Append("\\\""); break;
                    case '\n': result.Append("\\n"); break;
                    case '\r': result.Append("\\r"); break;
                    case '\t': result.Append("\\t"); break;
                    case '\v': result.Append("\\v"); break;
                    default: result.Append(c); break;
                }
            }
            result.Append('"');
            return result.ToString();
        }
    }

    internal static class CurlRunner
    {
        private const int MaxTextBody = 4 * 1024 * 1024;
        private const int MaxBinaryBody = 6 * 1024 * 1024;
        private const int MaxCapturedStdout = 2 * 1024 * 1024;
        private const int MaxCapturedStderr = 256 * 1024;
        private const int MaxHeaderFile = 1024 * 1024;
        private const int CurlMaxSeconds = 120;
        private const int HardTimeoutMs = (CurlMaxSeconds + 10) * 1000;

        private static readonly object ActiveLock = new object();
        private static Process _activeProcess;

        public static void CancelActive()
        {
            lock (ActiveLock)
            {
                if (_activeProcess != null)
                {
                    try
                    {
                        if (!_activeProcess.HasExited) _activeProcess.Kill();
                    }
                    catch { }
                }
            }
        }

        public static Task<CurlResult> RunAsync(
            CurlCommand command,
            string overrideMethod,
            bool followRedirects,
            bool insecure,
            bool verbose,
            CancellationToken token)
        {
            return Task.Run(() => RunInternal(command, overrideMethod, followRedirects, insecure, verbose, token));
        }

        private static CurlResult RunInternal(
            CurlCommand command,
            string overrideMethod,
            bool followRedirects,
            bool insecure,
            bool verbose,
            CancellationToken token)
        {
            CurlResult result = new CurlResult();
            result.Method = string.IsNullOrEmpty(overrideMethod) ? command.Method : overrideMethod.ToUpperInvariant();
            result.RequestedUrl = command.Url;
            result.Warnings.AddRange(command.Warnings);

            string tempRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "tmp");
            string tempDir = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempDir);
                string configPath = Path.Combine(tempDir, "request.conf");
                string headerPath = Path.Combine(tempDir, "headers.txt");
                string bodyPath = Path.Combine(tempDir, "body.bin");
                CurlConfigWriter.Write(command.Args, configPath);

                List<string> args = new List<string>();
                args.Add("--config");
                args.Add(configPath);
                if (followRedirects) args.Add("--location");
                if (insecure) args.Add("--insecure");
                if (verbose) args.Add("--verbose");
                if (!string.IsNullOrEmpty(overrideMethod))
                {
                    args.Add("--request");
                    args.Add(overrideMethod.ToUpperInvariant());
                }
                args.Add("--silent");
                args.Add("--show-error");
                args.Add("--dump-header");
                args.Add(headerPath);
                args.Add("--output");
                args.Add(bodyPath);
                args.Add("--write-out");
                args.Add("%{json}");
                args.Add("--max-time");
                args.Add(CurlMaxSeconds.ToString(CultureInfo.InvariantCulture));
                args.Add("--connect-timeout");
                args.Add("20");

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "curl.exe";
                psi.Arguments = BuildArguments(args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
                psi.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                Stopwatch stopwatch = Stopwatch.StartNew();
                Process process = new Process();
                process.StartInfo = psi;
                process.EnableRaisingEvents = true;

                TaskCompletionSource<int> exitSource = new TaskCompletionSource<int>();
                process.Exited += delegate
                {
                    try
                    {
                        exitSource.TrySetResult(process.ExitCode);
                    }
                    catch
                    {
                        exitSource.TrySetResult(-1);
                    }
                };

                try
                {
                    process.Start();
                }
                catch (Exception ex)
                {
                    result.Fatal = true;
                    result.FatalMessage = "无法启动 curl.exe：" + ex.Message;
                    return result;
                }

                lock (ActiveLock) { _activeProcess = process; }

                Task<CapturedText> stdoutTask = Task.Run(() => ReadCapped(process.StandardOutput, MaxCapturedStdout));
                Task<CapturedText> stderrTask = Task.Run(() => ReadCapped(process.StandardError, MaxCapturedStderr));

                CancellationTokenRegistration registration = token.Register(delegate
                {
                    try
                    {
                        if (!process.HasExited) process.Kill();
                    }
                    catch { }
                });

                bool timedOut = false;
                using (CancellationTokenSource timeoutSource = new CancellationTokenSource())
                {
                    Task delayTask = Task.Delay(HardTimeoutMs, timeoutSource.Token);
                    Task completed = Task.WhenAny(exitSource.Task, delayTask).GetAwaiter().GetResult();
                    if (completed != exitSource.Task)
                    {
                        timedOut = true;
                        try
                        {
                            if (!process.HasExited) process.Kill();
                        }
                        catch { }
                    }
                    else
                    {
                        timeoutSource.Cancel();
                    }
                }

                try
                {
                    exitSource.Task.Wait(5000);
                }
                catch { }

                registration.Dispose();

                string stdout = stdoutTask.GetAwaiter().GetResult().Text;
                CapturedText stderr = stderrTask.GetAwaiter().GetResult();
                stopwatch.Stop();

                lock (ActiveLock) { _activeProcess = null; }

                int exitCode = 0;
                try { exitCode = process.ExitCode; }
                catch { exitCode = -1; }
                process.Dispose();

                if (token.IsCancellationRequested)
                {
                    result.Canceled = true;
                    return result;
                }

                result.ExitCode = exitCode;
                result.TimedOut = timedOut;
                result.Stderr = (stderr.Text ?? string.Empty).Trim();
                result.StderrTruncated = stderr.Truncated;

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 16 * 1024 * 1024;
                Dictionary<string, object> meta = null;
                if (!string.IsNullOrEmpty(stdout.Trim()))
                {
                    try { meta = serializer.Deserialize<Dictionary<string, object>>(stdout.Trim()); }
                    catch { meta = null; }
                }

                result.TimeTotal = GetDouble(meta, "time_total");
                result.TimeNamelookup = GetDouble(meta, "time_namelookup");
                result.TimeConnect = GetDouble(meta, "time_connect");
                result.TimeStarttransfer = GetDouble(meta, "time_starttransfer");
                result.DurationMs = result.TimeTotal > 0
                    ? (long)Math.Round(result.TimeTotal * 1000)
                    : stopwatch.ElapsedMilliseconds;

                string rawHeaders = string.Empty;
                try
                {
                    if (File.Exists(headerPath))
                    {
                        long headerLength = new FileInfo(headerPath).Length;
                        int headerRead = (int)Math.Min(headerLength, MaxHeaderFile);
                        rawHeaders = ReadTextChunk(headerPath, headerRead, Encoding.GetEncoding(28591));
                    }
                }
                catch { }

                result.RawHeaders = rawHeaders;
                ParseHeaders(rawHeaders, result);

                result.StatusCode = GetInt(meta, "response_code");
                if (result.StatusCode == 0 && result.FinalHeaders != null)
                    result.StatusCode = result.FinalHeaders.Status;
                result.EffectiveUrl = GetString(meta, "url_effective");
                if (string.IsNullOrEmpty(result.EffectiveUrl)) result.EffectiveUrl = command.Url;
                result.ContentType = result.FinalHeaders != null ? result.FinalHeaders.GetHeader("content-type") : null;
                if (string.IsNullOrEmpty(result.ContentType)) result.ContentType = GetString(meta, "content_type");
                result.SizeDownload = GetLong(meta, "size_download");

                ReadBody(bodyPath, result);
                if (result.Method == "HEAD")
                {
                    result.BodyKind = "empty";
                    result.BodyText = null;
                    result.BodyBytes = null;
                    result.BodyTotalBytes = 0;
                    result.BodyTruncated = false;
                    result.HeadOnly = true;
                }

                result.Hint = HintForExit(exitCode);
            }
            catch (Exception ex)
            {
                if (result.Canceled) return result;
                result.Fatal = true;
                result.FatalMessage = "读取响应失败：" + ex.Message;
            }
            finally
            {
                lock (ActiveLock) { _activeProcess = null; }
                try
                {
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                }
                catch { }
            }

            return result;
        }

        private static string BuildArguments(List<string> args)
        {
            StringBuilder builder = new StringBuilder();
            foreach (string arg in args)
            {
                if (builder.Length > 0) builder.Append(' ');
                builder.Append('"');
                foreach (char c in arg)
                {
                    if (c == '"') builder.Append('\\');
                    builder.Append(c);
                }
                builder.Append('"');
            }
            return builder.ToString();
        }

        private static CapturedText ReadCapped(StreamReader reader, int limit)
        {
            CapturedText captured = new CapturedText();
            StringBuilder builder = new StringBuilder();
            char[] buffer = new char[8192];
            bool truncated = false;
            try
            {
                int read;
                while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (builder.Length >= limit)
                    {
                        truncated = true;
                        continue;
                    }
                    int take = Math.Min(read, limit - builder.Length);
                    builder.Append(buffer, 0, take);
                    if (take < read) truncated = true;
                }
            }
            catch { }
            captured.Text = builder.ToString();
            captured.Truncated = truncated;
            return captured;
        }

        private static void ParseHeaders(string raw, CurlResult result)
        {
            if (string.IsNullOrEmpty(raw)) return;
            string normalized = raw.Replace("\r\n", "\n").Trim();
            if (normalized.Length == 0) return;
            string[] chunks = Regex.Split(normalized, "\n\n+");

            foreach (string chunk in chunks)
            {
                string[] lines = chunk.Split('\n');
                if (lines.Length == 0) continue;
                Match match = Regex.Match(lines[0], @"^HTTP/\S+\s+(\d{3})(?:\s+(.*))?$");
                if (!match.Success) continue;

                HeaderBlock block = new HeaderBlock();
                block.StatusLine = lines[0];
                block.Status = Convert.ToInt32(match.Groups[1].Value, CultureInfo.InvariantCulture);
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.Length == 0) continue;
                    if ((line[0] == ' ' || line[0] == '\t') && block.Headers.Count > 0)
                    {
                        block.Headers[block.Headers.Count - 1].Value += " " + line.Trim();
                        continue;
                    }
                    int colon = line.IndexOf(':');
                    if (colon <= 0) continue;
                    block.Headers.Add(new HeaderItem(line.Substring(0, colon).Trim(), line.Substring(colon + 1).Trim()));
                }
                result.HeaderBlocks.Add(block);
            }

            if (result.HeaderBlocks.Count == 0) return;
            result.FinalHeaders = result.HeaderBlocks[result.HeaderBlocks.Count - 1];

            foreach (HeaderBlock block in result.HeaderBlocks)
            {
                if (block.Status >= 300 && block.Status < 400)
                {
                    string location = block.GetHeader("location");
                    if (!string.IsNullOrEmpty(location))
                    {
                        RedirectInfo redirect = new RedirectInfo();
                        redirect.Status = block.Status;
                        redirect.Location = location;
                        result.Redirects.Add(redirect);
                    }
                }
            }
        }

        private static void ReadBody(string bodyPath, CurlResult result)
        {
            if (!File.Exists(bodyPath)) return;
            FileInfo info = new FileInfo(bodyPath);
            result.BodyTotalBytes = info.Length;
            if (info.Length == 0)
            {
                result.BodyKind = "empty";
                return;
            }

            string contentType = result.ContentType ?? string.Empty;
            byte[] sample = ReadBytesChunk(bodyPath, (int)Math.Min(info.Length, 64 * 1024));
            bool binary = LooksBinary(sample, contentType);
            string mime = contentType.Split(';')[0].Trim();
            if (mime.Length == 0) mime = "application/octet-stream";
            result.Mime = mime;

            if (binary)
            {
                result.BodyKind = mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? "image" : "binary";
                if (info.Length <= MaxBinaryBody)
                {
                    result.BodyBytes = ReadBytesChunk(bodyPath, (int)info.Length);
                }
                else
                {
                    result.BodyTruncated = true;
                }
                return;
            }

            int readLength = (int)Math.Min(info.Length, MaxTextBody);
            byte[] bytes = ReadBytesChunk(bodyPath, readLength);
            result.BodyKind = "text";
            result.BodyText = DecodeText(bytes, contentType);
            result.BodyTruncated = readLength < info.Length;
        }

        private static bool LooksBinary(byte[] sample, string contentType)
        {
            string type = contentType ?? string.Empty;
            if (Regex.IsMatch(type, @"charset\s*=\s*[""']?utf-16|charset\s*=\s*[""']?utf-32", RegexOptions.IgnoreCase))
                return false;
            if (type.IndexOf("svg", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (Regex.IsMatch(type, @"^(image|audio|video|font)/", RegexOptions.IgnoreCase))
                return true;
            if (Regex.IsMatch(type, @"application/(zip|pdf|octet-stream|protobuf|msgpack|wasm|gzip|zstd|epub|x-tar|x-7z-compressed|x-rar-compressed|vnd\.rar|msword|vnd\.ms(excel|powerpoint|word)|vnd\.openxmlformats)", RegexOptions.IgnoreCase))
                return true;
            if (Regex.IsMatch(type, @"charset=binary", RegexOptions.IgnoreCase))
                return true;

            int control = 0;
            int count = Math.Min(sample.Length, 64 * 1024);
            for (int i = 0; i < count; i++)
            {
                byte b = sample[i];
                if (b == 0) return true;
                if (b < 7 || (b > 13 && b < 32)) control++;
            }
            return count > 0 && (double)control / count > 0.08;
        }

        private static string DecodeText(byte[] bytes, string contentType)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;

            Encoding encoding = null;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                encoding = new UTF8Encoding(false);
                byte[] trimmed = new byte[bytes.Length - 3];
                Buffer.BlockCopy(bytes, 3, trimmed, 0, trimmed.Length);
                return encoding.GetString(trimmed);
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

            Match match = Regex.Match(contentType ?? string.Empty, @"charset\s*=\s*[""']?([\w.-]+)", RegexOptions.IgnoreCase);
            string charset = match.Success ? match.Groups[1].Value : "utf-8";
            try
            {
                encoding = Encoding.GetEncoding(charset);
            }
            catch
            {
                encoding = new UTF8Encoding(false);
            }
            return encoding.GetString(bytes);
        }

        private static byte[] ReadBytesChunk(string path, int length)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] buffer = new byte[length];
                int offset = 0;
                while (offset < length)
                {
                    int read = stream.Read(buffer, offset, length - offset);
                    if (read <= 0) break;
                    offset += read;
                }
                if (offset == buffer.Length) return buffer;
                byte[] trimmed = new byte[offset];
                Buffer.BlockCopy(buffer, 0, trimmed, 0, offset);
                return trimmed;
            }
        }

        private static string ReadTextChunk(string path, int length, Encoding encoding)
        {
            byte[] bytes = ReadBytesChunk(path, length);
            return encoding.GetString(bytes);
        }

        private static string GetString(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.ContainsKey(key) || data[key] == null) return null;
            return Convert.ToString(data[key], CultureInfo.InvariantCulture);
        }

        private static int GetInt(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.ContainsKey(key) || data[key] == null) return 0;
            int value;
            return int.TryParse(Convert.ToString(data[key], CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out value) ? value : 0;
        }

        private static long GetLong(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.ContainsKey(key) || data[key] == null) return 0;
            long value;
            return long.TryParse(Convert.ToString(data[key], CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out value) ? value : 0;
        }

        private static double GetDouble(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.ContainsKey(key) || data[key] == null) return 0;
            double value;
            return double.TryParse(Convert.ToString(data[key], CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out value) ? value : 0;
        }

        private static string HintForExit(int code)
        {
            switch (code)
            {
                case 1: return "不支持的协议";
                case 2: return "curl 初始化失败";
                case 3: return "URL 格式不正确";
                case 5: return "代理服务器无法解析";
                case 6: return "域名解析失败，检查网络或域名";
                case 7: return "无法连接服务器，检查地址、端口或网络";
                case 18: return "响应传输提前中断";
                case 22: return "HTTP 返回了错误状态码（命令中可能带了 --fail）";
                case 26: return "读取本地文件失败";
                case 28: return "请求超时";
                case 35: return "TLS 握手失败";
                case 51: return "服务器证书校验失败";
                case 52: return "服务器没有返回任何内容";
                case 55: return "发送数据失败";
                case 56: return "接收数据失败";
                case 58: return "本地证书有问题";
                case 60: return "证书不受信任，可勾选“忽略证书”";
                case 77: return "读取本地证书文件失败";
                default: return null;
            }
        }
    }
}

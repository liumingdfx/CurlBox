using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CurlBox
{
    public sealed class BuilderRow
    {
        public bool Enabled { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }

        public BuilderRow()
        {
            Enabled = true;
            Key = string.Empty;
            Value = string.Empty;
        }

        public BuilderRow Clone()
        {
            return new BuilderRow { Enabled = Enabled, Key = Key ?? string.Empty, Value = Value ?? string.Empty };
        }
    }

    public sealed class BuilderExtraArg
    {
        public string Option { get; set; }
        public string Value { get; set; }

        public BuilderExtraArg()
        {
            Option = string.Empty;
        }

        public BuilderExtraArg Clone()
        {
            return new BuilderExtraArg { Option = Option, Value = Value };
        }
    }

    public sealed class BuilderDraft
    {
        public bool Active { get; set; }
        public string Method { get; set; }
        public string Url { get; set; }
        public string BodyMode { get; set; }
        public string BodyText { get; set; }
        public int ActiveTab { get; set; }
        public bool FollowRedirects { get; set; }
        public bool Insecure { get; set; }
        public bool Verbose { get; set; }
        public List<BuilderRow> Params { get; set; }
        public List<BuilderRow> Headers { get; set; }
        public List<BuilderRow> Form { get; set; }
        public List<BuilderExtraArg> ExtraArgs { get; set; }

        public BuilderDraft()
        {
            Method = "GET";
            Url = string.Empty;
            BodyMode = "none";
            BodyText = string.Empty;
            Params = new List<BuilderRow>();
            Headers = new List<BuilderRow>();
            Form = new List<BuilderRow>();
            ExtraArgs = new List<BuilderExtraArg>();
        }
    }

    internal sealed class BuilderRequest
    {
        public string Method;
        public string Url;
        public string BodyMode;
        public string BodyText;
        public List<BuilderRow> Params;
        public List<BuilderRow> Headers;
        public List<BuilderRow> Form;
        public List<BuilderExtraArg> ExtraArgs;
    }

    internal static class CurlBuilder
    {
        private static readonly Regex MethodPattern = new Regex(@"^[A-Za-z][A-Za-z0-9!#$%&'*+.^_`|~-]{0,31}$", RegexOptions.Compiled);
        private static readonly Regex HeaderNamePattern = new Regex(@"^[!#$%&'*+.^_`|~0-9A-Za-z-]+$", RegexOptions.Compiled);
        private static readonly Regex SchemePattern = new Regex(@"^[A-Za-z][A-Za-z0-9+.-]*://", RegexOptions.Compiled);

        public static string Build(BuilderRequest request, out string error)
        {
            error = null;
            if (request == null)
            {
                error = "请求内容为空。";
                return null;
            }

            string method = (request.Method ?? "GET").Trim().ToUpperInvariant();
            if (!MethodPattern.IsMatch(method))
            {
                error = "请求方法格式不正确。";
                return null;
            }

            string url = request.Url == null ? string.Empty : request.Url.Trim();
            if (url.Length == 0)
            {
                error = "请先填写请求 URL。";
                return null;
            }
            if (url.IndexOf('\r') >= 0 || url.IndexOf('\n') >= 0 || url.IndexOf('\0') >= 0)
            {
                error = "URL 中不能包含换行或空字符。";
                return null;
            }
            if (!SchemePattern.IsMatch(url))
            {
                if (url.StartsWith("//", StringComparison.Ordinal)) url = "http:" + url;
                else url = "http://" + url;
            }

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                string.IsNullOrEmpty(uri.Host))
            {
                error = "URL 格式不正确，只支持 http / https 地址。";
                return null;
            }

            url = AppendQuery(url, request.Params);

            List<BuilderRow> headerRows = request.Headers ?? new List<BuilderRow>();
            List<string> headers = new List<string>();
            bool hasContentType = false;
            foreach (BuilderRow row in headerRows)
            {
                if (row == null || !row.Enabled) continue;
                string name = (row.Key ?? string.Empty).Trim();
                string value = row.Value ?? string.Empty;
                if (name.Length == 0) continue;
                if (value.Length == 0 && name.IndexOf(':') > 0)
                {
                    int colon = name.IndexOf(':');
                    value = name.Substring(colon + 1).Trim();
                    name = name.Substring(0, colon).Trim();
                }
                if (!HeaderNamePattern.IsMatch(name))
                {
                    error = "请求头名称不合法：" + name;
                    return null;
                }
                if (value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0)
                {
                    error = "请求头 " + name + " 的值中包含换行。";
                    return null;
                }
                value = value.Trim();
                if (string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase))
                    hasContentType = true;
                headers.Add(name + ": " + value);
            }

            string bodyMode = NormalizeBodyMode(request.BodyMode);
            string body = null;
            if (bodyMode == "json" || bodyMode == "raw")
            {
                body = request.BodyText ?? string.Empty;
            }
            else if (bodyMode == "form")
            {
                body = EncodeForm(request.Form);
                if (body.Length == 0) body = null;
            }

            if (method == "HEAD" && body != null)
            {
                error = "HEAD 请求不能包含请求体。";
                return null;
            }

            if (body != null && !hasContentType)
            {
                if (bodyMode == "json") headers.Add("Content-Type: application/json");
                else if (bodyMode == "form") headers.Add("Content-Type: application/x-www-form-urlencoded");
                else headers.Add("Content-Type: text/plain; charset=utf-8");
            }

            StringBuilder command = new StringBuilder();
            if (method == "HEAD")
                command.Append("curl -I ").Append(Quote(url));
            else
                command.Append("curl -X ").Append(method).Append(' ').Append(Quote(url));

            foreach (string header in headers)
                command.Append(" -H ").Append(Quote(header));

            if (body != null)
                command.Append(" --data-raw ").Append(Quote(body));

            if (request.ExtraArgs != null)
            {
                foreach (BuilderExtraArg extra in request.ExtraArgs)
                {
                    if (extra == null || string.IsNullOrEmpty(extra.Option)) continue;
                    command.Append(' ').Append(extra.Option);
                    if (extra.Value != null)
                        command.Append(' ').Append(Quote(extra.Value));
                }
            }

            return command.ToString();
        }

        public static string NormalizeBodyMode(string mode)
        {
            string value = (mode ?? string.Empty).Trim().ToLowerInvariant();
            if (value == "json" || value == "form" || value == "raw" || value == "none") return value;
            return "none";
        }

        public static bool IsValidMethod(string method)
        {
            return MethodPattern.IsMatch((method ?? string.Empty).Trim());
        }

        public static int ActiveCount(List<BuilderRow> rows)
        {
            if (rows == null) return 0;
            int count = 0;
            foreach (BuilderRow row in rows)
            {
                if (row != null && row.Enabled && !string.IsNullOrWhiteSpace(row.Key)) count++;
            }
            return count;
        }

        private static string AppendQuery(string url, List<BuilderRow> rows)
        {
            if (rows == null || rows.Count == 0) return url;
            List<string> pairs = new List<string>();
            foreach (BuilderRow row in rows)
            {
                if (row == null || !row.Enabled) continue;
                string key = (row.Key ?? string.Empty).Trim();
                if (key.Length == 0) continue;
                pairs.Add(PercentEncode(key) + "=" + PercentEncode(row.Value ?? string.Empty));
            }
            if (pairs.Count == 0) return url;

            string fragment = string.Empty;
            int hash = url.IndexOf('#');
            if (hash >= 0)
            {
                fragment = url.Substring(hash);
                url = url.Substring(0, hash);
            }

            StringBuilder builder = new StringBuilder(url);
            if (url.IndexOf('?') < 0) builder.Append('?');
            else if (!url.EndsWith("?", StringComparison.Ordinal) && !url.EndsWith("&", StringComparison.Ordinal))
                builder.Append('&');
            for (int i = 0; i < pairs.Count; i++)
            {
                if (i > 0) builder.Append('&');
                builder.Append(pairs[i]);
            }
            builder.Append(fragment);
            return builder.ToString();
        }

        private static string EncodeForm(List<BuilderRow> rows)
        {
            if (rows == null) return string.Empty;
            StringBuilder builder = new StringBuilder();
            foreach (BuilderRow row in rows)
            {
                if (row == null || !row.Enabled) continue;
                string key = (row.Key ?? string.Empty).Trim();
                if (key.Length == 0) continue;
                if (builder.Length > 0) builder.Append('&');
                builder.Append(PercentEncode(key)).Append('=').Append(PercentEncode(row.Value ?? string.Empty));
            }
            return builder.ToString();
        }

        public static string PercentEncode(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            StringBuilder builder = new StringBuilder(bytes.Length * 3);
            foreach (byte b in bytes)
            {
                char c = (char)b;
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ||
                    c == '-' || c == '.' || c == '_' || c == '~')
                {
                    builder.Append(c);
                }
                else
                {
                    builder.Append('%');
                    builder.Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            return builder.ToString();
        }

        public static string Quote(string value)
        {
            return "'" + (value ?? string.Empty).Replace("'", "'\\''") + "'";
        }
    }

    internal static class CurlImporter
    {
        private static readonly HashSet<string> HeaderOptions = new HashSet<string>(new[] { "-H", "--header" }, StringComparer.Ordinal);
        private static readonly HashSet<string> DataOptions = new HashSet<string>(new[]
        {
            "-d", "--data", "--data-ascii", "--data-raw", "--data-binary", "--data-urlencode", "--json"
        }, StringComparer.Ordinal);
        private static readonly HashSet<string> FormOptions = new HashSet<string>(new[] { "-F", "--form", "--form-string" }, StringComparer.Ordinal);
        private static readonly HashSet<string> SkipOptions = new HashSet<string>(new[]
        {
            "-X", "--request", "-I", "--head", "-G", "--get",
            "-L", "--location", "-k", "--insecure", "-v", "--verbose",
            "--url"
        }, StringComparer.Ordinal);

        public static bool TryImport(string commandText, out BuilderDraft draft, out string message)
        {
            draft = null;
            message = null;
            if (string.IsNullOrWhiteSpace(commandText))
            {
                message = "没有可导入的 cURL。";
                return false;
            }

            CurlCommand parsed;
            try
            {
                parsed = CurlParser.Parse(commandText);
            }
            catch (CurlParseException ex)
            {
                message = ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                message = "无法解析 cURL：" + ex.Message;
                return false;
            }

            BuilderDraft result = new BuilderDraft();
            result.Method = string.IsNullOrEmpty(parsed.Method) ? "GET" : parsed.Method.ToUpperInvariant();

            string url = parsed.Url ?? string.Empty;
            List<string> dataValues = new List<string>();
            bool dataFromFile = false;
            bool multipart = false;
            bool uploadFile = false;
            bool isGet = false;

            foreach (CurlArg arg in parsed.Args)
            {
                if (arg == null || arg.IsUrl) continue;
                string option = arg.Option ?? string.Empty;
                string value = arg.Value;

                if (HeaderOptions.Contains(option))
                {
                    if (value != null)
                    {
                        string name = value;
                        string headerValue = string.Empty;
                        int colon = value.IndexOf(':');
                        if (colon >= 0)
                        {
                            name = value.Substring(0, colon).Trim();
                            headerValue = value.Substring(colon + 1).Trim();
                        }
                        else if (name.EndsWith(";", StringComparison.Ordinal))
                        {
                            name = name.TrimEnd(';');
                        }
                        if (name.Length > 0)
                            result.Headers.Add(new BuilderRow { Enabled = true, Key = name, Value = headerValue });
                    }
                    continue;
                }

                if (DataOptions.Contains(option))
                {
                    if (value == null) continue;
                    if (value.StartsWith("@", StringComparison.Ordinal))
                    {
                        dataFromFile = true;
                        continue;
                    }
                    dataValues.Add(value);
                    continue;
                }

                if (FormOptions.Contains(option))
                {
                    multipart = true;
                    continue;
                }

                if (option == "-T" || option == "--upload-file")
                {
                    uploadFile = true;
                    continue;
                }

                if (option == "-X" || option == "--request")
                {
                    if (!string.IsNullOrEmpty(value)) result.Method = value.Trim().ToUpperInvariant();
                    continue;
                }

                if (option == "-I" || option == "--head")
                {
                    result.Method = "HEAD";
                    continue;
                }

                if (option == "-G" || option == "--get")
                {
                    isGet = true;
                    continue;
                }

                if (option == "-L" || option == "--location")
                {
                    result.FollowRedirects = true;
                    continue;
                }

                if (option == "-k" || option == "--insecure")
                {
                    result.Insecure = true;
                    continue;
                }

                if (option == "-v" || option == "--verbose")
                {
                    result.Verbose = true;
                    continue;
                }

                if (SkipOptions.Contains(option)) continue;

                result.ExtraArgs.Add(new BuilderExtraArg { Option = option, Value = value });
            }

            if (isGet && dataValues.Count > 0)
            {
                string joined = string.Join("&", dataValues.ToArray());
                url = AppendRawQuery(url, joined);
                dataValues.Clear();
            }

            SplitQuery(url, out url, result.Params);
            result.Url = url;

            bool hasJsonOption = false;
            foreach (CurlArg arg in parsed.Args)
            {
                if (arg != null && string.Equals(arg.Option, "--json", StringComparison.Ordinal))
                {
                    hasJsonOption = true;
                    break;
                }
            }

            if (dataValues.Count > 0)
            {
                string body = string.Join("&", dataValues.ToArray());
                result.BodyText = body;
                string contentType = FindHeader(result.Headers, "Content-Type");
                if (hasJsonOption || LooksLikeJson(body) ||
                    (contentType != null && contentType.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0))
                    result.BodyMode = "json";
                else
                    result.BodyMode = "raw";
            }

            if (result.Method == "HEAD")
                result.BodyMode = "none";

            if (dataFromFile || multipart || uploadFile)
                message = "已导入基础请求；文件或 multipart 请求体未转换，可继续用 cURL 模式发送";
            else
                message = "已转换为请求";

            draft = result;
            return true;
        }

        private static string FindHeader(List<BuilderRow> rows, string name)
        {
            foreach (BuilderRow row in rows)
            {
                if (row != null && row.Enabled && string.Equals((row.Key ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase))
                    return row.Value ?? string.Empty;
            }
            return null;
        }

        private static bool LooksLikeJson(string text)
        {
            string trimmed = (text ?? string.Empty).Trim();
            return (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal)) ||
                   (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal));
        }

        private static string AppendRawQuery(string url, string query)
        {
            if (string.IsNullOrEmpty(query)) return url;
            string fragment = string.Empty;
            int hash = url.IndexOf('#');
            if (hash >= 0)
            {
                fragment = url.Substring(hash);
                url = url.Substring(0, hash);
            }
            StringBuilder builder = new StringBuilder(url);
            if (url.IndexOf('?') < 0) builder.Append('?');
            else if (!url.EndsWith("?", StringComparison.Ordinal) && !url.EndsWith("&", StringComparison.Ordinal)) builder.Append('&');
            builder.Append(query);
            builder.Append(fragment);
            return builder.ToString();
        }

        private static void SplitQuery(string url, out string baseUrl, List<BuilderRow> rows)
        {
            baseUrl = url ?? string.Empty;
            if (rows == null) return;

            string fragment = string.Empty;
            int hash = baseUrl.IndexOf('#');
            if (hash >= 0)
            {
                fragment = baseUrl.Substring(hash);
                baseUrl = baseUrl.Substring(0, hash);
            }

            int question = baseUrl.IndexOf('?');
            if (question < 0)
            {
                baseUrl += fragment;
                return;
            }

            string query = baseUrl.Substring(question + 1);
            baseUrl = baseUrl.Substring(0, question) + fragment;
            if (query.Length == 0) return;

            string[] parts = query.Split('&');
            foreach (string part in parts)
            {
                if (part.Length == 0) continue;
                int equals = part.IndexOf('=');
                string key = equals >= 0 ? part.Substring(0, equals) : part;
                string value = equals >= 0 ? part.Substring(equals + 1) : string.Empty;
                key = QueryDecode(key);
                value = QueryDecode(value);
                if (key.Length == 0) continue;
                rows.Add(new BuilderRow { Enabled = true, Key = key, Value = value });
            }
        }

        private static string QueryDecode(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            try
            {
                return Uri.UnescapeDataString(value.Replace("+", " "));
            }
            catch
            {
                return value;
            }
        }
    }

    internal sealed class KeyValueEditor : Panel
    {
        private sealed class RowUi
        {
            public BuilderRow Model;
            public CardPanel Card;
            public RoundButton Enabled;
            public TextBox KeyBox;
            public TextBox ValueBox;
            public RoundButton Remove;
        }

        private readonly Panel _scroll;
        private readonly RoundButton _addButton;
        private readonly Label _note;
        private readonly List<RowUi> _rows = new List<RowUi>();
        private readonly string _keyCue;
        private readonly string _valueCue;
        private readonly string _addText;
        private readonly string _noteText;

        public event EventHandler Changed;

        public KeyValueEditor(string addText, string keyCue, string valueCue, string noteText)
        {
            _addText = addText;
            _keyCue = keyCue;
            _valueCue = valueCue;
            _noteText = noteText;
            BackColor = Theme.Input;

            _scroll = new Panel();
            _scroll.BackColor = Theme.Input;
            _scroll.AutoScroll = true;
            _scroll.HandleCreated += delegate { NativeMethods.EnableLightScrollbars(_scroll); };
            Controls.Add(_scroll);

            _addButton = new RoundButton();
            _addButton.Text = addText;
            _addButton.Size = new Size(86, 24);
            _addButton.CornerRadius = 5;
            _addButton.FillColor = Color.Transparent;
            _addButton.FillColor2 = Color.Transparent;
            _addButton.HoverColor = Theme.Hover;
            _addButton.BorderColor = Color.Transparent;
            _addButton.TextColor = Theme.Accent;
            _addButton.Font = Theme.Ui(8f);
            _addButton.BackColor = Theme.Input;
            _addButton.Click += delegate { AddRow(true); };
            Controls.Add(_addButton);

            _note = new Label();
            _note.Text = noteText;
            _note.Font = Theme.Ui(7.5f);
            _note.ForeColor = Theme.Faint;
            _note.BackColor = Theme.Input;
            _note.AutoSize = false;
            _note.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(_note);

            SuspendLayout();
            AddRow(false);
            ResumeLayout(false);
        }

        public int ActiveCount
        {
            get { return CurlBuilder.ActiveCount(Snapshot()); }
        }

        public List<BuilderRow> Snapshot()
        {
            List<BuilderRow> rows = new List<BuilderRow>();
            foreach (RowUi row in _rows)
                rows.Add(row.Model.Clone());
            return rows;
        }

        public List<BuilderRow> ActiveRows()
        {
            List<BuilderRow> rows = new List<BuilderRow>();
            foreach (RowUi row in _rows)
            {
                if (row.Model.Enabled && !string.IsNullOrWhiteSpace(row.Model.Key))
                    rows.Add(row.Model.Clone());
            }
            return rows;
        }

        public void SetRows(List<BuilderRow> rows)
        {
            SuspendLayout();
            foreach (RowUi row in _rows)
            {
                ControlsRemoveRow(row);
            }
            _rows.Clear();

            if (rows != null)
            {
                foreach (BuilderRow row in rows)
                {
                    if (row == null) continue;
                    AddRowUi(row.Enabled, row.Key, row.Value);
                }
            }
            if (_rows.Count == 0) AddRowUi(true, string.Empty, string.Empty);
            ResumeLayout(false);
            LayoutRows();
            RaiseChanged();
        }

        public void Clear()
        {
            SetRows(null);
        }

        private void ControlsRemoveRow(RowUi row)
        {
            if (row == null) return;
            _scroll.Controls.Remove(row.Card);
            row.Card.Dispose();
        }

        public void AddRow(bool focus)
        {
            RowUi row = AddRowUi(true, string.Empty, string.Empty);
            LayoutRows();
            if (focus && row != null)
            {
                row.KeyBox.Focus();
                if (_scroll.IsHandleCreated)
                    _scroll.ScrollControlIntoView(row.Card);
            }
        }

        private RowUi AddRowUi(bool enabled, string key, string value)
        {
            RowUi row = new RowUi();
            row.Model = new BuilderRow { Enabled = enabled, Key = key ?? string.Empty, Value = value ?? string.Empty };

            row.Card = new CardPanel();
            row.Card.CornerRadius = 5;
            row.Card.FillColor = Theme.Input;
            row.Card.BorderColor = Theme.BorderSoft;
            _scroll.Controls.Add(row.Card);

            row.Enabled = new RoundButton();
            row.Enabled.Size = new Size(20, 20);
            row.Enabled.CornerRadius = 4;
            row.Enabled.FillColor2 = row.Enabled.FillColor;
            row.Enabled.HoverColor = Theme.Hover;
            row.Enabled.BorderColor = Theme.Border;
            row.Enabled.Font = Theme.Ui(7.5f);
            row.Enabled.BackColor = Theme.Input;
            row.Enabled.Click += delegate
            {
                row.Model.Enabled = !row.Model.Enabled;
                ApplyEnabledStyle(row);
                RaiseChanged();
            };
            row.Card.Controls.Add(row.Enabled);

            row.KeyBox = new TextBox();
            row.KeyBox.BorderStyle = BorderStyle.None;
            row.KeyBox.BackColor = Theme.Input;
            row.KeyBox.ForeColor = Theme.TextSoft;
            row.KeyBox.Font = Theme.Mono(8.5f);
            row.KeyBox.Text = row.Model.Key;
            row.KeyBox.HandleCreated += delegate { NativeMethods.SetCueBanner(row.KeyBox, _keyCue); };
            row.KeyBox.TextChanged += delegate
            {
                row.Model.Key = row.KeyBox.Text;
                RaiseChanged();
            };
            row.KeyBox.Enter += delegate
            {
                row.Card.BorderColor = Color.FromArgb(214, 154, 94);
                row.Card.Invalidate();
            };
            row.KeyBox.Leave += delegate
            {
                row.Card.BorderColor = Theme.BorderSoft;
                row.Card.Invalidate();
                SplitPastedHeader(row);
            };
            row.Card.Controls.Add(row.KeyBox);

            row.ValueBox = new TextBox();
            row.ValueBox.BorderStyle = BorderStyle.None;
            row.ValueBox.BackColor = Theme.Input;
            row.ValueBox.ForeColor = Theme.TextSoft;
            row.ValueBox.Font = Theme.Mono(8.5f);
            row.ValueBox.Text = row.Model.Value;
            row.ValueBox.HandleCreated += delegate { NativeMethods.SetCueBanner(row.ValueBox, _valueCue); };
            row.ValueBox.TextChanged += delegate
            {
                row.Model.Value = row.ValueBox.Text;
                RaiseChanged();
            };
            row.ValueBox.Enter += delegate
            {
                row.Card.BorderColor = Color.FromArgb(214, 154, 94);
                row.Card.Invalidate();
            };
            row.ValueBox.Leave += delegate
            {
                row.Card.BorderColor = Theme.BorderSoft;
                row.Card.Invalidate();
            };
            row.Card.Controls.Add(row.ValueBox);

            row.Remove = new RoundButton();
            row.Remove.Text = "×";
            row.Remove.Size = new Size(20, 20);
            row.Remove.CornerRadius = 4;
            row.Remove.FillColor = Color.Transparent;
            row.Remove.FillColor2 = Color.Transparent;
            row.Remove.HoverColor = Theme.ErrorSurface;
            row.Remove.BorderColor = Color.Transparent;
            row.Remove.TextColor = Theme.Faint;
            row.Remove.Font = Theme.Ui(9f);
            row.Remove.BackColor = Theme.Input;
            row.Remove.Click += delegate { RemoveRow(row); };
            row.Card.Controls.Add(row.Remove);

            ApplyEnabledStyle(row);
            _rows.Add(row);
            return row;
        }

        private void SplitPastedHeader(RowUi row)
        {
            if (row == null) return;
            if ((row.Model.Value ?? string.Empty).Length > 0) return;
            string key = row.Model.Key ?? string.Empty;
            int colon = key.IndexOf(':');
            if (colon <= 0) return;
            string name = key.Substring(0, colon).Trim();
            string value = key.Substring(colon + 1).Trim();
            if (name.Length == 0) return;
            row.KeyBox.Text = name;
            row.ValueBox.Text = value;
        }

        private void ApplyEnabledStyle(RowUi row)
        {
            bool enabled = row.Model.Enabled;
            row.Enabled.Text = enabled ? "✓" : "—";
            row.Enabled.TextColor = enabled ? Theme.Mint : Theme.Faint;
            row.Enabled.FillColor = enabled ? Color.FromArgb(232, 247, 241) : Color.Transparent;
            row.Enabled.FillColor2 = row.Enabled.FillColor;
            row.Enabled.BorderColor = enabled ? Color.FromArgb(190, 230, 218) : Theme.BorderSoft;
            row.KeyBox.ForeColor = enabled ? Theme.TextSoft : Theme.Faint;
            row.ValueBox.ForeColor = enabled ? Theme.TextSoft : Theme.Faint;
        }

        private void RemoveRow(RowUi row)
        {
            if (row == null) return;
            _rows.Remove(row);
            ControlsRemoveRow(row);
            if (_rows.Count == 0) AddRowUi(true, string.Empty, string.Empty);
            LayoutRows();
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            EventHandler handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            LayoutRows();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) LayoutRows();
        }

        private void LayoutRows()
        {
            if (_scroll == null || _addButton == null) return;
            int width = Width;
            int height = Height;
            if (width <= 0 || height <= 0) return;

            _scroll.SetBounds(0, 0, width, Math.Max(10, height - 28));
            _addButton.SetBounds(2, Math.Max(0, height - 26), _addButton.Width, 24);
            _note.SetBounds(_addButton.Right + 4, Math.Max(0, height - 26), Math.Max(10, width - _addButton.Right - 8), 24);
            _note.Text = _rows.Count > 1 ? _noteText : string.Empty;

            int top = 2;
            int scrollWidth = _scroll.Width;
            bool needBar = _rows.Count * 32 + 4 > _scroll.ClientSize.Height;
            int usable = Math.Max(80, scrollWidth - 6 - (needBar ? SystemInformation.VerticalScrollBarWidth : 0));
            foreach (RowUi row in _rows)
            {
                row.Card.SetBounds(2, top, usable, 28);
                int removeX = Math.Max(24, row.Card.Width - 25);
                row.Enabled.SetBounds(5, 4, 20, 20);
                row.Remove.SetBounds(removeX, 4, 20, 20);
                int fieldsLeft = 30;
                int fieldsWidth = Math.Max(30, removeX - fieldsLeft - 6);
                int keyWidth = Math.Max(28, (int)(fieldsWidth * 0.42));
                int valueWidth = Math.Max(28, fieldsWidth - keyWidth - 5);
                row.KeyBox.SetBounds(fieldsLeft, 6, keyWidth, 17);
                row.ValueBox.SetBounds(fieldsLeft + keyWidth + 5, 6, valueWidth, 17);
                top += 32;
            }
            _scroll.AutoScrollMinSize = new Size(0, top + 2);
        }
    }
}

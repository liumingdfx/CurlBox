using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CurlBox
{
    internal sealed class AutoTokenLoginResult
    {
        public bool Success;
        public bool Canceled;
        public string Token;
        public string Error;
    }

    internal static class AutoTokenService
    {
        private static readonly Regex SchemePattern = new Regex(@"^[A-Za-z][A-Za-z0-9+.-]*://", RegexOptions.Compiled);

        public static bool ShouldApply(string url, bool directoryEnabled)
        {
            return directoryEnabled && IsBackendPath(url);
        }

        public static bool IsConfigured(AppSettings settings)
        {
            if (settings == null) return false;
            return NormalizeLoginUrl(settings.AdminLoginUrl).Length > 0;
        }

        public static bool IsBackendPath(string url)
        {
            string path = ExtractPath(url);
            if (string.IsNullOrEmpty(path)) return false;
            return string.Equals(path, "/backend", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("/backend/", StringComparison.OrdinalIgnoreCase);
        }

        public static void ApplyAuthorization(CurlCommand command, string token)
        {
            if (command == null) return;
            string value = (token ?? string.Empty).Trim();
            if (value.Length == 0) return;
            if (!value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                value = "Bearer " + value;

            command.Args.RemoveAll(delegate(CurlArg arg)
            {
                if (arg == null || arg.Value == null) return false;
                if (!string.Equals(arg.Option, "-H", StringComparison.Ordinal) &&
                    !string.Equals(arg.Option, "--header", StringComparison.Ordinal)) return false;
                int colon = arg.Value.IndexOf(':');
                if (colon < 0) return false;
                return string.Equals(arg.Value.Substring(0, colon).Trim(), "Authorization",
                    StringComparison.OrdinalIgnoreCase);
            });
            command.Args.Add(new CurlArg("--header", "Authorization: " + value));

            int count = 0;
            foreach (CurlArg arg in command.Args)
            {
                if (arg != null && (string.Equals(arg.Option, "-H", StringComparison.Ordinal) ||
                                    string.Equals(arg.Option, "--header", StringComparison.Ordinal)))
                    count++;
            }
            command.HeaderCount = count;
        }

        public static bool IsUnauthorized(CurlResult result)
        {
            if (result == null) return false;
            if (result.StatusCode == 401) return true;
            if (result.BodyKind != "text" || string.IsNullOrEmpty(result.BodyText)) return false;
            if (result.BodyText.Length > 2 * 1024 * 1024) return false;
            string trimmed = result.BodyText.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal) && !trimmed.StartsWith("[", StringComparison.Ordinal))
                return false;
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 4 * 1024 * 1024;
                Dictionary<string, object> root = serializer.Deserialize<Dictionary<string, object>>(trimmed);
                object code;
                if (root != null && root.TryGetValue("code", out code) && code != null)
                {
                    string text = Convert.ToString(code, CultureInfo.InvariantCulture);
                    return string.Equals((text ?? string.Empty).Trim(), "401", StringComparison.Ordinal);
                }
            }
            catch { }
            return false;
        }

        public static Task<AutoTokenLoginResult> LoginAsync(AppSettings settings, CancellationToken token)
        {
            return Task.Run(delegate { return LoginInternal(settings, token); });
        }

        private static AutoTokenLoginResult LoginInternal(AppSettings settings, CancellationToken token)
        {
            AutoTokenLoginResult result = new AutoTokenLoginResult();
            if (settings == null)
            {
                result.Error = "缺少自动登录配置。";
                return result;
            }
            string url = NormalizeLoginUrl(settings.AdminLoginUrl);
            if (url.Length == 0)
            {
                result.Error = "请先在设置中配置自动 Token 的登录接口。";
                return result;
            }

            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["username"] = settings.AdminUsername ?? string.Empty;
            payload["password"] = settings.AdminPassword ?? string.Empty;
            payload["code"] = settings.AdminAuthCode ?? string.Empty;

            string body;
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                body = serializer.Serialize(payload);
            }
            catch (Exception ex)
            {
                result.Error = "登录参数生成失败：" + ex.Message;
                return result;
            }

            CurlCommand command = new CurlCommand();
            command.Url = url;
            command.Method = "POST";
            command.HasBody = true;
            command.BodyKind = "data";
            command.BodyBytes = Encoding.UTF8.GetByteCount(body);
            command.Args.Add(new CurlArg { IsUrl = true, Value = url });
            command.Args.Add(new CurlArg("-X", "POST"));
            command.Args.Add(new CurlArg("--header", "Content-Type: application/json"));
            command.Args.Add(new CurlArg("--header", "Accept: application/json"));
            command.Args.Add(new CurlArg("--data-raw", body));

            CurlResult login;
            try
            {
                login = CurlRunner.RunAsync(command, null, true, settings.Insecure, false, token)
                    .GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                result.Error = "登录请求失败：" + ex.Message;
                return result;
            }

            if (token.IsCancellationRequested || (login != null && login.Canceled))
            {
                result.Canceled = true;
                result.Error = "已取消登录。";
                return result;
            }
            if (login == null)
            {
                result.Error = "登录接口没有返回结果。";
                return result;
            }
            if (login.Fatal)
            {
                result.Error = login.FatalMessage ?? "登录请求失败。";
                return result;
            }
            if (login.ExitCode != 0)
            {
                result.Error = !string.IsNullOrEmpty(login.Hint)
                    ? login.Hint
                    : (!string.IsNullOrEmpty(login.Stderr) ? login.Stderr : "登录请求失败（curl " + login.ExitCode + "）。");
                return result;
            }
            if (string.IsNullOrEmpty(login.BodyText))
            {
                result.Error = login.StatusCode > 0
                    ? "登录接口没有返回内容（HTTP " + login.StatusCode + "）。"
                    : "登录接口没有返回内容。";
                return result;
            }

            JavaScriptSerializer json = new JavaScriptSerializer();
            json.MaxJsonLength = 8 * 1024 * 1024;
            Dictionary<string, object> root = null;
            try
            {
                root = json.Deserialize<Dictionary<string, object>>(login.BodyText.Trim());
            }
            catch
            {
                result.Error = login.StatusCode > 0 && (login.StatusCode < 200 || login.StatusCode >= 300)
                    ? "登录失败（HTTP " + login.StatusCode + "），返回内容不是 JSON。"
                    : "登录接口返回的不是 JSON。";
                return result;
            }

            string message = GetMessage(root);
            if (login.StatusCode < 200 || login.StatusCode >= 300)
            {
                result.Error = "登录失败（HTTP " + login.StatusCode + "）" +
                               (string.IsNullOrEmpty(message) ? "。" : "：" + message);
                return result;
            }

            object dataObject;
            string rawToken = null;
            if (root != null && root.TryGetValue("data", out dataObject))
            {
                Dictionary<string, object> data = dataObject as Dictionary<string, object>;
                object tokenObject;
                if (data != null && data.TryGetValue("token", out tokenObject) && tokenObject != null)
                    rawToken = Convert.ToString(tokenObject, CultureInfo.InvariantCulture);
            }
            rawToken = (rawToken ?? string.Empty).Trim();
            if (rawToken.Length == 0)
            {
                result.Error = "登录接口没有返回 data.token" +
                               (string.IsNullOrEmpty(message) ? "。" : "：" + message);
                return result;
            }
            if (!rawToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                rawToken = "Bearer " + rawToken;
            result.Success = true;
            result.Token = rawToken;
            return result;
        }

        private static string NormalizeLoginUrl(string value)
        {
            string url = (value ?? string.Empty).Trim();
            if (url.Length == 0) return string.Empty;
            if (!SchemePattern.IsMatch(url)) url = "http://" + url;
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) return string.Empty;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return string.Empty;
            if (string.IsNullOrEmpty(uri.Host)) return string.Empty;
            return url;
        }

        private static string GetMessage(Dictionary<string, object> root)
        {
            if (root == null) return string.Empty;
            string[] keys = new[] { "message", "msg", "error" };
            foreach (string key in keys)
            {
                object value;
                if (root.TryGetValue(key, out value) && value != null)
                {
                    string text = Convert.ToString(value, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(text))
                        return text.Length > 200 ? text.Substring(0, 200) : text;
                }
            }
            return string.Empty;
        }

        private static string ExtractPath(string url)
        {
            string value = (url ?? string.Empty).Trim();
            if (value.Length == 0) return string.Empty;
            Uri uri;
            if (Uri.TryCreate(value, UriKind.Absolute, out uri))
                return uri.AbsolutePath ?? string.Empty;

            int scheme = value.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0)
            {
                int slash = value.IndexOf('/', scheme + 3);
                value = slash >= 0 ? value.Substring(slash) : "/";
            }
            else if (!value.StartsWith("/", StringComparison.Ordinal))
            {
                int slash = value.IndexOf('/');
                value = slash >= 0 ? value.Substring(slash) : string.Empty;
            }
            int query = value.IndexOf('?');
            if (query >= 0) value = value.Substring(0, query);
            int hash = value.IndexOf('#');
            if (hash >= 0) value = value.Substring(0, hash);
            return value;
        }
    }
}

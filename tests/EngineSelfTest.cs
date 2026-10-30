using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CurlBox
{
    internal static class EngineSelfTest
    {
        private static int _port;
        private static int _failures;

        private static void Main()
        {
            StartTargetServer();
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("target port: " + _port);

            TestSimpleGet();
            TestUnicodeBody();
            TestMethodOverride();
            TestFormats();
            TestHead();
            TestRedirect();
            TestOutputStripped();
            Test404();
            TestConnectionError();
            TestUnicodeUrl();
            TestForm();
            TestGbk();
            TestImage();
            TestSpecialBody();
            TestCurlconverterCompatibility();

            Console.WriteLine();
            Console.WriteLine(_failures == 0 ? "ALL TESTS PASSED" : ("FAILURES: " + _failures));
            Environment.ExitCode = _failures == 0 ? 0 : 1;
        }

        private static void Check(bool condition, string message)
        {
            if (condition)
            {
                Console.WriteLine("  ok  " + message);
            }
            else
            {
                _failures++;
                Console.WriteLine("  FAIL " + message);
            }
        }

        private static void TestSimpleGet()
        {
            Console.WriteLine("\n[simple GET]");
            string cmd = "curl 'http://127.0.0.1:" + _port + "/hello?a=1' -H 'x-demo: yes' -sS";
            CurlCommand parsed = CurlParser.Parse(cmd);
            Check(parsed.Method == "GET", "method GET");
            Check(parsed.Url.Contains("/hello?a=1"), "url parsed");
            Check(parsed.HeaderCount == 1, "one header");
            CurlResult result = Run(parsed, null, false, false, false);
            Check(!result.Fatal && result.ExitCode == 0, "curl exit 0");
            Check(result.StatusCode == 200, "status 200");
            Check(result.BodyKind == "text" && result.BodyText.Contains("\"method\":\"GET\""), "echo method GET");
            Check(result.HeaderBlocks.Count > 0 && result.FinalHeaders != null, "headers parsed");
        }

        private static void TestUnicodeBody()
        {
            Console.WriteLine("\n[unicode POST body]");
            string body = "{\"message\":\"你好\",\"quote\":\"a\\\"b\",\"slash\":\"x\\\\y\",\"line\":\"a\\nb\"}";
            string cmd = "curl -X POST 'http://127.0.0.1:" + _port + "/echo' -H 'content-type: application/json' --data-raw '" + body.Replace("'", "'\\''") + "'";
            CurlCommand parsed = CurlParser.Parse(cmd);
            CurlResult result = Run(parsed, null, false, false, false);
            Check(!result.Fatal && result.ExitCode == 0, "curl exit 0");
            string expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(body));
            Check(result.BodyText.Contains("\"bodyBase64\":\"" + expectedBase64 + "\""), "UTF-8 body bytes intact");
            Check(result.BodyText.Contains("\"method\":\"POST\""), "POST method");
        }

        private static void TestMethodOverride()
        {
            Console.WriteLine("\n[custom method override]");
            CurlCommand parsed = CurlParser.Parse("curl 'http://127.0.0.1:" + _port + "/method' -d 'x=1'");
            CurlResult result = Run(parsed, "PROPFIND", false, false, false);
            Check(result.Method == "PROPFIND", "effective method PROPFIND");
            Check(result.BodyText.Contains("\"method\":\"PROPFIND\""), "wire method PROPFIND");
        }

        private static void TestFormats()
        {
            Console.WriteLine("\n[input formats]");
            string cmdStyle = "curl \"http://example.com/login\" ^\n  -H \"content-type: application/json\" ^\n  --data-raw \"{\\\"user\\\":\\\"me\\\"}\"";
            CurlCommand parsed = CurlParser.Parse(cmdStyle);
            Check(parsed.Method == "POST" && parsed.Url == "http://example.com/login", "cmd caret format");

            string psStyle = "PS C:\\Users\\me> curl.exe \"https://example.com/x\" `\n -H \"accept: */*\"";
            parsed = CurlParser.Parse(psStyle);
            Check(parsed.Method == "GET" && parsed.Url == "https://example.com/x", "PowerShell format");

            string ansiStyle = "curl 'https://example.com' --data-raw $'{\"name\":\"张三\"}'";
            parsed = CurlParser.Parse(ansiStyle);
            Check(parsed.Method == "POST", "ANSI-C quote parsed");
        }

        private static void TestHead()
        {
            Console.WriteLine("\n[HEAD]");
            CurlCommand parsed = CurlParser.Parse("curl -I 'http://127.0.0.1:" + _port + "/head'");
            CurlResult result = Run(parsed, null, false, false, false);
            Check(result.Method == "HEAD", "method HEAD");
            Check(result.BodyKind == "empty" && result.HeadOnly, "no response body rendered");
            Check(result.StatusCode == 200, "status 200");
        }

        private static void TestRedirect()
        {
            Console.WriteLine("\n[redirect]");
            CurlCommand parsed = CurlParser.Parse("curl 'http://127.0.0.1:" + _port + "/redirect'");
            CurlResult result = Run(parsed, null, true, false, false);
            Check(result.StatusCode == 200, "final status 200");
            Check(result.Redirects.Count == 1, "one redirect captured");
            Check(result.EffectiveUrl.EndsWith("/final"), "effective URL final");
        }

        private static void TestOutputStripped()
        {
            Console.WriteLine("\n[output flags stripped]");
            CurlCommand parsed = CurlParser.Parse("curl 'http://127.0.0.1:" + _port + "/hello' -o C:\\Users\\Public\\curlbox-should-not-exist.txt");
            Check(parsed.Warnings.Count > 0, "warning emitted");
            CurlResult result = Run(parsed, null, false, false, false);
            Check(result.StatusCode == 200 && result.BodyKind == "text", "response still captured");
            Check(!File.Exists("C:\\Users\\Public\\curlbox-should-not-exist.txt"), "output file not written");
        }

        private static void Test404()
        {
            Console.WriteLine("\n[404]");
            CurlCommand parsed = CurlParser.Parse("curl 'http://127.0.0.1:" + _port + "/status/404'");
            CurlResult result = Run(parsed, null, false, false, false);
            Check(result.ExitCode == 0 && result.StatusCode == 404, "HTTP 404 without fatal");
            Check(result.BodyText != null && result.BodyText.Contains("not found"), "error body captured");
        }

        private static void TestConnectionError()
        {
            Console.WriteLine("\n[connection error]");
            CurlCommand parsed = CurlParser.Parse("curl 'http://127.0.0.1:1/nope'");
            CurlResult result = Run(parsed, null, false, false, false);
            Check(!result.Fatal && result.ExitCode == 7, "exit 7");
            Check(!string.IsNullOrEmpty(result.Stderr), "stderr captured");
            Check(!string.IsNullOrEmpty(result.Hint), "hint present");
        }

        private static void TestUnicodeUrl()
        {
            Console.WriteLine("\n[unicode URL]");
            CurlCommand parsed = CurlParser.Parse("curl 'http://127.0.0.1:" + _port + "/中文路径?q=值'");
            CurlResult result = Run(parsed, null, false, false, false);
            Check(result.StatusCode == 200, "unicode URL accepted");
            Check(result.BodyText.Contains("%E4%B8%AD%E6%96%87") || result.BodyText.Contains("/中文"), "path reached server encoded");
        }

        private static void TestForm()
        {
            Console.WriteLine("\n[multipart unicode]");
            CurlCommand parsed = CurlParser.Parse("curl 'http://127.0.0.1:" + _port + "/form' -F 'name=你好' -F 'note=abc'");
            CurlResult result = Run(parsed, null, false, false, false);
            Check(result.StatusCode == 200, "form request 200");
            byte[] bodyBytes = Convert.FromBase64String(ExtractJsonString(result.BodyText, "bodyBase64"));
            string multipart = Encoding.UTF8.GetString(bodyBytes);
            Check(multipart.Contains("你好"), "unicode form value intact");
        }

        private static void TestGbk()
        {
            Console.WriteLine("\n[GBK response]");
            CurlCommand parsed = CurlParser.Parse("curl 'http://127.0.0.1:" + _port + "/gbk'");
            CurlResult result = Run(parsed, null, false, false, false);
            Check(result.BodyKind == "text", "body is text");
            Check(result.BodyText == "你好，CurlBox", "GBK decoded: " + result.BodyText);
        }

        private static void TestImage()
        {
            Console.WriteLine("\n[image]");
            CurlCommand parsed = CurlParser.Parse("curl 'http://127.0.0.1:" + _port + "/image'");
            CurlResult result = Run(parsed, null, false, false, false);
            Check(result.BodyKind == "image", "image classified");
            Check(result.BodyBytes != null && result.BodyBytes.Length > 0, "image bytes captured");
        }

        private static void TestSpecialBody()
        {
            Console.WriteLine("\n[special body]");
            string command = "curl 'http://127.0.0.1:" + _port + "/echo' --data-binary 'line1\nline2\tend'";
            CurlCommand parsed = CurlParser.Parse(command);
            CurlResult result = Run(parsed, null, false, false, false);
            string expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("line1\nline2\tend"));
            Check(result.BodyText.Contains(expected), "newline/tab body intact");
        }

        private static void TestCurlconverterCompatibility()
        {
            Console.WriteLine("\n[curlconverter compatibility]");

            CurlCommand parsed = CurlParser.Parse(
                "curl --insec --sil 'http://127.0.0.1:" + _port + "/abbrev' --heade 'x-abbr: yes' --data-r 'abbr=yes'");
            Check(parsed.Method == "POST" && parsed.HeaderCount == 1 && parsed.HasBody, "abbreviated long options parsed");
            CurlResult result = Run(parsed, null, false, false, false);
            Check(result.BodyText.Contains("\"x-abbr\":\"yes\""), "canonical header abbreviation reached curl");
            byte[] abbrevBody = Convert.FromBase64String(ExtractJsonString(result.BodyText, "bodyBase64"));
            Check(Encoding.UTF8.GetString(abbrevBody) == "abbr=yes", "canonical data abbreviation reached curl");

            bool ambiguous = false;
            try { CurlParser.Parse("curl --loc http://127.0.0.1/"); }
            catch (CurlParseException ex) { ambiguous = ex.Message.Contains("歧义"); }
            Check(ambiguous, "ambiguous long option rejected");

            bool unknown = false;
            try { CurlParser.Parse("curl --definitely-not-a-curl-option http://127.0.0.1/"); }
            catch (CurlParseException ex) { unknown = ex.Message.Contains("未知"); }
            Check(unknown, "unknown long option rejected");

            string ansi = "curl $'http://127.0.0.1:" + _port + "/ansi' --data-raw $'\\x41\\u4F60\\cA\\E'";
            parsed = CurlParser.Parse(ansi);
            result = Run(parsed, null, false, false, false);
            string expected = Convert.ToBase64String(new byte[] { 0x41, 0xE4, 0xBD, 0xA0, 0x01, 0x1B });
            Check(result.BodyText.Contains(expected), "ANSI-C escapes match Bash rules");

            parsed = CurlParser.Parse("curl 'http://localhost:28139/' -H 'Accept-Encoding: gzip, deflate, sdch' -H 'Accept-Language: en-US,en;q=0.8' -H 'User-Agent: Mozilla/5.0' -H 'Accept: text/html,application/xhtml+xml' -H 'Referer: http://www.wikipedia.org/' -H 'Cookie: a=b; c=d' -H 'Connection: keep-alive' --compressed");
            Check(parsed.Method == "GET" && parsed.HeaderCount == 7 && parsed.Url == "http://localhost:28139/", "browser headers fixture");

            parsed = CurlParser.Parse("curl 'http://localhost:28139/echo/html/' -X POST -H 'Content-Type: application/x-www-form-urlencoded' --data 'msg1=wow&msg2=such' --compressed");
            Check(parsed.Method == "POST" && parsed.HasBody, "urlencoded fixture");

            parsed = CurlParser.Parse("curl -X GET 'http://localhost:28139/echo/html/' -H 'Origin: http://fiddle.jshell.net' -d msg1=\"value1\" -d msg2=\"value2\"");
            Check(parsed.Method == "GET" && parsed.HasBody, "explicit GET with data fixture");

            parsed = CurlParser.Parse("curl -X wHat localhost:28139");
            Check(parsed.Method == "wHat" && parsed.Url == "localhost:28139", "strange method case preserved");

            parsed = CurlParser.Parse("curl -X GET -H \"Authorization: Bearer $DO_API_TOKEN\" \"http://localhost:28139/v2/images?type=distribution\"");
            Check(parsed.Warnings.Any(delegate(string w) { return w.Contains("变量"); }), "unexpanded shell variable warning");

            parsed = CurlParser.Parse("curl --insecure -- 'http://127.0.0.1/test'");
            Check(parsed.Url == "http://127.0.0.1/test", "end-of-options URL parsed");
        }

        private static CurlResult Run(CurlCommand parsed, string method, bool follow, bool insecure, bool verbose)
        {
            try
            {
                return CurlRunner.RunAsync(parsed, method, follow, insecure, verbose, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine("  run exception: " + ex);
                return new CurlResult { Fatal = true, FatalMessage = ex.Message };
            }
        }

        private static string ExtractJsonString(string json, string key)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> data = serializer.Deserialize<Dictionary<string, object>>(json);
            return Convert.ToString(data[key]);
        }

        private static void StartTargetServer()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            _port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Thread thread = new Thread(delegate()
            {
                while (true)
                {
                    try
                    {
                        TcpClient client = listener.AcceptTcpClient();
                        ThreadPool.QueueUserWorkItem(HandleClient, client);
                    }
                    catch
                    {
                        return;
                    }
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private static void HandleClient(object state)
        {
            using (TcpClient client = (TcpClient)state)
            {
                try
                {
                    client.ReceiveTimeout = 5000;
                    NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[65536];
                    MemoryStream headerStream = new MemoryStream();
                    int headerEnd = -1;
                    while (headerEnd < 0)
                    {
                        int read = stream.Read(buffer, 0, buffer.Length);
                        if (read <= 0) return;
                        headerStream.Write(buffer, 0, read);
                        byte[] current = headerStream.ToArray();
                        for (int i = 0; i < current.Length - 3; i++)
                        {
                            if (current[i] == 13 && current[i + 1] == 10 && current[i + 2] == 13 && current[i + 3] == 10)
                            {
                                headerEnd = i + 4;
                                break;
                            }
                        }
                    }

                    byte[] all = headerStream.ToArray();
                    string headerText = Encoding.GetEncoding(28591).GetString(all, 0, headerEnd);
                    string[] headerLines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    string[] requestLine = headerLines[0].Split(' ');
                    string method = requestLine.Length > 0 ? requestLine[0] : "GET";
                    string path = requestLine.Length > 1 ? requestLine[1] : "/";
                    Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 1; i < headerLines.Length; i++)
                    {
                        int colon = headerLines[i].IndexOf(':');
                        if (colon > 0)
                            headers[headerLines[i].Substring(0, colon).Trim()] = headerLines[i].Substring(colon + 1).Trim();
                    }

                    int contentLength = 0;
                    if (headers.ContainsKey("Content-Length"))
                        int.TryParse(headers["Content-Length"], out contentLength);

                    MemoryStream bodyStream = new MemoryStream();
                    int alreadyRead = all.Length - headerEnd;
                    if (alreadyRead > 0)
                        bodyStream.Write(all, headerEnd, alreadyRead);
                    while (bodyStream.Length < contentLength)
                    {
                        int read = stream.Read(buffer, 0, Math.Min(buffer.Length, contentLength - (int)bodyStream.Length));
                        if (read <= 0) break;
                        bodyStream.Write(buffer, 0, read);
                    }
                    byte[] body = bodyStream.ToArray();

                    if (path.StartsWith("/redirect", StringComparison.Ordinal))
                    {
                        SendResponse(stream, 302, "Found", "text/plain", new byte[0],
                            new Dictionary<string, string> { { "Location", "/final" } }, method);
                        return;
                    }
                    if (path.StartsWith("/final", StringComparison.Ordinal))
                    {
                        SendResponse(stream, 200, "OK", "application/json; charset=utf-8",
                            Encoding.UTF8.GetBytes("{\"final\":true}"), null, method);
                        return;
                    }
                    if (path.StartsWith("/status/404", StringComparison.Ordinal))
                    {
                        SendResponse(stream, 404, "Not Found", "application/json; charset=utf-8",
                            Encoding.UTF8.GetBytes("{\"error\":\"not found\"}"), null, method);
                        return;
                    }
                    if (path.StartsWith("/gbk", StringComparison.Ordinal))
                    {
                        SendResponse(stream, 200, "OK", "text/plain; charset=gbk",
                            new byte[] { 0xc4, 0xe3, 0xba, 0xc3, 0xa3, 0xac, 0x43, 0x75, 0x72, 0x6c, 0x42, 0x6f, 0x78 },
                            null, method);
                        return;
                    }
                    if (path.StartsWith("/image", StringComparison.Ordinal))
                    {
                        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAYAAACp8Z5+AAAAFklEQVR4nGP8z8Dwn4GBgYGJAQsAANsABf7l6iQAAAAASUVORK5CYII=");
                        SendResponse(stream, 200, "OK", "image/png", png, null, method);
                        return;
                    }

                    Dictionary<string, object> payload = new Dictionary<string, object>();
                    payload["method"] = method;
                    payload["url"] = path;
                    payload["headers"] = headers;
                    payload["bodyBase64"] = Convert.ToBase64String(body);
                    payload["bodyUtf8"] = Encoding.UTF8.GetString(body);
                    string json = new JavaScriptSerializer().Serialize(payload);
                    SendResponse(stream, 200, "OK", "application/json; charset=utf-8",
                        Encoding.UTF8.GetBytes(json), null, method);
                }
                catch
                {
                    // Ignore test server client errors.
                }
            }
        }

        private static void SendResponse(
            NetworkStream stream,
            int status,
            string statusText,
            string contentType,
            byte[] body,
            Dictionary<string, string> extraHeaders,
            string requestMethod)
        {
            StringBuilder headers = new StringBuilder();
            headers.Append("HTTP/1.1 ").Append(status).Append(' ').Append(statusText).Append("\r\n");
            headers.Append("Content-Type: ").Append(contentType).Append("\r\n");
            headers.Append("Content-Length: ").Append(body.Length).Append("\r\n");
            headers.Append("Connection: close\r\n");
            if (extraHeaders != null)
            {
                foreach (KeyValuePair<string, string> pair in extraHeaders)
                    headers.Append(pair.Key).Append(": ").Append(pair.Value).Append("\r\n");
            }
            headers.Append("\r\n");
            byte[] headerBytes = Encoding.GetEncoding(28591).GetBytes(headers.ToString());
            stream.Write(headerBytes, 0, headerBytes.Length);
            if (!string.Equals(requestMethod, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                stream.Write(body, 0, body.Length);
            }
            stream.Flush();
        }
    }
}

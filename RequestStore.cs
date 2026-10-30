using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace CurlBox
{
    public sealed class SavedRequest
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Folder { get; set; }
        public long UpdatedAt { get; set; }
        public BuilderDraft Request { get; set; }

        public SavedRequest()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = string.Empty;
            Folder = string.Empty;
            UpdatedAt = 0;
            Request = new BuilderDraft();
        }
    }

    public sealed class RequestLibrary
    {
        public List<string> Folders { get; set; }
        public List<SavedRequest> Requests { get; set; }
        public List<string> AutoTokenFolders { get; set; }
        public bool AutoTokenUngrouped { get; set; }

        public RequestLibrary()
        {
            Folders = new List<string>();
            Requests = new List<SavedRequest>();
            AutoTokenFolders = new List<string>();
            AutoTokenUngrouped = false;
        }
    }

    internal static class RequestLibraryStore
    {
        private const int MaxLibraryBytes = 64 * 1024 * 1024;

        public static RequestLibrary Load(string path, out string error)
        {
            error = null;
            RequestLibrary library = new RequestLibrary();
            try
            {
                if (!File.Exists(path)) return library;
                FileInfo info = new FileInfo(path);
                if (info.Length > MaxLibraryBytes)
                {
                    error = "请求库文件过大，未加载。";
                    return library;
                }
                string text = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(text)) return library;
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = MaxLibraryBytes;
                RequestLibrary loaded = serializer.Deserialize<RequestLibrary>(text);
                if (loaded != null) library = loaded;
            }
            catch (Exception ex)
            {
                error = "请求库读取失败：" + ex.Message;
            }
            Normalize(library);
            return library;
        }

        public static bool Save(string path, RequestLibrary library, out string error)
        {
            error = null;
            try
            {
                Normalize(library);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = MaxLibraryBytes;
                string json = serializer.Serialize(library);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temporary, path, null);
                    }
                    catch
                    {
                        File.Copy(temporary, path, true);
                        File.Delete(temporary);
                    }
                }
                else
                {
                    File.Move(temporary, path);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static List<string> AllFolders(RequestLibrary library)
        {
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            if (library != null)
            {
                if (library.Folders != null)
                {
                    foreach (string folder in library.Folders)
                    {
                        string value = NormalizeFolder(folder);
                        if (value.Length > 0 && seen.Add(value)) result.Add(value);
                    }
                }
                if (library.Requests != null)
                {
                    foreach (SavedRequest request in library.Requests)
                    {
                        if (request == null) continue;
                        string value = NormalizeFolder(request.Folder);
                        if (value.Length > 0 && seen.Add(value)) result.Add(value);
                    }
                }
            }
            result.Sort(StringComparer.CurrentCultureIgnoreCase);
            return result;
        }

        public static string FindFolder(RequestLibrary library, string name)
        {
            string value = NormalizeFolder(name);
            if (value.Length == 0) return string.Empty;
            foreach (string folder in AllFolders(library))
            {
                if (string.Equals(folder, value, StringComparison.CurrentCultureIgnoreCase)) return folder;
            }
            return value;
        }

        public static string NormalizeFolder(string folder)
        {
            if (folder == null) return string.Empty;
            string value = folder.Replace("\r", " ").Replace("\n", " ").Trim();
            if (value.Length > 60) value = value.Substring(0, 60).Trim();
            return value;
        }

        private static void Normalize(RequestLibrary library)
        {
            if (library == null) return;
            if (library.Folders == null) library.Folders = new List<string>();
            if (library.Requests == null) library.Requests = new List<SavedRequest>();

            HashSet<string> folders = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            library.Folders.RemoveAll(delegate(string item) { return string.IsNullOrWhiteSpace(item); });
            List<string> normalizedFolders = new List<string>();
            foreach (string folder in library.Folders)
            {
                string value = NormalizeFolder(folder);
                if (value.Length == 0 || !folders.Add(value)) continue;
                normalizedFolders.Add(value);
            }
            library.Folders = normalizedFolders;

            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<SavedRequest> requests = new List<SavedRequest>();
            foreach (SavedRequest request in library.Requests)
            {
                if (request == null) continue;
                if (string.IsNullOrWhiteSpace(request.Id) || !ids.Add(request.Id))
                {
                    request.Id = Guid.NewGuid().ToString("N");
                    ids.Add(request.Id);
                }
                request.Name = NormalizeName(request.Name);
                if (request.Name.Length == 0) request.Name = "未命名请求";
                request.Folder = FindFolder(library, request.Folder);
                if (request.Folder.Length > 0) folders.Add(request.Folder);
                if (request.Request == null) request.Request = new BuilderDraft();
                NormalizeDraft(request.Request);
                requests.Add(request);
            }
            library.Requests = requests;

            if (library.AutoTokenFolders == null) library.AutoTokenFolders = new List<string>();
            Dictionary<string, string> validFolders = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (string folder in library.Folders)
            {
                string value = NormalizeFolder(folder);
                if (value.Length > 0 && !validFolders.ContainsKey(value)) validFolders[value] = value;
            }
            foreach (SavedRequest request in library.Requests)
            {
                string value = NormalizeFolder(request.Folder);
                if (value.Length > 0 && !validFolders.ContainsKey(value)) validFolders[value] = value;
            }
            HashSet<string> autoSeen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            List<string> autoFolders = new List<string>();
            foreach (string folder in library.AutoTokenFolders)
            {
                string value = NormalizeFolder(folder);
                string canonical;
                if (value.Length == 0 || !validFolders.TryGetValue(value, out canonical)) continue;
                if (autoSeen.Add(canonical)) autoFolders.Add(canonical);
            }
            library.AutoTokenFolders = autoFolders;
        }

        private static string NormalizeName(string name)
        {
            if (name == null) return string.Empty;
            string value = name.Replace("\r", " ").Replace("\n", " ").Trim();
            if (value.Length > 80) value = value.Substring(0, 80).Trim();
            return value;
        }

        private static void NormalizeDraft(BuilderDraft draft)
        {
            if (draft == null) return;
            draft.Active = false;
            if (draft.Method == null || !CurlBuilder.IsValidMethod(draft.Method)) draft.Method = "GET";
            if (draft.Url == null) draft.Url = string.Empty;
            if (draft.BodyText == null) draft.BodyText = string.Empty;
            draft.BodyMode = CurlBuilder.NormalizeBodyMode(draft.BodyMode);
            draft.ActiveTab = Math.Max(0, Math.Min(2, draft.ActiveTab));
            draft.Params = NormalizeRows(draft.Params);
            draft.Headers = NormalizeRows(draft.Headers);
            draft.Form = NormalizeRows(draft.Form);
            if (draft.ExtraArgs == null) draft.ExtraArgs = new List<BuilderExtraArg>();
            List<BuilderExtraArg> extras = new List<BuilderExtraArg>();
            foreach (BuilderExtraArg extra in draft.ExtraArgs)
            {
                if (extra == null || string.IsNullOrEmpty(extra.Option)) continue;
                extras.Add(new BuilderExtraArg { Option = extra.Option, Value = extra.Value });
            }
            draft.ExtraArgs = extras;
        }

        private static List<BuilderRow> NormalizeRows(List<BuilderRow> rows)
        {
            List<BuilderRow> result = new List<BuilderRow>();
            if (rows == null) return result;
            foreach (BuilderRow row in rows)
            {
                if (row == null) continue;
                result.Add(new BuilderRow
                {
                    Enabled = row.Enabled,
                    Key = row.Key ?? string.Empty,
                    Value = row.Value ?? string.Empty
                });
            }
            return result;
        }
    }
}

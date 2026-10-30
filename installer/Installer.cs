using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CurlBoxSetup
{
    internal static class Program
    {
        internal const string ProductName = "CurlBox";
        internal const string DisplayVersion = "1.0.0";
        internal const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CurlBox";

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (HasArg(args, "--uninstall"))
                {
                    RunUninstall(args);
                    return 0;
                }
                if (HasArg(args, "--silent-install"))
                {
                    string directory = ArgValue(args, "--silent-install");
                    if (string.IsNullOrWhiteSpace(directory)) return 2;
                    string error;
                    string warning;
                    bool ok = InstallerCore.Install(
                        directory,
                        !HasArg(args, "--no-shortcuts"),
                        !HasArg(args, "--no-shortcuts"),
                        !HasArg(args, "--no-registry"),
                        HasArg(args, "--launch"),
                        out error,
                        out warning);
                    return ok ? 0 : 1;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new InstallerForm());
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("安装程序出错：\r\n" + ex.Message, ProductName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private static bool HasArg(string[] args, string name)
        {
            if (args == null) return false;
            foreach (string arg in args)
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string ArgValue(string[] args, string name)
        {
            if (args == null) return string.Empty;
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return string.Empty;
        }

        private static void RunUninstall(string[] args)
        {
            bool silent = HasArg(args, "--silent");
            string installDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            Process[] running = Process.GetProcessesByName("CurlBox");
            if (running.Length > 0)
            {
                if (!silent)
                    MessageBox.Show("CurlBox 正在运行，请先从托盘右键退出后再卸载。", ProductName,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool deleteData = HasArg(args, "--delete-data");
            if (!silent)
            {
                if (MessageBox.Show("确定卸载 CurlBox 吗？", ProductName,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                DialogResult dataChoice = MessageBox.Show(
                    "是否同时删除 data 目录？\r\n\r\n" + Path.Combine(installDirectory, "data") +
                    "\r\n\r\n是：删除设置、历史和请求库\r\n否：保留数据\r\n取消：中止卸载",
                    ProductName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (dataChoice == DialogResult.Cancel) return;
                deleteData = dataChoice == DialogResult.Yes;
            }

            InstallerCore.RemoveShortcuts();
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); }
            catch { }

            string[] files = new[]
            {
                "CurlBox.exe",
                "CurlBox.exe.config",
                "THIRD_PARTY_NOTICES.txt"
            };
            foreach (string file in files)
            {
                try
                {
                    string path = Path.Combine(installDirectory, file);
                    if (File.Exists(path)) File.Delete(path);
                }
                catch { }
            }

            string dataDirectory = Path.Combine(installDirectory, "data");
            if (deleteData)
            {
                try
                {
                    if (Directory.Exists(dataDirectory)) Directory.Delete(dataDirectory, true);
                }
                catch { }
            }

            if (!silent)
                MessageBox.Show("CurlBox 已卸载。" + (deleteData ? "" : "\r\n数据目录已保留。"),
                    ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);

            ScheduleSelfDelete(installDirectory, deleteData);
        }

        private static void ScheduleSelfDelete(string installDirectory, bool removeEmptyDirectory)
        {
            try
            {
                string self = Application.ExecutablePath;
                string command = "/c ping 127.0.0.1 -n 3 >nul & del /f /q \"" + self + "\"";
                if (removeEmptyDirectory)
                    command += " & rmdir \"" + installDirectory.TrimEnd('\\') + "\" 2>nul";
                ProcessStartInfo info = new ProcessStartInfo("cmd.exe", command);
                info.CreateNoWindow = true;
                info.UseShellExecute = false;
                info.WorkingDirectory = Path.GetTempPath();
                Process.Start(info);
            }
            catch { }
            Application.Exit();
        }
    }

    internal sealed class InstallerForm : Form
    {
        private readonly TextBox _pathBox;
        private readonly CheckBox _desktopBox;
        private readonly CheckBox _startMenuBox;
        private readonly CheckBox _launchBox;
        private readonly Label _status;
        private readonly Button _installButton;
        private readonly Button _cancelButton;

        public InstallerForm()
        {
            Text = "CurlBox 安装程序";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            BackColor = Color.FromArgb(243, 245, 248);
            ForeColor = Color.FromArgb(19, 28, 38);
            Font = new Font("Microsoft YaHei UI", 9f);
            ClientSize = new Size(620, 400);
            AutoScaleMode = AutoScaleMode.Dpi;

            Label title = MakeLabel("CurlBox 安装程序", 13f, FontStyle.Bold, 24, 18, 560, 28);
            Controls.Add(title);
            Controls.Add(MakeLabel("本机 cURL / HTTP 请求工具 · 版本 " + Program.DisplayVersion,
                8.5f, FontStyle.Regular, 24, 48, 560, 20));
            Controls.Add(MakeLabel("选择安装位置", 9f, FontStyle.Bold, 24, 84, 300, 20));

            _pathBox = new TextBox();
            _pathBox.Font = new Font("Microsoft YaHei UI", 9f);
            _pathBox.BackColor = Color.White;
            _pathBox.ForeColor = Color.FromArgb(19, 28, 38);
            _pathBox.SetBounds(24, 106, 470, 26);
            _pathBox.Text = DefaultInstallDirectory();
            Controls.Add(_pathBox);

            Button browse = new Button();
            browse.Text = "浏览...";
            browse.Font = new Font("Microsoft YaHei UI", 8.5f);
            browse.FlatStyle = FlatStyle.Flat;
            browse.BackColor = Color.White;
            browse.ForeColor = Color.FromArgb(41, 55, 69);
            browse.FlatAppearance.BorderColor = Color.FromArgb(205, 215, 225);
            browse.SetBounds(504, 105, 90, 28);
            browse.Click += delegate { BrowseDirectory(); };
            Controls.Add(browse);

            Controls.Add(MakeLabel("数据目录会放在安装位置下的 data 文件夹；请选择有写入权限的目录。",
                8f, FontStyle.Regular, 24, 140, 570, 20));

            _desktopBox = MakeCheckBox("创建桌面快捷方式", 24, 176, true);
            Controls.Add(_desktopBox);
            _startMenuBox = MakeCheckBox("创建开始菜单快捷方式", 24, 202, true);
            Controls.Add(_startMenuBox);
            _launchBox = MakeCheckBox("安装完成后启动 CurlBox", 24, 228, true);
            Controls.Add(_launchBox);

            _status = MakeLabel(string.Empty, 8.5f, FontStyle.Regular, 24, 278, 570, 42);
            _status.ForeColor = Color.FromArgb(176, 58, 10);
            Controls.Add(_status);

            _cancelButton = new Button();
            _cancelButton.Text = "取消";
            _cancelButton.Font = new Font("Microsoft YaHei UI", 8.5f);
            _cancelButton.FlatStyle = FlatStyle.Flat;
            _cancelButton.BackColor = Color.White;
            _cancelButton.ForeColor = Color.FromArgb(41, 55, 69);
            _cancelButton.FlatAppearance.BorderColor = Color.FromArgb(205, 215, 225);
            _cancelButton.SetBounds(402, 344, 90, 34);
            _cancelButton.Click += delegate { Close(); };
            Controls.Add(_cancelButton);

            _installButton = new Button();
            _installButton.Text = "安装";
            _installButton.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            _installButton.FlatStyle = FlatStyle.Flat;
            _installButton.FlatAppearance.BorderSize = 0;
            _installButton.BackColor = Color.FromArgb(255, 108, 55);
            _installButton.ForeColor = Color.White;
            _installButton.SetBounds(500, 344, 94, 34);
            _installButton.Click += delegate { StartInstall(); };
            Controls.Add(_installButton);

            AcceptButton = _installButton;
            CancelButton = _cancelButton;
        }

        private static Label MakeLabel(string text, float size, FontStyle style, int x, int y, int width, int height)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = new Font("Microsoft YaHei UI", size, style);
            label.ForeColor = Color.FromArgb(41, 55, 69);
            label.BackColor = Color.Transparent;
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.SetBounds(x, y, width, height);
            return label;
        }

        private CheckBox MakeCheckBox(string text, int x, int y, bool isChecked)
        {
            CheckBox box = new CheckBox();
            box.Text = text;
            box.Font = new Font("Microsoft YaHei UI", 8.8f);
            box.ForeColor = Color.FromArgb(19, 28, 38);
            box.BackColor = Color.Transparent;
            box.FlatStyle = FlatStyle.Flat;
            box.AutoSize = true;
            box.Checked = isChecked;
            box.SetBounds(x, y, 300, 22);
            return box;
        }

        private static string DefaultInstallDirectory()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(Program.UninstallKey, false))
                {
                    if (key != null)
                    {
                        string existing = Convert.ToString(key.GetValue("InstallLocation", string.Empty));
                        if (!string.IsNullOrWhiteSpace(existing) && Directory.Exists(existing)) return existing;
                    }
                }
            }
            catch { }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "CurlBox");
        }

        private void BrowseDirectory()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择 CurlBox 安装目录";
                dialog.ShowNewFolderButton = true;
                dialog.SelectedPath = _pathBox.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) _pathBox.Text = dialog.SelectedPath;
            }
        }

        private void StartInstall()
        {
            string directory = (_pathBox.Text ?? string.Empty).Trim();
            if (directory.Length == 0 || !Path.IsPathRooted(directory) ||
                string.Equals(Path.GetPathRoot(directory), directory, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "请选择具体的安装目录，不能直接安装到磁盘根目录。", Program.ProductName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            directory = Path.GetFullPath(directory);
            bool existing = File.Exists(Path.Combine(directory, "CurlBox.exe"));
            if (existing && MessageBox.Show(this,
                "该目录已经安装过 CurlBox。\r\n覆盖安装不会删除 data 目录，是否继续？",
                Program.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _installButton.Enabled = false;
            _cancelButton.Enabled = false;
            _pathBox.Enabled = false;
            _desktopBox.Enabled = false;
            _startMenuBox.Enabled = false;
            _launchBox.Enabled = false;
            _status.Text = "正在安装…";
            Application.DoEvents();

            string error;
            string warning;
            bool ok = InstallerCore.Install(directory, _desktopBox.Checked, _startMenuBox.Checked,
                true, false, out error, out warning);
            if (!ok)
            {
                _status.Text = "安装失败：" + error;
                MessageBox.Show(this, "安装失败：\r\n" + error, Program.ProductName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _installButton.Enabled = true;
                _cancelButton.Enabled = true;
                _pathBox.Enabled = true;
                _desktopBox.Enabled = true;
                _startMenuBox.Enabled = true;
                _launchBox.Enabled = true;
                return;
            }

            _status.Text = "安装完成";
            string message = "CurlBox 已安装到：\r\n" + directory +
                             "\r\n\r\n数据目录：" + Path.Combine(directory, "data");
            if (!string.IsNullOrEmpty(warning)) message += "\r\n\r\n注意：" + warning;
            MessageBox.Show(this, message, Program.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (_launchBox.Checked)
            {
                try { Process.Start(Path.Combine(directory, "CurlBox.exe")); }
                catch { }
            }
            Close();
        }
    }

    internal static class InstallerCore
    {
        internal static bool Install(
            string directory,
            bool desktopShortcut,
            bool startMenuShortcut,
            bool writeRegistry,
            bool launchAfterInstall,
            out string error,
            out string warning)
        {
            error = null;
            warning = null;
            try
            {
                directory = Path.GetFullPath(directory.Trim());
                if (Process.GetProcessesByName("CurlBox").Length > 0)
                {
                    error = "CurlBox 正在运行，请先从托盘右键退出后再安装。";
                    return false;
                }

                Directory.CreateDirectory(directory);
                string probe = Path.Combine(directory, ".curbbox-write-test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);

                string exePath = Path.Combine(directory, "CurlBox.exe");
                string configPath = Path.Combine(directory, "CurlBox.exe.config");
                string noticesPath = Path.Combine(directory, "THIRD_PARTY_NOTICES.txt");
                ExtractResource("CurlBoxPayload.exe", exePath);
                ExtractResource("CurlBoxPayload.config", configPath);
                ExtractResource("CurlBoxPayload.notices", noticesPath);
                Directory.CreateDirectory(Path.Combine(directory, "data"));

                string uninstallerPath = Path.Combine(directory, "CurlBoxUninstall.exe");
                File.Copy(Application.ExecutablePath, uninstallerPath, true);

                bool shortcutFailed = false;
                if (desktopShortcut)
                {
                    try
                    {
                        CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                            "CurlBox.lnk"), exePath, directory);
                    }
                    catch { shortcutFailed = true; }
                }
                if (startMenuShortcut)
                {
                    try
                    {
                        CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                            "CurlBox.lnk"), exePath, directory);
                    }
                    catch { shortcutFailed = true; }
                }

                bool registryFailed = false;
                if (writeRegistry)
                {
                    try
                    {
                        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(Program.UninstallKey))
                        {
                            if (key != null)
                            {
                                key.SetValue("DisplayName", Program.ProductName);
                                key.SetValue("DisplayVersion", Program.DisplayVersion);
                                key.SetValue("Publisher", Program.ProductName);
                                key.SetValue("InstallLocation", directory);
                                key.SetValue("DisplayIcon", exePath + ",0");
                                key.SetValue("UninstallString", "\"" + uninstallerPath + "\" --uninstall");
                                key.SetValue("QuietUninstallString", "\"" + uninstallerPath + "\" --uninstall --silent --keep-data");
                                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                            }
                        }
                    }
                    catch { registryFailed = true; }
                }

                if (shortcutFailed && registryFailed)
                    warning = "快捷方式或卸载项未完全创建，但程序文件已安装。";
                else if (shortcutFailed)
                    warning = "快捷方式创建失败，可从安装目录直接启动。";
                else if (registryFailed)
                    warning = "系统卸载项创建失败，可手动删除安装目录。";
                if (launchAfterInstall)
                {
                    try { Process.Start(exePath); }
                    catch { }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        internal static void RemoveShortcuts()
        {
            string[] paths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "CurlBox.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "CurlBox.lnk")
            };
            foreach (string path in paths)
            {
                try { if (File.Exists(path)) File.Delete(path); }
                catch { }
            }
        }

        private static void ExtractResource(string name, string path)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null) throw new InvalidOperationException("安装包中缺少资源：" + name);
                using (FileStream file = new FileStream(path, FileMode.Create, FileAccess.Write))
                    stream.CopyTo(file);
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) throw new InvalidOperationException("WScript.Shell 不可用。");
            object shell = Activator.CreateInstance(shellType);
            object shortcut = null;
            try
            {
                shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod,
                    null, shell, new object[] { shortcutPath });
                Type type = shortcut.GetType();
                type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                type.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDirectory });
                type.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath + ",0" });
                type.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "CurlBox" });
                type.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (shortcut != null) Marshal.ReleaseComObject(shortcut);
                if (shell != null) Marshal.ReleaseComObject(shell);
            }
        }
    }
}

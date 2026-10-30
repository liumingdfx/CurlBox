using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CurlBox
{
    internal static class AutoStartStore
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "CurlBox";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    return key != null && key.GetValue(ValueName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (key == null) throw new InvalidOperationException("无法打开开机启动注册表项。");
                if (enabled)
                {
                    string command = "\"" + Application.ExecutablePath + "\" --startup";
                    key.SetValue(ValueName, command, RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(ValueName, false);
                }
            }
        }
    }

    internal static class Program
    {
        internal const string ShowMessageName = "CurlBox.ShowWindow.2026";
        private static Mutex _mutex;

        [STAThread]
        private static void Main(string[] args)
        {
            bool enableAutoStart = args.Any(delegate(string a) { return string.Equals(a, "--enable-autostart", StringComparison.OrdinalIgnoreCase); });
            bool disableAutoStart = args.Any(delegate(string a) { return string.Equals(a, "--disable-autostart", StringComparison.OrdinalIgnoreCase); });
            if (enableAutoStart || disableAutoStart)
            {
                try
                {
                    AutoStartStore.SetEnabled(enableAutoStart);
                    Environment.ExitCode = 0;
                }
                catch
                {
                    Environment.ExitCode = 1;
                }
                return;
            }

            bool createdNew;
            _mutex = new Mutex(true, @"Local\CurlBox.SingleInstance", out createdNew);
            if (!createdNew)
            {
                uint message = NativeMethods.RegisterWindowMessage(ShowMessageName);
                IntPtr existing = IntPtr.Zero;
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    existing = NativeMethods.FindWindow(null, "CurlBox");
                    if (existing != IntPtr.Zero) break;
                    Thread.Sleep(100);
                }
                if (existing != IntPtr.Zero)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        NativeMethods.PostMessage(existing, message, IntPtr.Zero, IntPtr.Zero);
                        Thread.Sleep(80);
                    }
                    return;
                }
                for (int i = 0; i < 6; i++)
                {
                    NativeMethods.PostMessage(NativeMethods.HwndBroadcast, message, IntPtr.Zero, IntPtr.Zero);
                    Thread.Sleep(80);
                }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                WriteErrorLog(e.Exception);
                MessageBox.Show("CurlBox 遇到错误：\r\n\r\n" + e.Exception.Message,
                    "CurlBox", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Exception exception = e.ExceptionObject as Exception;
                WriteErrorLog(exception);
            };

            bool startHidden = args.Any(delegate(string a) { return string.Equals(a, "--startup", StringComparison.OrdinalIgnoreCase); });
            try
            {
                Application.Run(new MainForm(startHidden));
            }
            finally
            {
                GC.KeepAlive(_mutex);
            }
        }

        internal static string DataDirectory
        {
            get
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static void WriteErrorLog(Exception exception)
        {
            try
            {
                string path = Path.Combine(DataDirectory, "error.log");
                File.AppendAllText(path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" +
                    (exception == null ? "Unknown error" : exception.ToString()) + "\r\n\r\n");
            }
            catch
            {
                // Logging must never take down the app.
            }
        }
    }
}

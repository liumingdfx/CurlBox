using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CurlBox
{
    internal static class NativeMethods
    {
        public const int WmHotkey = 0x0312;
        public const int ModAlt = 0x0001;
        public const int ModControl = 0x0002;
        public const int EmSetCueBanner = 0x1501;
        public static readonly IntPtr HwndBroadcast = new IntPtr(0xffff);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint RegisterWindowMessage(string message);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, int modifiers, int virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, string lParam);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        public static extern int SetWindowTheme(IntPtr hWnd, string subAppName, string subIdList);

        public static void SetCueBanner(TextBox box, string text)
        {
            if (box == null || !box.IsHandleCreated) return;
            SendMessage(box.Handle, EmSetCueBanner, new IntPtr(1), text);
        }

        public static void EnableLightScrollbars(Control control)
        {
            if (control == null) return;
            try
            {
                if (control.IsHandleCreated)
                    SetWindowTheme(control.Handle, "Explorer", null);
            }
            catch
            {
                // Theming is a nice-to-have only.
            }
        }

        public static void EnableLightTitleBar(IntPtr handle)
        {
            try
            {
                int light = 0;
                if (DwmSetWindowAttribute(handle, 20, ref light, sizeof(int)) != 0)
                    DwmSetWindowAttribute(handle, 19, ref light, sizeof(int));

                int caption = ColorTranslator.ToWin32(Theme.Bg);
                int text = ColorTranslator.ToWin32(Theme.Text);
                int border = ColorTranslator.ToWin32(Theme.Border);
                DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
                DwmSetWindowAttribute(handle, 36, ref text, sizeof(int));
                DwmSetWindowAttribute(handle, 34, ref border, sizeof(int));
            }
            catch
            {
                // Older Windows versions keep their default title bar colors.
            }
        }
    }
}

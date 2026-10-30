using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CurlBox
{
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(241, 244, 247);
        public static readonly Color Panel = Color.FromArgb(255, 255, 255);
        public static readonly Color PanelRaised = Color.FromArgb(246, 248, 251);
        public static readonly Color Input = Color.FromArgb(253, 254, 255);
        public static readonly Color Border = Color.FromArgb(205, 215, 225);
        public static readonly Color BorderSoft = Color.FromArgb(226, 232, 239);
        public static readonly Color Text = Color.FromArgb(19, 28, 38);
        public static readonly Color TextSoft = Color.FromArgb(41, 55, 69);
        public static readonly Color Muted = Color.FromArgb(73, 89, 104);
        public static readonly Color Faint = Color.FromArgb(98, 113, 128);
        public static readonly Color Hover = Color.FromArgb(236, 241, 246);
        public static readonly Color Selected = Color.FromArgb(255, 243, 228);
        public static readonly Color Accent = Color.FromArgb(176, 58, 10);
        public static readonly Color AccentStrong = Color.FromArgb(138, 44, 7);
        public static readonly Color AccentInk = Color.FromArgb(255, 255, 255);
        public static readonly Color AccentSurface = Color.FromArgb(255, 242, 225);
        public static readonly Color AccentBorder = Color.FromArgb(238, 196, 146);
        public static readonly Color Mint = Color.FromArgb(10, 102, 95);
        public static readonly Color Sky = Color.FromArgb(2, 86, 135);
        public static readonly Color Violet = Color.FromArgb(88, 28, 185);
        public static readonly Color Red = Color.FromArgb(185, 28, 28);
        public static readonly Color Orange = Color.FromArgb(176, 58, 10);
        public static readonly Color Warning = Color.FromArgb(151, 64, 6);
        public static readonly Color ErrorSurface = Color.FromArgb(255, 240, 242);
        public static readonly Color ErrorBorder = Color.FromArgb(250, 191, 199);
        public static readonly Color ErrorTitle = Color.FromArgb(142, 13, 49);
        public static readonly Color ErrorText = Color.FromArgb(109, 21, 21);
        public static readonly Color CodeString = Color.FromArgb(2, 101, 74);
        public static readonly Color CodeKey = Color.FromArgb(2, 84, 127);
        public static readonly Color CodeNumber = Color.FromArgb(154, 62, 5);
        public static readonly Color CodeBool = Color.FromArgb(86, 31, 174);
        public static readonly Color CodeNull = Color.FromArgb(89, 104, 119);

        private static readonly Dictionary<string, Font> FontCache = new Dictionary<string, Font>();

        private static Font GetFont(string family, float size, FontStyle style)
        {
            string key = family + "|" + size.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "|" + (int)style;
            lock (FontCache)
            {
                Font font;
                if (!FontCache.TryGetValue(key, out font))
                {
                    font = new Font(family, size, style, GraphicsUnit.Point);
                    FontCache[key] = font;
                }
                return font;
            }
        }

        public static Font Ui(float size)
        {
            return GetFont("Microsoft YaHei UI", size, FontStyle.Regular);
        }

        public static Font UiBold(float size)
        {
            return GetFont("Microsoft YaHei UI", size, FontStyle.Bold);
        }

        public static Font Mono(float size)
        {
            return GetFont("Consolas", size, FontStyle.Regular);
        }

        public static Font MonoBold(float size)
        {
            return GetFont("Consolas", size, FontStyle.Bold);
        }

        public static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            GraphicsPath path = new GraphicsPath();
            if (diameter <= 1)
            {
                path.AddRectangle(bounds);
                path.CloseFigure();
                return path;
            }
            Rectangle arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.X;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Color Tint(Color color, int alpha)
        {
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        public static void DrawPill(
            Graphics graphics,
            Rectangle bounds,
            string text,
            Font font,
            Color textColor,
            Color fillColor,
            Color borderColor)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Rounded(bounds, Math.Min(5, bounds.Height / 2)))
            {
                using (SolidBrush fill = new SolidBrush(fillColor))
                    graphics.FillPath(fill, path);
                if (borderColor.A > 0)
                {
                    using (Pen pen = new Pen(borderColor, 1))
                        graphics.DrawPath(pen, path);
                }
            }
            TextRenderer.DrawText(
                graphics,
                text,
                font,
                bounds,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        public static Color ColorForMethod(string method)
        {
            switch ((method ?? string.Empty).ToUpperInvariant())
            {
                case "GET": return Mint;
                case "POST": return Sky;
                case "PUT": return Warning;
                case "PATCH": return Violet;
                case "DELETE": return Red;
                case "HEAD":
                case "OPTIONS": return Color.FromArgb(71, 85, 105);
                default: return Accent;
            }
        }

        public static Color ColorForStatus(int status)
        {
            if (status <= 0) return Red;
            if (status < 300) return Mint;
            if (status < 400) return Warning;
            if (status < 500) return Orange;
            return Red;
        }
    }

    internal sealed class CardPanel : Panel
    {
        public int CornerRadius = 11;
        public Color FillColor = Theme.Panel;
        public Color BorderColor = Theme.Border;

        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Panel;
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            UpdateRegion();
        }

        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            using (GraphicsPath path = Theme.Rounded(new Rectangle(0, 0, Width, Height), CornerRadius))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.Rounded(bounds, CornerRadius))
            {
                using (SolidBrush fill = new SolidBrush(FillColor))
                    e.Graphics.FillPath(fill, path);
                using (Pen pen = new Pen(BorderColor, 1))
                    e.Graphics.DrawPath(pen, path);
            }
        }
    }

    internal class RoundButton : Button
    {
        private bool _hover;
        private bool _pressed;

        public int CornerRadius = 8;
        public Color FillColor = Theme.PanelRaised;
        public Color FillColor2 = Theme.PanelRaised;
        public Color HoverColor = Theme.Hover;
        public Color BorderColor = Theme.Border;
        public Color TextColor = Theme.TextSoft;
        public bool Active;
        public bool DrawUnderline;
        public Color UnderlineColor = Theme.Accent;

        public RoundButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            BackColor = Theme.Panel;
            ForeColor = Theme.TextSoft;
            Font = Theme.Ui(9f);
            Cursor = Cursors.Hand;
            Height = 30;
            UseMnemonic = false;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            _pressed = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics graphics = pevent.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (SolidBrush clear = new SolidBrush(BackColor))
                graphics.FillRectangle(clear, ClientRectangle);
            Color fill = FillColor;
            Color border = BorderColor;
            Color text = TextColor;

            if (!Enabled)
            {
                fill = Theme.PanelRaised;
                border = Theme.BorderSoft;
                text = Theme.Faint;
            }
            else if (_pressed)
            {
                fill = ControlPaint.Dark(HoverColor, 0.08f);
            }
            else if (_hover)
            {
                fill = HoverColor;
            }

            using (GraphicsPath path = Theme.Rounded(bounds, CornerRadius))
            {
                if (fill.A > 0)
                {
                    if (FillColor2 != FillColor && Enabled && !_hover && !_pressed)
                    {
                        using (LinearGradientBrush gradient = new LinearGradientBrush(
                            new Rectangle(0, 0, Width, Height), FillColor, FillColor2, LinearGradientMode.Vertical))
                            graphics.FillPath(gradient, path);
                    }
                    else
                    {
                        using (SolidBrush brush = new SolidBrush(fill))
                            graphics.FillPath(brush, path);
                    }
                }
                if (border.A > 0)
                {
                    using (Pen pen = new Pen(border, 1))
                        graphics.DrawPath(pen, path);
                }
            }

            if (DrawUnderline && Active)
            {
                using (SolidBrush brush = new SolidBrush(UnderlineColor))
                    graphics.FillRectangle(brush, 0, Height - 2, Width, 2);
            }

            Rectangle textBounds = new Rectangle(Padding.Left, 0, Width - Padding.Left - Padding.Right, Height);
            TextRenderer.DrawText(
                graphics,
                Text,
                Font,
                textBounds,
                text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            if (Focused && ShowFocusCues)
            {
                Rectangle focus = new Rectangle(3, 3, Math.Max(0, Width - 7), Math.Max(0, Height - 7));
                ControlPaint.DrawFocusRectangle(graphics, focus, text, Color.Transparent);
            }
        }
    }

    internal sealed class Pill : Control
    {
        public Color FillColor = Theme.PanelRaised;
        public Color BorderColor = Theme.Border;
        public Color TextColor = Theme.TextSoft;

        public Pill()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.MonoBold(8f);
            Height = 20;
            Width = 48;
        }

        public void FitToText(int extra)
        {
            Size measured = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding);
            Width = Math.Max(24, measured.Width + extra);
            Invalidate();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            Invalidate();
            base.OnTextChanged(e);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            using (SolidBrush clear = new SolidBrush(BackColor))
                pevent.Graphics.FillRectangle(clear, ClientRectangle);
            Theme.DrawPill(pevent.Graphics, new Rectangle(0, 0, Width - 1, Height - 1),
                Text, Font, TextColor, FillColor, BorderColor);
        }
    }

    internal sealed class BrandMark : Control
    {
        public BrandMark()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(31, 31);
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.Rounded(bounds, 8))
            {
                using (LinearGradientBrush fill = new LinearGradientBrush(bounds,
                    Color.FromArgb(255, 247, 237), Color.FromArgb(255, 232, 210), LinearGradientMode.Vertical))
                    e.Graphics.FillPath(fill, path);
                using (Pen pen = new Pen(Theme.AccentBorder, 1))
                    e.Graphics.DrawPath(pen, path);
            }
            using (Pen pen = new Pen(Theme.Accent, 1.8f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                e.Graphics.DrawLines(pen, new[]
                {
                    new PointF(8f, 9f), new PointF(14f, 15.5f), new PointF(8f, 22f)
                });
                e.Graphics.DrawLine(pen, 16f, 22f, 23f, 22f);
            }
        }
    }

    internal sealed class LightColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Panel; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color MenuItemBorder { get { return Theme.Border; } }
        public override Color MenuItemSelected { get { return Theme.Hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Hover; } }
        public override Color MenuItemPressedGradientBegin { get { return Theme.Hover; } }
        public override Color MenuItemPressedGradientEnd { get { return Theme.Hover; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Panel; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Panel; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Panel; } }
        public override Color SeparatorDark { get { return Theme.BorderSoft; } }
        public override Color SeparatorLight { get { return Theme.BorderSoft; } }
        public override Color CheckBackground { get { return Color.FromArgb(232, 247, 241); } }
        public override Color CheckSelectedBackground { get { return Color.FromArgb(220, 244, 234); } }
        public override Color CheckPressedBackground { get { return Color.FromArgb(232, 247, 241); } }
    }

    internal sealed class LightMenuRenderer : ToolStripProfessionalRenderer
    {
        public LightMenuRenderer() : base(new LightColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.Text : Theme.Faint;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            if (e.Item is ToolStripMenuItem && ((ToolStripMenuItem)e.Item).Checked && e.Item.Enabled)
            {
                Rectangle bounds = new Rectangle(e.ImageRectangle.X + 3, e.ImageRectangle.Y + 3,
                    Math.Max(0, e.ImageRectangle.Width - 6), Math.Max(0, e.ImageRectangle.Height - 6));
                using (Pen pen = new Pen(Theme.Mint, 1.6f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.DrawLines(pen, new[]
                    {
                        new PointF(bounds.Left, bounds.Top + bounds.Height * 0.55f),
                        new PointF(bounds.Left + bounds.Width * 0.38f, bounds.Bottom - 1),
                        new PointF(bounds.Right + 1, bounds.Top + 1)
                    });
                }
            }
        }
    }

    internal sealed class SafePasteTextBox : TextBox
    {
        private const int WmPaste = 0x0302;

        // Win11 comctl32 can corrupt its heap when a multiline edit handles
        // WM_PASTE while the control is being reconfigured. Paste through the
        // managed TextBox path instead of the native clipboard routine.
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmPaste)
            {
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        string text = Clipboard.GetText();
                        if (text.Length > 0)
                        {
                            int start = SelectionStart;
                            SelectedText = text;
                            SelectionStart = Math.Min(TextLength, start + text.Length);
                        }
                    }
                }
                catch { }
                return;
            }
            base.WndProc(ref m);
        }
    }

    internal sealed class SplitHandle : Control
    {
        private bool _hover;
        private bool _dragging;

        public event EventHandler Dragging;
        public event EventHandler DragFinished;

        public SplitHandle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Cursor = Cursors.SizeWE;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            _dragging = true;
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;
            EventHandler handler = Dragging;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !_dragging) return;
            _dragging = false;
            Capture = false;
            Invalidate();
            EventHandler handler = DragFinished;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (Capture) return;
            _dragging = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (SolidBrush clear = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(clear, ClientRectangle);

            int centerX = Width / 2;
            int centerY = Height / 2;
            Color color = _hover || _dragging ? Theme.Accent : Theme.Faint;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(color))
            {
                for (int i = -1; i <= 1; i++)
                    e.Graphics.FillEllipse(brush, centerX - 2, centerY + i * 7 - 2, 4, 4);
            }
            if (_dragging)
            {
                using (SolidBrush brush = new SolidBrush(Theme.Tint(Theme.Accent, 36)))
                    e.Graphics.FillRectangle(brush, centerX - 1, 4, 2, Math.Max(0, Height - 8));
            }
        }
    }

    internal static class UiText
    {
        public static string Clip(string value, int max)
        {
            if (value == null) return string.Empty;
            if (value.Length <= max) return value;
            return value.Substring(0, Math.Max(0, max - 1)) + "…";
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024)
            {
                double kb = bytes / 1024.0;
                return (kb < 10 ? kb.ToString("0.0") : Math.Round(kb).ToString("0")) + " KB";
            }
            double mb = bytes / (1024.0 * 1024.0);
            return (mb < 10 ? mb.ToString("0.0") : Math.Round(mb).ToString("0")) + " MB";
        }

        public static string FormatMs(long ms)
        {
            if (ms < 0) return "—";
            if (ms < 1000) return ms + " ms";
            if (ms < 10000) return (ms / 1000.0).ToString("0.00") + " s";
            return (ms / 1000.0).ToString("0.0") + " s";
        }

        public static string RelativeTime(long unixMilliseconds)
        {
            DateTime time = DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).LocalDateTime;
            TimeSpan diff = DateTime.Now - time;
            if (diff.TotalSeconds < 60) return "刚刚";
            if (diff.TotalMinutes < 60) return ((int)diff.TotalMinutes) + " 分钟前";
            if (diff.TotalHours < 24) return ((int)diff.TotalHours) + " 小时前";
            if (diff.TotalDays < 7) return ((int)diff.TotalDays) + " 天前";
            return time.ToString("MM-dd HH:mm");
        }

        public static string DisplayUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "（未识别 URL）";
            int scheme = url.IndexOf("://", StringComparison.Ordinal);
            string rest = scheme >= 0 ? url.Substring(scheme + 3) : url;
            return rest;
        }
    }
}

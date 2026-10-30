using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CurlBox
{
    internal abstract class LightDialog : Form
    {
        protected LightDialog(string title, Size clientSize)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ShowIcon = false;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Ui(9f);
            ClientSize = clientSize;
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            NativeMethods.EnableLightTitleBar(Handle);
        }
    }

    internal static class DialogUi
    {
        public static RoundButton Button(string text, int width, bool primary)
        {
            RoundButton button = new RoundButton();
            button.Text = text;
            button.Size = new Size(width, 32);
            button.CornerRadius = 7;
            button.Font = primary ? Theme.UiBold(8.5f) : Theme.Ui(8.5f);
            button.BackColor = Theme.Bg;
            if (primary)
            {
                button.FillColor = Theme.Accent;
                button.FillColor2 = Theme.AccentStrong;
                button.HoverColor = Color.FromArgb(150, 48, 8);
                button.BorderColor = Theme.AccentStrong;
                button.TextColor = Theme.AccentInk;
            }
            else
            {
                button.FillColor = Theme.Panel;
                button.FillColor2 = Theme.Panel;
                button.HoverColor = Theme.Hover;
                button.BorderColor = Theme.Border;
                button.TextColor = Theme.TextSoft;
            }
            return button;
        }

        public static RoundButton SmallButton(string text, int width)
        {
            RoundButton button = Button(text, width, false);
            button.Height = 30;
            button.CornerRadius = 6;
            button.Font = Theme.Ui(8f);
            return button;
        }

        public static Label Label(string text, Font font, Color color, int x, int y, int width, int height)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Theme.Bg;
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.SetBounds(x, y, width, height);
            return label;
        }
    }

    internal sealed class SaveRequestDialog : LightDialog
    {
        private readonly TextBox _nameBox;
        private readonly ComboBox _folderBox;
        private readonly Label _error;
        private readonly bool _allowSaveAs;

        public string RequestName { get; private set; }
        public string RequestFolder { get; private set; }
        public bool SaveAs { get; private set; }

        public SaveRequestDialog(
            string initialName,
            string initialFolder,
            List<string> folders,
            string method,
            string url,
            bool allowSaveAs)
            : base("保存请求", new Size(520, 356))
        {
            _allowSaveAs = allowSaveAs;

            Label title = DialogUi.Label("保存请求", Theme.UiBold(13f), Theme.Text, 22, 14, 300, 26);
            Controls.Add(title);

            Pill methodPill = new Pill();
            methodPill.Text = string.IsNullOrEmpty(method) ? "GET" : method.ToUpperInvariant();
            methodPill.Font = Theme.MonoBold(8f);
            methodPill.Height = 20;
            methodPill.FitToText(14);
            Color methodColor = Theme.ColorForMethod(methodPill.Text);
            methodPill.TextColor = methodColor;
            methodPill.FillColor = Color.FromArgb(30, methodColor);
            methodPill.BorderColor = Color.FromArgb(70, methodColor);
            methodPill.BackColor = Theme.Bg;
            methodPill.Location = new Point(22, 47);
            Controls.Add(methodPill);

            Label urlLabel = DialogUi.Label(UiText.DisplayUrl(url), Theme.Mono(8.5f), Theme.Muted,
                31 + methodPill.Width, 45, 467 - methodPill.Width, 22);
            urlLabel.AutoEllipsis = true;
            urlLabel.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(urlLabel);

            Controls.Add(DialogUi.Label("名称", Theme.Ui(8.5f), Theme.TextSoft, 22, 82, 80, 18));

            CardPanel nameCard = new CardPanel();
            nameCard.CornerRadius = 8;
            nameCard.FillColor = Theme.Input;
            nameCard.BorderColor = Theme.Border;
            nameCard.SetBounds(22, 102, 476, 38);
            Controls.Add(nameCard);

            _nameBox = new TextBox();
            _nameBox.BorderStyle = BorderStyle.None;
            _nameBox.BackColor = Theme.Input;
            _nameBox.ForeColor = Theme.Text;
            _nameBox.Font = Theme.Ui(9.5f);
            _nameBox.SetBounds(11, 10, 454, 19);
            _nameBox.HandleCreated += delegate { NativeMethods.SetCueBanner(_nameBox, "例如：获取用户详情"); };
            _nameBox.Enter += delegate { nameCard.BorderColor = Color.FromArgb(214, 154, 94); nameCard.Invalidate(); };
            _nameBox.Leave += delegate { nameCard.BorderColor = Theme.Border; nameCard.Invalidate(); };
            nameCard.Controls.Add(_nameBox);

            Controls.Add(DialogUi.Label("目录", Theme.Ui(8.5f), Theme.TextSoft, 22, 152, 80, 18));

            _folderBox = new ComboBox();
            _folderBox.DropDownStyle = ComboBoxStyle.DropDown;
            _folderBox.FlatStyle = FlatStyle.Flat;
            _folderBox.BackColor = Theme.Input;
            _folderBox.ForeColor = Theme.Text;
            _folderBox.Font = Theme.Ui(9f);
            _folderBox.SetBounds(22, 172, 476, 26);
            _folderBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _folderBox.AutoCompleteSource = AutoCompleteSource.ListItems;
            Controls.Add(_folderBox);

            _folderBox.Items.Add("未分组");
            if (folders != null)
            {
                foreach (string folder in folders)
                {
                    string value = RequestLibraryStore.NormalizeFolder(folder);
                    if (value.Length == 0 || string.Equals(value, "未分组", StringComparison.CurrentCultureIgnoreCase)) continue;
                    if (!ContainsFolder(value)) _folderBox.Items.Add(value);
                }
            }
            string initial = RequestLibraryStore.NormalizeFolder(initialFolder);
            if (initial.Length > 0 && !ContainsFolder(initial)) _folderBox.Items.Add(initial);
            _folderBox.Text = initial.Length == 0 ? "未分组" : initial;

            Label folderHint = DialogUi.Label("可选择已有目录，或直接输入新目录；留空保存到「未分组」。",
                Theme.Ui(7.8f), Theme.Faint, 22, 202, 476, 18);
            Controls.Add(folderHint);

            _error = DialogUi.Label(string.Empty, Theme.Ui(8f), Theme.Red, 22, 228, 476, 18);
            Controls.Add(_error);

            RoundButton cancel = DialogUi.Button("取消", 70, false);
            cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(ClientSize.Width - 22 - cancel.Width, 304, cancel.Width, 32);
            Controls.Add(cancel);

            RoundButton save = DialogUi.Button("保存", 82, true);
            save.SetBounds(cancel.Left - 8 - save.Width, 304, save.Width, 32);
            save.Click += delegate { Commit(false); };
            Controls.Add(save);

            if (_allowSaveAs)
            {
                RoundButton saveAs = DialogUi.Button("另存为", 74, false);
                saveAs.SetBounds(save.Left - 8 - saveAs.Width, 304, saveAs.Width, 32);
                saveAs.Click += delegate { Commit(true); };
                Controls.Add(saveAs);
            }

            AcceptButton = save;
            CancelButton = cancel;

            _nameBox.Text = initialName ?? string.Empty;
            Shown += delegate
            {
                _nameBox.Focus();
                _nameBox.SelectAll();
            };
        }

        private bool ContainsFolder(string name)
        {
            foreach (object item in _folderBox.Items)
            {
                if (string.Equals(Convert.ToString(item), name, StringComparison.CurrentCultureIgnoreCase)) return true;
            }
            return false;
        }

        private void Commit(bool asCopy)
        {
            string name = (_nameBox.Text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (name.Length > 80) name = name.Substring(0, 80).Trim();
            if (name.Length == 0)
            {
                _error.Text = "请填写请求名称。";
                _nameBox.Focus();
                return;
            }
            string folderText = (_folderBox.Text ?? string.Empty).Trim();
            if (string.Equals(folderText, "未分组", StringComparison.CurrentCultureIgnoreCase))
                folderText = string.Empty;
            string folder = RequestLibraryStore.NormalizeFolder(folderText);
            RequestName = name;
            RequestFolder = folder;
            SaveAs = asCopy;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal sealed class PromptDialog : LightDialog
    {
        private readonly TextBox _box;
        private readonly Label _error;

        public string Value { get; private set; }

        private PromptDialog(string title, string label, string initialValue)
            : base(title, new Size(420, 202))
        {
            Controls.Add(DialogUi.Label(label, Theme.Ui(8.5f), Theme.TextSoft, 22, 20, 376, 18));

            CardPanel card = new CardPanel();
            card.CornerRadius = 8;
            card.FillColor = Theme.Input;
            card.BorderColor = Theme.Border;
            card.SetBounds(22, 42, 376, 38);
            Controls.Add(card);

            _box = new TextBox();
            _box.BorderStyle = BorderStyle.None;
            _box.BackColor = Theme.Input;
            _box.ForeColor = Theme.Text;
            _box.Font = Theme.Ui(9.5f);
            _box.SetBounds(11, 10, 354, 19);
            _box.Text = initialValue ?? string.Empty;
            _box.Enter += delegate { card.BorderColor = Color.FromArgb(214, 154, 94); card.Invalidate(); };
            _box.Leave += delegate { card.BorderColor = Theme.Border; card.Invalidate(); };
            _box.TextChanged += delegate { _error.Text = string.Empty; };
            card.Controls.Add(_box);

            _error = DialogUi.Label(string.Empty, Theme.Ui(8f), Theme.Red, 22, 86, 376, 18);
            Controls.Add(_error);

            RoundButton cancel = DialogUi.Button("取消", 70, false);
            cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(ClientSize.Width - 22 - cancel.Width, 150, cancel.Width, 32);
            Controls.Add(cancel);

            RoundButton ok = DialogUi.Button("确定", 82, true);
            ok.SetBounds(cancel.Left - 8 - ok.Width, 150, ok.Width, 32);
            ok.Click += delegate { Commit(); };
            Controls.Add(ok);

            AcceptButton = ok;
            CancelButton = cancel;
            Shown += delegate
            {
                _box.Focus();
                _box.SelectAll();
            };
        }

        private void Commit()
        {
            string value = (_box.Text ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                _error.Text = "名称不能为空。";
                _box.Focus();
                return;
            }
            Value = value;
            DialogResult = DialogResult.OK;
            Close();
        }

        public static string Ask(IWin32Window owner, string title, string label, string initialValue)
        {
            using (PromptDialog dialog = new PromptDialog(title, label, initialValue))
            {
                return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.Value : null;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CurlBox
{
    internal sealed class SavedRequestEventArgs : EventArgs
    {
        public SavedRequest Request;
        public bool Send;
    }

    internal sealed class RequestLibraryView : Panel
    {
        private sealed class FolderEntry
        {
            public string Key;
            public string Title;
            public string Folder;
            public bool IsAll;
            public bool IsUngrouped;
            public bool CanEdit;
            public int Count;
        }

        private readonly RequestLibrary _library;
        private readonly string _libraryPath;
        private readonly Label _title;
        private readonly Label _subtitle;
        private readonly Label _folderCaption;
        private readonly Label _requestCaption;
        private readonly Label _requestCount;
        private readonly Label _requestEmpty;
        private readonly ListBox _folderList;
        private readonly ListBox _requestList;
        private readonly CardPanel _folderCard;
        private readonly CardPanel _requestCard;
        private readonly RoundButton _newFolderButton;
        private readonly RoundButton _renameFolderButton;
        private readonly RoundButton _deleteFolderButton;
        private readonly RoundButton _openButton;
        private readonly RoundButton _sendButton;
        private readonly RoundButton _deleteRequestButton;
        private readonly List<FolderEntry> _folders = new List<FolderEntry>();
        private List<SavedRequest> _requests = new List<SavedRequest>();

        public event EventHandler<SavedRequestEventArgs> RequestActivated;
        public bool AutoTokenConfigured { get; set; }

        public RequestLibraryView(RequestLibrary library, string libraryPath)
        {
            _library = library ?? new RequestLibrary();
            _libraryPath = libraryPath;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Visible = false;

            _title = MakeLabel("请求库", Theme.UiBold(14f), Theme.Text);
            Controls.Add(_title);
            _subtitle = MakeLabel("目录右侧开关控制该目录的自动 Token；未保存请求按「未分组」处理。登录配置在设置中。",
                Theme.Ui(8.2f), Theme.Faint);
            Controls.Add(_subtitle);

            _folderCaption = MakeLabel("目录", Theme.UiBold(8.5f), Theme.TextSoft);
            Controls.Add(_folderCaption);
            _requestCaption = MakeLabel("请求", Theme.UiBold(8.5f), Theme.TextSoft);
            Controls.Add(_requestCaption);
            _requestCount = MakeLabel(string.Empty, Theme.Mono(8f), Theme.Faint);
            _requestCount.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(_requestCount);

            _folderCard = new CardPanel();
            _folderCard.CornerRadius = 10;
            _folderCard.FillColor = Theme.Panel;
            _folderCard.BorderColor = Theme.BorderSoft;
            Controls.Add(_folderCard);

            _folderList = new ListBox();
            _folderList.BorderStyle = BorderStyle.None;
            _folderList.BackColor = Theme.Panel;
            _folderList.ForeColor = Theme.Text;
            _folderList.Font = Theme.Ui(9f);
            _folderList.DrawMode = DrawMode.OwnerDrawFixed;
            _folderList.ItemHeight = 36;
            _folderList.IntegralHeight = false;
            _folderList.SelectedIndexChanged += delegate
            {
                RefreshRequests();
                UpdateActionState();
            };
            _folderList.DrawItem += DrawFolderItem;
            _folderList.MouseClick += FolderListMouseClick;
            _folderList.HandleCreated += delegate { NativeMethods.EnableLightScrollbars(_folderList); };
            _folderCard.Controls.Add(_folderList);

            _requestCard = new CardPanel();
            _requestCard.CornerRadius = 10;
            _requestCard.FillColor = Theme.Panel;
            _requestCard.BorderColor = Theme.BorderSoft;
            Controls.Add(_requestCard);

            _requestList = new ListBox();
            _requestList.BorderStyle = BorderStyle.None;
            _requestList.BackColor = Theme.Panel;
            _requestList.ForeColor = Theme.Text;
            _requestList.Font = Theme.Ui(9f);
            _requestList.DrawMode = DrawMode.OwnerDrawFixed;
            _requestList.ItemHeight = 58;
            _requestList.IntegralHeight = false;
            _requestList.SelectedIndexChanged += delegate { UpdateActionState(); };
            _requestList.DoubleClick += delegate { ActivateSelected(false); };
            _requestList.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    ActivateSelected(false);
                }
                else if (e.KeyCode == Keys.Delete)
                {
                    e.SuppressKeyPress = true;
                    DeleteSelectedRequest();
                }
            };
            _requestList.DrawItem += DrawRequestItem;
            _requestList.HandleCreated += delegate { NativeMethods.EnableLightScrollbars(_requestList); };
            _requestCard.Controls.Add(_requestList);

            _requestEmpty = MakeLabel("还没有保存的请求。\r\n在构建器里填写请求后点「保存」即可。", Theme.Ui(8.5f), Theme.Faint);
            _requestEmpty.TextAlign = ContentAlignment.MiddleCenter;
            _requestEmpty.BackColor = Theme.Panel;
            _requestCard.Controls.Add(_requestEmpty);

            _newFolderButton = DialogUi.SmallButton("新建目录", 70);
            _newFolderButton.Click += delegate { CreateFolder(); };
            Controls.Add(_newFolderButton);

            _renameFolderButton = DialogUi.SmallButton("重命名", 56);
            _renameFolderButton.Click += delegate { RenameFolder(); };
            Controls.Add(_renameFolderButton);

            _deleteFolderButton = DialogUi.SmallButton("删除", 48);
            _deleteFolderButton.Click += delegate { DeleteFolder(); };
            Controls.Add(_deleteFolderButton);

            _openButton = DialogUi.Button("打开", 64, false);
            _openButton.Click += delegate { ActivateSelected(false); };
            Controls.Add(_openButton);

            _sendButton = DialogUi.Button("发送", 64, true);
            _sendButton.Click += delegate { ActivateSelected(true); };
            Controls.Add(_sendButton);

            _deleteRequestButton = DialogUi.SmallButton("删除", 48);
            _deleteRequestButton.Click += delegate { DeleteSelectedRequest(); };
            Controls.Add(_deleteRequestButton);

            LayoutView();
            RefreshFolders(null);
        }

        private static Label MakeLabel(string text, Font font, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Theme.Bg;
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        public void Reload()
        {
            RefreshFolders(null);
        }

        public void FocusInitial()
        {
            if (_requestList.Items.Count > 0) _requestList.Focus();
            else _folderList.Focus();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            LayoutView();
        }

        private void LayoutView()
        {
            int width = Width;
            int height = Height;
            if (width <= 0 || height <= 0) return;

            _title.SetBounds(2, 0, Math.Min(320, width), 26);
            _subtitle.SetBounds(2, 28, Math.Max(120, width - 300), 18);

            int folderWidth = Math.Max(220, Math.Min(300, width / 4));
            _folderCaption.SetBounds(2, 52, folderWidth, 18);
            int requestX = folderWidth + 16;
            _requestCaption.SetBounds(requestX, 52, Math.Max(100, width - requestX - 130), 18);
            _requestCount.SetBounds(width - 122, 52, 120, 18);

            int contentTop = 74;
            int actionTop = Math.Max(contentTop + 120, height - 37);
            int contentHeight = Math.Max(120, actionTop - 8 - contentTop);
            _folderCard.SetBounds(2, contentTop, folderWidth, contentHeight);
            _requestCard.SetBounds(requestX, contentTop, Math.Max(140, width - requestX - 2), contentHeight);
            _folderList.SetBounds(1, 5, Math.Max(40, folderWidth - 2), Math.Max(40, contentHeight - 8));
            int requestWidth = _requestCard.Width;
            _requestList.SetBounds(1, 5, Math.Max(40, requestWidth - 2), Math.Max(40, contentHeight - 8));
            _requestEmpty.SetBounds(20, 20, Math.Max(40, requestWidth - 40), Math.Max(20, contentHeight - 40));
            _requestEmpty.BringToFront();

            _newFolderButton.SetBounds(2, actionTop, 70, 30);
            _renameFolderButton.SetBounds(_newFolderButton.Right + 6, actionTop, 56, 30);
            _deleteFolderButton.SetBounds(_renameFolderButton.Right + 6, actionTop, 48, 30);

            int right = width - 2;
            _deleteRequestButton.SetBounds(right - 48, actionTop, 48, 30);
            right -= 48 + 8;
            _sendButton.SetBounds(right - 64, actionTop - 1, 64, 32);
            right -= 64 + 8;
            _openButton.SetBounds(right - 64, actionTop - 1, 64, 32);
        }

        private void RefreshFolders(string selectKey)
        {
            string currentKey = selectKey;
            FolderEntry selected = _folderList.SelectedIndex >= 0 && _folderList.SelectedIndex < _folders.Count
                ? _folders[_folderList.SelectedIndex]
                : null;
            if (currentKey == null && selected != null) currentKey = selected.Key;

            _folders.Clear();
            List<SavedRequest> requests = _library.Requests ?? new List<SavedRequest>();
            _folders.Add(new FolderEntry
            {
                Key = "all",
                Title = "全部请求",
                Folder = null,
                IsAll = true,
                IsUngrouped = false,
                CanEdit = false,
                Count = requests.Count
            });
            int ungrouped = 0;
            foreach (SavedRequest request in requests)
            {
                if (request != null && string.IsNullOrEmpty(RequestLibraryStore.NormalizeFolder(request.Folder))) ungrouped++;
            }
            _folders.Add(new FolderEntry
            {
                Key = "ungrouped",
                Title = "未分组",
                Folder = string.Empty,
                IsAll = false,
                IsUngrouped = true,
                CanEdit = false,
                Count = ungrouped
            });
            foreach (string folder in RequestLibraryStore.AllFolders(_library))
            {
                int count = 0;
                foreach (SavedRequest request in requests)
                {
                    if (request != null && string.Equals(RequestLibraryStore.NormalizeFolder(request.Folder),
                        folder, StringComparison.CurrentCultureIgnoreCase)) count++;
                }
                _folders.Add(new FolderEntry
                {
                    Key = "folder:" + folder,
                    Title = folder,
                    Folder = folder,
                    IsAll = false,
                    CanEdit = true,
                    Count = count
                });
            }

            _folderList.BeginUpdate();
            _folderList.Items.Clear();
            int selectIndex = 0;
            for (int i = 0; i < _folders.Count; i++)
            {
                _folderList.Items.Add(_folders[i]);
                if (string.Equals(_folders[i].Key, currentKey, StringComparison.Ordinal)) selectIndex = i;
            }
            _folderList.EndUpdate();
            _folderList.SelectedIndex = _folderList.Items.Count > 0 ? selectIndex : -1;
            RefreshRequests();
            UpdateActionState();
        }

        private void RefreshRequests()
        {
            FolderEntry folder = SelectedFolder();
            IEnumerable<SavedRequest> source = _library.Requests ?? new List<SavedRequest>();
            List<SavedRequest> items;
            if (folder == null || folder.IsAll)
            {
                items = source.Where(delegate(SavedRequest request) { return request != null; }).ToList();
            }
            else
            {
                string wanted = folder.Folder ?? string.Empty;
                items = source.Where(delegate(SavedRequest request)
                {
                    return request != null && string.Equals(RequestLibraryStore.NormalizeFolder(request.Folder),
                        wanted, StringComparison.CurrentCultureIgnoreCase);
                }).ToList();
            }
            items.Sort(delegate(SavedRequest a, SavedRequest b)
            {
                int compare = string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
                if (compare != 0) return compare;
                return b.UpdatedAt.CompareTo(a.UpdatedAt);
            });
            _requests = items;

            _requestList.BeginUpdate();
            _requestList.Items.Clear();
            foreach (SavedRequest request in _requests) _requestList.Items.Add(request);
            _requestList.EndUpdate();
            if (_requestList.Items.Count > 0) _requestList.SelectedIndex = 0;
            _requestEmpty.Visible = _requests.Count == 0;
            _requestCaption.Text = folder == null || folder.IsAll ? "请求" : folder.Title;
            _requestCount.Text = _requests.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " 个";
        }

        private FolderEntry SelectedFolder()
        {
            if (_folderList.SelectedIndex < 0 || _folderList.SelectedIndex >= _folders.Count) return null;
            return _folders[_folderList.SelectedIndex];
        }

        private SavedRequest SelectedRequest()
        {
            if (_requestList.SelectedIndex < 0 || _requestList.SelectedIndex >= _requests.Count) return null;
            return _requests[_requestList.SelectedIndex];
        }

        private void UpdateActionState()
        {
            FolderEntry folder = SelectedFolder();
            _renameFolderButton.Enabled = folder != null && folder.CanEdit;
            _deleteFolderButton.Enabled = folder != null && folder.CanEdit;
            bool hasRequest = SelectedRequest() != null;
            _openButton.Enabled = hasRequest;
            _sendButton.Enabled = hasRequest;
            _deleteRequestButton.Enabled = hasRequest;
        }

        private static Rectangle AutoSwitchBounds(Rectangle row)
        {
            int width = 54;
            int height = 20;
            return new Rectangle(row.Right - 10 - width, row.Top + (row.Height - height) / 2, width, height);
        }

        private bool FolderAutoEnabled(FolderEntry folder)
        {
            if (folder == null || folder.IsAll) return false;
            if (folder.IsUngrouped) return _library.AutoTokenUngrouped;
            string name = RequestLibraryStore.NormalizeFolder(folder.Folder);
            if (_library.AutoTokenFolders == null) return false;
            foreach (string item in _library.AutoTokenFolders)
            {
                if (string.Equals(RequestLibraryStore.NormalizeFolder(item), name,
                    StringComparison.CurrentCultureIgnoreCase)) return true;
            }
            return false;
        }

        private void FolderListMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            int index = _folderList.IndexFromPoint(e.Location);
            if (index < 0 || index >= _folders.Count) return;
            FolderEntry folder = _folders[index];
            if (folder.IsAll) return;
            Rectangle row = _folderList.GetItemRectangle(index);
            Rectangle bounds = AutoSwitchBounds(row);
            if (!bounds.Contains(e.Location)) return;

            bool enable = !FolderAutoEnabled(folder);
            if (enable && !AutoTokenConfigured)
            {
                MessageBox.Show(this,
                    "还没有配置登录接口。\r\n请先在「设置 → 自动 Token 登录设置…」中填写登录 URL 和账号信息。",
                    "CurlBox", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (folder.IsUngrouped)
            {
                bool old = _library.AutoTokenUngrouped;
                _library.AutoTokenUngrouped = enable;
                if (!Persist()) _library.AutoTokenUngrouped = old;
            }
            else
            {
                string name = RequestLibraryStore.NormalizeFolder(folder.Folder);
                bool was = FolderAutoEnabled(folder);
                if (enable)
                {
                    if (!was) _library.AutoTokenFolders.Add(name);
                }
                else
                {
                    _library.AutoTokenFolders.RemoveAll(delegate(string item)
                    {
                        return string.Equals(RequestLibraryStore.NormalizeFolder(item), name,
                            StringComparison.CurrentCultureIgnoreCase);
                    });
                }
                if (!Persist() && !was)
                {
                    _library.AutoTokenFolders.RemoveAll(delegate(string item)
                    {
                        return string.Equals(RequestLibraryStore.NormalizeFolder(item), name,
                            StringComparison.CurrentCultureIgnoreCase);
                    });
                }
            }
            _folderList.Invalidate();
        }

        private void DrawFolderItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _folders.Count) return;
            FolderEntry folder = _folders[e.Index];
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (SolidBrush background = new SolidBrush(selected ? Theme.Selected : Theme.Panel))
                e.Graphics.FillRectangle(background, e.Bounds);
            if (selected)
            {
                using (SolidBrush accent = new SolidBrush(Theme.Accent))
                    e.Graphics.FillRectangle(accent, e.Bounds.Left, e.Bounds.Top + 6, 2, e.Bounds.Height - 12);
            }

            Color iconColor = selected ? Theme.Accent : Theme.Muted;
            int iconX = e.Bounds.Left + 12;
            int iconY = e.Bounds.Top + e.Bounds.Height / 2 - 5;
            using (SolidBrush icon = new SolidBrush(iconColor))
            {
                e.Graphics.FillRectangle(icon, iconX, iconY + 2, 14, 9);
                e.Graphics.FillRectangle(icon, iconX + 1, iconY, 7, 3);
            }

            bool hasSwitch = !folder.IsAll;
            Rectangle switchBounds = AutoSwitchBounds(e.Bounds);
            int countX = hasSwitch ? switchBounds.Left - 26 : e.Bounds.Right - 30;
            int titleRight = hasSwitch ? switchBounds.Left - 8 : e.Bounds.Right - 34;
            int textWidth = Math.Max(16, titleRight - (e.Bounds.Left + 34));
            TextRenderer.DrawText(e.Graphics, folder.Title, selected ? Theme.UiBold(8.8f) : Theme.Ui(8.8f),
                new Rectangle(e.Bounds.Left + 34, e.Bounds.Top, textWidth, e.Bounds.Height),
                selected ? Theme.Text : Theme.TextSoft,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPadding);
            TextRenderer.DrawText(e.Graphics, folder.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Theme.Mono(7.8f), new Rectangle(countX, e.Bounds.Top, 20, e.Bounds.Height),
                selected ? Theme.Accent : Theme.Faint,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            if (hasSwitch)
                DrawAutoSwitch(e.Graphics, switchBounds, FolderAutoEnabled(folder));
        }

        private static void DrawAutoSwitch(Graphics graphics, Rectangle bounds, bool enabled)
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (System.Drawing.Drawing2D.GraphicsPath path = Theme.Rounded(bounds, 10))
            {
                using (SolidBrush fill = new SolidBrush(enabled ? Theme.Mint : Theme.PanelRaised))
                    graphics.FillPath(fill, path);
                using (Pen pen = new Pen(enabled ? Theme.Mint : Theme.Border, 1))
                    graphics.DrawPath(pen, path);
            }
            TextRenderer.DrawText(graphics, enabled ? "自动开" : "自动关", Theme.UiBold(7.2f), bounds,
                enabled ? Color.White : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }

        private void DrawRequestItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _requests.Count) return;
            SavedRequest request = _requests[e.Index];
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (SolidBrush background = new SolidBrush(selected ? Theme.Selected : Theme.Panel))
                e.Graphics.FillRectangle(background, e.Bounds);
            using (Pen line = new Pen(Theme.BorderSoft))
                e.Graphics.DrawLine(line, e.Bounds.Left + 8, e.Bounds.Bottom - 1, e.Bounds.Right - 8, e.Bounds.Bottom - 1);
            if (selected)
            {
                using (SolidBrush accent = new SolidBrush(Theme.Accent))
                    e.Graphics.FillRectangle(accent, e.Bounds.Left, e.Bounds.Top + 6, 2, e.Bounds.Height - 12);
            }

            BuilderDraft draft = request.Request ?? new BuilderDraft();
            string method = string.IsNullOrEmpty(draft.Method) ? "GET" : draft.Method.ToUpperInvariant();
            Color methodColor = Theme.ColorForMethod(method);
            Size measured = TextRenderer.MeasureText(method, Theme.MonoBold(7.2f), Size.Empty, TextFormatFlags.NoPadding);
            int pillWidth = Math.Min(58, Math.Max(34, measured.Width + 12));
            Rectangle pill = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top + 10, pillWidth, 17);
            Theme.DrawPill(e.Graphics, pill, method, Theme.MonoBold(7.2f), methodColor,
                Color.FromArgb(28, methodColor), Color.FromArgb(58, methodColor));

            int textLeft = pill.Right + 9;
            int textWidth = Math.Max(40, e.Bounds.Right - textLeft - 12);
            TextRenderer.DrawText(e.Graphics, request.Name ?? string.Empty, Theme.UiBold(9f),
                new Rectangle(textLeft, e.Bounds.Top + 7, textWidth, 20),
                selected ? Theme.Text : Theme.TextSoft,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPadding);

            string folder = RequestLibraryStore.NormalizeFolder(request.Folder);
            string meta = (folder.Length == 0 ? "未分组" : folder) + "  ·  " +
                (request.UpdatedAt > 0 ? UiText.RelativeTime(request.UpdatedAt) : "—") + "  ·  " +
                UiText.DisplayUrl(draft.Url);
            TextRenderer.DrawText(e.Graphics, meta, Theme.Ui(7.8f),
                new Rectangle(textLeft, e.Bounds.Top + 29, textWidth, 18),
                selected ? Theme.Muted : Theme.Faint,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPadding);
        }

        private bool Persist()
        {
            string error;
            if (RequestLibraryStore.Save(_libraryPath, _library, out error)) return true;
            MessageBox.Show(this, "请求库保存失败：" + error, "CurlBox", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        private void CreateFolder()
        {
            string name = PromptDialog.Ask(FindForm(), "新建目录", "目录名称", string.Empty);
            if (name == null) return;
            name = RequestLibraryStore.NormalizeFolder(name);
            if (name.Length == 0) return;
            foreach (string folder in RequestLibraryStore.AllFolders(_library))
            {
                if (string.Equals(folder, name, StringComparison.CurrentCultureIgnoreCase))
                {
                    MessageBox.Show(this, "已存在同名目录。", "CurlBox", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            _library.Folders.Add(name);
            if (!Persist()) return;
            RefreshFolders("folder:" + name);
        }

        private void RenameFolder()
        {
            FolderEntry folder = SelectedFolder();
            if (folder == null || !folder.CanEdit) return;
            string name = PromptDialog.Ask(FindForm(), "重命名目录", "目录名称", folder.Title);
            if (name == null) return;
            name = RequestLibraryStore.NormalizeFolder(name);
            if (name.Length == 0) return;
            if (string.Equals(name, folder.Title, StringComparison.CurrentCultureIgnoreCase)) return;
            foreach (string existing in RequestLibraryStore.AllFolders(_library))
            {
                if (string.Equals(existing, name, StringComparison.CurrentCultureIgnoreCase))
                {
                    MessageBox.Show(this, "已存在同名目录。", "CurlBox", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            for (int i = 0; i < _library.Folders.Count; i++)
            {
                if (string.Equals(RequestLibraryStore.NormalizeFolder(_library.Folders[i]),
                    folder.Title, StringComparison.CurrentCultureIgnoreCase))
                    _library.Folders[i] = name;
            }
            foreach (SavedRequest request in _library.Requests)
            {
                if (request != null && string.Equals(RequestLibraryStore.NormalizeFolder(request.Folder),
                    folder.Title, StringComparison.CurrentCultureIgnoreCase))
                    request.Folder = name;
            }
            if (_library.AutoTokenFolders != null)
            {
                for (int i = 0; i < _library.AutoTokenFolders.Count; i++)
                {
                    if (string.Equals(RequestLibraryStore.NormalizeFolder(_library.AutoTokenFolders[i]),
                        folder.Title, StringComparison.CurrentCultureIgnoreCase))
                        _library.AutoTokenFolders[i] = name;
                }
            }
            if (!Persist()) return;
            RefreshFolders("folder:" + name);
        }

        private void DeleteFolder()
        {
            FolderEntry folder = SelectedFolder();
            if (folder == null || !folder.CanEdit) return;
            if (MessageBox.Show(this,
                "删除目录「" + folder.Title + "」？\r\n其中的 " + folder.Count + " 个请求会移动到「未分组」。",
                "CurlBox", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                return;

            _library.Folders.RemoveAll(delegate(string item)
            {
                return string.Equals(RequestLibraryStore.NormalizeFolder(item), folder.Title,
                    StringComparison.CurrentCultureIgnoreCase);
            });
            foreach (SavedRequest request in _library.Requests)
            {
                if (request != null && string.Equals(RequestLibraryStore.NormalizeFolder(request.Folder),
                    folder.Title, StringComparison.CurrentCultureIgnoreCase))
                    request.Folder = string.Empty;
            }
            if (_library.AutoTokenFolders != null)
            {
                _library.AutoTokenFolders.RemoveAll(delegate(string item)
                {
                    return string.Equals(RequestLibraryStore.NormalizeFolder(item), folder.Title,
                        StringComparison.CurrentCultureIgnoreCase);
                });
            }
            if (!Persist()) return;
            RefreshFolders("ungrouped");
        }

        private void DeleteSelectedRequest()
        {
            SavedRequest request = SelectedRequest();
            if (request == null) return;
            if (MessageBox.Show(this, "删除请求「" + request.Name + "」？", "CurlBox",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                return;
            _library.Requests.Remove(request);
            if (!Persist()) return;
            RefreshFolders(null);
        }

        private void ActivateSelected(bool send)
        {
            SavedRequest request = SelectedRequest();
            if (request == null) return;
            EventHandler<SavedRequestEventArgs> handler = RequestActivated;
            if (handler == null) return;
            handler(this, new SavedRequestEventArgs { Request = request, Send = send });
        }
    }
}

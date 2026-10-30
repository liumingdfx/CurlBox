using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using WinTimer = System.Windows.Forms.Timer;

namespace CurlBox
{
    public sealed class AppSettings
    {
        public bool FollowRedirects { get; set; }
        public bool Insecure { get; set; }
        public bool Verbose { get; set; }
        public bool Pretty { get; set; }
        public bool Wrap { get; set; }
        public string MethodOverride { get; set; }
        public string Draft { get; set; }
        public int WindowLeft { get; set; }
        public int WindowTop { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public bool WindowMaximized { get; set; }
        public double SplitRatio { get; set; }
        public BuilderDraft Builder { get; set; }
        public string AdminLoginUrl { get; set; }
        public string AdminUsername { get; set; }
        public string AdminPassword { get; set; }
        public string AdminAuthCode { get; set; }
        public string AdminToken { get; set; }

        public AppSettings()
        {
            Pretty = true;
            Wrap = true;
            MethodOverride = string.Empty;
            Draft = string.Empty;
            WindowWidth = 0;
            WindowHeight = 0;
            SplitRatio = 0.43;
            Builder = new BuilderDraft();
            AdminLoginUrl = string.Empty;
            AdminUsername = string.Empty;
            AdminPassword = string.Empty;
            AdminAuthCode = string.Empty;
            AdminToken = string.Empty;
        }
    }

    public sealed class HistoryEntry
    {
        public string Id { get; set; }
        public string Command { get; set; }
        public string Method { get; set; }
        public string Override { get; set; }
        public long At { get; set; }
        public int Status { get; set; }
        public long DurationMs { get; set; }
        public string Url { get; set; }
        public int ExitCode { get; set; }

        public HistoryEntry()
        {
            Id = Guid.NewGuid().ToString("N");
            Command = string.Empty;
            Method = "GET";
            Override = string.Empty;
            Url = string.Empty;
        }
    }

    internal sealed class MainForm : Form
    {
        private enum RightState { Empty, Loading, Result, Fatal }
        private enum ResponseTab { Body, Headers, Log }
        private enum ToastKind { Info, Error }

        private const int HotkeyId = 0xC0DE;
        private const int MaxHistory = 30;

        private bool _startHidden;
        private readonly string _settingsPath;
        private readonly string _historyPath;
        private AppSettings _settings;
        private List<HistoryEntry> _history;
        private Icon _appIcon;
        private string _curlVersion = "curl";
        private bool _reallyExit;
        private bool _trayBalloonShown;

        private Panel _topBar;
        private BrandMark _brandMark;
        private Label _brandTitle;
        private Label _brandSub;
        private Label _topHotkey;
        private RoundButton _settingsButton;
        private ContextMenuStrip _settingsMenu;
        private ToolStripMenuItem _settingsAutoStart;

        private CardPanel _leftCard;
        private Label _leftTitle;
        private Label _leftSubtitle;
        private RoundButton _pasteButton;
        private RoundButton _clearButton;
        private CardPanel _editorCard;
        private TextBox _curlBox;
        private Label _editorPlaceholder;
        private CardPanel _previewCard;
        private Pill _previewMethod;
        private Label _previewUrl;
        private Label _previewStats;
        private Label _previewWarn;
        private RoundButton _sendButton;
        private CardPanel _methodCard;
        private Label _methodLabel;
        private TextBox _methodBox;
        private Label _methodPlaceholder;
        private RoundButton _optionsButton;
        private ContextMenuStrip _optionsMenu;
        private ToolStripMenuItem _followItem;
        private ToolStripMenuItem _insecureItem;
        private ToolStripMenuItem _verboseItem;
        private Label _historyTitle;
        private Label _historyCount;
        private LinkLabel _clearHistory;
        private ListBox _historyList;
        private Label _historyEmpty;

        private CardPanel _rightCard;
        private Panel _emptyPanel;
        private CardPanel _emptyArt;
        private Label _emptyPostMethod;
        private Label _emptyPostPath;
        private Label _emptyPostStatus;
        private Label _emptyGetMethod;
        private Label _emptyGetPath;
        private Label _emptyGetStatus;
        private Label _emptyEyebrow;
        private Label _emptyTitle;
        private Label _emptyDescription;
        private Label _emptySteps;
        private RoundButton _sampleButton;
        private Label _emptyPrivacy;

        private Panel _loadingPanel;
        private Label _loadingTitle;
        private Label _loadingUrl;
        private Label _loadingMeta;
        private RoundButton _cancelButton;

        private Panel _fatalPanel;
        private Pill _fatalMark;
        private Label _fatalTitle;
        private Label _fatalMessage;
        private TextBox _fatalDetail;
        private RoundButton _retryButton;
        private RoundButton _backButton;

        private Panel _resultPanel;
        private Panel _metaPanel;
        private Panel _metaDivider;
        private Pill _statusPill;
        private Label _statusText;
        private Label _metaFacts;
        private Pill _methodPill;
        private Label _urlLabel;
        private RoundButton _copyUrlButton;
        private CardPanel _alertPanel;
        private Label _alertMark;
        private Label _alertTitle;
        private Label _alertText;
        private RoundButton _alertLogButton;
        private Panel _tabsPanel;
        private Panel _tabsDivider;
        private RoundButton _bodyTab;
        private RoundButton _headersTab;
        private RoundButton _logTab;
        private Panel _bodyPanel;
        private Panel _headersPanel;
        private Panel _logPanel;

        private Label _bodyKindLabel;
        private Label _bodySizeLabel;
        private Label _bodyTruncatedLabel;
        private RoundButton _prettyButton;
        private RoundButton _wrapButton;
        private RoundButton _copyBodyButton;
        private RoundButton _saveButton;
        private CardPanel _bodyCard;
        private RichTextBox _bodyBox;
        private Panel _imagePanel;
        private PictureBox _imageBox;
        private Label _imageInfo;
        private Label _bodyEmptyLabel;

        private Label _headersCaption;
        private RoundButton _copyHeadersButton;
        private Label _redirectLabel;
        private CardPanel _headersCard;
        private RichTextBox _headersBox;
        private Label _timingLabel;

        private Label _logCaption;
        private RoundButton _copyLogButton;
        private CardPanel _logCard;
        private RichTextBox _logBox;

        private Panel _footer;
        private Label _footerDot;
        private Label _footerLeft;
        private Label _footerRight;

        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private ToolStripMenuItem _trayOpen;
        private ToolStripMenuItem _trayAutoStart;
        private ToolStripMenuItem _trayExit;

        private WinTimer _parseTimer;
        private WinTimer _draftTimer;
        private WinTimer _elapsedTimer;
        private WinTimer _memoryTimer;
        private WinTimer _toastTimer;
        private ToolTip _toolTip;

        private CardPanel _toast;
        private Label _toastLabel;

        private CurlCommand _preview;
        private string _previewError;
        private bool _running;
        private CancellationTokenSource _cts;
        private CurlResult _result;
        private DateTime _requestStarted;
        private RightState _rightState = RightState.Empty;
        private ResponseTab _activeTab = ResponseTab.Body;
        private bool _pretty;
        private bool _wrap;
        private bool _prettyAvailable;
        private string _prettyText;
        private bool _suppressHistorySelection;
        private uint _showMessage;
        private bool _hotkeyRegistered;
        private long _memoryBytes;

        private bool _builderMode;
        private bool _builderUiReady;
        private bool _applyingBuilderDraft;
        private SplitHandle _splitHandle;
        private double _splitRatio = 0.43;
        private RoundButton _modeCurlButton;
        private RoundButton _modeBuilderButton;
        private RoundButton _modeLibraryButton;
        private bool _libraryMode;
        private RequestLibraryView _libraryView;
        private RoundButton _builderImportButton;
        private RoundButton _builderResetButton;
        private RoundButton _builderSaveButton;
        private string _libraryPath;
        private RequestLibrary _library;
        private bool _libraryLoaded;
        private string _currentSavedId;
        private string _currentSavedName;
        private string _currentSavedFolder;
        private Panel _builderPanel;
        private RoundButton _builderMethodButton;
        private ContextMenuStrip _builderMethodMenu;
        private string _builderMethod = "GET";
        private CardPanel _builderUrlCard;
        private TextBox _builderUrlBox;
        private Label _builderUrlPlaceholder;
        private RoundButton _builderTabParams;
        private RoundButton _builderTabHeaders;
        private RoundButton _builderTabBody;
        private Panel _builderParamsPanel;
        private Panel _builderHeadersPanel;
        private Panel _builderBodyPanel;
        private CardPanel _builderParamsCard;
        private CardPanel _builderHeadersCard;
        private CardPanel _builderBodyCard;
        private KeyValueEditor _builderParamsGrid;
        private KeyValueEditor _builderHeadersGrid;
        private KeyValueEditor _builderFormGrid;
        private TextBox _builderBodyBox;
        private Label _builderBodyEmpty;
        private Label _builderBodyHint;
        private RoundButton _bodyModeNone;
        private RoundButton _bodyModeJson;
        private RoundButton _bodyModeForm;
        private RoundButton _bodyModeRaw;
        private int _builderTab;
        private string _builderBodyMode = "none";
        private List<BuilderExtraArg> _builderExtraArgs = new List<BuilderExtraArg>();
        private RoundButton _builderCopyButton;
        private Label _builderActionHint;

        public MainForm(bool startHidden)
        {
            _startHidden = startHidden;
            _settingsPath = Path.Combine(Program.DataDirectory, "settings.json");
            _historyPath = Path.Combine(Program.DataDirectory, "history.json");
            _libraryPath = Path.Combine(Program.DataDirectory, "requests.json");
            LoadData();
            _pretty = _settings.Pretty;
            _wrap = _settings.Wrap;
            _curlVersion = QueryCurlVersion();

            InitializeWindow();
            BuildUi();
            BuildMenus();
            BuildTimers();
            BuildTray();

            ApplyLoadedState();
            RenderHistory(false);
            ShowRightState(RightState.Empty);
            UpdatePreview();
            ApplyWindowBounds();

            if (_startHidden)
            {
                ShowInTaskbar = false;
            }
        }

        private void InitializeWindow()
        {
            Text = "CurlBox";
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Ui(9f);
            ClientSize = new Size(1180, 780);
            MinimumSize = new Size(1000, 680);
            KeyPreview = true;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            AllowDrop = false;

            try
            {
                _appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }
            if (_appIcon == null) _appIcon = SystemIcons.Application;
            Icon = _appIcon;
        }

        private void BuildUi()
        {
            SuspendLayout();
            BuildTopBar();
            BuildWorkspace();
            BuildFooter();
            BuildToast();
            ResumeLayout(true);
            ApplyOpaqueBackgrounds(this);
            LayoutAll();
        }

        private static Color EffectiveBackground(Control control)
        {
            Control parent = control == null ? null : control.Parent;
            while (parent != null)
            {
                CardPanel card = parent as CardPanel;
                if (card != null) return card.FillColor;
                if (parent.BackColor.A == 255) return parent.BackColor;
                parent = parent.Parent;
            }
            return Theme.Bg;
        }

        private static void AttachLightScrollbars(Control control)
        {
            if (control == null) return;
            control.HandleCreated += delegate { NativeMethods.EnableLightScrollbars(control); };
            if (control.IsHandleCreated) NativeMethods.EnableLightScrollbars(control);
        }

        private static void ApplyOpaqueBackgrounds(Control root)
        {
            if (root == null) return;
            foreach (Control child in root.Controls)
            {
                if (child is Label || child is Pill || child is RoundButton)
                {
                    child.BackColor = EffectiveBackground(child);
                }
                if (child.HasChildren)
                    ApplyOpaqueBackgrounds(child);
            }
        }

        private void BuildTopBar()
        {
            _topBar = new Panel();
            _topBar.Dock = DockStyle.Top;
            _topBar.Height = 54;
            _topBar.BackColor = Theme.Bg;
            Controls.Add(_topBar);

            _brandMark = new BrandMark();
            _brandMark.Location = new Point(18, 11);
            _topBar.Controls.Add(_brandMark);

            _brandTitle = MakeLabel("CurlBox", Theme.UiBold(13f), Theme.Text);
            _brandTitle.Location = new Point(58, 8);
            _brandTitle.Size = new Size(160, 23);
            _topBar.Controls.Add(_brandTitle);

            _brandSub = MakeLabel("LOCAL CURL RUNNER", Theme.Mono(7f), Theme.Faint);
            _brandSub.Location = new Point(59, 31);
            _brandSub.Size = new Size(220, 14);
            _topBar.Controls.Add(_brandSub);

            _modeCurlButton = MakeModeButton("粘贴 cURL", 74);
            _topBar.Controls.Add(_modeCurlButton);

            _modeBuilderButton = MakeModeButton("构建请求", 74);
            _topBar.Controls.Add(_modeBuilderButton);

            _modeLibraryButton = MakeModeButton("请求库", 74);
            _topBar.Controls.Add(_modeLibraryButton);

            _topHotkey = MakeLabel("Ctrl + Alt + C 唤起", Theme.Ui(8f), Theme.Faint);
            _topHotkey.TextAlign = ContentAlignment.MiddleRight;
            _topHotkey.Size = new Size(150, 28);
            _topBar.Controls.Add(_topHotkey);

            _settingsButton = new RoundButton();
            _settingsButton.Text = "设置";
            _settingsButton.Size = new Size(54, 28);
            _settingsButton.CornerRadius = 7;
            _settingsButton.FillColor = Theme.Panel;
            _settingsButton.FillColor2 = Theme.PanelRaised;
            _settingsButton.HoverColor = Theme.Hover;
            _settingsButton.BorderColor = Theme.Border;
            _settingsButton.TextColor = Theme.Muted;
            _settingsButton.Font = Theme.Ui(8.5f);
            _settingsButton.BackColor = Theme.Bg;
            _topBar.Controls.Add(_settingsButton);

            Panel topLine = new Panel();
            topLine.Height = 1;
            topLine.Dock = DockStyle.Bottom;
            topLine.BackColor = Theme.BorderSoft;
            _topBar.Controls.Add(topLine);
        }

        private void BuildWorkspace()
        {
            _leftCard = new CardPanel();
            _leftCard.CornerRadius = 14;
            Controls.Add(_leftCard);

            _rightCard = new CardPanel();
            _rightCard.CornerRadius = 14;
            Controls.Add(_rightCard);

            _splitHandle = new SplitHandle();
            _splitHandle.Dragging += delegate { ApplySplitDrag(); };
            _splitHandle.DragFinished += delegate { SaveSettings(); };
            _splitHandle.DoubleClick += delegate
            {
                _splitRatio = 0.43;
                LayoutWorkspace();
                SaveSettings();
            };
            Controls.Add(_splitHandle);

            BuildLeftPanel();
            BuildRightPanel();
            _splitHandle.BringToFront();
        }

        private void BuildLeftPanel()
        {
            _leftTitle = MakeLabel("粘贴 cURL", Theme.UiBold(10f), Theme.Text);
            _leftTitle.Size = new Size(220, 20);
            _leftCard.Controls.Add(_leftTitle);

            _leftSubtitle = MakeLabel("支持 bash / cmd / PowerShell 复制格式", Theme.Ui(8f), Theme.Faint);
            _leftSubtitle.Size = new Size(320, 16);
            _leftSubtitle.AutoEllipsis = true;
            _leftCard.Controls.Add(_leftSubtitle);

            _clearButton = MakeTextButton("清空", 48);
            _clearButton.Click += delegate { _curlBox.Text = string.Empty; UpdatePreview(); _curlBox.Focus(); };
            _leftCard.Controls.Add(_clearButton);

            _pasteButton = MakeTextButton("粘贴", 56);
            _pasteButton.Click += delegate { PasteFromClipboard(); };
            _leftCard.Controls.Add(_pasteButton);

            _builderImportButton = MakeTextButton("导入 cURL", 78);
            _builderImportButton.TextColor = Theme.Accent;
            _builderImportButton.Visible = false;
            _builderImportButton.Click += delegate { ImportCurlFromClipboard(); };
            _leftCard.Controls.Add(_builderImportButton);

            _builderResetButton = MakeTextButton("重置", 48);
            _builderResetButton.Visible = false;
            _builderResetButton.Click += delegate { ResetBuilder(); };
            _leftCard.Controls.Add(_builderResetButton);

            _builderSaveButton = MakeTextButton("保存", 48);
            _builderSaveButton.Visible = false;
            _builderSaveButton.FillColor = Color.Transparent;
            _builderSaveButton.FillColor2 = Color.Transparent;
            _builderSaveButton.BorderColor = Theme.AccentBorder;
            _builderSaveButton.TextColor = Theme.Accent;
            _builderSaveButton.Click += delegate { SaveBuilderRequest(); };
            _leftCard.Controls.Add(_builderSaveButton);

            _builderImportButton.BringToFront();
            _builderResetButton.BringToFront();
            _builderSaveButton.BringToFront();
            _pasteButton.BringToFront();
            _clearButton.BringToFront();

            _editorCard = new CardPanel();
            _editorCard.CornerRadius = 8;
            _editorCard.FillColor = Theme.Input;
            _editorCard.BorderColor = Theme.Border;
            _leftCard.Controls.Add(_editorCard);

            _curlBox = new TextBox();
            _curlBox.Multiline = true;
            _curlBox.AcceptsReturn = true;
            _curlBox.AcceptsTab = true;
            _curlBox.ScrollBars = ScrollBars.Vertical;
            _curlBox.WordWrap = true;
            _curlBox.BorderStyle = BorderStyle.None;
            _curlBox.BackColor = Theme.Input;
            _curlBox.ForeColor = Theme.TextSoft;
            _curlBox.Font = Theme.Mono(10f);
            _curlBox.ShortcutsEnabled = true;
            _curlBox.TextChanged += delegate
            {
                UpdatePlaceholder();
                ScheduleParse();
                ScheduleDraftSave();
            };
            _curlBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.A)
                {
                    _curlBox.SelectAll();
                    e.SuppressKeyPress = true;
                }
            };
            _editorCard.Controls.Add(_curlBox);
            AttachLightScrollbars(_curlBox);
            _curlBox.Enter += delegate
            {
                _editorCard.BorderColor = Color.FromArgb(214, 154, 94);
                _editorCard.Invalidate();
            };
            _curlBox.Leave += delegate
            {
                _editorCard.BorderColor = Theme.Border;
                _editorCard.Invalidate();
            };

            _editorPlaceholder = new Label();
            _editorPlaceholder.Text = "curl 'https://api.example.com/users' -H 'Authorization: Bearer ...'";
            _editorPlaceholder.Font = Theme.Mono(9.5f);
            _editorPlaceholder.ForeColor = Theme.Faint;
            _editorPlaceholder.BackColor = Theme.Input;
            _editorPlaceholder.AutoSize = false;
            _editorPlaceholder.Cursor = Cursors.IBeam;
            _editorPlaceholder.Click += delegate { _curlBox.Focus(); };
            _editorCard.Controls.Add(_editorPlaceholder);
            _editorPlaceholder.BringToFront();

            _previewCard = new CardPanel();
            _previewCard.CornerRadius = 8;
            _previewCard.FillColor = Theme.PanelRaised;
            _previewCard.BorderColor = Theme.BorderSoft;
            _leftCard.Controls.Add(_previewCard);

            _previewMethod = new Pill();
            _previewMethod.Height = 19;
            _previewMethod.Font = Theme.MonoBold(7.5f);
            _previewCard.Controls.Add(_previewMethod);

            _previewUrl = MakeLabel(string.Empty, Theme.Mono(8.5f), Theme.TextSoft);
            _previewUrl.AutoEllipsis = true;
            _previewCard.Controls.Add(_previewUrl);

            _previewStats = MakeLabel(string.Empty, Theme.Mono(7.8f), Theme.Faint);
            _previewStats.TextAlign = ContentAlignment.MiddleRight;
            _previewStats.AutoEllipsis = true;
            _previewCard.Controls.Add(_previewStats);

            _previewWarn = MakeLabel(string.Empty, Theme.Ui(7.8f), Theme.Warning);
            _previewWarn.AutoEllipsis = true;
            _previewCard.Controls.Add(_previewWarn);

            _sendButton = new RoundButton();
            _sendButton.Text = "发送   Ctrl+↵";
            _sendButton.CornerRadius = 9;
            _sendButton.Height = 38;
            _sendButton.FillColor = Theme.Accent;
            _sendButton.FillColor2 = Theme.AccentStrong;
            _sendButton.HoverColor = Color.FromArgb(168, 58, 11);
            _sendButton.BorderColor = Color.FromArgb(143, 46, 9);
            _sendButton.TextColor = Theme.AccentInk;
            _sendButton.Font = Theme.UiBold(9f);
            _sendButton.Click += SendButtonClick;
            _leftCard.Controls.Add(_sendButton);

            _methodCard = new CardPanel();
            _methodCard.CornerRadius = 9;
            _methodCard.FillColor = Theme.Input;
            _methodCard.BorderColor = Theme.Border;
            _leftCard.Controls.Add(_methodCard);

            _methodLabel = MakeLabel("方法", Theme.Ui(8f), Theme.Faint);
            _methodLabel.TextAlign = ContentAlignment.MiddleLeft;
            _methodLabel.AutoEllipsis = false;
            _methodCard.Controls.Add(_methodLabel);

            _methodBox = new TextBox();
            _methodBox.BorderStyle = BorderStyle.None;
            _methodBox.BackColor = Theme.Input;
            _methodBox.ForeColor = Theme.TextSoft;
            _methodBox.Font = Theme.Mono(9f);
            _methodBox.ShortcutsEnabled = true;
            _methodBox.CharacterCasing = CharacterCasing.Upper;
            _methodBox.HandleCreated += delegate { NativeMethods.SetCueBanner(_methodBox, "自动"); };
            _methodBox.TextChanged += delegate
            {
                if (_methodPlaceholder != null)
                    _methodPlaceholder.Visible = _methodBox.Text.Length == 0;
                UpdatePreview();
                ScheduleDraftSave();
            };
            _methodBox.Leave += delegate
            {
                _methodBox.Text = _methodBox.Text.Trim().ToUpperInvariant();
            };
            _methodBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    BeginSend();
                }
            };
            _methodCard.Controls.Add(_methodBox);
            _methodBox.Enter += delegate
            {
                _methodCard.BorderColor = Color.FromArgb(214, 154, 94);
                _methodCard.Invalidate();
            };
            _methodBox.Leave += delegate
            {
                _methodCard.BorderColor = Theme.Border;
                _methodCard.Invalidate();
            };
            _methodPlaceholder = new Label();
            _methodPlaceholder.Text = "自动";
            _methodPlaceholder.Font = Theme.Mono(9f);
            _methodPlaceholder.ForeColor = Theme.Faint;
            _methodPlaceholder.BackColor = Theme.Input;
            _methodPlaceholder.AutoSize = false;
            _methodPlaceholder.TextAlign = ContentAlignment.MiddleLeft;
            _methodPlaceholder.Cursor = Cursors.IBeam;
            _methodPlaceholder.Click += delegate { _methodBox.Focus(); };
            _methodCard.Controls.Add(_methodPlaceholder);
            _methodPlaceholder.BringToFront();
            AttachLightScrollbars(_methodBox);

            _optionsButton = new RoundButton();
            _optionsButton.Text = "选项";
            _optionsButton.CornerRadius = 9;
            _optionsButton.Height = 38;
            _optionsButton.FillColor = Theme.PanelRaised;
            _optionsButton.FillColor2 = Theme.PanelRaised;
            _optionsButton.HoverColor = Theme.Hover;
            _optionsButton.BorderColor = Theme.Border;
            _optionsButton.TextColor = Theme.Muted;
            _optionsButton.Font = Theme.Ui(8.5f);
            _leftCard.Controls.Add(_optionsButton);

            _historyTitle = MakeLabel("最近请求", Theme.UiBold(8.5f), Theme.Muted);
            _leftCard.Controls.Add(_historyTitle);

            _historyCount = MakeLabel("0", Theme.Mono(8f), Theme.Faint);
            _leftCard.Controls.Add(_historyCount);

            _clearHistory = new LinkLabel();
            _clearHistory.Text = "清空";
            _clearHistory.Font = Theme.Ui(8f);
            _clearHistory.LinkColor = Theme.Faint;
            _clearHistory.ActiveLinkColor = Theme.Accent;
            _clearHistory.VisitedLinkColor = Theme.Faint;
            _clearHistory.LinkBehavior = LinkBehavior.NeverUnderline;
            _clearHistory.Cursor = Cursors.Hand;
            _clearHistory.Click += delegate { ClearHistory(); };
            _leftCard.Controls.Add(_clearHistory);

            _historyList = new ListBox();
            _historyList.BorderStyle = BorderStyle.None;
            _historyList.BackColor = Theme.Panel;
            _historyList.ForeColor = Theme.Text;
            _historyList.Font = Theme.Ui(9f);
            _historyList.DrawMode = DrawMode.OwnerDrawFixed;
            _historyList.ItemHeight = 46;
            _historyList.IntegralHeight = false;
            _historyList.HorizontalScrollbar = false;
            _historyList.SelectedIndexChanged += delegate
            {
                if (_suppressHistorySelection) return;
                if (_historyList.SelectedIndex >= 0 && _historyList.SelectedIndex < _history.Count)
                {
                    LoadHistoryEntry(_history[_historyList.SelectedIndex]);
                }
            };
            _historyList.DrawItem += DrawHistoryItem;
            _leftCard.Controls.Add(_historyList);
            AttachLightScrollbars(_historyList);

            _historyEmpty = MakeLabel("发送过的请求会自动保存在本机，点击即可再次发送。", Theme.Ui(8.5f), Theme.Faint);
            _leftCard.Controls.Add(_historyEmpty);
            _historyEmpty.BringToFront();

            _toolTip = new ToolTip();
            _toolTip.InitialDelay = 300;
            _toolTip.AutoPopDelay = 8000;
            _toolTip.SetToolTip(_pasteButton, "从系统剪贴板粘贴 cURL");
            _toolTip.SetToolTip(_sendButton, "发送请求（Ctrl+Enter）");
            _toolTip.SetToolTip(_methodBox, "留空自动识别；也可以输入任意 HTTP 方法");
            _toolTip.SetToolTip(_optionsButton, "请求选项：重定向、忽略证书、详细日志");
        }

        private void EnsureBuilderUi()
        {
            if (_builderUiReady) return;

            _builderPanel = new Panel();
            _builderPanel.BackColor = Theme.Panel;
            _builderPanel.Visible = false;
            _leftCard.Controls.Add(_builderPanel);

            _builderMethodButton = new RoundButton();
            _builderMethodButton.Text = "GET";
            _builderMethodButton.Size = new Size(84, 36);
            _builderMethodButton.CornerRadius = 8;
            _builderMethodButton.HoverColor = Theme.Hover;
            _builderMethodButton.Font = Theme.MonoBold(9f);
            _builderMethodButton.BackColor = Theme.Panel;
            _builderMethodButton.Click += delegate
            {
                if (_builderMethodMenu == null) return;
                _builderMethodMenu.Show(_builderMethodButton, new Point(0, _builderMethodButton.Height + 4));
            };
            _builderPanel.Controls.Add(_builderMethodButton);

            _builderMethodMenu = new ContextMenuStrip();
            _builderMethodMenu.Font = Theme.Ui(9f);
            _builderMethodMenu.BackColor = Theme.Panel;
            _builderMethodMenu.ForeColor = Theme.Text;
            _builderMethodMenu.Renderer = new LightMenuRenderer();
            _builderMethodMenu.ShowImageMargin = false;
            string[] methods = new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" };
            foreach (string method in methods)
            {
                string value = method;
                ToolStripMenuItem item = new ToolStripMenuItem(method);
                item.Font = Theme.MonoBold(8.5f);
                item.ForeColor = Theme.ColorForMethod(method);
                item.Click += delegate { SetBuilderMethod(value); };
                _builderMethodMenu.Items.Add(item);
            }

            _builderUrlCard = new CardPanel();
            _builderUrlCard.CornerRadius = 8;
            _builderUrlCard.FillColor = Theme.Input;
            _builderUrlCard.BorderColor = Theme.Border;
            _builderPanel.Controls.Add(_builderUrlCard);

            _builderUrlBox = new TextBox();
            _builderUrlBox.BorderStyle = BorderStyle.None;
            _builderUrlBox.BackColor = Theme.Input;
            _builderUrlBox.ForeColor = Theme.TextSoft;
            _builderUrlBox.Font = Theme.Mono(9.5f);
            _builderUrlBox.ShortcutsEnabled = true;
            _builderUrlBox.TextChanged += delegate
            {
                UpdateBuilderUrlPlaceholder();
                BuilderChanged(null, EventArgs.Empty);
            };
            _builderUrlBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    BeginSend();
                    return;
                }
                if (e.Control && e.KeyCode == Keys.V)
                {
                    try
                    {
                        if (Clipboard.ContainsText())
                        {
                            string text = Clipboard.GetText();
                            if (LooksLikeCurl(text))
                            {
                                e.SuppressKeyPress = true;
                                TryImportCurl(text, false);
                            }
                        }
                    }
                    catch { }
                }
            };
            _builderUrlBox.Enter += delegate
            {
                _builderUrlCard.BorderColor = Color.FromArgb(214, 154, 94);
                _builderUrlCard.Invalidate();
            };
            _builderUrlBox.Leave += delegate
            {
                _builderUrlCard.BorderColor = Theme.Border;
                _builderUrlCard.Invalidate();
            };
            _builderUrlCard.Controls.Add(_builderUrlBox);
            AttachLightScrollbars(_builderUrlBox);

            _builderUrlPlaceholder = MakeLabel("https://api.example.com/v1/users", Theme.Mono(9.5f), Theme.Faint);
            _builderUrlPlaceholder.BackColor = Theme.Input;
            _builderUrlPlaceholder.Cursor = Cursors.IBeam;
            _builderUrlPlaceholder.Click += delegate { _builderUrlBox.Focus(); };
            _builderUrlCard.Controls.Add(_builderUrlPlaceholder);
            _builderUrlPlaceholder.BringToFront();

            _builderTabParams = MakeBuilderTab("参数");
            _builderTabParams.Click += delegate { SetBuilderTab(0); };
            _builderPanel.Controls.Add(_builderTabParams);

            _builderTabHeaders = MakeBuilderTab("请求头");
            _builderTabHeaders.Click += delegate { SetBuilderTab(1); };
            _builderPanel.Controls.Add(_builderTabHeaders);

            _builderTabBody = MakeBuilderTab("请求体");
            _builderTabBody.Click += delegate { SetBuilderTab(2); };
            _builderPanel.Controls.Add(_builderTabBody);

            _builderParamsPanel = new Panel();
            _builderParamsPanel.BackColor = Theme.Panel;
            _builderPanel.Controls.Add(_builderParamsPanel);

            _builderParamsCard = new CardPanel();
            _builderParamsCard.CornerRadius = 8;
            _builderParamsCard.FillColor = Theme.Input;
            _builderParamsCard.BorderColor = Theme.BorderSoft;
            _builderParamsPanel.Controls.Add(_builderParamsCard);

            _builderParamsGrid = new KeyValueEditor("+ 添加参数", "键", "值", "查询参数会自动拼接到 URL");
            _builderParamsGrid.Changed += BuilderChanged;
            _builderParamsCard.Controls.Add(_builderParamsGrid);

            _builderHeadersPanel = new Panel();
            _builderHeadersPanel.BackColor = Theme.Panel;
            _builderPanel.Controls.Add(_builderHeadersPanel);

            _builderHeadersCard = new CardPanel();
            _builderHeadersCard.CornerRadius = 8;
            _builderHeadersCard.FillColor = Theme.Input;
            _builderHeadersCard.BorderColor = Theme.BorderSoft;
            _builderHeadersPanel.Controls.Add(_builderHeadersCard);

            _builderHeadersGrid = new KeyValueEditor("+ 添加请求头", "名称", "值", "未设置 Content-Type 时会按请求体补充");
            _builderHeadersGrid.Changed += BuilderChanged;
            _builderHeadersCard.Controls.Add(_builderHeadersGrid);

            _builderBodyPanel = new Panel();
            _builderBodyPanel.BackColor = Theme.Panel;
            _builderPanel.Controls.Add(_builderBodyPanel);

            _bodyModeNone = MakeBodyModeButton("无");
            _bodyModeNone.Click += delegate { SetBodyMode("none", true); };
            _builderBodyPanel.Controls.Add(_bodyModeNone);

            _bodyModeJson = MakeBodyModeButton("JSON");
            _bodyModeJson.Click += delegate { SetBodyMode("json", true); };
            _builderBodyPanel.Controls.Add(_bodyModeJson);

            _bodyModeForm = MakeBodyModeButton("表单");
            _bodyModeForm.Click += delegate { SetBodyMode("form", true); };
            _builderBodyPanel.Controls.Add(_bodyModeForm);

            _bodyModeRaw = MakeBodyModeButton("文本");
            _bodyModeRaw.Click += delegate { SetBodyMode("raw", true); };
            _builderBodyPanel.Controls.Add(_bodyModeRaw);

            _builderBodyHint = MakeLabel(string.Empty, Theme.Ui(7.5f), Theme.Faint);
            _builderBodyHint.BackColor = Theme.Panel;
            _builderBodyPanel.Controls.Add(_builderBodyHint);

            _builderBodyCard = new CardPanel();
            _builderBodyCard.CornerRadius = 8;
            _builderBodyCard.FillColor = Theme.Input;
            _builderBodyCard.BorderColor = Theme.BorderSoft;
            _builderBodyPanel.Controls.Add(_builderBodyCard);

            _builderBodyBox = new TextBox();
            _builderBodyBox.Multiline = true;
            _builderBodyBox.AcceptsReturn = true;
            _builderBodyBox.AcceptsTab = true;
            _builderBodyBox.ScrollBars = ScrollBars.Vertical;
            _builderBodyBox.WordWrap = true;
            _builderBodyBox.BorderStyle = BorderStyle.None;
            _builderBodyBox.BackColor = Theme.Input;
            _builderBodyBox.ForeColor = Theme.TextSoft;
            _builderBodyBox.Font = Theme.Mono(9.5f);
            _builderBodyBox.ShortcutsEnabled = true;
            _builderBodyBox.TextChanged += delegate { BuilderChanged(null, EventArgs.Empty); };
            _builderBodyCard.Controls.Add(_builderBodyBox);
            AttachLightScrollbars(_builderBodyBox);

            _builderFormGrid = new KeyValueEditor("+ 添加字段", "键", "值", "以 x-www-form-urlencoded 发送");
            _builderFormGrid.Changed += BuilderChanged;
            _builderBodyCard.Controls.Add(_builderFormGrid);

            _builderBodyEmpty = MakeLabel("当前请求不发送请求体。", Theme.Ui(8.5f), Theme.Faint);
            _builderBodyEmpty.TextAlign = ContentAlignment.MiddleCenter;
            _builderBodyEmpty.BackColor = Theme.Input;
            _builderBodyCard.Controls.Add(_builderBodyEmpty);

            _builderCopyButton = new RoundButton();
            _builderCopyButton.Text = "复制 cURL";
            _builderCopyButton.CornerRadius = 9;
            _builderCopyButton.Height = 38;
            _builderCopyButton.FillColor = Theme.PanelRaised;
            _builderCopyButton.FillColor2 = Theme.PanelRaised;
            _builderCopyButton.HoverColor = Theme.Hover;
            _builderCopyButton.BorderColor = Theme.Border;
            _builderCopyButton.TextColor = Theme.Muted;
            _builderCopyButton.Font = Theme.Ui(8.5f);
            _builderCopyButton.Visible = false;
            _builderCopyButton.Click += delegate { CopyBuilderCommand(); };
            _leftCard.Controls.Add(_builderCopyButton);

            _builderActionHint = MakeLabel("自动生成 cURL · 本机执行", Theme.Ui(7.5f), Theme.Faint);
            _builderActionHint.AutoEllipsis = true;
            _builderActionHint.BackColor = Theme.Panel;
            _builderActionHint.Visible = false;
            _leftCard.Controls.Add(_builderActionHint);

            if (_toolTip != null)
            {
                _toolTip.SetToolTip(_builderMethodButton, "切换请求方法");
                _toolTip.SetToolTip(_builderUrlBox, "请求 URL；也可以直接粘贴一条 cURL 自动导入");
                _toolTip.SetToolTip(_builderImportButton, "从剪贴板读取 cURL，并转换为结构化请求");
                _toolTip.SetToolTip(_builderResetButton, "清空构建器内容");
                _toolTip.SetToolTip(_builderSaveButton, "保存当前请求（Ctrl+S）");
                _toolTip.SetToolTip(_builderCopyButton, "复制当前构建器生成的 cURL 命令");
            }

            _builderUiReady = true;
            ApplyBuilderDraft(_settings.Builder, false);
        }

        private static RoundButton MakeBuilderTab(string text)
        {
            RoundButton button = new RoundButton();
            button.Text = text;
            button.Size = new Size(54, 28);
            button.CornerRadius = 6;
            button.FillColor = Color.Transparent;
            button.FillColor2 = Color.Transparent;
            button.HoverColor = Theme.Hover;
            button.BorderColor = Color.Transparent;
            button.TextColor = Theme.Muted;
            button.Font = Theme.Ui(8.5f);
            button.BackColor = Theme.Panel;
            return button;
        }

        private static RoundButton MakeBodyModeButton(string text)
        {
            RoundButton button = new RoundButton();
            button.Text = text;
            button.Size = new Size(46, 26);
            button.CornerRadius = 5;
            button.FillColor = Color.Transparent;
            button.FillColor2 = Color.Transparent;
            button.HoverColor = Theme.Hover;
            button.BorderColor = Color.Transparent;
            button.TextColor = Theme.Muted;
            button.Font = Theme.Ui(8f);
            button.BackColor = Theme.Panel;
            return button;
        }

        private void BuildRightPanel()
        {
            _emptyPanel = new Panel();
            _emptyPanel.BackColor = Theme.Panel;
            _rightCard.Controls.Add(_emptyPanel);

            _emptyArt = new CardPanel();
            _emptyArt.CornerRadius = 10;
            _emptyArt.FillColor = Theme.Input;
            _emptyArt.BorderColor = Theme.BorderSoft;
            _emptyPanel.Controls.Add(_emptyArt);

            _emptyPostMethod = MakeLabel("POST", Theme.MonoBold(8f), Theme.Sky);
            _emptyPostMethod.TextAlign = ContentAlignment.MiddleLeft;
            _emptyArt.Controls.Add(_emptyPostMethod);
            _emptyPostPath = MakeLabel("/v1/messages", Theme.Mono(9f), Theme.Muted);
            _emptyArt.Controls.Add(_emptyPostPath);
            _emptyPostStatus = MakeLabel("201", Theme.Mono(8.5f), Theme.Mint);
            _emptyPostStatus.TextAlign = ContentAlignment.MiddleRight;
            _emptyArt.Controls.Add(_emptyPostStatus);
            _emptyGetMethod = MakeLabel("GET", Theme.MonoBold(8f), Theme.Mint);
            _emptyGetMethod.TextAlign = ContentAlignment.MiddleLeft;
            _emptyArt.Controls.Add(_emptyGetMethod);
            _emptyGetPath = MakeLabel("/v1/users?limit=20", Theme.Mono(9f), Theme.Muted);
            _emptyArt.Controls.Add(_emptyGetPath);
            _emptyGetStatus = MakeLabel("200", Theme.Mono(8.5f), Theme.Mint);
            _emptyGetStatus.TextAlign = ContentAlignment.MiddleRight;
            _emptyArt.Controls.Add(_emptyGetStatus);

            _emptyEyebrow = MakeLabel("READY WHEN YOU ARE", Theme.Mono(7.5f), Theme.Accent);
            _emptyEyebrow.TextAlign = ContentAlignment.MiddleCenter;
            _emptyPanel.Controls.Add(_emptyEyebrow);

            _emptyTitle = MakeLabel("粘贴 cURL，直接看结果", Theme.UiBold(18f), Theme.Text);
            _emptyTitle.TextAlign = ContentAlignment.MiddleCenter;
            _emptyPanel.Controls.Add(_emptyTitle);

            _emptyDescription = MakeLabel("支持 bash / cmd / PowerShell 复制格式，任意请求方法。\r\n不用打开 Postman，粘贴后按 Ctrl + Enter 即可发送。",
                Theme.Ui(9f), Theme.Muted);
            _emptyDescription.TextAlign = ContentAlignment.MiddleCenter;
            _emptyPanel.Controls.Add(_emptyDescription);

            _emptySteps = MakeLabel("01   从 DevTools 右键请求 → Copy → Copy as cURL\r\n02   粘贴到左侧，按 Ctrl + Enter 发送",
                Theme.Ui(8.5f), Theme.Faint);
            _emptySteps.TextAlign = ContentAlignment.MiddleCenter;
            _emptyPanel.Controls.Add(_emptySteps);

            _sampleButton = new RoundButton();
            _sampleButton.Text = "填入一条示例请求";
            _sampleButton.Size = new Size(142, 30);
            _sampleButton.CornerRadius = 7;
            _sampleButton.FillColor = Theme.AccentSurface;
            _sampleButton.FillColor2 = _sampleButton.FillColor;
            _sampleButton.HoverColor = Color.FromArgb(255, 233, 207);
            _sampleButton.BorderColor = Theme.AccentBorder;
            _sampleButton.TextColor = Theme.Accent;
            _sampleButton.Font = Theme.Ui(8.5f);
            _sampleButton.Click += delegate { FillSample(); };
            _emptyPanel.Controls.Add(_sampleButton);

            _emptyPrivacy = MakeLabel("请求由本机 curl 直接发出，不经过第三方服务", Theme.Ui(8f), Theme.Faint);
            _emptyPrivacy.TextAlign = ContentAlignment.MiddleCenter;
            _emptyPanel.Controls.Add(_emptyPrivacy);

            _loadingPanel = new Panel();
            _loadingPanel.BackColor = Theme.Panel;
            _loadingPanel.Visible = false;
            _rightCard.Controls.Add(_loadingPanel);

            _loadingTitle = MakeLabel("正在请求", Theme.UiBold(14f), Theme.Text);
            _loadingTitle.TextAlign = ContentAlignment.MiddleCenter;
            _loadingPanel.Controls.Add(_loadingTitle);

            _loadingUrl = MakeLabel(string.Empty, Theme.Mono(9f), Theme.Muted);
            _loadingUrl.TextAlign = ContentAlignment.MiddleCenter;
            _loadingUrl.AutoEllipsis = true;
            _loadingPanel.Controls.Add(_loadingUrl);

            _loadingMeta = MakeLabel("0.0s · 最长等待 120 秒", Theme.Mono(8.5f), Theme.Faint);
            _loadingMeta.TextAlign = ContentAlignment.MiddleCenter;
            _loadingPanel.Controls.Add(_loadingMeta);

            _cancelButton = new RoundButton();
            _cancelButton.Text = "取消 (Esc)";
            _cancelButton.Size = new Size(96, 28);
            _cancelButton.CornerRadius = 7;
            _cancelButton.FillColor = Theme.PanelRaised;
            _cancelButton.FillColor2 = Theme.PanelRaised;
            _cancelButton.HoverColor = Theme.Hover;
            _cancelButton.BorderColor = Theme.Border;
            _cancelButton.TextColor = Theme.Muted;
            _cancelButton.Font = Theme.Ui(8.5f);
            _cancelButton.Click += delegate { CancelCurrent(); };
            _loadingPanel.Controls.Add(_cancelButton);

            _fatalPanel = new Panel();
            _fatalPanel.BackColor = Theme.Panel;
            _fatalPanel.Visible = false;
            _rightCard.Controls.Add(_fatalPanel);

            _fatalMark = new Pill();
            _fatalMark.Text = "!";
            _fatalMark.Font = Theme.MonoBold(12f);
            _fatalMark.FillColor = Theme.ErrorSurface;
            _fatalMark.BorderColor = Theme.ErrorBorder;
            _fatalMark.TextColor = Theme.Red;
            _fatalMark.Size = new Size(44, 38);
            _fatalPanel.Controls.Add(_fatalMark);

            _fatalTitle = MakeLabel("请求未完成", Theme.UiBold(15f), Theme.Text);
            _fatalTitle.TextAlign = ContentAlignment.MiddleCenter;
            _fatalPanel.Controls.Add(_fatalTitle);

            _fatalMessage = MakeLabel(string.Empty, Theme.Ui(9f), Theme.Muted);
            _fatalMessage.TextAlign = ContentAlignment.MiddleCenter;
            _fatalPanel.Controls.Add(_fatalMessage);

            _fatalDetail = new TextBox();
            _fatalDetail.Multiline = true;
            _fatalDetail.ReadOnly = true;
            _fatalDetail.ScrollBars = ScrollBars.Vertical;
            _fatalDetail.BorderStyle = BorderStyle.FixedSingle;
            _fatalDetail.BackColor = Theme.Input;
            _fatalDetail.ForeColor = Theme.Muted;
            _fatalDetail.Font = Theme.Mono(8.5f);
            _fatalDetail.Visible = false;
            _fatalPanel.Controls.Add(_fatalDetail);
            AttachLightScrollbars(_fatalDetail);

            _retryButton = new RoundButton();
            _retryButton.Text = "重试";
            _retryButton.Size = new Size(80, 32);
            _retryButton.CornerRadius = 7;
            _retryButton.FillColor = Theme.Accent;
            _retryButton.FillColor2 = Theme.AccentStrong;
            _retryButton.HoverColor = Color.FromArgb(168, 58, 11);
            _retryButton.BorderColor = Color.FromArgb(143, 46, 9);
            _retryButton.TextColor = Theme.AccentInk;
            _retryButton.Font = Theme.UiBold(8.5f);
            _retryButton.Click += SendButtonClick;
            _fatalPanel.Controls.Add(_retryButton);

            _backButton = MakeTextButton("返回", 64);
            _backButton.Click += delegate
            {
                if (_result != null) ShowRightState(RightState.Result);
                else ShowRightState(RightState.Empty);
            };
            _fatalPanel.Controls.Add(_backButton);

            _resultPanel = new Panel();
            _resultPanel.BackColor = Theme.Panel;
            _resultPanel.Visible = false;
            _rightCard.Controls.Add(_resultPanel);

            _metaPanel = new Panel();
            _metaPanel.BackColor = Theme.Panel;
            _resultPanel.Controls.Add(_metaPanel);

            _statusPill = new Pill();
            _statusPill.Font = Theme.MonoBold(9f);
            _statusPill.Height = 22;
            _metaPanel.Controls.Add(_statusPill);

            _statusText = MakeLabel(string.Empty, Theme.Mono(8.5f), Theme.Muted);
            _statusText.AutoEllipsis = true;
            _metaPanel.Controls.Add(_statusText);

            _metaFacts = MakeLabel(string.Empty, Theme.Mono(8f), Theme.TextSoft);
            _metaFacts.TextAlign = ContentAlignment.MiddleRight;
            _metaFacts.AutoEllipsis = true;
            _metaPanel.Controls.Add(_metaFacts);

            _methodPill = new Pill();
            _methodPill.Font = Theme.MonoBold(7.5f);
            _methodPill.Height = 18;
            _metaPanel.Controls.Add(_methodPill);

            _urlLabel = MakeLabel(string.Empty, Theme.Mono(8.5f), Theme.Muted);
            _urlLabel.AutoEllipsis = true;
            _metaPanel.Controls.Add(_urlLabel);

            _copyUrlButton = new RoundButton();
            _copyUrlButton.Text = "复制";
            _copyUrlButton.Size = new Size(40, 22);
            _copyUrlButton.CornerRadius = 5;
            _copyUrlButton.FillColor = Theme.Panel;
            _copyUrlButton.FillColor2 = Theme.Panel;
            _copyUrlButton.HoverColor = Theme.Hover;
            _copyUrlButton.BorderColor = Color.Transparent;
            _copyUrlButton.TextColor = Theme.Faint;
            _copyUrlButton.Font = Theme.Ui(7.5f);
            _copyUrlButton.Click += delegate { CopyText(_urlLabel.Text, "URL 已复制"); };
            _metaPanel.Controls.Add(_copyUrlButton);

            _metaDivider = new Panel();
            _metaDivider.BackColor = Theme.BorderSoft;
            _metaDivider.Height = 1;
            _resultPanel.Controls.Add(_metaDivider);

            _alertPanel = new CardPanel();
            _alertPanel.CornerRadius = 6;
            _alertPanel.FillColor = Theme.ErrorSurface;
            _alertPanel.BorderColor = Theme.ErrorBorder;
            _alertPanel.Visible = false;
            _resultPanel.Controls.Add(_alertPanel);

            _alertMark = MakeLabel("!", Theme.MonoBold(10f), Theme.Red);
            _alertMark.TextAlign = ContentAlignment.MiddleCenter;
            _alertPanel.Controls.Add(_alertMark);

            _alertTitle = MakeLabel(string.Empty, Theme.UiBold(8.5f), Theme.ErrorTitle);
            _alertPanel.Controls.Add(_alertTitle);

            _alertText = MakeLabel(string.Empty, Theme.Ui(8f), Theme.ErrorText);
            _alertText.AutoEllipsis = true;
            _alertPanel.Controls.Add(_alertText);

            _alertLogButton = new RoundButton();
            _alertLogButton.Text = "查看日志";
            _alertLogButton.Size = new Size(60, 22);
            _alertLogButton.CornerRadius = 5;
            _alertLogButton.FillColor = Color.Transparent;
            _alertLogButton.FillColor2 = Color.Transparent;
            _alertLogButton.HoverColor = Color.FromArgb(254, 226, 226);
            _alertLogButton.BorderColor = Color.Transparent;
            _alertLogButton.TextColor = Theme.ErrorTitle;
            _alertLogButton.Font = Theme.Ui(8f);
            _alertLogButton.BackColor = Theme.ErrorSurface;
            _alertLogButton.Click += delegate { SetActiveTab(ResponseTab.Log); };
            _alertPanel.Controls.Add(_alertLogButton);

            _tabsPanel = new Panel();
            _tabsPanel.BackColor = Theme.Panel;
            _resultPanel.Controls.Add(_tabsPanel);

            _bodyTab = MakeTabButton("响应体");
            _headersTab = MakeTabButton("响应头");
            _logTab = MakeTabButton("curl 日志");
            _bodyTab.Click += delegate { SetActiveTab(ResponseTab.Body); };
            _headersTab.Click += delegate { SetActiveTab(ResponseTab.Headers); };
            _logTab.Click += delegate { SetActiveTab(ResponseTab.Log); };
            _tabsPanel.Controls.Add(_bodyTab);
            _tabsPanel.Controls.Add(_headersTab);
            _tabsPanel.Controls.Add(_logTab);

            _tabsDivider = new Panel();
            _tabsDivider.BackColor = Theme.BorderSoft;
            _tabsDivider.Height = 1;
            _resultPanel.Controls.Add(_tabsDivider);

            _bodyPanel = new Panel();
            _bodyPanel.BackColor = Theme.Panel;
            _resultPanel.Controls.Add(_bodyPanel);

            _headersPanel = new Panel();
            _headersPanel.BackColor = Theme.Panel;
            _headersPanel.Visible = false;
            _resultPanel.Controls.Add(_headersPanel);

            _logPanel = new Panel();
            _logPanel.BackColor = Theme.Panel;
            _logPanel.Visible = false;
            _resultPanel.Controls.Add(_logPanel);

            BuildBodyPanel();
            BuildHeadersPanel();
            BuildLogPanel();
        }

        private void BuildBodyPanel()
        {
            _bodyKindLabel = MakeLabel(string.Empty, Theme.MonoBold(7.5f), Theme.Accent);
            _bodyPanel.Controls.Add(_bodyKindLabel);

            _bodySizeLabel = MakeLabel(string.Empty, Theme.Mono(7.5f), Theme.Faint);
            _bodyPanel.Controls.Add(_bodySizeLabel);

            _bodyTruncatedLabel = MakeLabel("已截断", Theme.Ui(7.5f), Theme.Orange);
            _bodyTruncatedLabel.Visible = false;
            _bodyPanel.Controls.Add(_bodyTruncatedLabel);

            _prettyButton = MakeToolButton("格式化");
            _prettyButton.Click += delegate
            {
                _pretty = !_pretty;
                _settings.Pretty = _pretty;
                SaveSettings();
                RenderBodyContent();
            };
            _bodyPanel.Controls.Add(_prettyButton);

            _wrapButton = MakeToolButton("换行");
            _wrapButton.Click += delegate
            {
                _wrap = !_wrap;
                _settings.Wrap = _wrap;
                SaveSettings();
                RenderBodyContent();
            };
            _bodyPanel.Controls.Add(_wrapButton);

            _copyBodyButton = MakeToolButton("复制");
            _copyBodyButton.Click += delegate { CopyResultBody(); };
            _bodyPanel.Controls.Add(_copyBodyButton);

            _saveButton = MakeToolButton("保存");
            _saveButton.Click += delegate { SaveResultBody(); };
            _bodyPanel.Controls.Add(_saveButton);

            _bodyCard = new CardPanel();
            _bodyCard.CornerRadius = 8;
            _bodyCard.FillColor = Theme.Input;
            _bodyCard.BorderColor = Theme.BorderSoft;
            _bodyPanel.Controls.Add(_bodyCard);

            _bodyBox = new RichTextBox();
            _bodyBox.BorderStyle = BorderStyle.None;
            _bodyBox.BackColor = Theme.Input;
            _bodyBox.ForeColor = Theme.TextSoft;
            _bodyBox.Font = Theme.Mono(9.5f);
            _bodyBox.ReadOnly = true;
            _bodyBox.DetectUrls = false;
            _bodyBox.WordWrap = true;
            _bodyBox.ScrollBars = RichTextBoxScrollBars.Both;
            _bodyBox.ShortcutsEnabled = true;
            _bodyBox.HideSelection = false;
            _bodyCard.Controls.Add(_bodyBox);
            AttachLightScrollbars(_bodyBox);

            _imagePanel = new Panel();
            _imagePanel.BackColor = Theme.Input;
            _imagePanel.Visible = false;
            _bodyCard.Controls.Add(_imagePanel);

            _imageBox = new PictureBox();
            _imageBox.BackColor = Theme.Input;
            _imageBox.SizeMode = PictureBoxSizeMode.Zoom;
            _imagePanel.Controls.Add(_imageBox);

            _imageInfo = MakeLabel(string.Empty, Theme.Mono(8f), Theme.Muted);
            _imageInfo.TextAlign = ContentAlignment.MiddleCenter;
            _imagePanel.Controls.Add(_imageInfo);

            _bodyEmptyLabel = MakeLabel(string.Empty, Theme.Ui(9f), Theme.Faint);
            _bodyEmptyLabel.TextAlign = ContentAlignment.MiddleCenter;
            _bodyEmptyLabel.Visible = false;
            _bodyCard.Controls.Add(_bodyEmptyLabel);
        }

        private void BuildHeadersPanel()
        {
            _headersCaption = MakeLabel("最终响应头", Theme.Mono(8f), Theme.Faint);
            _headersPanel.Controls.Add(_headersCaption);

            _copyHeadersButton = MakeToolButton("复制全部");
            _copyHeadersButton.Click += delegate { CopyHeaders(); };
            _headersPanel.Controls.Add(_copyHeadersButton);

            _redirectLabel = MakeLabel(string.Empty, Theme.Ui(8f), Theme.Warning);
            _redirectLabel.AutoEllipsis = true;
            _redirectLabel.Visible = false;
            _headersPanel.Controls.Add(_redirectLabel);

            _headersCard = new CardPanel();
            _headersCard.CornerRadius = 8;
            _headersCard.FillColor = Theme.Input;
            _headersCard.BorderColor = Theme.BorderSoft;
            _headersPanel.Controls.Add(_headersCard);

            _headersBox = new RichTextBox();
            _headersBox.BorderStyle = BorderStyle.None;
            _headersBox.BackColor = Theme.Input;
            _headersBox.ForeColor = Theme.TextSoft;
            _headersBox.Font = Theme.Mono(8.8f);
            _headersBox.ReadOnly = true;
            _headersBox.DetectUrls = false;
            _headersBox.WordWrap = true;
            _headersBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            _headersBox.ShortcutsEnabled = true;
            _headersCard.Controls.Add(_headersBox);
            AttachLightScrollbars(_headersBox);

            _timingLabel = MakeLabel(string.Empty, Theme.Mono(7.8f), Theme.Faint);
            _headersPanel.Controls.Add(_timingLabel);
        }

        private void BuildLogPanel()
        {
            _logCaption = MakeLabel("curl stderr", Theme.Mono(8f), Theme.Faint);
            _logPanel.Controls.Add(_logCaption);

            _copyLogButton = MakeToolButton("复制");
            _copyLogButton.Click += delegate
            {
                if (_result != null) CopyText(_result.Stderr, "日志已复制");
            };
            _logPanel.Controls.Add(_copyLogButton);

            _logCard = new CardPanel();
            _logCard.CornerRadius = 8;
            _logCard.FillColor = Theme.Input;
            _logCard.BorderColor = Theme.BorderSoft;
            _logPanel.Controls.Add(_logCard);

            _logBox = new RichTextBox();
            _logBox.BorderStyle = BorderStyle.None;
            _logBox.BackColor = Theme.Input;
            _logBox.ForeColor = Theme.TextSoft;
            _logBox.Font = Theme.Mono(8.8f);
            _logBox.ReadOnly = true;
            _logBox.DetectUrls = false;
            _logBox.WordWrap = true;
            _logBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            _logBox.ShortcutsEnabled = true;
            _logCard.Controls.Add(_logBox);
            AttachLightScrollbars(_logBox);
        }

        private void BuildFooter()
        {
            _footer = new Panel();
            _footer.Dock = DockStyle.Bottom;
            _footer.Height = 28;
            _footer.BackColor = Theme.Bg;
            Controls.Add(_footer);

            Panel line = new Panel();
            line.Height = 1;
            line.Dock = DockStyle.Top;
            line.BackColor = Theme.BorderSoft;
            _footer.Controls.Add(line);

            _footerDot = MakeLabel("●", Theme.Ui(6f), Theme.Mint);
            _footerDot.Size = new Size(10, 18);
            _footerDot.TextAlign = ContentAlignment.MiddleLeft;
            _footer.Controls.Add(_footerDot);

            _footerLeft = MakeLabel("本机 curl 执行 · 数据不上传 · 关闭窗口后驻留托盘", Theme.Ui(8f), Theme.Faint);
            _footer.Controls.Add(_footerLeft);

            _footerRight = MakeLabel(string.Empty, Theme.Mono(7.8f), Theme.Faint);
            _footerRight.TextAlign = ContentAlignment.MiddleRight;
            _footerRight.AutoEllipsis = true;
            _footer.Controls.Add(_footerRight);
        }

        private void BuildToast()
        {
            _toast = new CardPanel();
            _toast.CornerRadius = 8;
            _toast.FillColor = Theme.Panel;
            _toast.BorderColor = Theme.Border;
            _toast.Visible = false;
            Controls.Add(_toast);
            _toast.BringToFront();

            _toastLabel = MakeLabel(string.Empty, Theme.Ui(8.5f), Theme.TextSoft);
            _toastLabel.TextAlign = ContentAlignment.MiddleCenter;
            _toast.Controls.Add(_toastLabel);
        }

        private void BuildTimers()
        {
            _parseTimer = new WinTimer();
            _parseTimer.Interval = 180;
            _parseTimer.Tick += delegate
            {
                _parseTimer.Stop();
                UpdatePreview();
            };

            _draftTimer = new WinTimer();
            _draftTimer.Interval = 700;
            _draftTimer.Tick += delegate
            {
                _draftTimer.Stop();
                SaveSettings();
            };

            _elapsedTimer = new WinTimer();
            _elapsedTimer.Interval = 100;
            _elapsedTimer.Tick += delegate
            {
                TimeSpan elapsed = DateTime.Now - _requestStarted;
                _loadingMeta.Text = elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s · 最长等待 120 秒";
            };

            _memoryTimer = new WinTimer();
            _memoryTimer.Interval = 5000;
            _memoryTimer.Tick += delegate { UpdateMemoryLabel(); };
            _memoryTimer.Start();

            _toastTimer = new WinTimer();
            _toastTimer.Interval = 2100;
            _toastTimer.Tick += delegate
            {
                _toastTimer.Stop();
                _toast.Visible = false;
            };

            UpdateMemoryLabel();
        }

        private void BuildMenus()
        {
            _settingsMenu = new ContextMenuStrip();
            _settingsMenu.Font = Theme.Ui(9f);
            _settingsMenu.BackColor = Theme.Panel;
            _settingsMenu.ForeColor = Theme.Text;
            _settingsMenu.Renderer = new LightMenuRenderer();
            _settingsMenu.ShowImageMargin = false;
            _settingsMenu.ShowCheckMargin = true;

            _settingsAutoStart = new ToolStripMenuItem("开机自动启动");
            _settingsAutoStart.Click += delegate { ToggleAutoStart(); };
            _settingsMenu.Items.Add(_settingsAutoStart);

            ToolStripMenuItem autoTokenItem = new ToolStripMenuItem("自动 Token 登录设置…");
            autoTokenItem.Click += delegate { OpenAutoTokenSettings(); };
            _settingsMenu.Items.Add(autoTokenItem);

            ToolStripMenuItem openData = new ToolStripMenuItem("打开数据目录");
            openData.Click += delegate
            {
                try { Process.Start("explorer.exe", Program.DataDirectory); }
                catch { }
            };
            _settingsMenu.Items.Add(openData);

            _settingsMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem hideItem = new ToolStripMenuItem("隐藏到托盘");
            hideItem.Click += delegate { HideToTray(true); };
            _settingsMenu.Items.Add(hideItem);

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出 CurlBox");
            exitItem.Click += delegate { ExitApplication(); };
            _settingsMenu.Items.Add(exitItem);

            _settingsButton.Click += delegate
            {
                _settingsMenu.Show(_settingsButton, new Point(0, _settingsButton.Height + 4));
            };

            if (_modeCurlButton != null)
                _modeCurlButton.Click += delegate { ActivateCurlMode(true); };
            if (_modeBuilderButton != null)
                _modeBuilderButton.Click += delegate { ActivateBuilderMode(true, true); };
            if (_modeLibraryButton != null)
                _modeLibraryButton.Click += delegate { ActivateLibraryMode(true); };
            UpdateModeSwitchStyles();

            if (_toolTip != null)
            {
                _toolTip.SetToolTip(_modeCurlButton, "粘贴并快速发送 cURL");
                _toolTip.SetToolTip(_modeBuilderButton, "像 Postman 一样创建请求；当前 cURL 会自动导入");
                _toolTip.SetToolTip(_modeLibraryButton, "查看已保存请求（Ctrl+O）");
                _toolTip.SetToolTip(_splitHandle, "拖动调整左右宽度，双击恢复默认");
            }

            _optionsMenu = new ContextMenuStrip();
            _optionsMenu.Font = Theme.Ui(9f);
            _optionsMenu.BackColor = Theme.Panel;
            _optionsMenu.ForeColor = Theme.Text;
            _optionsMenu.Renderer = new LightMenuRenderer();
            _optionsMenu.ShowImageMargin = false;
            _optionsMenu.ShowCheckMargin = true;

            _followItem = new ToolStripMenuItem("跟随重定向  (--location)");
            _followItem.Click += delegate { ToggleOption("follow"); };
            _optionsMenu.Items.Add(_followItem);

            _insecureItem = new ToolStripMenuItem("忽略 TLS 证书  (--insecure)");
            _insecureItem.Click += delegate { ToggleOption("insecure"); };
            _optionsMenu.Items.Add(_insecureItem);

            _verboseItem = new ToolStripMenuItem("显示 curl 详细日志  (--verbose)");
            _verboseItem.Click += delegate { ToggleOption("verbose"); };
            _optionsMenu.Items.Add(_verboseItem);

            _optionsButton.Click += delegate
            {
                RefreshOptionChecks();
                _optionsMenu.Show(_optionsButton, new Point(0, _optionsButton.Height + 4));
            };
            RefreshOptionChecks();
            RefreshAutoStartChecks();
        }

        private void BuildTray()
        {
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Font = Theme.Ui(9f);
            _trayMenu.BackColor = Theme.Panel;
            _trayMenu.ForeColor = Theme.Text;
            _trayMenu.Renderer = new LightMenuRenderer();
            _trayMenu.ShowImageMargin = false;
            _trayMenu.ShowCheckMargin = true;

            _trayOpen = new ToolStripMenuItem("打开 CurlBox");
            _trayOpen.Font = Theme.UiBold(9f);
            _trayOpen.Click += delegate { ShowFromTray(); };
            _trayMenu.Items.Add(_trayOpen);

            _trayAutoStart = new ToolStripMenuItem("开机自动启动");
            _trayAutoStart.Click += delegate { ToggleAutoStart(); };
            _trayMenu.Items.Add(_trayAutoStart);

            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayExit = new ToolStripMenuItem("退出");
            _trayExit.Click += delegate { ExitApplication(); };
            _trayMenu.Items.Add(_trayExit);

            _trayIcon = new NotifyIcon();
            _trayIcon.Text = "CurlBox · cURL 请求工具";
            _trayIcon.Icon = _appIcon;
            _trayIcon.ContextMenuStrip = _trayMenu;
            _trayIcon.Visible = true;
            _trayIcon.DoubleClick += delegate { ShowFromTray(); };
        }

        private void ApplyLoadedState()
        {
            _curlBox.Text = _settings.Draft ?? string.Empty;
            _methodBox.Text = _settings.MethodOverride ?? string.Empty;
            UpdatePlaceholder();
            RefreshOptionChecks();
            RefreshAutoStartChecks();
            UpdateMemoryLabel();
            _footerRight.Text = BuildFooterRight();

            if (_settings.Builder == null) _settings.Builder = new BuilderDraft();
            if (_settings.Builder.Active)
            {
                EnsureBuilderUi();
                SetBuilderMode(true, false, false);
            }
            UpdateModeSwitchStyles();
        }

        private void ApplyWindowBounds()
        {
            bool sizeValid = _settings.WindowWidth >= 960 && _settings.WindowHeight >= 620;
            if (!sizeValid)
            {
                Rectangle area = Screen.PrimaryScreen.WorkingArea;
                int width = Math.Min(1180, Math.Max(960, area.Width - 100));
                int height = Math.Min(780, Math.Max(620, area.Height - 100));
                StartPosition = FormStartPosition.CenterScreen;
                ClientSize = new Size(width, height);
            }
            else
            {
                Rectangle desired = new Rectangle(_settings.WindowLeft, _settings.WindowTop,
                    _settings.WindowWidth, _settings.WindowHeight);
                bool visible = false;
                foreach (Screen screen in Screen.AllScreens)
                {
                    if (screen.WorkingArea.IntersectsWith(desired))
                    {
                        visible = true;
                        break;
                    }
                }
                if (visible)
                {
                    StartPosition = FormStartPosition.Manual;
                    Location = new Point(_settings.WindowLeft, _settings.WindowTop);
                }
                ClientSize = new Size(_settings.WindowWidth, _settings.WindowHeight);
            }

            if (_settings.WindowMaximized)
            {
                StartPosition = FormStartPosition.Manual;
                WindowState = FormWindowState.Maximized;
            }
            LayoutAll();
        }

        private void LoadData()
        {
            _settings = LoadJson<AppSettings>(_settingsPath) ?? new AppSettings();
            List<HistoryEntry> loaded = LoadJson<List<HistoryEntry>>(_historyPath);
            _history = loaded == null
                ? new List<HistoryEntry>()
                : loaded.Where(delegate(HistoryEntry item) { return item != null && !string.IsNullOrEmpty(item.Command); })
                        .Take(MaxHistory)
                        .ToList();
            if (_settings.MethodOverride == null) _settings.MethodOverride = string.Empty;
            if (_settings.Draft == null) _settings.Draft = string.Empty;
            if (_settings.Builder == null) _settings.Builder = new BuilderDraft();
            if (_settings.AdminLoginUrl == null) _settings.AdminLoginUrl = string.Empty;
            if (_settings.AdminUsername == null) _settings.AdminUsername = string.Empty;
            if (_settings.AdminPassword == null) _settings.AdminPassword = string.Empty;
            if (_settings.AdminAuthCode == null) _settings.AdminAuthCode = string.Empty;
            if (_settings.AdminToken == null) _settings.AdminToken = string.Empty;
            _splitRatio = _settings.SplitRatio >= 0.1 && _settings.SplitRatio <= 0.9
                ? _settings.SplitRatio
                : 0.43;
        }

        private T LoadJson<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path)) return null;
                string text = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(text)) return null;
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 16 * 1024 * 1024;
                return serializer.Deserialize<T>(text);
            }
            catch
            {
                return null;
            }
        }

        private void SaveJson<T>(string path, T value)
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 16 * 1024 * 1024;
                File.WriteAllText(path, serializer.Serialize(value), new UTF8Encoding(false));
            }
            catch
            {
                // Persistence must never interrupt the request flow.
            }
        }

        private void SaveSettings()
        {
            if (_settings == null || _curlBox == null) return;
            _settings.Draft = _curlBox.Text;
            _settings.MethodOverride = _methodBox.Text.Trim();
            _settings.Pretty = _pretty;
            _settings.Wrap = _wrap;
            _settings.SplitRatio = _splitRatio;
            if (_builderUiReady)
            {
                _settings.Builder = CaptureBuilderDraft();
            }
            else if (_settings.Builder == null)
            {
                _settings.Builder = new BuilderDraft();
            }
            if (_settings.Builder != null) _settings.Builder.Active = _builderMode && _builderUiReady;

            Rectangle bounds = WindowState == FormWindowState.Maximized ? RestoreBounds : Bounds;
            _settings.WindowLeft = bounds.Left;
            _settings.WindowTop = bounds.Top;
            _settings.WindowWidth = WindowState == FormWindowState.Maximized ? RestoreBounds.Width : ClientSize.Width;
            _settings.WindowHeight = WindowState == FormWindowState.Maximized ? RestoreBounds.Height : ClientSize.Height;
            _settings.WindowMaximized = WindowState == FormWindowState.Maximized;
            SaveJson(_settingsPath, _settings);
        }

        private void SaveHistory()
        {
            SaveJson(_historyPath, _history);
        }

        private static Label MakeLabel(string text, Font font, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        private static RoundButton MakeModeButton(string text, int width)
        {
            RoundButton button = new RoundButton();
            button.Text = text;
            button.Size = new Size(width, 26);
            button.CornerRadius = 6;
            button.FillColor = Theme.PanelRaised;
            button.FillColor2 = Theme.PanelRaised;
            button.HoverColor = Theme.Hover;
            button.BorderColor = Theme.Border;
            button.TextColor = Theme.Muted;
            button.Font = Theme.Ui(8.5f);
            button.BackColor = Theme.Bg;
            return button;
        }

        private static RoundButton MakeTextButton(string text, int width)
        {
            RoundButton button = new RoundButton();
            button.Text = text;
            button.Size = new Size(width, 27);
            button.CornerRadius = 6;
            button.FillColor = Theme.Panel;
            button.FillColor2 = button.FillColor;
            button.HoverColor = Theme.Hover;
            button.BorderColor = Theme.Border;
            button.TextColor = Theme.Muted;
            button.Font = Theme.Ui(8f);
            return button;
        }

        private static RoundButton MakeToolButton(string text)
        {
            RoundButton button = new RoundButton();
            button.Text = text;
            button.Size = new Size(48, 24);
            button.CornerRadius = 5;
            button.FillColor = Color.Transparent;
            button.FillColor2 = Color.Transparent;
            button.HoverColor = Theme.Hover;
            button.BorderColor = Color.Transparent;
            button.TextColor = Theme.Muted;
            button.Font = Theme.Ui(7.8f);
            return button;
        }

        private static RoundButton MakeTabButton(string text)
        {
            RoundButton button = new RoundButton();
            button.Text = text;
            button.Height = 38;
            button.CornerRadius = 0;
            button.FillColor = Theme.Panel;
            button.FillColor2 = Theme.Panel;
            button.HoverColor = Theme.Hover;
            button.BorderColor = Color.Transparent;
            button.TextColor = Theme.Muted;
            button.Font = Theme.Ui(8.5f);
            button.DrawUnderline = true;
            return button;
        }

        private void UpdatePlaceholder()
        {
            if (_curlBox == null || _editorPlaceholder == null) return;
            _editorPlaceholder.Visible = _curlBox.Text.Length == 0;
            if (_methodPlaceholder != null)
                _methodPlaceholder.Visible = _methodBox.Text.Length == 0;
            UpdateEditorScrollBars();
        }

        private void UpdateEditorScrollBars()
        {
            if (_curlBox == null) return;
            bool needScrollbar = _curlBox.TextLength > 260 || _curlBox.Lines.Length > 6;
            ScrollBars target = needScrollbar ? ScrollBars.Vertical : ScrollBars.None;
            if (_curlBox.ScrollBars != target)
                _curlBox.ScrollBars = target;
        }

        private void ScheduleParse()
        {
            if (_parseTimer == null) return;
            _parseTimer.Stop();
            _parseTimer.Start();
        }

        private void ScheduleDraftSave()
        {
            if (_draftTimer == null) return;
            _draftTimer.Stop();
            _draftTimer.Start();
        }

        private void UpdatePreview()
        {
            if (_curlBox == null) return;
            string text = _curlBox.Text.Trim();
            _preview = null;
            _previewError = null;

            if (text.Length == 0)
            {
                SetPreviewIdle();
                UpdateSendEnabled();
                return;
            }

            string methodText = _methodBox.Text.Trim();
            bool methodValid = string.IsNullOrEmpty(methodText) || IsValidMethod(methodText);
            if (!methodValid)
            {
                _previewError = "请求方法格式不正确，只能包含字母、数字和常用符号。";
                ShowPreviewError(_previewError);
                UpdateSendEnabled();
                return;
            }

            try
            {
                _preview = CurlParser.Parse(text);
            }
            catch (CurlParseException ex)
            {
                _previewError = ex.Message;
                ShowPreviewError(ex.Message);
                UpdateSendEnabled();
                return;
            }
            catch (Exception ex)
            {
                _previewError = "解析失败：" + ex.Message;
                ShowPreviewError(_previewError);
                UpdateSendEnabled();
                return;
            }

            string effectiveMethod = string.IsNullOrEmpty(methodText) ? _preview.Method : methodText.ToUpperInvariant();
            Color methodColor = Theme.ColorForMethod(effectiveMethod);
            _previewMethod.Visible = true;
            _previewMethod.Text = effectiveMethod;
            _previewMethod.TextColor = methodColor;
            _previewMethod.FillColor = Color.FromArgb(30, methodColor);
            _previewMethod.BorderColor = Color.FromArgb(65, methodColor);
            _previewMethod.FitToText(12);
            _previewMethod.Height = 19;

            _previewUrl.Visible = true;
            _previewUrl.Text = _preview.Url;
            _previewUrl.ForeColor = Theme.TextSoft;
            _previewUrl.TextAlign = ContentAlignment.MiddleLeft;

            List<string> stats = new List<string>();
            if (!string.IsNullOrEmpty(methodText)) stats.Add("手动方法");
            if (_preview.HeaderCount > 0) stats.Add(_preview.HeaderCount + " 个请求头");
            if (_preview.HasBody)
            {
                string bodySize = _preview.BodyFromFile ? "文件" : UiText.FormatBytes(_preview.BodyBytes);
                stats.Add("body " + bodySize);
            }
            _previewStats.Visible = stats.Count > 0;
            _previewStats.Text = string.Join(" · ", stats.ToArray());

            if (_preview.Warnings.Count > 0)
            {
                _previewWarn.Visible = true;
                _previewWarn.Text = _preview.Warnings[0];
                _toolTip.SetToolTip(_previewWarn, string.Join("\r\n", _preview.Warnings.ToArray()));
            }
            else
            {
                _previewWarn.Visible = false;
                _previewWarn.Text = string.Empty;
            }

            LayoutPreviewContent();
            UpdateSendEnabled();
        }

        private void SetPreviewIdle()
        {
            _previewMethod.Visible = false;
            _previewStats.Visible = false;
            _previewWarn.Visible = false;
            _previewUrl.Visible = true;
            _previewUrl.Text = "等待粘贴 cURL…";
            _previewUrl.ForeColor = Theme.Muted;
            _previewUrl.TextAlign = ContentAlignment.MiddleLeft;
            LayoutPreviewContent();
        }

        private void ShowPreviewError(string message)
        {
            _previewMethod.Visible = false;
            _previewStats.Visible = false;
            _previewWarn.Visible = false;
            _previewUrl.Visible = true;
            _previewUrl.Text = message;
            _previewUrl.ForeColor = Theme.Red;
            _previewUrl.TextAlign = ContentAlignment.MiddleLeft;
            _previewUrl.AutoEllipsis = true;
            LayoutPreviewContent();
        }

        private static bool IsValidMethod(string method)
        {
            return Regex.IsMatch(method, @"^[A-Za-z][A-Za-z0-9!#$%&'*+.^_`|~-]{0,31}$");
        }

        private void LayoutPreviewContent()
        {
            if (_previewCard == null) return;
            int width = _previewCard.Width;
            int methodWidth = _previewMethod.Visible ? _previewMethod.Width : 0;
            _previewMethod.Location = new Point(10, 9);

            int statsWidth = 0;
            if (_previewStats.Visible)
            {
                Size measured = TextRenderer.MeasureText(_previewStats.Text, _previewStats.Font,
                    Size.Empty, TextFormatFlags.NoPadding);
                statsWidth = Math.Min(220, measured.Width + 12);
                _previewStats.Bounds = new Rectangle(width - statsWidth - 10, 9, statsWidth, 19);
                _previewStats.Visible = statsWidth > 0;
            }

            int urlX = methodWidth > 0 ? 10 + methodWidth + 8 : 10;
            int urlWidth = width - urlX - 10 - (statsWidth > 0 ? statsWidth + 10 : 0);
            _previewUrl.Bounds = new Rectangle(urlX, 8, Math.Max(40, urlWidth), 21);

            _previewWarn.Bounds = new Rectangle(10, 30, Math.Max(40, width - 20), 14);
        }

        private void UpdateSendEnabled()
        {
            bool hasBuilderUrl = _builderUiReady && _builderUrlBox != null && !string.IsNullOrWhiteSpace(_builderUrlBox.Text);
            bool canSend = !_running && (_builderMode
                ? hasBuilderUrl
                : !string.IsNullOrEmpty(_curlBox.Text.Trim()) && _previewError == null);
            _sendButton.Enabled = canSend;
            _sendButton.Text = _running ? "请求中…" : "发送   Ctrl+↵";
            _pasteButton.Enabled = !_running;
            if (_builderCopyButton != null) _builderCopyButton.Enabled = hasBuilderUrl;
        }

        private void PasteFromClipboard()
        {
            try
            {
                if (!Clipboard.ContainsText() || string.IsNullOrWhiteSpace(Clipboard.GetText()))
                {
                    ShowToast("剪贴板里没有文本", ToastKind.Error);
                    return;
                }
                _curlBox.Text = Clipboard.GetText();
                UpdatePlaceholder();
                UpdatePreview();
                _curlBox.Focus();
                _curlBox.SelectionStart = _curlBox.Text.Length;
                ShowToast("已从剪贴板粘贴");
            }
            catch (Exception ex)
            {
                ShowToast("读取剪贴板失败：" + ex.Message, ToastKind.Error);
            }
        }

        private void FillSample()
        {
            _curlBox.Text = "curl 'https://example.com' -H 'accept: */*'";
            _methodBox.Text = string.Empty;
            UpdatePlaceholder();
            UpdatePreview();
            _curlBox.Focus();
            _curlBox.SelectionStart = _curlBox.Text.Length;
            ShowToast("示例已填入，按 Ctrl+Enter 发送");
        }

        private void SetBuilderMode(bool active, bool autoImport, bool focus)
        {
            if (active == _builderMode) return;
            if (active) EnsureBuilderUi();
            _builderMode = active;

            if (_builderPanel != null) _builderPanel.Visible = active;
            _editorCard.Visible = !active;
            _previewCard.Visible = !active;
            _methodCard.Visible = !active;
            _pasteButton.Visible = !active;
            _clearButton.Visible = !active;
            _builderImportButton.Visible = active;
            _builderResetButton.Visible = active;
            _builderSaveButton.Visible = active;
            _builderCopyButton.Visible = active;
            _builderActionHint.Visible = active;

            _historyTitle.Visible = !active;
            _historyCount.Visible = !active;
            _clearHistory.Visible = !active && _history.Count > 0;
            _historyList.Visible = !active;
            _historyEmpty.Visible = !active && _history.Count == 0;

            _leftTitle.Text = active ? "构建请求" : "粘贴 cURL";
            if (!active)
            {
                _leftSubtitle.Text = "支持 bash / cmd / PowerShell 复制格式";
                UpdatePreview();
            }

            UpdateModeSwitchStyles();
            UpdateSendEnabled();

            if (active && autoImport && BuilderIsEmpty() && _curlBox.Text.Trim().Length > 0)
                TryImportCurl(_curlBox.Text, true);

            if (active)
            {
                UpdateBuilderSummary();
                if (_builderUrlBox != null) UpdateBuilderUrlPlaceholder();
            }

            LayoutAll();
            if (_leftCard != null)
            {
                _leftCard.Invalidate(true);
                _leftCard.Update();
            }
            ScheduleDraftSave();
            if (focus)
            {
                if (active && _builderUrlBox != null) _builderUrlBox.Focus();
                else _curlBox.Focus();
            }
        }

        private void ActivateCurlMode(bool focus)
        {
            ExitLibraryMode();
            SetBuilderMode(false, false, false);
            UpdateModeSwitchStyles();
            LayoutAll();
            if (focus) _curlBox.Focus();
            ScheduleDraftSave();
        }

        private void ActivateBuilderMode(bool focus, bool autoImport)
        {
            ExitLibraryMode();
            SetBuilderMode(true, autoImport, false);
            UpdateModeSwitchStyles();
            LayoutAll();
            if (focus && _builderUrlBox != null) _builderUrlBox.Focus();
            ScheduleDraftSave();
        }

        private void ActivateLibraryMode(bool focus)
        {
            if (_libraryMode)
            {
                if (_libraryView != null)
                {
                    _libraryView.AutoTokenConfigured = AutoTokenService.IsConfigured(_settings);
                    _libraryView.Reload();
                    if (focus) _libraryView.FocusInitial();
                }
                return;
            }
            EnsureLibrary();
            EnsureLibraryView();
            _libraryMode = true;
            if (_libraryView != null)
            {
                _libraryView.AutoTokenConfigured = AutoTokenService.IsConfigured(_settings);
                _libraryView.Reload();
            }
            UpdateModeSwitchStyles();
            LayoutAll();
            if (focus && _libraryView != null) _libraryView.FocusInitial();
            ScheduleDraftSave();
        }

        private void ExitLibraryMode()
        {
            if (!_libraryMode) return;
            _libraryMode = false;
            SyncCurrentSavedRequest();
            if (_libraryView != null) _libraryView.Visible = false;
            UpdateModeSwitchStyles();
        }

        private void EnsureLibraryView()
        {
            if (_libraryView != null) return;
            RequestLibrary library = EnsureLibrary();
            _libraryView = new RequestLibraryView(library, _libraryPath);
            _libraryView.AutoTokenConfigured = AutoTokenService.IsConfigured(_settings);
            _libraryView.RequestActivated += LibraryViewRequestActivated;
            Controls.Add(_libraryView);
            _libraryView.Visible = false;
            _libraryView.BringToFront();
        }

        private void LibraryViewRequestActivated(object sender, SavedRequestEventArgs e)
        {
            if (e == null || e.Request == null) return;
            ActivateBuilderMode(false, false);
            OpenSavedRequest(e.Request, e.Send);
        }

        private void UpdateModeSwitchStyles()
        {
            StyleModeButton(_modeCurlButton, !_libraryMode && !_builderMode);
            StyleModeButton(_modeBuilderButton, !_libraryMode && _builderMode);
            StyleModeButton(_modeLibraryButton, _libraryMode);
        }

        private void OpenAutoTokenSettings()
        {
            if (_settings == null) return;
            using (AutoTokenDialog dialog = new AutoTokenDialog(
                _settings.AdminLoginUrl,
                _settings.AdminUsername,
                _settings.AdminPassword,
                _settings.AdminAuthCode,
                _settings.AdminToken))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _settings.AdminLoginUrl = dialog.LoginUrl;
                _settings.AdminUsername = dialog.Username;
                _settings.AdminPassword = dialog.Password;
                _settings.AdminAuthCode = dialog.AuthCode;
                _settings.AdminToken = dialog.Token ?? string.Empty;
                SaveSettings();
                if (_libraryView != null)
                    _libraryView.AutoTokenConfigured = AutoTokenService.IsConfigured(_settings);
                ShowToast("自动 Token 登录配置已保存");
            }
        }

        private bool IsAutoTokenEnabledForRequest(string url)
        {
            if (!AutoTokenService.IsBackendPath(url)) return false;
            RequestLibrary library = EnsureLibrary();
            if (library == null) return false;
            string folder = _builderMode && !string.IsNullOrEmpty(_currentSavedId)
                ? RequestLibraryStore.NormalizeFolder(_currentSavedFolder)
                : string.Empty;
            if (folder.Length == 0) return library.AutoTokenUngrouped;
            if (library.AutoTokenFolders == null) return false;
            foreach (string item in library.AutoTokenFolders)
            {
                if (string.Equals(RequestLibraryStore.NormalizeFolder(item), folder,
                    StringComparison.CurrentCultureIgnoreCase)) return true;
            }
            return false;
        }

        private static void StyleModeButton(RoundButton button, bool active)
        {
            if (button == null) return;
            button.FillColor = active ? Theme.AccentSurface : Theme.PanelRaised;
            button.FillColor2 = button.FillColor;
            button.BorderColor = active ? Theme.AccentBorder : Theme.Border;
            button.TextColor = active ? Theme.Accent : Theme.Muted;
            button.HoverColor = active ? Color.FromArgb(255, 236, 214) : Theme.Hover;
            button.Invalidate();
        }

        private bool BuilderIsEmpty()
        {
            if (!_builderUiReady) return true;
            if (!string.IsNullOrWhiteSpace(_builderUrlBox.Text)) return false;
            if (RowsHaveContent(_builderParamsGrid.Snapshot())) return false;
            if (RowsHaveContent(_builderHeadersGrid.Snapshot())) return false;
            if (RowsHaveContent(_builderFormGrid.Snapshot())) return false;
            if (!string.IsNullOrEmpty(_builderBodyBox.Text)) return false;
            if (CurlBuilder.NormalizeBodyMode(_builderBodyMode) != "none") return false;
            if (_builderExtraArgs.Count > 0) return false;
            return true;
        }

        private static bool RowsHaveContent(List<BuilderRow> rows)
        {
            if (rows == null) return false;
            foreach (BuilderRow row in rows)
            {
                if (row == null) continue;
                if (!string.IsNullOrEmpty(row.Key) || !string.IsNullOrEmpty(row.Value)) return true;
            }
            return false;
        }

        private void SetBuilderMethod(string method)
        {
            string value = (method ?? string.Empty).Trim().ToUpperInvariant();
            if (!CurlBuilder.IsValidMethod(value)) value = "GET";
            _builderMethod = value;
            if (value == "HEAD" && CurlBuilder.NormalizeBodyMode(_builderBodyMode) != "none")
            {
                SetBodyMode("none", false);
                ShowToast("HEAD 请求不发送请求体");
            }
            UpdateBuilderMethodStyle();
            BuilderChanged(null, EventArgs.Empty);
        }

        private void UpdateBuilderMethodStyle()
        {
            if (_builderMethodButton == null) return;
            Color color = Theme.ColorForMethod(_builderMethod);
            _builderMethodButton.Text = _builderMethod;
            _builderMethodButton.TextColor = color;
            _builderMethodButton.FillColor = Theme.Tint(color, 26);
            _builderMethodButton.FillColor2 = _builderMethodButton.FillColor;
            _builderMethodButton.BorderColor = Theme.Tint(color, 78);
            _builderMethodButton.Invalidate();
        }

        private void SetBuilderTab(int tab)
        {
            _builderTab = Math.Max(0, Math.Min(2, tab));
            if (_builderParamsPanel != null) _builderParamsPanel.Visible = _builderTab == 0;
            if (_builderHeadersPanel != null) _builderHeadersPanel.Visible = _builderTab == 1;
            if (_builderBodyPanel != null) _builderBodyPanel.Visible = _builderTab == 2;
            UpdateBuilderTabStyles();
            ScheduleDraftSave();
        }

        private void UpdateBuilderTabStyles()
        {
            StyleBuilderTab(_builderTabParams, _builderTab == 0);
            StyleBuilderTab(_builderTabHeaders, _builderTab == 1);
            StyleBuilderTab(_builderTabBody, _builderTab == 2);
        }

        private static void StyleBuilderTab(RoundButton button, bool active)
        {
            if (button == null) return;
            button.FillColor = active ? Theme.AccentSurface : Color.Transparent;
            button.FillColor2 = button.FillColor;
            button.BorderColor = active ? Theme.AccentBorder : Color.Transparent;
            button.TextColor = active ? Theme.Accent : Theme.Muted;
            button.HoverColor = active ? Color.FromArgb(255, 236, 214) : Theme.Hover;
            button.Invalidate();
        }

        private void SetBodyMode(string mode, bool focus)
        {
            if (_builderBodyPanel == null) return;
            string value = CurlBuilder.NormalizeBodyMode(mode);
            if (value != "none" && string.Equals(_builderMethod, "HEAD", StringComparison.Ordinal))
            {
                ShowToast("HEAD 请求不能包含请求体", ToastKind.Error);
                return;
            }
            _builderBodyMode = value;
            ApplyBodyMode(focus);
            BuilderChanged(null, EventArgs.Empty);
        }

        private void ApplyBodyMode(bool focus)
        {
            if (_builderBodyPanel == null) return;
            string mode = CurlBuilder.NormalizeBodyMode(_builderBodyMode);
            _builderBodyBox.Visible = mode == "json" || mode == "raw";
            _builderFormGrid.Visible = mode == "form";
            _builderBodyEmpty.Visible = mode == "none";
            _bodyModeNone.FillColor = mode == "none" ? Theme.AccentSurface : Color.Transparent;
            _bodyModeNone.FillColor2 = _bodyModeNone.FillColor;
            _bodyModeNone.BorderColor = mode == "none" ? Theme.AccentBorder : Color.Transparent;
            _bodyModeNone.TextColor = mode == "none" ? Theme.Accent : Theme.Muted;
            StyleBodyModeButton(_bodyModeJson, mode == "json");
            StyleBodyModeButton(_bodyModeForm, mode == "form");
            StyleBodyModeButton(_bodyModeRaw, mode == "raw");
            if (mode == "json") _builderBodyHint.Text = "作为 application/json 原始内容发送";
            else if (mode == "form") _builderBodyHint.Text = "键值会 URL 编码后发送";
            else if (mode == "raw") _builderBodyHint.Text = "作为原始文本发送";
            else _builderBodyHint.Text = "不发送请求体";
            _builderBodyHint.Visible = true;
            if (focus && (mode == "json" || mode == "raw")) _builderBodyBox.Focus();
        }

        private static void StyleBodyModeButton(RoundButton button, bool active)
        {
            if (button == null) return;
            button.FillColor = active ? Theme.AccentSurface : Color.Transparent;
            button.FillColor2 = button.FillColor;
            button.BorderColor = active ? Theme.AccentBorder : Color.Transparent;
            button.TextColor = active ? Theme.Accent : Theme.Muted;
            button.HoverColor = active ? Color.FromArgb(255, 236, 214) : Theme.Hover;
            button.Invalidate();
        }

        private void UpdateBuilderUrlPlaceholder()
        {
            if (_builderUrlBox == null || _builderUrlPlaceholder == null) return;
            _builderUrlPlaceholder.Visible = _builderUrlBox.TextLength == 0;
        }

        private void UpdateBuilderSummary()
        {
            if (!_builderMode || !_builderUiReady) return;
            List<string> parts = new List<string>();
            parts.Add(_builderMethod);
            int parameters = _builderParamsGrid == null ? 0 : _builderParamsGrid.ActiveCount;
            int headers = _builderHeadersGrid == null ? 0 : _builderHeadersGrid.ActiveCount;
            int fields = _builderFormGrid == null ? 0 : _builderFormGrid.ActiveCount;
            if (parameters > 0) parts.Add(parameters + " 参数");
            if (headers > 0) parts.Add(headers + " 请求头");
            string mode = CurlBuilder.NormalizeBodyMode(_builderBodyMode);
            if (mode == "json") parts.Add("JSON");
            else if (mode == "form") parts.Add("表单 " + fields);
            else if (mode == "raw") parts.Add("文本");
            if (_builderExtraArgs.Count > 0) parts.Add(_builderExtraArgs.Count + " 原始选项");
            string summary = string.Join("  ·  ", parts.ToArray());
            if (string.IsNullOrWhiteSpace(_builderUrlBox.Text))
                summary = "填写 URL 发送，或粘贴 cURL 自动导入";
            _leftSubtitle.Text = summary;
        }

        private void BuilderChanged(object sender, EventArgs e)
        {
            if (!_builderUiReady || _applyingBuilderDraft) return;
            UpdateBuilderSummary();
            UpdateSendEnabled();
            ScheduleDraftSave();
        }

        private BuilderDraft CaptureBuilderDraft()
        {
            BuilderDraft draft = new BuilderDraft();
            draft.Active = _builderMode;
            if (!_builderUiReady) return draft;
            draft.Method = _builderMethod;
            draft.Url = _builderUrlBox.Text;
            draft.BodyMode = _builderBodyMode;
            draft.BodyText = _builderBodyBox.Text;
            draft.ActiveTab = _builderTab;
            draft.Params = CompactRows(_builderParamsGrid.Snapshot());
            draft.Headers = CompactRows(_builderHeadersGrid.Snapshot());
            draft.Form = CompactRows(_builderFormGrid.Snapshot());
            draft.ExtraArgs = CloneExtras(_builderExtraArgs);
            if (_settings != null)
            {
                draft.FollowRedirects = _settings.FollowRedirects;
                draft.Insecure = _settings.Insecure;
                draft.Verbose = _settings.Verbose;
            }
            return draft;
        }

        private static List<BuilderRow> CompactRows(List<BuilderRow> rows)
        {
            List<BuilderRow> result = new List<BuilderRow>();
            if (rows == null) return result;
            foreach (BuilderRow row in rows)
            {
                if (row == null) continue;
                bool hasContent = !string.IsNullOrEmpty(row.Key) || !string.IsNullOrEmpty(row.Value);
                if (hasContent) result.Add(row.Clone());
            }
            return result;
        }

        private static List<BuilderExtraArg> CloneExtras(List<BuilderExtraArg> extras)
        {
            List<BuilderExtraArg> result = new List<BuilderExtraArg>();
            if (extras == null) return result;
            foreach (BuilderExtraArg extra in extras)
            {
                if (extra == null || string.IsNullOrEmpty(extra.Option)) continue;
                result.Add(extra.Clone());
            }
            return result;
        }

        private void ApplyBuilderDraft(BuilderDraft draft, bool applyOptions)
        {
            if (!_builderUiReady) return;
            if (draft == null) draft = new BuilderDraft();
            _applyingBuilderDraft = true;
            try
            {
                string method = (draft.Method ?? "GET").Trim().ToUpperInvariant();
                if (!CurlBuilder.IsValidMethod(method)) method = "GET";
                _builderMethod = method;
                UpdateBuilderMethodStyle();
                _builderUrlBox.Text = draft.Url ?? string.Empty;
                _builderParamsGrid.SetRows(draft.Params);
                _builderHeadersGrid.SetRows(draft.Headers);
                _builderFormGrid.SetRows(draft.Form);
                _builderBodyBox.Text = draft.BodyText ?? string.Empty;
                _builderExtraArgs = CloneExtras(draft.ExtraArgs);
                _builderBodyMode = CurlBuilder.NormalizeBodyMode(draft.BodyMode);
                _builderTab = Math.Max(0, Math.Min(2, draft.ActiveTab));
                if (_builderMethod == "HEAD") _builderBodyMode = "none";
            }
            finally
            {
                _applyingBuilderDraft = false;
            }

            if (applyOptions && _settings != null)
            {
                _settings.FollowRedirects = draft.FollowRedirects;
                _settings.Insecure = draft.Insecure;
                _settings.Verbose = draft.Verbose;
                RefreshOptionChecks();
            }

            SetBuilderTab(_builderTab);
            ApplyBodyMode(false);
            UpdateBuilderUrlPlaceholder();
            UpdateBuilderSummary();
            UpdateSendEnabled();
            ScheduleDraftSave();
        }

        private void ResetBuilder()
        {
            if (!_builderUiReady) return;
            ClearCurrentSaved();
            ApplyBuilderDraft(new BuilderDraft(), false);
            if (_builderUrlBox != null) _builderUrlBox.Focus();
            ShowToast("构建器已重置");
        }

        private void ClearCurrentSaved()
        {
            _currentSavedId = null;
            _currentSavedName = null;
            _currentSavedFolder = null;
        }

        private BuilderRequest CaptureBuilderRequest()
        {
            return new BuilderRequest
            {
                Method = _builderMethod,
                Url = _builderUrlBox.Text,
                BodyMode = _builderBodyMode,
                BodyText = _builderBodyBox.Text,
                Params = _builderParamsGrid.ActiveRows(),
                Headers = _builderHeadersGrid.ActiveRows(),
                Form = _builderFormGrid.ActiveRows(),
                ExtraArgs = CloneExtras(_builderExtraArgs)
            };
        }

        private void CopyBuilderCommand()
        {
            if (!_builderUiReady) return;
            string error;
            string command = CurlBuilder.Build(CaptureBuilderRequest(), out error);
            if (command == null)
            {
                ShowToast(error ?? "无法生成 cURL", ToastKind.Error);
                return;
            }
            CopyText(command, "cURL 已复制");
        }

        private void ImportCurlFromClipboard()
        {
            string text;
            try
            {
                if (!Clipboard.ContainsText() || string.IsNullOrWhiteSpace(Clipboard.GetText()))
                {
                    ShowToast("剪贴板里没有文本", ToastKind.Error);
                    return;
                }
                text = Clipboard.GetText();
            }
            catch (Exception ex)
            {
                ShowToast("读取剪贴板失败：" + ex.Message, ToastKind.Error);
                return;
            }
            TryImportCurl(text, false);
        }

        private bool TryImportCurl(string text, bool silentOnError)
        {
            if (!_builderUiReady) EnsureBuilderUi();
            BuilderDraft draft;
            string message;
            if (!CurlImporter.TryImport(text, out draft, out message))
            {
                if (!silentOnError) ShowToast(message ?? "无法导入 cURL", ToastKind.Error);
                return false;
            }
            ApplyBuilderDraft(draft, true);
            ClearCurrentSaved();
            if (_settings != null && _settings.Builder != null) _settings.Builder.Active = _builderMode;
            ShowToast(message ?? "已转换为请求");
            return true;
        }

        private RequestLibrary EnsureLibrary()
        {
            if (_libraryLoaded && _library != null) return _library;
            string error;
            _library = RequestLibraryStore.Load(_libraryPath, out error);
            _libraryLoaded = true;
            if (!string.IsNullOrEmpty(error))
            {
                try
                {
                    if (File.Exists(_libraryPath)) File.Copy(_libraryPath, _libraryPath + ".bad", true);
                }
                catch { }
                ShowToast(error, ToastKind.Error);
            }
            return _library;
        }

        private bool PersistLibrary()
        {
            if (_library == null) return false;
            string error;
            if (RequestLibraryStore.Save(_libraryPath, _library, out error)) return true;
            ShowToast("请求库保存失败：" + error, ToastKind.Error);
            return false;
        }

        private void SaveBuilderRequest()
        {
            if (!_builderUiReady || !_builderMode) return;
            if (string.IsNullOrWhiteSpace(_builderUrlBox.Text))
            {
                ShowToast("先填写请求 URL 再保存", ToastKind.Error);
                _builderUrlBox.Focus();
                return;
            }

            RequestLibrary library = EnsureLibrary();
            bool allowSaveAs = !string.IsNullOrEmpty(_currentSavedId) &&
                library.Requests.Any(delegate(SavedRequest item)
                {
                    return item != null && string.Equals(item.Id, _currentSavedId, StringComparison.Ordinal);
                });
            string initialName = _currentSavedName ?? string.Empty;
            string initialFolder = _currentSavedFolder ?? string.Empty;

            while (true)
            {
                string name;
                string folder;
                bool copy;
                using (SaveRequestDialog dialog = new SaveRequestDialog(
                    initialName,
                    initialFolder,
                    RequestLibraryStore.AllFolders(library),
                    _builderMethod,
                    _builderUrlBox.Text,
                    allowSaveAs))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    name = dialog.RequestName;
                    folder = dialog.RequestFolder;
                    copy = dialog.SaveAs;
                }

                name = (name ?? string.Empty).Trim();
                if (name.Length == 0) continue;
                folder = RequestLibraryStore.FindFolder(library, folder);

                SavedRequest target = null;
                if (!copy && !string.IsNullOrEmpty(_currentSavedId))
                {
                    target = library.Requests.FirstOrDefault(delegate(SavedRequest item)
                    {
                        return item != null && string.Equals(item.Id, _currentSavedId, StringComparison.Ordinal);
                    });
                }

                SavedRequest duplicate = library.Requests.FirstOrDefault(delegate(SavedRequest item)
                {
                    if (item == null || item == target) return false;
                    string itemFolder = RequestLibraryStore.NormalizeFolder(item.Folder);
                    return string.Equals((item.Name ?? string.Empty).Trim(), name, StringComparison.CurrentCultureIgnoreCase) &&
                           string.Equals(itemFolder, folder, StringComparison.CurrentCultureIgnoreCase);
                });

                if (duplicate != null)
                {
                    DialogResult answer = MessageBox.Show(this,
                        "目录中已有同名请求「" + duplicate.Name + "」，是否覆盖它？",
                        "CurlBox", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (answer == DialogResult.Cancel) return;
                    if (answer == DialogResult.No)
                    {
                        initialName = name;
                        initialFolder = folder;
                        continue;
                    }
                    if (target == null) target = duplicate;
                    else library.Requests.Remove(duplicate);
                }

                bool isNew = target == null;
                if (isNew)
                {
                    target = new SavedRequest();
                    library.Requests.Add(target);
                }
                target.Name = name;
                target.Folder = folder;
                target.UpdatedAt = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                BuilderDraft savedDraft = CaptureBuilderDraft();
                savedDraft.Active = false;
                target.Request = savedDraft;

                if (folder.Length > 0 && !library.Folders.Any(delegate(string item)
                {
                    return string.Equals(RequestLibraryStore.NormalizeFolder(item), folder,
                        StringComparison.CurrentCultureIgnoreCase);
                }))
                {
                    library.Folders.Add(folder);
                }

                if (!PersistLibrary())
                {
                    if (isNew) library.Requests.Remove(target);
                    return;
                }

                _currentSavedId = target.Id;
                _currentSavedName = target.Name;
                _currentSavedFolder = target.Folder;
                ShowToast(folder.Length == 0
                    ? "已保存到「未分组」：" + name
                    : "已保存到「" + folder + "」：" + name);
                return;
            }
        }

        private void ShowRequestLibrary()
        {
            ActivateLibraryMode(true);
        }

        private void OpenSavedRequest(SavedRequest saved, bool send)
        {
            if (saved == null || saved.Request == null) return;
            if (!_builderMode) SetBuilderMode(true, false, false);
            ApplyBuilderDraft(saved.Request, true);
            _currentSavedId = saved.Id;
            _currentSavedName = saved.Name;
            _currentSavedFolder = saved.Folder;
            bool canSend = send && !_running;
            if (send && _running)
                ShowToast("当前请求还在执行，已打开「" + saved.Name + "」但未发送", ToastKind.Error);
            else
                ShowToast(send ? "正在发送「" + saved.Name + "」" : "已打开「" + saved.Name + "」");
            if (canSend) BeginSend();
            else if (_builderUrlBox != null) _builderUrlBox.Focus();
            ScheduleDraftSave();
        }

        private void SyncCurrentSavedRequest()
        {
            if (string.IsNullOrEmpty(_currentSavedId) || _library == null) return;
            SavedRequest found = _library.Requests.FirstOrDefault(delegate(SavedRequest item)
            {
                return item != null && string.Equals(item.Id, _currentSavedId, StringComparison.Ordinal);
            });
            if (found == null)
            {
                ClearCurrentSaved();
                return;
            }
            _currentSavedName = found.Name;
            _currentSavedFolder = found.Folder;
        }

        private static bool LooksLikeCurl(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return Regex.IsMatch(text.TrimStart(), @"^(?:PS [^>\r\n]*>\s*)?curl(?:\.exe)?\s", RegexOptions.IgnoreCase);
        }

        private async void SendButtonClick(object sender, EventArgs e)
        {
            await SendCurrentAsync();
        }

        private async Task SendCurrentAsync()
        {
            if (_running) return;

            bool fromBuilder = _builderMode;
            string text;
            string methodText = string.Empty;

            if (fromBuilder)
            {
                if (!_builderUiReady) return;
                string buildError;
                text = CurlBuilder.Build(CaptureBuilderRequest(), out buildError);
                if (text == null)
                {
                    ShowToast(buildError ?? "无法生成请求", ToastKind.Error);
                    if (_builderUrlBox != null) _builderUrlBox.Focus();
                    return;
                }
                _previewError = null;
            }
            else
            {
                text = _curlBox.Text.Trim();
                if (text.Length == 0)
                {
                    ShowToast("先粘贴一条 cURL 命令", ToastKind.Error);
                    _curlBox.Focus();
                    return;
                }

                methodText = _methodBox.Text.Trim();
                if (!string.IsNullOrEmpty(methodText) && !IsValidMethod(methodText))
                {
                    ShowToast("请求方法格式不正确", ToastKind.Error);
                    _methodBox.Focus();
                    return;
                }
            }

            CurlCommand parsed;
            try
            {
                parsed = CurlParser.Parse(text);
            }
            catch (CurlParseException ex)
            {
                ShowToast(ex.Message, ToastKind.Error);
                UpdatePreview();
                return;
            }
            catch (Exception ex)
            {
                ShowToast("解析失败：" + ex.Message, ToastKind.Error);
                return;
            }

            _preview = parsed;
            _previewError = null;
            SaveSettings();

            _running = true;
            _cts = new CancellationTokenSource();
            _requestStarted = DateTime.Now;
            _loadingUrl.Text = parsed.Url;
            _toolTip.SetToolTip(_loadingUrl, parsed.Url);
            _loadingMeta.Text = "0.0s · 最长等待 120 秒";
            ShowRightState(RightState.Loading);
            _elapsedTimer.Start();
            UpdateSendEnabled();

            try
            {
                bool autoToken = IsAutoTokenEnabledForRequest(parsed.Url);
                if (autoToken)
                {
                    if (string.IsNullOrWhiteSpace(_settings.AdminToken))
                    {
                        ShowToast("正在自动登录获取 Token…");
                        AutoTokenLoginResult login = await AutoTokenService.LoginAsync(_settings, _cts.Token);
                        if (IsDisposed) return;
                        if (login.Success)
                        {
                            _settings.AdminToken = login.Token;
                            SaveSettings();
                            AutoTokenService.ApplyAuthorization(parsed, login.Token);
                        }
                        else if (login.Canceled)
                        {
                            ShowRequestCanceled();
                            return;
                        }
                        else
                        {
                            ShowFatal("自动登录失败", login.Error ?? "无法获取 Token。", null);
                            return;
                        }
                    }
                    else
                    {
                        AutoTokenService.ApplyAuthorization(parsed, _settings.AdminToken);
                    }
                }

                string overrideMethod = fromBuilder || string.IsNullOrEmpty(methodText)
                    ? null
                    : methodText.ToUpperInvariant();
                CurlResult result = await CurlRunner.RunAsync(
                    parsed,
                    overrideMethod,
                    _settings.FollowRedirects,
                    _settings.Insecure,
                    _settings.Verbose,
                    _cts.Token);

                if (IsDisposed) return;

                if (autoToken && result != null && !result.Canceled && !result.Fatal &&
                    AutoTokenService.IsUnauthorized(result))
                {
                    ShowToast("Token 已失效，正在自动登录…");
                    AutoTokenLoginResult login = await AutoTokenService.LoginAsync(_settings, _cts.Token);
                    if (IsDisposed) return;
                    if (login.Success)
                    {
                        _settings.AdminToken = login.Token;
                        SaveSettings();
                        AutoTokenService.ApplyAuthorization(parsed, login.Token);
                        result = await CurlRunner.RunAsync(
                            parsed,
                            overrideMethod,
                            _settings.FollowRedirects,
                            _settings.Insecure,
                            _settings.Verbose,
                            _cts.Token);
                        if (IsDisposed) return;
                        if (result != null && !result.Canceled && !result.Fatal &&
                            AutoTokenService.IsUnauthorized(result))
                            ShowToast("已重新登录，但接口仍返回 401", ToastKind.Error);
                        else if (result != null && !result.Canceled && !result.Fatal)
                            ShowToast("Token 已刷新，已重试请求");
                    }
                    else if (login.Canceled)
                    {
                        ShowRequestCanceled();
                        return;
                    }
                    else
                    {
                        ShowToast("重新登录失败：" + (login.Error ?? "未知错误"), ToastKind.Error);
                    }
                }

                if (result == null)
                {
                    ShowFatal("请求未完成", "curl 没有返回结果。", null);
                }
                else if (result.Fatal)
                {
                    ShowFatal("请求未完成", result.FatalMessage, null);
                }
                else if (result.Canceled)
                {
                    ShowRequestCanceled();
                }
                else
                {
                    _result = result;
                    RenderResult(result);
                    AddHistory(parsed, fromBuilder ? null : methodText, result, fromBuilder ? text : null);
                    ShowRightState(RightState.Result);
                    if (!string.IsNullOrEmpty(result.Stderr) && result.ExitCode != 0 && result.BodyKind == "empty")
                        SetActiveTab(ResponseTab.Log);
                    else
                        SetActiveTab(ResponseTab.Body);
                }
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    _result = null;
                    ShowFatal("请求执行失败", ex.Message, ex.ToString());
                }
            }
            finally
            {
                if (!IsDisposed)
                {
                    _elapsedTimer.Stop();
                    _running = false;
                    if (_cts != null)
                    {
                        _cts.Dispose();
                        _cts = null;
                    }
                    UpdateSendEnabled();
                }
            }
        }

        private void BeginSend()
        {
            Task ignored = SendCurrentAsync();
        }

        private void CancelCurrent()
        {
            if (!_running) return;
            _loadingMeta.Text = "正在取消…";
            try
            {
                if (_cts != null) _cts.Cancel();
            }
            catch { }
            CurlRunner.CancelActive();
        }

        private void ShowRequestCanceled()
        {
            if (_result != null) ShowRightState(RightState.Result);
            else ShowRightState(RightState.Empty);
            ShowToast("已取消请求");
        }

        private void ShowFatal(string title, string message, string detail)
        {
            _fatalTitle.Text = title;
            _fatalMessage.Text = message ?? string.Empty;
            if (!string.IsNullOrEmpty(detail))
            {
                _fatalDetail.Text = detail;
                _fatalDetail.Visible = true;
            }
            else
            {
                _fatalDetail.Text = string.Empty;
                _fatalDetail.Visible = false;
            }
            ShowRightState(RightState.Fatal);
            UpdateSendEnabled();
        }

        private void ShowRightState(RightState state)
        {
            _rightState = state;
            if (_emptyPanel != null) _emptyPanel.Visible = state == RightState.Empty;
            if (_loadingPanel != null) _loadingPanel.Visible = state == RightState.Loading;
            if (_fatalPanel != null) _fatalPanel.Visible = state == RightState.Fatal;
            if (_resultPanel != null) _resultPanel.Visible = state == RightState.Result;
            LayoutRight();
            if (state == RightState.Loading)
            {
                _cancelButton.Focus();
            }
            else if (state == RightState.Result)
            {
                LayoutResult();
            }
            else if (state == RightState.Empty)
            {
                LayoutEmpty();
            }
            else if (state == RightState.Fatal)
            {
                LayoutFatal();
            }
            Invalidate(true);
            Update();
        }

        private void RenderResult(CurlResult result)
        {
            if (_resultPanel == null) return;

            int status = result.StatusCode;
            Color statusColor = Theme.ColorForStatus(status);
            _statusPill.Text = status > 0 ? status.ToString(CultureInfo.InvariantCulture) : "ERR";
            _statusPill.TextColor = statusColor;
            _statusPill.FillColor = Color.FromArgb(30, statusColor);
            _statusPill.BorderColor = Color.FromArgb(68, statusColor);
            _statusPill.Height = 22;
            _statusPill.FitToText(12);

            _statusText.Text = StatusText(result);
            _statusText.ForeColor = statusColor;

            string size = UiText.FormatBytes(result.BodyTotalBytes > 0 ? result.BodyTotalBytes : result.SizeDownload);
            string type = ShortType(result.ContentType);
            _metaFacts.Text = "耗时 " + UiText.FormatMs(result.DurationMs) + "   ·   大小 " + size + "   ·   " + type;
            _toolTip.SetToolTip(_metaFacts, _metaFacts.Text);

            Color methodColor = Theme.ColorForMethod(result.Method);
            _methodPill.Text = result.Method ?? "—";
            _methodPill.TextColor = methodColor;
            _methodPill.FillColor = Color.FromArgb(28, methodColor);
            _methodPill.BorderColor = Color.FromArgb(60, methodColor);
            _methodPill.Height = 18;
            _methodPill.FitToText(10);

            _urlLabel.Text = result.EffectiveUrl ?? result.RequestedUrl ?? string.Empty;
            _toolTip.SetToolTip(_urlLabel, _urlLabel.Text);

            bool failed = result.TimedOut || (result.ExitCode != 0);
            _alertPanel.Visible = failed;
            if (failed)
            {
                if (result.TimedOut)
                {
                    _alertTitle.Text = "请求超时";
                    _alertText.Text = "请求超过 120 秒，已由本机 curl 终止。";
                }
                else
                {
                    _alertTitle.Text = "请求未正常结束 · curl " + result.ExitCode;
                    string hint = result.Hint;
                    if (string.IsNullOrEmpty(hint) && !string.IsNullOrEmpty(result.Stderr))
                    {
                        hint = result.Stderr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(delegate(string line) { return line.Trim(); })
                            .FirstOrDefault(delegate(string line) { return line.Length > 0; });
                    }
                    _alertText.Text = string.IsNullOrEmpty(hint) ? "curl 没有正常完成这次请求。" : hint;
                }
                _toolTip.SetToolTip(_alertText, _alertText.Text);
                _alertLogButton.Visible = !string.IsNullOrEmpty(result.Stderr);
            }

            int headerCount = result.FinalHeaders == null ? 0 : result.FinalHeaders.Headers.Count;
            _headersTab.Text = "响应头 " + headerCount;
            _logTab.Visible = !string.IsNullOrEmpty(result.Stderr);
            SizeTab(_bodyTab);
            SizeTab(_headersTab);
            SizeTab(_logTab);

            _headersBox.Text = BuildHeadersText(result);
            ColorHeaders();
            _copyHeadersButton.Enabled = !string.IsNullOrEmpty(result.RawHeaders) || headerCount > 0;

            if (result.Redirects.Count > 0)
            {
                List<string> chain = new List<string>();
                foreach (RedirectInfo redirect in result.Redirects)
                    chain.Add(redirect.Status + " → " + redirect.Location);
                _redirectLabel.Visible = true;
                _redirectLabel.Text = string.Join("   ", chain.ToArray());
                _toolTip.SetToolTip(_redirectLabel, string.Join("\r\n", chain.ToArray()));
            }
            else
            {
                _redirectLabel.Visible = false;
                _redirectLabel.Text = string.Empty;
            }
            _timingLabel.Text = BuildTimingText(result);
            _timingLabel.Visible = !string.IsNullOrEmpty(_timingLabel.Text);

            _prettyAvailable = false;
            _prettyText = null;
            RenderBodyContent();
            _copyLogButton.Enabled = !string.IsNullOrEmpty(result.Stderr);
            _logBox.Text = result.Stderr ?? string.Empty;
            if (result.StderrTruncated) _logBox.AppendText("\r\n\r\n[日志过长，仅显示前 256 KB]");
            _logBox.Select(0, 0);
            _logBox.ScrollToCaret();

            _activeTab = ResponseTab.Body;
            SetActiveTab(ResponseTab.Body);
            LayoutResult();
        }

        private string StatusText(CurlResult result)
        {
            if (result.TimedOut) return "请求超时";
            if (result.StatusCode <= 0) return "无响应";
            switch (result.StatusCode)
            {
                case 200: return "OK";
                case 201: return "Created";
                case 202: return "Accepted";
                case 204: return "No Content";
                case 301: return "Moved Permanently";
                case 302: return "Found";
                case 304: return "Not Modified";
                case 307: return "Temporary Redirect";
                case 308: return "Permanent Redirect";
                case 400: return "Bad Request";
                case 401: return "Unauthorized";
                case 403: return "Forbidden";
                case 404: return "Not Found";
                case 405: return "Method Not Allowed";
                case 409: return "Conflict";
                case 422: return "Unprocessable Entity";
                case 429: return "Too Many Requests";
                case 500: return "Internal Server Error";
                case 502: return "Bad Gateway";
                case 503: return "Service Unavailable";
                case 504: return "Gateway Timeout";
                default: return string.Empty;
            }
        }

        private static string ShortType(string contentType)
        {
            if (string.IsNullOrEmpty(contentType)) return "—";
            string value = contentType.Split(';')[0].Trim();
            return value.Length == 0 ? "—" : value;
        }

        private void RenderBodyContent()
        {
            if (_result == null || _bodyPanel == null) return;

            CurlResult result = _result;
            DisposeImage();

            _bodyBox.Visible = false;
            _imagePanel.Visible = false;
            _bodyEmptyLabel.Visible = false;
            _prettyButton.Visible = false;
            _wrapButton.Visible = false;
            _copyBodyButton.Enabled = false;
            _saveButton.Enabled = false;
            _bodyTruncatedLabel.Visible = result.BodyTruncated;

            _bodySizeLabel.Text = UiText.FormatBytes(result.BodyTotalBytes);
            string kind = BodyKindLabel(result);
            _bodyKindLabel.Text = kind;

            if (result.BodyKind == "text")
            {
                string raw = result.BodyText ?? string.Empty;
                _bodyBox.Visible = true;
                _prettyAvailable = JsonDisplay.TryPretty(raw, result.ContentType, out _prettyText);
                if (_prettyAvailable) _bodyKindLabel.Text = "JSON";

                _prettyButton.Visible = _prettyAvailable;
                _wrapButton.Visible = true;
                _copyBodyButton.Enabled = raw.Length > 0;
                _saveButton.Enabled = !result.BodyTruncated && raw.Length > 0;
                _prettyButton.Text = "格式化";
                _wrapButton.Text = "换行";
                UpdateToggleStyle(_prettyButton, _prettyAvailable && _pretty);
                UpdateToggleStyle(_wrapButton, _wrap);

                bool usePretty = _prettyAvailable && _pretty;
                string shown = usePretty ? _prettyText : raw;
                SetBodyText(shown, usePretty && shown.Length <= 800000);

                if (raw.Length == 0)
                {
                    _bodyBox.Visible = false;
                    _bodyEmptyLabel.Visible = true;
                    _bodyEmptyLabel.Text = "响应体为空。";
                }
            }
            else if (result.BodyKind == "image" || result.BodyKind == "binary")
            {
                bool imageShown = false;
                if (result.BodyKind == "image" && result.BodyBytes != null && !result.BodyTruncated)
                {
                    try
                    {
                        using (MemoryStream stream = new MemoryStream(result.BodyBytes))
                        using (Image image = Image.FromStream(stream))
                        {
                            _imageBox.Image = new Bitmap(image);
                        }
                        imageShown = true;
                    }
                    catch
                    {
                        imageShown = false;
                    }
                }

                if (imageShown)
                {
                    _imagePanel.Visible = true;
                    _imageInfo.Text = (result.Mime ?? "image") + "   ·   " + UiText.FormatBytes(result.BodyTotalBytes);
                }
                else
                {
                    _bodyEmptyLabel.Visible = true;
                    _bodyEmptyLabel.Text = "二进制响应  ·  " + (result.Mime ?? "application/octet-stream") +
                        "\r\n" + UiText.FormatBytes(result.BodyTotalBytes) +
                        (result.BodyTruncated ? "（过大，未载入）" : "");
                }
                _saveButton.Enabled = result.BodyBytes != null && !result.BodyTruncated;
            }
            else
            {
                _bodyEmptyLabel.Visible = true;
                if (result.HeadOnly)
                    _bodyEmptyLabel.Text = "HEAD 请求没有响应体，响应头已单独展示。";
                else if (result.StatusCode == 204)
                    _bodyEmptyLabel.Text = "204 No Content，响应体为空。";
                else if (result.StatusCode == 304)
                    _bodyEmptyLabel.Text = "304 Not Modified，响应体为空。";
                else if (result.StatusCode <= 0)
                    _bodyEmptyLabel.Text = "没有收到响应体。";
                else
                    _bodyEmptyLabel.Text = "响应体为空。";
            }

            LayoutBodyPanel();
        }

        private void SetBodyText(string text, bool highlight)
        {
            _bodyBox.WordWrap = _wrap;
            _bodyBox.ScrollBars = _wrap ? RichTextBoxScrollBars.Vertical : RichTextBoxScrollBars.Both;
            _bodyBox.Text = text;
            _bodyBox.SelectAll();
            _bodyBox.SelectionColor = Theme.TextSoft;
            _bodyBox.Select(0, 0);

            if (highlight)
            {
                try
                {
                    string actual = _bodyBox.Text;
                    Regex regex = new Regex("\"(?:\\\\.|[^\"\\\\])*\"\\s*:?|\\btrue\\b|\\bfalse\\b|\\bnull\\b|-?\\d+(?:\\.\\d+)?(?:[eE][+-]?\\d+)?");
                    foreach (Match match in regex.Matches(actual))
                    {
                        string token = match.Value;
                        Color color = Theme.TextSoft;
                        if (token.StartsWith("\"", StringComparison.Ordinal))
                        {
                            color = token.TrimEnd().EndsWith(":", StringComparison.Ordinal)
                                ? Theme.CodeKey : Theme.CodeString;
                        }
                        else if (token == "true" || token == "false")
                            color = Theme.CodeBool;
                        else if (token == "null")
                            color = Theme.CodeNull;
                        else
                            color = Theme.CodeNumber;
                        _bodyBox.Select(match.Index, match.Length);
                        _bodyBox.SelectionColor = color;
                    }
                }
                catch { }
            }
            _bodyBox.Select(0, 0);
            _bodyBox.ScrollToCaret();
        }

        private static string BodyKindLabel(CurlResult result)
        {
            if (result.BodyKind == "empty") return "EMPTY";
            if (result.BodyKind == "image") return "IMAGE";
            if (result.BodyKind == "binary") return "BINARY";
            string type = (result.ContentType ?? string.Empty).ToLowerInvariant();
            if (type.Contains("json")) return "JSON";
            if (type.Contains("html")) return "HTML";
            if (type.Contains("xml")) return "XML";
            if (type.Contains("javascript")) return "JS";
            if (type.Contains("css")) return "CSS";
            if (type.Contains("csv")) return "CSV";
            if (type.Contains("event-stream")) return "STREAM";
            return "TEXT";
        }

        private void UpdateToggleStyle(RoundButton button, bool active)
        {
            if (button == null) return;
            button.TextColor = active ? Theme.Accent : Theme.Muted;
            button.FillColor = active ? Theme.AccentSurface : Color.Transparent;
            button.FillColor2 = button.FillColor;
            button.BorderColor = active ? Theme.AccentBorder : Color.Transparent;
            button.Invalidate();
        }

        private string BuildHeadersText(CurlResult result)
        {
            if (result.FinalHeaders == null)
                return "没有收到响应头。";
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(result.FinalHeaders.StatusLine);
            foreach (HeaderItem header in result.FinalHeaders.Headers)
                builder.Append(header.Name).Append(": ").AppendLine(header.Value);
            return builder.ToString();
        }

        private void ColorHeaders()
        {
            if (_headersBox.TextLength == 0) return;
            _headersBox.SelectAll();
            _headersBox.SelectionColor = Theme.TextSoft;
            string[] lines = _headersBox.Text.Split('\n');
            int start = 0;
            int maximum = Math.Min(lines.Length, 400);
            for (int i = 0; i < maximum; i++)
            {
                string line = lines[i];
                int colon = line.IndexOf(':');
                if (i == 0 && line.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
                {
                    _headersBox.Select(start, line.Length);
                    _headersBox.SelectionColor = Theme.Mint;
                }
                else if (colon > 0)
                {
                    _headersBox.Select(start, colon);
                    _headersBox.SelectionColor = Theme.Sky;
                    _headersBox.Select(start + colon, line.Length - colon);
                    _headersBox.SelectionColor = Theme.TextSoft;
                }
                start += line.Length + 1;
            }
            _headersBox.Select(0, 0);
            _headersBox.ScrollToCaret();
        }

        private static string BuildTimingText(CurlResult result)
        {
            if (result.TimeTotal <= 0) return string.Empty;
            long dns = (long)Math.Round(result.TimeNamelookup * 1000);
            long connect = (long)Math.Round(Math.Max(0, result.TimeConnect - result.TimeNamelookup) * 1000);
            long ttfb = (long)Math.Round(Math.Max(0, result.TimeStarttransfer - result.TimeConnect) * 1000);
            return "DNS " + dns + " ms   ·   连接 " + connect + " ms   ·   首字节 " + ttfb + " ms   ·   总计 " +
                UiText.FormatMs(result.DurationMs);
        }

        private void SetActiveTab(ResponseTab tab)
        {
            if (tab == ResponseTab.Log && _logTab != null && !_logTab.Visible)
                tab = ResponseTab.Body;
            _activeTab = tab;
            _bodyTab.Active = tab == ResponseTab.Body;
            _headersTab.Active = tab == ResponseTab.Headers;
            _logTab.Active = tab == ResponseTab.Log;
            _bodyTab.TextColor = tab == ResponseTab.Body ? Theme.Text : Theme.Muted;
            _headersTab.TextColor = tab == ResponseTab.Headers ? Theme.Text : Theme.Muted;
            _logTab.TextColor = tab == ResponseTab.Log ? Theme.Text : Theme.Muted;
            _bodyTab.Invalidate();
            _headersTab.Invalidate();
            _logTab.Invalidate();
            _bodyPanel.Visible = tab == ResponseTab.Body;
            _headersPanel.Visible = tab == ResponseTab.Headers;
            _logPanel.Visible = tab == ResponseTab.Log;
            LayoutResultPanels();
            if (_resultPanel.Visible)
            {
                _resultPanel.Invalidate(true);
                _resultPanel.Update();
            }
        }

        private void SizeTab(RoundButton tab)
        {
            if (tab == null) return;
            Size measured = TextRenderer.MeasureText(tab.Text, tab.Font, Size.Empty, TextFormatFlags.NoPadding);
            tab.Width = Math.Max(52, measured.Width + 18);
        }

        private void AddHistory(CurlCommand parsed, string overrideMethod, CurlResult result, string commandText)
        {
            string overrideValue = string.IsNullOrEmpty(overrideMethod) ? string.Empty : overrideMethod.ToUpperInvariant();
            HistoryEntry entry = new HistoryEntry();
            entry.Command = string.IsNullOrEmpty(commandText) ? _curlBox.Text : commandText;
            entry.Method = result.Method ?? parsed.Method;
            entry.Override = overrideValue;
            entry.At = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            entry.Status = result.StatusCode;
            entry.DurationMs = result.DurationMs;
            entry.Url = result.EffectiveUrl ?? parsed.Url;
            entry.ExitCode = result.ExitCode;

            _history.RemoveAll(delegate(HistoryEntry item)
            {
                string existingOverride = item.Override ?? string.Empty;
                return string.Equals(item.Command, entry.Command, StringComparison.Ordinal) &&
                       string.Equals(existingOverride, overrideValue, StringComparison.OrdinalIgnoreCase);
            });
            _history.Insert(0, entry);
            if (_history.Count > MaxHistory)
                _history.RemoveRange(MaxHistory, _history.Count - MaxHistory);
            SaveHistory();
            RenderHistory(true);
        }

        private void RenderHistory(bool selectFirst)
        {
            if (_historyList == null) return;
            _suppressHistorySelection = true;
            _historyList.BeginUpdate();
            _historyList.Items.Clear();
            foreach (HistoryEntry entry in _history)
                _historyList.Items.Add(entry);
            _historyList.EndUpdate();
            _historyCount.Text = _history.Count.ToString(CultureInfo.InvariantCulture);
            _historyEmpty.Visible = !_builderMode && _history.Count == 0;
            _clearHistory.Visible = !_builderMode && _history.Count > 0;
            if (selectFirst && _historyList.Items.Count > 0)
                _historyList.SelectedIndex = 0;
            else if (_historyList.SelectedIndex >= _historyList.Items.Count)
                _historyList.SelectedIndex = -1;
            _suppressHistorySelection = false;
            _historyList.Invalidate();
        }

        private void LoadHistoryEntry(HistoryEntry entry)
        {
            if (entry == null) return;
            _curlBox.Text = entry.Command ?? string.Empty;
            _methodBox.Text = entry.Override ?? string.Empty;
            UpdatePlaceholder();
            UpdatePreview();
            _curlBox.Focus();
            _curlBox.SelectionStart = _curlBox.Text.Length;
        }

        private void ClearHistory()
        {
            if (_history.Count == 0) return;
            if (MessageBox.Show("清空全部请求历史？", "CurlBox", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                return;
            _history.Clear();
            SaveHistory();
            RenderHistory(false);
            ShowToast("历史已清空");
        }

        private void DrawHistoryItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _historyList.Items.Count) return;
            HistoryEntry entry = _historyList.Items[e.Index] as HistoryEntry;
            if (entry == null) return;

            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color background = selected ? Theme.Selected : Theme.Panel;
            using (SolidBrush brush = new SolidBrush(background))
                e.Graphics.FillRectangle(brush, e.Bounds);
            using (Pen line = new Pen(Theme.BorderSoft))
                e.Graphics.DrawLine(line, e.Bounds.Left + 6, e.Bounds.Bottom - 1, e.Bounds.Right - 6, e.Bounds.Bottom - 1);

            if (selected)
            {
                using (SolidBrush accent = new SolidBrush(Theme.Accent))
                    e.Graphics.FillRectangle(accent, e.Bounds.Left, e.Bounds.Top + 6, 2, e.Bounds.Height - 12);
            }

            string method = string.IsNullOrEmpty(entry.Method) ? "GET" : entry.Method.ToUpperInvariant();
            Color methodColor = Theme.ColorForMethod(method);
            int pillWidth = Math.Min(58, TextRenderer.MeasureText(method, Theme.MonoBold(7f), Size.Empty, TextFormatFlags.NoPadding).Width + 10);
            Rectangle pill = new Rectangle(e.Bounds.Left + 8, e.Bounds.Top + 8, pillWidth, 17);
            Theme.DrawPill(e.Graphics, pill, method, Theme.MonoBold(7f), methodColor,
                Color.FromArgb(28, methodColor), Color.FromArgb(58, methodColor));

            int textLeft = pill.Right + 9;
            int textWidth = Math.Max(20, e.Bounds.Right - textLeft - 8);
            TextRenderer.DrawText(e.Graphics,
                UiText.DisplayUrl(entry.Url),
                Theme.Mono(8.7f),
                new Rectangle(textLeft, e.Bounds.Top + 5, textWidth, 17),
                selected ? Theme.Text : Theme.TextSoft,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

            string status = entry.Status > 0 ? entry.Status.ToString(CultureInfo.InvariantCulture) : "ERR";
            Color statusColor = Theme.ColorForStatus(entry.Status);
            int statusWidth = 34;
            TextRenderer.DrawText(e.Graphics,
                status,
                Theme.Mono(7.8f),
                new Rectangle(textLeft, e.Bounds.Top + 24, statusWidth, 15),
                statusColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            string meta = "  " + UiText.FormatMs(entry.DurationMs) + "  ·  " + UiText.RelativeTime(entry.At);
            TextRenderer.DrawText(e.Graphics,
                meta,
                Theme.Ui(7.5f),
                new Rectangle(textLeft + statusWidth, e.Bounds.Top + 24, Math.Max(20, textWidth - statusWidth), 15),
                Theme.Faint,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private void CopyResultBody()
        {
            if (_result == null || _result.BodyKind != "text" || string.IsNullOrEmpty(_result.BodyText)) return;
            CopyText(_result.BodyText, _result.BodyTruncated ? "已复制已载入的部分" : "响应体已复制");
        }

        private void CopyHeaders()
        {
            if (_result == null) return;
            string text = !string.IsNullOrEmpty(_result.RawHeaders) ? _result.RawHeaders : BuildHeadersText(_result);
            CopyText(text, "响应头已复制");
        }

        private void CopyText(string text, string message)
        {
            if (string.IsNullOrEmpty(text)) return;
            Exception last = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    Clipboard.SetText(text);
                    ShowToast(message);
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    Thread.Sleep(40);
                }
            }
            ShowToast("复制失败：" + (last == null ? "未知错误" : last.Message), ToastKind.Error);
        }

        private void SaveResultBody()
        {
            if (_result == null) return;
            if (_result.BodyTruncated)
            {
                ShowToast("响应过大，内容不完整，无法保存", ToastKind.Error);
                return;
            }

            string extension;
            bool binary = _result.BodyKind == "image" || _result.BodyKind == "binary";
            string type = (_result.ContentType ?? string.Empty).ToLowerInvariant();
            if (type.Contains("json")) extension = "json";
            else if (type.Contains("html")) extension = "html";
            else if (type.Contains("xml")) extension = "xml";
            else if (type.Contains("css")) extension = "css";
            else if (type.Contains("javascript")) extension = "js";
            else if (type.Contains("png")) extension = "png";
            else if (type.Contains("jpeg") || type.Contains("jpg")) extension = "jpg";
            else if (type.Contains("gif")) extension = "gif";
            else if (type.Contains("webp")) extension = "webp";
            else if (type.Contains("pdf")) extension = "pdf";
            else if (binary) extension = "bin";
            else extension = "txt";

            string filter = "响应文件 (*." + extension + ")|*." + extension + "|所有文件 (*.*)|*.*";
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "保存响应";
                dialog.FileName = "response." + extension;
                dialog.Filter = filter;
                dialog.OverwritePrompt = true;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    if (binary)
                    {
                        if (_result.BodyBytes == null)
                        {
                            ShowToast("二进制内容未载入，无法保存", ToastKind.Error);
                            return;
                        }
                        File.WriteAllBytes(dialog.FileName, _result.BodyBytes);
                    }
                    else
                    {
                        File.WriteAllText(dialog.FileName, _result.BodyText ?? string.Empty, new UTF8Encoding(false));
                    }
                    ShowToast("响应已保存");
                }
                catch (Exception ex)
                {
                    ShowToast("保存失败：" + ex.Message, ToastKind.Error);
                }
            }
        }

        private void DisposeImage()
        {
            if (_imageBox != null && _imageBox.Image != null)
            {
                Image image = _imageBox.Image;
                _imageBox.Image = null;
                image.Dispose();
            }
        }

        private void ToggleOption(string name)
        {
            if (name == "follow") _settings.FollowRedirects = !_settings.FollowRedirects;
            else if (name == "insecure") _settings.Insecure = !_settings.Insecure;
            else if (name == "verbose") _settings.Verbose = !_settings.Verbose;
            RefreshOptionChecks();
            SaveSettings();
        }

        private void RefreshOptionChecks()
        {
            if (_followItem != null) _followItem.Checked = _settings.FollowRedirects;
            if (_insecureItem != null) _insecureItem.Checked = _settings.Insecure;
            if (_verboseItem != null) _verboseItem.Checked = _settings.Verbose;
        }

        private void ToggleAutoStart()
        {
            try
            {
                bool enable = !AutoStartStore.IsEnabled();
                AutoStartStore.SetEnabled(enable);
                RefreshAutoStartChecks();
                ShowToast(enable ? "已开启开机自动启动" : "已关闭开机自动启动");
            }
            catch (Exception ex)
            {
                MessageBox.Show("设置开机启动失败：\r\n" + ex.Message, "CurlBox",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RefreshAutoStartChecks()
        {
            bool enabled = AutoStartStore.IsEnabled();
            if (_settingsAutoStart != null) _settingsAutoStart.Checked = enabled;
            if (_trayAutoStart != null) _trayAutoStart.Checked = enabled;
        }

        private void ShowToast(string message)
        {
            ShowToast(message, ToastKind.Info);
        }

        private void ShowToast(string message, ToastKind kind)
        {
            if (_toast == null) return;
            _toastLabel.Text = message;
            Size measured = TextRenderer.MeasureText(message, _toastLabel.Font, new Size(520, 40), TextFormatFlags.WordBreak);
            int width = Math.Min(520, Math.Max(180, measured.Width + 38));
            int height = 34;
            _toast.Size = new Size(width, height);
            _toastLabel.Bounds = new Rectangle(10, 0, width - 20, height);
            _toast.FillColor = kind == ToastKind.Error ? Theme.ErrorSurface : Theme.Panel;
            _toast.BorderColor = kind == ToastKind.Error ? Theme.ErrorBorder : Theme.Border;
            _toastLabel.BackColor = _toast.FillColor;
            _toastLabel.ForeColor = kind == ToastKind.Error ? Theme.ErrorTitle : Theme.TextSoft;
            LayoutToast();
            _toast.Visible = true;
            _toast.BringToFront();
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        private void UpdateMemoryLabel()
        {
            try
            {
                using (Process process = Process.GetCurrentProcess())
                    _memoryBytes = process.PrivateMemorySize64;
            }
            catch
            {
                _memoryBytes = 0;
            }
            if (_footerRight != null)
                _footerRight.Text = BuildFooterRight();
        }

        private string BuildFooterRight()
        {
            string memory = _memoryBytes > 0 ? (Math.Round(_memoryBytes / (1024.0 * 1024.0)) + " MB") : "—";
            string hotkey = _hotkeyRegistered ? "Ctrl+Alt+C 唤起" : "CurlBox";
            return _curlVersion + "  ·  内存 " + memory + "  ·  " + hotkey;
        }

        private static string QueryCurlVersion()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("curl.exe", "--version");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                using (Process process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        string first = process.StandardOutput.ReadLine();
                        process.WaitForExit(1500);
                        if (!string.IsNullOrEmpty(first))
                        {
                            Match versionMatch = Regex.Match(first, @"^curl\s+([0-9][^\s]*)", RegexOptions.IgnoreCase);
                            if (versionMatch.Success) return "curl " + versionMatch.Groups[1].Value;
                        }
                    }
                }
            }
            catch { }
            return "curl";
        }

        private void HideToTray(bool showBalloon)
        {
            if (!Visible) return;
            SaveSettings();
            Hide();
            if (showBalloon && !_trayBalloonShown && _trayIcon != null)
            {
                _trayBalloonShown = true;
                try
                {
                    _trayIcon.ShowBalloonTip(2600, "CurlBox 仍在运行",
                        "点击右下角托盘图标即可重新打开。", ToolTipIcon.Info);
                }
                catch { }
            }
        }

        private void ShowFromTray()
        {
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            Show();
            Activate();
            NativeMethods.SetForegroundWindow(Handle);
            _curlBox.Focus();
            LayoutAll();
        }

        private void ExitApplication()
        {
            _reallyExit = true;
            SaveSettings();
            SaveHistory();
            if (_cts != null)
            {
                try { _cts.Cancel(); }
                catch { }
            }
            CurlRunner.CancelActive();
            if (_trayIcon != null) _trayIcon.Visible = false;
            Close();
            Application.Exit();
        }

        private void LayoutAll()
        {
            if (_topBar == null) return;
            SuspendLayout();
            LayoutTopBar();
            LayoutWorkspace();
            LayoutFooter();
            LayoutToast();
            ResumeLayout(true);
        }

        private void LayoutTopBar()
        {
            int width = ClientSize.Width;
            _settingsButton.Location = new Point(width - 18 - _settingsButton.Width, 13);
            _topHotkey.Location = new Point(width - 18 - _settingsButton.Width - 12 - _topHotkey.Width, 13);
            _brandMark.Location = new Point(18, 11);
            _brandTitle.Location = new Point(58, 8);
            _brandSub.Location = new Point(59, 31);
            if (_modeCurlButton != null && _modeBuilderButton != null)
            {
                int modeX = Math.Max(300, Math.Min(306, width - 740));
                _modeCurlButton.Location = new Point(modeX, 14);
                _modeBuilderButton.Location = new Point(_modeCurlButton.Right + 6, 14);
                if (_modeLibraryButton != null)
                    _modeLibraryButton.Location = new Point(_modeBuilderButton.Right + 6, 14);
            }
        }

        private void LayoutWorkspace()
        {
            int top = 54 + 14;
            int bottom = ClientSize.Height - 28 - 14;
            int width = ClientSize.Width - 32;
            int gap = 12;
            if (width <= gap || bottom <= top) return;

            if (_libraryMode)
            {
                _leftCard.Visible = false;
                _rightCard.Visible = false;
                if (_splitHandle != null) _splitHandle.Visible = false;
                if (_libraryView != null)
                {
                    _libraryView.SetBounds(16, top, width, bottom - top);
                    _libraryView.Visible = true;
                    _libraryView.BringToFront();
                }
                return;
            }

            _leftCard.Visible = true;
            _rightCard.Visible = true;
            if (_libraryView != null) _libraryView.Visible = false;

            int usable = width - gap;
            int minLeft = Math.Min(400, Math.Max(280, usable / 2));
            int minRight = Math.Min(380, Math.Max(260, usable / 2));
            if (usable <= minLeft + minRight)
            {
                minLeft = Math.Max(260, usable / 2);
                minRight = Math.Max(240, usable - minLeft);
            }
            double ratio = _splitRatio;
            if (ratio < 0.1 || ratio > 0.9) ratio = 0.43;
            int leftWidth = (int)Math.Round(usable * ratio);
            leftWidth = Math.Max(minLeft, Math.Min(usable - minRight, leftWidth));
            int rightWidth = usable - leftWidth;
            _leftCard.Bounds = new Rectangle(16, top, leftWidth, bottom - top);
            _rightCard.Bounds = new Rectangle(16 + leftWidth + gap, top, rightWidth, bottom - top);
            if (_splitHandle != null)
            {
                _splitHandle.Bounds = new Rectangle(16 + leftWidth, top, gap, bottom - top);
                _splitHandle.Visible = true;
                _splitHandle.BringToFront();
            }

            LayoutLeft();
            LayoutRight();
        }

        private void ApplySplitDrag()
        {
            int width = ClientSize.Width - 32;
            int gap = 12;
            int usable = width - gap;
            if (usable <= 0) return;
            int minLeft = Math.Min(400, Math.Max(280, usable / 2));
            int minRight = Math.Min(380, Math.Max(260, usable / 2));
            if (usable <= minLeft + minRight) return;
            int x = PointToClient(Cursor.Position).X;
            int desired = Math.Max(minLeft, Math.Min(usable - minRight, x - 16));
            _splitRatio = (double)desired / usable;
            LayoutWorkspace();
        }

        private void LayoutLeft()
        {
            int pad = 16;
            int width = _leftCard.Width;
            int height = _leftCard.Height;
            if (width <= 0 || height <= 0) return;

            _leftTitle.Location = new Point(pad, 13);
            int innerWidth = width - 2 * pad;

            if (_builderMode && _builderUiReady)
            {
                _builderImportButton.Location = new Point(width - pad - _builderImportButton.Width, 12);
                _builderSaveButton.Location = new Point(_builderImportButton.Left - 6 - _builderSaveButton.Width, 12);
                _builderResetButton.Location = new Point(_builderSaveButton.Left - 6 - _builderResetButton.Width, 12);
                _leftTitle.SetBounds(pad, 13, Math.Max(64, _builderResetButton.Left - pad - 8), 20);
                _leftSubtitle.SetBounds(pad, 34, Math.Max(80, _builderResetButton.Left - pad - 8), 16);

                int builderActionTop = height - pad - 38;
                _builderPanel.SetBounds(pad, 55, innerWidth, Math.Max(140, builderActionTop - 55 - 12));
                _sendButton.SetBounds(pad, builderActionTop, 126, 38);
                _builderCopyButton.SetBounds(pad + 126 + 8, builderActionTop, 82, 38);
                _optionsButton.SetBounds(width - pad - _optionsButton.Width, builderActionTop, _optionsButton.Width, 38);
                int hintLeft = _builderCopyButton.Right + 8;
                int hintWidth = Math.Max(0, _optionsButton.Left - 8 - hintLeft);
                _builderActionHint.SetBounds(hintLeft, builderActionTop, hintWidth, 38);
                _builderActionHint.Visible = hintWidth >= 130;
                LayoutBuilderPanel();
                return;
            }

            int topButtonY = 12;
            _pasteButton.Location = new Point(width - pad - _pasteButton.Width, topButtonY);
            _clearButton.Location = new Point(_pasteButton.Left - 6 - _clearButton.Width, topButtonY);
            _leftTitle.SetBounds(pad, 13, Math.Max(64, _clearButton.Left - pad - 8), 20);
            _leftSubtitle.SetBounds(pad, 34, Math.Max(80, _clearButton.Left - pad - 8), 16);

            int editorTop = 55;
            int editorHeight = Math.Max(112, Math.Min(190, (int)(height * 0.29)));
            _editorCard.SetBounds(pad, editorTop, innerWidth, editorHeight);
            _curlBox.SetBounds(11, 10, Math.Max(20, innerWidth - 22), Math.Max(20, editorHeight - 20));
            _editorPlaceholder.SetBounds(14, 14, Math.Max(20, innerWidth - 38), 20);

            int previewTop = editorTop + editorHeight + 9;
            _previewCard.SetBounds(pad, previewTop, innerWidth, 50);
            LayoutPreviewContent();

            int actionTop = previewTop + 50 + 9;
            _sendButton.SetBounds(pad, actionTop, 126, 38);
            _methodCard.SetBounds(pad + 126 + 8, actionTop, 120, 38);
            _methodLabel.SetBounds(9, 0, 34, 38);
            _methodBox.SetBounds(45, 10, Math.Max(20, 120 - 54), 18);
            _methodPlaceholder.SetBounds(47, 10, 60, 18);
            _optionsButton.SetBounds(_methodCard.Right + 8, actionTop, 60, 38);

            int historyTop = actionTop + 38 + 15;
            _historyTitle.Location = new Point(pad, historyTop);
            _historyTitle.Size = new Size(86, 16);
            _historyCount.Location = new Point(pad + 62, historyTop);
            _historyCount.Size = new Size(30, 16);
            _clearHistory.Location = new Point(width - pad - 28, historyTop);
            _clearHistory.Size = new Size(28, 16);

            int listTop = historyTop + 22;
            int listHeight = Math.Max(40, height - listTop - 13);
            _historyList.SetBounds(pad, listTop, innerWidth, listHeight);
            _historyEmpty.SetBounds(pad + 2, listTop + 2, innerWidth - 4, 40);
            _historyEmpty.Visible = _history.Count == 0;
        }

        private void LayoutBuilderPanel()
        {
            if (_builderPanel == null) return;
            int width = _builderPanel.Width;
            int height = _builderPanel.Height;
            if (width <= 0 || height <= 0) return;

            _builderMethodButton.SetBounds(0, 0, 84, 36);
            _builderUrlCard.SetBounds(92, 0, Math.Max(80, width - 92), 36);
            int urlWidth = _builderUrlCard.Width;
            _builderUrlBox.SetBounds(10, 8, Math.Max(20, urlWidth - 22), 20);
            _builderUrlPlaceholder.SetBounds(13, 9, Math.Max(20, urlWidth - 30), 20);

            _builderTabParams.SetBounds(0, 48, _builderTabParams.Width, 28);
            _builderTabHeaders.SetBounds(_builderTabParams.Right + 6, 48, _builderTabHeaders.Width, 28);
            _builderTabBody.SetBounds(_builderTabHeaders.Right + 6, 48, _builderTabBody.Width, 28);

            int contentTop = 84;
            int contentHeight = Math.Max(60, height - contentTop);
            _builderParamsPanel.SetBounds(0, contentTop, width, contentHeight);
            _builderHeadersPanel.SetBounds(0, contentTop, width, contentHeight);
            _builderBodyPanel.SetBounds(0, contentTop, width, contentHeight);

            _builderParamsCard.SetBounds(0, 0, width, contentHeight);
            _builderParamsGrid.SetBounds(6, 6, Math.Max(40, width - 12), Math.Max(40, contentHeight - 12));
            _builderHeadersCard.SetBounds(0, 0, width, contentHeight);
            _builderHeadersGrid.SetBounds(6, 6, Math.Max(40, width - 12), Math.Max(40, contentHeight - 12));

            _bodyModeNone.SetBounds(0, 0, 42, 26);
            _bodyModeJson.SetBounds(48, 0, 46, 26);
            _bodyModeForm.SetBounds(100, 0, 46, 26);
            _bodyModeRaw.SetBounds(152, 0, 46, 26);
            _builderBodyHint.SetBounds(0, 30, width, 16);

            int cardTop = 50;
            _builderBodyCard.SetBounds(0, cardTop, width, Math.Max(40, contentHeight - cardTop));
            int cardWidth = _builderBodyCard.Width;
            int cardHeight = _builderBodyCard.Height;
            _builderBodyBox.SetBounds(10, 10, Math.Max(20, cardWidth - 20), Math.Max(20, cardHeight - 20));
            _builderFormGrid.SetBounds(6, 6, Math.Max(40, cardWidth - 12), Math.Max(40, cardHeight - 12));
            _builderBodyEmpty.SetBounds(20, 20, Math.Max(20, cardWidth - 40), Math.Max(20, cardHeight - 40));
        }

        private void LayoutRight()
        {
            int width = _rightCard.Width;
            int height = _rightCard.Height;
            if (width <= 0 || height <= 0) return;
            Rectangle full = new Rectangle(1, 1, Math.Max(0, width - 2), Math.Max(0, height - 2));
            _emptyPanel.Bounds = full;
            _loadingPanel.Bounds = full;
            _fatalPanel.Bounds = full;
            _resultPanel.Bounds = full;

            LayoutEmpty();
            LayoutLoading();
            LayoutFatal();
            if (_resultPanel.Visible) LayoutResult();
        }

        private void LayoutEmpty()
        {
            int width = _emptyPanel.Width;
            int height = _emptyPanel.Height;
            if (width <= 0 || height <= 0) return;

            int groupWidth = Math.Min(470, Math.Max(280, width - 80));
            int left = (width - groupWidth) / 2;
            int totalHeight = 365;
            int top = Math.Max(26, (height - totalHeight) / 2);
            bool showArt = height > 560;

            _emptyArt.Visible = showArt;
            if (showArt)
            {
                int artWidth = 340;
                _emptyArt.SetBounds((width - artWidth) / 2, top, artWidth, 82);
                _emptyPostMethod.SetBounds(16, 10, 44, 20);
                _emptyPostPath.SetBounds(66, 10, artWidth - 110, 20);
                _emptyPostStatus.SetBounds(artWidth - 46, 10, 30, 20);
                _emptyGetMethod.SetBounds(16, 46, 44, 20);
                _emptyGetPath.SetBounds(66, 46, artWidth - 110, 20);
                _emptyGetStatus.SetBounds(artWidth - 46, 46, 30, 20);
                top += 104;
            }

            _emptyEyebrow.SetBounds(left, top, groupWidth, 16);
            _emptyEyebrow.TextAlign = ContentAlignment.MiddleCenter;
            top += 20;
            _emptyTitle.SetBounds(left, top, groupWidth, 36);
            top += 44;
            _emptyDescription.SetBounds(left, top, groupWidth, 55);
            top += 62;
            _emptySteps.SetBounds(left, top, groupWidth, 42);
            top += 54;
            _sampleButton.SetBounds((width - _sampleButton.Width) / 2, top, _sampleButton.Width, 30);
            top += 48;
            _emptyPrivacy.SetBounds(left, top, groupWidth, 18);
        }

        private void LayoutLoading()
        {
            int width = _loadingPanel.Width;
            int height = _loadingPanel.Height;
            int center = height / 2;
            _loadingTitle.SetBounds(30, center - 80, Math.Max(100, width - 60), 28);
            _loadingTitle.TextAlign = ContentAlignment.MiddleCenter;
            _loadingUrl.SetBounds(50, center - 42, Math.Max(80, width - 100), 22);
            _loadingMeta.SetBounds(50, center - 14, Math.Max(80, width - 100), 20);
            _cancelButton.SetBounds((width - _cancelButton.Width) / 2, center + 26, _cancelButton.Width, _cancelButton.Height);
        }

        private void LayoutFatal()
        {
            int width = _fatalPanel.Width;
            int height = _fatalPanel.Height;
            int top = Math.Max(24, height / 2 - 170);
            _fatalMark.SetBounds((width - _fatalMark.Width) / 2, top, _fatalMark.Width, _fatalMark.Height);
            top += _fatalMark.Height + 14;
            _fatalTitle.SetBounds(30, top, Math.Max(100, width - 60), 28);
            top += 34;
            _fatalMessage.SetBounds(40, top, Math.Max(100, width - 80), 42);
            top += 48;
            if (_fatalDetail.Visible)
            {
                _fatalDetail.SetBounds(40, top, Math.Max(100, width - 80), 92);
                top += 104;
            }
            int buttonWidth = _retryButton.Width + 10 + _backButton.Width;
            _retryButton.Location = new Point((width - buttonWidth) / 2, top);
            _backButton.Location = new Point(_retryButton.Right + 10, top + 2);
        }

        private void LayoutResult()
        {
            if (!_resultPanel.Visible) return;
            int width = _resultPanel.Width;
            int height = _resultPanel.Height;
            if (width <= 0 || height <= 0) return;

            const int metaHeight = 82;
            _metaPanel.SetBounds(0, 0, width, metaHeight);
            _metaDivider.SetBounds(0, metaHeight - 1, width, 1);

            int statusWidth = _statusPill.Width;
            _statusPill.Location = new Point(18, 14);
            _statusText.SetBounds(18 + statusWidth + 8, 14, 120, 22);
            _metaFacts.SetBounds(196, 14, Math.Max(80, width - 214), 22);

            int methodWidth = _methodPill.Width;
            _methodPill.Location = new Point(18, 48);
            _urlLabel.SetBounds(18 + methodWidth + 8, 47, Math.Max(40, width - (18 + methodWidth + 8) - 62), 23);
            _copyUrlButton.Location = new Point(width - 18 - _copyUrlButton.Width, 47);

            int top = metaHeight;
            if (_alertPanel.Visible)
            {
                _alertPanel.SetBounds(16, top + 8, width - 32, 46);
                _alertMark.SetBounds(10, 8, 16, 18);
                int textLeft = 31;
                _alertLogButton.SetBounds(_alertPanel.Width - 10 - _alertLogButton.Width, 12, _alertLogButton.Width, 22);
                int textWidth = Math.Max(60, _alertLogButton.Left - textLeft - 8);
                _alertTitle.SetBounds(textLeft, 6, textWidth, 16);
                _alertText.SetBounds(textLeft, 23, textWidth, 15);
                top += 8 + 46 + 8;
            }

            _tabsPanel.SetBounds(0, top, width, 38);
            _tabsDivider.SetBounds(0, top + 37, width, 1);
            int tabX = 18;
            _bodyTab.Location = new Point(tabX, 0);
            tabX += _bodyTab.Width + 18;
            _headersTab.Location = new Point(tabX, 0);
            tabX += _headersTab.Width + 18;
            _logTab.Location = new Point(tabX, 0);
            top += 38;

            _bodyPanel.SetBounds(0, top, width, Math.Max(0, height - top));
            _headersPanel.SetBounds(0, top, width, Math.Max(0, height - top));
            _logPanel.SetBounds(0, top, width, Math.Max(0, height - top));
            LayoutResultPanels();
        }

        private void LayoutResultPanels()
        {
            LayoutBodyPanel();
            LayoutHeadersPanel();
            LayoutLogPanel();
        }

        private void LayoutBodyPanel()
        {
            if (_bodyPanel == null) return;
            int width = _bodyPanel.Width;
            int height = _bodyPanel.Height;
            if (width <= 0 || height <= 0) return;

            int x = 18;
            _bodyKindLabel.SetBounds(x, 8, 60, 18);
            int kindWidth = TextRenderer.MeasureText(_bodyKindLabel.Text, _bodyKindLabel.Font,
                Size.Empty, TextFormatFlags.NoPadding).Width + 8;
            _bodyKindLabel.Width = Math.Max(28, kindWidth);
            _bodySizeLabel.SetBounds(_bodyKindLabel.Right + 7, 8, 70, 18);
            _bodyTruncatedLabel.SetBounds(_bodySizeLabel.Right + 5, 8, 48, 18);

            int buttonY = 5;
            int buttonRight = width - 18;
            _saveButton.SetBounds(buttonRight - _saveButton.Width, buttonY, _saveButton.Width, 24);
            _copyBodyButton.SetBounds(_saveButton.Left - 2 - _copyBodyButton.Width, buttonY, _copyBodyButton.Width, 24);
            _wrapButton.SetBounds(_copyBodyButton.Left - 2 - _wrapButton.Width, buttonY, _wrapButton.Width, 24);
            _prettyButton.SetBounds(_wrapButton.Left - 2 - _prettyButton.Width, buttonY, _prettyButton.Width, 24);

            _bodyCard.SetBounds(18, 36, Math.Max(20, width - 36), Math.Max(20, height - 50));
            int cardWidth = _bodyCard.Width;
            int cardHeight = _bodyCard.Height;
            _bodyBox.SetBounds(10, 10, Math.Max(20, cardWidth - 20), Math.Max(20, cardHeight - 20));
            _imagePanel.SetBounds(10, 10, Math.Max(20, cardWidth - 20), Math.Max(20, cardHeight - 20));
            _imageBox.SetBounds(0, 0, _imagePanel.Width, Math.Max(20, _imagePanel.Height - 30));
            _imageInfo.SetBounds(0, Math.Max(0, _imagePanel.Height - 26), _imagePanel.Width, 20);
            _bodyEmptyLabel.SetBounds(20, 20, Math.Max(20, cardWidth - 40), Math.Max(20, cardHeight - 40));
        }

        private void LayoutHeadersPanel()
        {
            if (_headersPanel == null) return;
            int width = _headersPanel.Width;
            int height = _headersPanel.Height;
            if (width <= 0 || height <= 0) return;

            _headersCaption.SetBounds(18, 8, 120, 18);
            _copyHeadersButton.SetBounds(width - 18 - _copyHeadersButton.Width, 5, _copyHeadersButton.Width, 24);

            int top = 32;
            if (_redirectLabel.Visible)
            {
                _redirectLabel.SetBounds(18, top, Math.Max(40, width - 36), 18);
                top += 22;
            }
            int timingHeight = _timingLabel.Visible ? 22 : 0;
            _headersCard.SetBounds(18, top, Math.Max(20, width - 36), Math.Max(20, height - top - 12 - timingHeight));
            _headersBox.SetBounds(10, 10, Math.Max(20, _headersCard.Width - 20), Math.Max(20, _headersCard.Height - 20));
            _timingLabel.SetBounds(18, _headersCard.Bottom + 4, Math.Max(40, width - 36), 18);
        }

        private void LayoutLogPanel()
        {
            if (_logPanel == null) return;
            int width = _logPanel.Width;
            int height = _logPanel.Height;
            if (width <= 0 || height <= 0) return;
            _logCaption.SetBounds(18, 8, 120, 18);
            _copyLogButton.SetBounds(width - 18 - _copyLogButton.Width, 5, _copyLogButton.Width, 24);
            _logCard.SetBounds(18, 32, Math.Max(20, width - 36), Math.Max(20, height - 46));
            _logBox.SetBounds(10, 10, Math.Max(20, _logCard.Width - 20), Math.Max(20, _logCard.Height - 20));
        }

        private void LayoutFooter()
        {
            if (_footer == null) return;
            int width = ClientSize.Width;
            _footerDot.Location = new Point(20, 5);
            int leftWidth = TextRenderer.MeasureText(_footerLeft.Text, _footerLeft.Font,
                Size.Empty, TextFormatFlags.NoPadding).Width + 4;
            _footerLeft.SetBounds(30, 5, Math.Min(leftWidth, Math.Max(120, width - 460)), 18);
            _footerRight.SetBounds(width - 18 - 430, 5, 430, 18);
        }

        private void LayoutToast()
        {
            if (_toast == null) return;
            _toast.Location = new Point((ClientSize.Width - _toast.Width) / 2,
                ClientSize.Height - 28 - 12 - _toast.Height);
        }

        protected override void SetVisibleCore(bool value)
        {
            if (value && _startHidden)
            {
                _startHidden = false;
                if (!IsHandleCreated) CreateHandle();
                base.SetVisibleCore(false);
                return;
            }
            base.SetVisibleCore(value);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.EnableLightTitleBar(Handle);
            _showMessage = NativeMethods.RegisterWindowMessage(Program.ShowMessageName);
            _hotkeyRegistered = NativeMethods.RegisterHotKey(Handle, HotkeyId,
                NativeMethods.ModControl | NativeMethods.ModAlt, (int)Keys.C);
            UpdateMemoryLabel();
            _footerRight.Text = BuildFooterRight();
            LayoutAll();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (_hotkeyRegistered)
            {
                NativeMethods.UnregisterHotKey(Handle, HotkeyId);
                _hotkeyRegistered = false;
            }
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WmHotkey && m.WParam.ToInt64() == HotkeyId)
            {
                ShowFromTray();
                return;
            }
            if (_showMessage != 0 && (uint)m.Msg == _showMessage)
            {
                ShowFromTray();
                return;
            }
            base.WndProc(ref m);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!_libraryMode && _builderMode && keyData == (Keys.Control | Keys.S))
            {
                SaveBuilderRequest();
                return true;
            }
            if (keyData == (Keys.Control | Keys.O))
            {
                ShowRequestLibrary();
                return true;
            }
            if (keyData == (Keys.Control | Keys.Enter))
            {
                if (!_running && !_libraryMode) BeginSend();
                return true;
            }
            if (keyData == Keys.Escape && _running)
            {
                CancelCurrent();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutAll();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LayoutAll();
            if (!_startHidden)
            {
                _curlBox.Focus();
                if (_curlBox.TextLength == 0)
                    _editorPlaceholder.Visible = true;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_reallyExit && e.CloseReason != CloseReason.WindowsShutDown &&
                e.CloseReason != CloseReason.TaskManagerClosing)
            {
                e.Cancel = true;
                HideToTray(true);
                return;
            }
            SaveSettings();
            SaveHistory();
            if (_cts != null)
            {
                try { _cts.Cancel(); }
                catch { }
            }
            CurlRunner.CancelActive();
            if (_trayIcon != null) _trayIcon.Visible = false;
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            StopTimers();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            if (_trayMenu != null) _trayMenu.Dispose();
            if (_optionsMenu != null) _optionsMenu.Dispose();
            if (_settingsMenu != null) _settingsMenu.Dispose();
            if (_builderMethodMenu != null)
            {
                _builderMethodMenu.Dispose();
                _builderMethodMenu = null;
            }
            DisposeImage();
            base.OnFormClosed(e);
        }

        private void StopTimers()
        {
            WinTimer[] timers = new[] { _parseTimer, _draftTimer, _elapsedTimer, _memoryTimer, _toastTimer };
            foreach (WinTimer timer in timers)
            {
                if (timer == null) continue;
                timer.Stop();
                timer.Dispose();
            }
        }
    }

    internal static class JsonDisplay
    {
        public static bool TryPretty(string raw, string contentType, out string pretty)
        {
            pretty = null;
            if (string.IsNullOrEmpty(raw) || raw.Length < 2 || raw.Length > 2 * 1024 * 1024) return false;
            string trimmed = raw.Trim();
            bool looksJson = (contentType ?? string.Empty).IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             trimmed.StartsWith("{", StringComparison.Ordinal) ||
                             trimmed.StartsWith("[", StringComparison.Ordinal);
            if (!looksJson) return false;

            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 4 * 1024 * 1024;
                serializer.Deserialize<object>(trimmed);
            }
            catch
            {
                return false;
            }

            List<string> tokens = Tokenize(trimmed);
            if (tokens.Count == 0) return false;
            StringBuilder builder = new StringBuilder(raw.Length + 128);
            int indent = 0;
            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];
                string previous = i > 0 ? tokens[i - 1] : null;
                string next = i + 1 < tokens.Count ? tokens[i + 1] : null;
                if (token == "{" || token == "[")
                {
                    builder.Append(token);
                    bool empty = next == "}" || next == "]";
                    if (!empty)
                    {
                        indent++;
                        NewLine(builder, indent);
                    }
                }
                else if (token == "}" || token == "]")
                {
                    bool empty = previous == "{" || previous == "[";
                    if (!empty)
                    {
                        indent = Math.Max(0, indent - 1);
                        NewLine(builder, indent);
                    }
                    builder.Append(token);
                }
                else if (token == ",")
                {
                    builder.Append(token);
                    NewLine(builder, indent);
                }
                else if (token == ":")
                {
                    builder.Append(": ");
                }
                else
                {
                    builder.Append(DecodeUnicodeEscapes(token));
                }
            }
            pretty = builder.ToString();
            return true;
        }

        private static void NewLine(StringBuilder builder, int indent)
        {
            builder.Append('\n');
            builder.Append(' ', indent * 2);
        }

        private static string DecodeUnicodeEscapes(string token)
        {
            if (token == null || token.Length < 6 || token.IndexOf("\\u", StringComparison.Ordinal) < 0)
                return token;

            StringBuilder builder = new StringBuilder(token.Length);
            int i = 0;
            while (i < token.Length)
            {
                if (token[i] == '\\' && i + 5 < token.Length && token[i + 1] == 'u' &&
                    IsHex(token[i + 2]) && IsHex(token[i + 3]) && IsHex(token[i + 4]) && IsHex(token[i + 5]))
                {
                    int code = Convert.ToInt32(token.Substring(i + 2, 4), 16);
                    if (code >= 0xD800 && code <= 0xDBFF && i + 11 < token.Length &&
                        token[i + 6] == '\\' && token[i + 7] == 'u' &&
                        IsHex(token[i + 8]) && IsHex(token[i + 9]) && IsHex(token[i + 10]) && IsHex(token[i + 11]))
                    {
                        int low = Convert.ToInt32(token.Substring(i + 8, 4), 16);
                        if (low >= 0xDC00 && low <= 0xDFFF)
                        {
                            builder.Append(char.ConvertFromUtf32(0x10000 + ((code - 0xD800) << 10) + (low - 0xDC00)));
                            i += 12;
                            continue;
                        }
                    }
                    if (code >= 0x80 && code != 0x2028 && code != 0x2029 && (code < 0xD800 || code > 0xDFFF))
                    {
                        builder.Append((char)code);
                        i += 6;
                        continue;
                    }
                }
                builder.Append(token[i]);
                i++;
            }
            return builder.ToString();
        }

        private static bool IsHex(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private static List<string> Tokenize(string text)
        {
            List<string> tokens = new List<string>();
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }
                if (c == '"')
                {
                    int start = i;
                    i++;
                    bool escaped = false;
                    while (i < text.Length)
                    {
                        char current = text[i];
                        if (escaped)
                        {
                            escaped = false;
                            i++;
                            continue;
                        }
                        if (current == '\\')
                        {
                            escaped = true;
                            i++;
                            continue;
                        }
                        if (current == '"')
                        {
                            i++;
                            break;
                        }
                        i++;
                    }
                    tokens.Add(text.Substring(start, i - start));
                    continue;
                }
                if ("{}[],:".IndexOf(c) >= 0)
                {
                    tokens.Add(c.ToString());
                    i++;
                    continue;
                }
                int valueStart = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && "{}[],:".IndexOf(text[i]) < 0)
                    i++;
                tokens.Add(text.Substring(valueStart, i - valueStart));
            }
            return tokens;
        }
    }
}

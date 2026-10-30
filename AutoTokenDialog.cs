using System;
using System.Drawing;
using System.Windows.Forms;

namespace CurlBox
{
    internal sealed class AutoTokenDialog : LightDialog
    {
        private readonly TextBox _urlBox;
        private readonly TextBox _usernameBox;
        private readonly TextBox _passwordBox;
        private readonly TextBox _codeBox;
        private readonly Label _tokenLabel;
        private readonly Label _error;
        private readonly RoundButton _clearTokenButton;
        private string _token;

        public string LoginUrl { get; private set; }
        public string Username { get; private set; }
        public string Password { get; private set; }
        public string AuthCode { get; private set; }
        public string Token { get; private set; }

        public AutoTokenDialog(
            string loginUrl,
            string username,
            string password,
            string authCode,
            string token)
            : base("自动 Token 设置", new Size(560, 392))
        {
            _token = token ?? string.Empty;

            Controls.Add(DialogUi.Label("自动 Token 设置", Theme.UiBold(13f), Theme.Text, 22, 14, 320, 26));
            Controls.Add(DialogUi.Label(
                "在请求库目录上开启开关后生效；仅对路径以 /backend/ 开头的请求自动添加 Authorization，返回 401 时自动登录并重试一次。",
                Theme.Ui(8f), Theme.Faint, 22, 42, 516, 36));

            _urlBox = CreateInput("登录接口 URL", 22, 110, 516, false, "https://api.example.com/login");
            _usernameBox = CreateInput("用户名", 22, 174, 248, false, "admin");
            _passwordBox = CreateInput("密码", 292, 174, 246, true, string.Empty);
            _codeBox = CreateInput("固定验证码 / code", 22, 238, 248, false, "111111");

            Controls.Add(DialogUi.Label("当前 Token", Theme.Ui(8.5f), Theme.TextSoft, 292, 220, 246, 18));
            _tokenLabel = DialogUi.Label(string.Empty, Theme.Ui(8f), Theme.Faint, 292, 238, 150, 30);
            _tokenLabel.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(_tokenLabel);

            _clearTokenButton = DialogUi.SmallButton("清除 Token", 92);
            _clearTokenButton.SetBounds(444, 238, 94, 30);
            _clearTokenButton.Click += delegate
            {
                _token = string.Empty;
                UpdateTokenLabel();
            };
            Controls.Add(_clearTokenButton);

            Controls.Add(DialogUi.Label(
                "登录接口需返回 { data: { token: \"...\" } }。以上配置和 Token 仅保存在本机。",
                Theme.Ui(7.8f), Theme.Faint, 22, 284, 516, 28));

            _error = DialogUi.Label(string.Empty, Theme.Ui(8f), Theme.Red, 22, 316, 516, 18);
            Controls.Add(_error);

            RoundButton cancel = DialogUi.Button("取消", 70, false);
            cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(ClientSize.Width - 22 - cancel.Width, 346, cancel.Width, 32);
            Controls.Add(cancel);

            RoundButton save = DialogUi.Button("保存", 82, true);
            save.SetBounds(cancel.Left - 8 - save.Width, 346, save.Width, 32);
            save.Click += delegate { Commit(); };
            Controls.Add(save);

            AcceptButton = save;
            CancelButton = cancel;

            _urlBox.Text = loginUrl ?? string.Empty;
            _usernameBox.Text = username ?? string.Empty;
            _passwordBox.Text = password ?? string.Empty;
            _codeBox.Text = authCode ?? string.Empty;
            UpdateTokenLabel();

            Shown += delegate
            {
                _urlBox.Focus();
                _urlBox.SelectAll();
            };
        }

        private TextBox CreateInput(string label, int x, int y, int width, bool password, string cue)
        {
            Controls.Add(DialogUi.Label(label, Theme.Ui(8.5f), Theme.TextSoft, x, y - 18, width, 18));

            CardPanel card = new CardPanel();
            card.CornerRadius = 8;
            card.FillColor = Theme.Input;
            card.BorderColor = Theme.Border;
            card.SetBounds(x, y, width, 36);
            Controls.Add(card);

            TextBox box = new TextBox();
            box.BorderStyle = BorderStyle.None;
            box.BackColor = Theme.Input;
            box.ForeColor = Theme.Text;
            box.Font = Theme.Ui(9.2f);
            box.SetBounds(10, 9, Math.Max(30, width - 20), 19);
            if (password) box.UseSystemPasswordChar = true;
            if (!string.IsNullOrEmpty(cue))
                box.HandleCreated += delegate { NativeMethods.SetCueBanner(box, cue); };
            box.Enter += delegate { card.BorderColor = Color.FromArgb(214, 154, 94); card.Invalidate(); };
            box.Leave += delegate { card.BorderColor = Theme.Border; card.Invalidate(); };
            card.Controls.Add(box);
            return box;
        }

        private void UpdateTokenLabel()
        {
            bool hasToken = !string.IsNullOrEmpty(_token);
            _tokenLabel.Text = hasToken ? "已保存 · " + _token.Length + " 字符" : "未获取";
            _tokenLabel.ForeColor = hasToken ? Theme.Mint : Theme.Faint;
            _clearTokenButton.Enabled = hasToken;
        }

        private void Commit()
        {
            string url = (_urlBox.Text ?? string.Empty).Trim();
            Uri uri;
            if (url.Length == 0 ||
                !Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                string.IsNullOrEmpty(uri.Host))
            {
                _error.Text = "请填写完整的登录接口 URL（http / https）。";
                _urlBox.Focus();
                return;
            }

            LoginUrl = url;
            Username = (_usernameBox.Text ?? string.Empty).Trim();
            Password = _passwordBox.Text ?? string.Empty;
            AuthCode = (_codeBox.Text ?? string.Empty).Trim();
            Token = _token;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

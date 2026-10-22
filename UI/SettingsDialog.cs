using System.ComponentModel;
using System.Diagnostics;
using ModSorterAgent.Agent;
using ModSorterAgent.Discovery;
using ModSorterAgent.Services;
using ModSorterAgent.UI.Controls;

namespace ModSorterAgent.UI;

/// <summary>
/// 设置对话框：左侧分组导航 + 右侧卡片式表单。
/// 路径留空跟随自动检测；Agent 留空则始终使用本地代码核查。
/// 所有内容保存到 exe 旁的 AppSettings.json。
/// </summary>
internal sealed class SettingsDialog : Form
{
    private sealed record PathSpec(
        string Title,
        string Hint,
        Func<AppSettings, string> GetValue,
        Action<AppSettings, string> SetValue,
        Func<string?>? AutoDetect,
        Func<string> Effective,
        bool IsFile);

    private readonly List<PathRow> _rows = new();

    private readonly SegmentedControl _providerInput = new("仅本地核查", "OpenAI 兼容", "Player2");
    private readonly SettingsField _baseUrlInput = new();
    private readonly SettingsField _modelInput = new();
    private readonly SettingsField _apiKeyInput = new();
    private readonly SettingsField _patchDirInput = new();
    private readonly ToggleSwitch _verifyGabs = new();
    private readonly ToggleSwitch _includeWorkshop = new();

    private readonly SettingsBadge _providerBadge = new();
    private readonly Label _providerHint = new();

    /// <summary>Agent 字段行。Player2 只需授权那一条，其余行隐藏。</summary>
    private AgentFieldRow? _baseUrlRow;
    private AgentFieldRow? _modelRow;
    private AgentFieldRow? _apiKeyRow;

    private readonly ModernButton _player2AuthButton = new("网页鉴权", ButtonVariant.Secondary);

    private readonly List<SettingsNavItem> _navItems = new();
    private readonly List<Panel> _pages = new();
    private readonly List<Action> _layoutActions = new();
    private readonly Panel _contentHost = new();
    private bool _dpiScaled;

    private readonly string _existingProtectedKey;

    private static readonly Color CardBg = UiTheme.CardBg;

    public SettingsDialog()
    {
        var settings = ModListResolver.PathOptions.Settings;
        _existingProtectedKey = settings.AgentApiKeyProtected;

        Text = "设置";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(944, 704);
        BackColor = UiTheme.AppBg;
        ForeColor = UiTheme.TextPrimary;
        Font = UiTheme.BodyFont;
        KeyPreview = true;
        DoubleBuffered = true;
        Padding = new Padding(1);

        BuildHeader();
        BuildNavigation();
        BuildFooter();
        BuildPages(settings);

        Controls.Add(_contentHost);
        Controls.Add(_navHost);
        Controls.Add(_footer);
        Controls.Add(_header);

        ShowPage(0, updateNav: true);
        UpdateProviderHint();

        // 布局完成后再触发 DPI 自动缩放，缩放结束后用缩放后的间距重排一次。
        AutoScaleDimensions = new SizeF(96F, 96F);
        _dpiScaled = true;
        foreach (var layout in _layoutActions)
            layout();
    }

    private int Px(int value) => _dpiScaled ? UiTheme.Scale(this, value) : value;

    // ───────────────────────────── 结构 ─────────────────────────────

    private readonly Panel _header = new();
    private readonly Panel _navHost = new();
    private readonly Panel _footer = new();

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return cp;
        }
    }

    private void BuildHeader()
    {
        _header.Dock = DockStyle.Top;
        _header.Height = 64;
        _header.BackColor = UiTheme.SurfaceBg;

        var titleLabel = new Label
        {
            Text = "设置",
            AutoSize = true,
            Font = UiTheme.DialogTitleFont,
            ForeColor = UiTheme.TextPrimary,
            BackColor = UiTheme.SurfaceBg,
            Location = new Point(64, 11)
        };

        var subtitleLabel = new Label
        {
            Text = "路径、模型与核查选项 · 修改后立即生效",
            AutoSize = true,
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.TextMuted,
            BackColor = UiTheme.SurfaceBg,
            Location = new Point(66, 38)
        };

        var close = new WindowChromeButton(WindowChromeButton.WindowControlKind.Close)
        {
            BackColor = UiTheme.SurfaceBg,
            Size = new Size(42, 30)
        };
        void PlaceClose() =>
            close.Location = new Point(Math.Max(0, _header.ClientSize.Width - Px(58)), Px(17));
        _header.Resize += (_, _) => PlaceClose();
        _layoutActions.Add(PlaceClose);
        PlaceClose();
        close.Click += (_, _) =>
        {
            Save();
        };

        _header.Controls.Add(titleLabel);
        _header.Controls.Add(subtitleLabel);
        _header.Controls.Add(close);
        _header.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var markRect = new Rectangle(UiTheme.Scale(_header, 22), UiTheme.Scale(_header, 18),
                UiTheme.Scale(_header, 30), UiTheme.Scale(_header, 30));
            using (var path = UiTheme.RoundedRect(markRect, UiTheme.Scale(_header, 9)))
            using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                       markRect, UiTheme.Accent, Color.FromArgb(96, 92, 220),
                       System.Drawing.Drawing2D.LinearGradientMode.ForwardDiagonal))
            {
                g.FillPath(brush, path);
            }

            using (var pen = new Pen(Color.FromArgb(235, 245, 255), 1.5f))
            {
                var cx = markRect.X + markRect.Width / 2f;
                var cy = markRect.Y + markRect.Height / 2f;
                var half = UiTheme.Scale(_header, 6);
                var knob = UiTheme.Scale(_header, 2);
                for (var i = -1; i <= 1; i++)
                {
                    var y = cy + i * UiTheme.Scale(_header, 4);
                    g.DrawLine(pen, cx - half, y, cx + half, y);
                    var knobX = cx + (i == -1 ? half - UiTheme.Scale(_header, 3) : i == 0 ? -half + UiTheme.Scale(_header, 4) : half - UiTheme.Scale(_header, 7));
                    g.FillEllipse(Brushes.White, knobX - knob, y - knob, knob * 2, knob * 2);
                }
            }

            using var line = new Pen(UiTheme.BorderSubtle, 1f);
            g.DrawLine(line, 0, _header.Height - 1, _header.Width, _header.Height - 1);
        };

        var drag = new MouseEventHandler((_, e) =>
        {
            if (e.Button != MouseButtons.Left)
                return;

            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, NativeMethods.WM_NCLBUTTONDOWN, NativeMethods.HTCAPTION, 0);
        });
        _header.MouseDown += drag;
        titleLabel.MouseDown += drag;
        subtitleLabel.MouseDown += drag;
    }

    private void BuildNavigation()
    {
        _navHost.Dock = DockStyle.Left;
        _navHost.Width = 216;
        _navHost.BackColor = UiTheme.NavBg;

        var items = new[]
        {
            (SettingsNavIcon.Paths, "路径与扫描", "游戏 / 工坊 / 配置"),
            (SettingsNavIcon.Agent, "Agent 模型", "接口与补丁输出"),
            (SettingsNavIcon.Options, "分析选项", "实机核验 / 订阅池")
        };

        for (var i = 0; i < items.Length; i++)
        {
            var index = i;
            var item = new SettingsNavItem(items[i].Item1, items[i].Item2, items[i].Item3)
            {
                Location = new Point(12, 22 + i * 66),
                Width = 192
            };
            item.Click += (_, _) => ShowPage(index, updateNav: true);
            _navItems.Add(item);
            _navHost.Controls.Add(item);
        }

        var navFooter = new Label
        {
            Text = "所有路径与密钥只保存在\n本机 AppSettings.json",
            AutoSize = false,
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.TextMuted,
            BackColor = UiTheme.NavBg,
            TextAlign = ContentAlignment.BottomLeft,
            Location = new Point(24, _navHost.Height - 64),
            Size = new Size(170, 44),
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom
        };
        _navHost.Controls.Add(navFooter);

        _navHost.Paint += (_, e) =>
        {
            using var pen = new Pen(UiTheme.BorderSubtle, 1f);
            e.Graphics.DrawLine(pen, _navHost.Width - 1, 0, _navHost.Width - 1, _navHost.Height);
        };

        _contentHost.Dock = DockStyle.Fill;
        _contentHost.BackColor = UiTheme.AppBg;
    }

    private void BuildFooter()
    {
        _footer.Dock = DockStyle.Bottom;
        _footer.Height = 66;
        _footer.BackColor = UiTheme.SurfaceBg;

        var storeLabel = new Label
        {
            Text = "设置仅保存在本机，不会上传",
            AutoSize = false,
            AutoEllipsis = true,
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.TextMuted,
            BackColor = UiTheme.SurfaceBg,
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(26, 10),
            Size = new Size(540, 20)
        };

        var cancel = new ModernButton("取消", ButtonVariant.Ghost)
        {
            Height = 32,
            Width = 88,
            BackColor = UiTheme.SurfaceBg,
            Font = UiTheme.BodyFont
        };
        var save = new ModernButton("保存设置", ButtonVariant.Primary)
        {
            Height = 32,
            Width = 128,
            BackColor = UiTheme.SurfaceBg
        };

        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        save.Click += (_, _) => Save();

        void PlaceButtons()
        {
            save.Location = new Point(Math.Max(0, _footer.Width - Px(28) - save.Width), Px(17));
            cancel.Location = new Point(Math.Max(0, save.Left - Px(12) - cancel.Width), Px(17));
            storeLabel.Width = Math.Max(Px(120), cancel.Left - Px(40));
        }

        _footer.Resize += (_, _) => PlaceButtons();
        _layoutActions.Add(PlaceButtons);
        _footer.Controls.Add(storeLabel);
        _footer.Controls.Add(cancel);
        _footer.Controls.Add(save);
        PlaceButtons();

        var hint = new Label
        {
            Text = "Ctrl+S 保存 · Esc 取消",
            AutoSize = true,
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.TextMuted,
            BackColor = UiTheme.SurfaceBg,
            Location = new Point(26, 37)
        };
        _footer.Controls.Add(hint);
        hint.BringToFront();

        _footer.Paint += (_, e) =>
        {
            using var pen = new Pen(UiTheme.BorderSubtle, 1f);
            e.Graphics.DrawLine(pen, 0, 0, _footer.Width, 0);
        };
    }

    private void BuildPages(AppSettings settings)
    {
        var pathsPage = CreatePage("游戏与模组路径", "留空表示跟随自动检测；自定义路径会立即用于扫描与配置写入。");
        var agentPage = CreatePage("Agent 代码核查", "本地核查始终可用；接入模型后可获得更深入的隐患分析与可编译的兼容补丁。");
        var optionsPage = CreatePage("分析选项", "控制分析时的实机核验与扫描范围，不影响已生成的报告。");

        BuildPathsPage(pathsPage, settings);
        BuildAgentPage(agentPage, settings);
        BuildOptionsPage(optionsPage, settings);

        foreach (var page in new[] { pathsPage, agentPage, optionsPage })
        {
            page.Dock = DockStyle.Fill;
            page.Visible = false;
            _pages.Add(page);
            _contentHost.Controls.Add(page);
        }
    }

    private Panel CreatePage(string title, string subtitle)
    {
        var page = new Panel { BackColor = UiTheme.AppBg };

        var titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = UiTheme.PageTitleFont,
            ForeColor = UiTheme.TextPrimary,
            BackColor = UiTheme.AppBg,
            Location = new Point(26, 20)
        };

        var subtitleLabel = new Label
        {
            Text = subtitle,
            AutoSize = false,
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.TextSecondary,
            BackColor = UiTheme.AppBg,
            Location = new Point(27, 54),
            Size = new Size(520, 18)
        };

        page.Controls.Add(titleLabel);
        page.Controls.Add(subtitleLabel);
        return page;
    }

    // ───────────────────────────── 路径页 ─────────────────────────────

    private void BuildPathsPage(Panel page, AppSettings settings)
    {
        var specs = new[]
        {
            new PathSpec(
                "RimWorld 游戏安装目录",
                "包含 RimWorldWin64.exe / Data / Mods（Steam 库 steamapps\\common\\RimWorld）",
                s => s.GameInstallDir,
                (s, v) => s.GameInstallDir = v,
                PathAutoDetector.DetectGameInstallDir,
                () => ModListResolver.PathOptions.RimWorldInstallDir,
                IsFile: false),
            new PathSpec(
                "Steam 创意工坊内容目录",
                "steamapps\\workshop\\content\\294100（本地 mod 与订阅池扫描根目录）",
                s => s.WorkshopContentDir,
                (s, v) => s.WorkshopContentDir = v,
                PathAutoDetector.DetectWorkshopContentDir,
                () => ModListResolver.PathOptions.WorkshopDir,
                IsFile: false),
            new PathSpec(
                "本地模组工作区目录",
                "存放若干未打包 mod 文件夹的工作区（留空 = 不扫描）",
                s => s.LocalModWorkspaceDir,
                (s, v) => s.LocalModWorkspaceDir = v,
                null,
                () => ModListResolver.PathOptions.WorkspaceDir,
                IsFile: false),
            new PathSpec(
                "ModsConfig.xml 路径",
                "游戏模组加载配置（留空 = 自动检测 LocalLow\\Ludeon Studios\\…\\Config）",
                s => s.ModsConfigPath,
                (s, v) => s.ModsConfigPath = v,
                PathAutoDetector.DetectModsConfigPath,
                () => ModListResolver.DefaultModsConfigPath,
                IsFile: true)
        };

        var card = new RoundedCard
        {
            Location = new Point(26, 86),
            BackColor = CardBg
        };

        for (var i = 0; i < specs.Length; i++)
        {
            var row = new PathRow(this, specs[i], showSeparator: i < specs.Length - 1)
            {
                Location = new Point(1, 1 + i * 114)
            };
            _rows.Add(row);
            card.Controls.Add(row);
        }

        var btnDetectAll = new ModernButton("全部自动检测", ButtonVariant.Secondary)
        {
            Height = 30,
            Width = 126,
            BackColor = UiTheme.AppBg
        };
        var btnReset = new ModernButton("清空覆盖", ButtonVariant.Ghost)
        {
            Height = 30,
            Width = 96,
            BackColor = UiTheme.AppBg
        };

        btnDetectAll.Click += (_, _) => DetectAll();
        btnReset.Click += (_, _) =>
        {
            foreach (var row in _rows)
                row.Field.Text = string.Empty;
        };

        void LayoutPage()
        {
            card.Width = Math.Max(Px(300), page.ClientSize.Width - Px(52));
            card.Height = Px(458);
            foreach (var row in card.Controls.OfType<PathRow>())
                row.Width = card.Width - Px(2);

            btnDetectAll.Location = new Point(
                Math.Max(0, page.ClientSize.Width - Px(26) - btnReset.Width - Px(8) - btnDetectAll.Width), Px(24));
            btnReset.Location = new Point(
                Math.Max(0, page.ClientSize.Width - Px(26) - btnReset.Width), Px(24));
        }

        page.Controls.Add(card);
        page.Controls.Add(btnDetectAll);
        page.Controls.Add(btnReset);
        page.Resize += (_, _) => LayoutPage();
        _layoutActions.Add(LayoutPage);
        LayoutPage();
    }

    private void DetectAll()
    {
        var any = false;
        foreach (var row in _rows.Where(r => r.Spec.AutoDetect != null))
        {
            var detected = row.Spec.AutoDetect!();
            if (detected == null)
                continue;

            row.Field.Text = detected;
            any = true;
        }

        if (!any)
        {
            MessageBox.Show(this,
                "未检测到任何可自动识别的路径。", "自动检测",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void DetectOne(PathSpec spec, SettingsField field)
    {
        var detected = spec.AutoDetect?.Invoke();
        if (detected == null)
        {
            MessageBox.Show(this,
                $"未能自动检测到「{spec.Title}」。", "自动检测",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        field.Text = detected;
    }

    private void Browse(PathSpec spec, SettingsField field)
    {
        if (spec.IsFile)
        {
            using var dialog = new OpenFileDialog
            {
                Title = $"选择 {spec.Title}",
                Filter = "ModsConfig.xml|ModsConfig.xml|XML 文件 (*.xml)|*.xml",
                FileName = "ModsConfig.xml"
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                field.Text = dialog.FileName;
            return;
        }

        using var folder = new FolderBrowserDialog
        {
            ShowNewFolderButton = false,
            Description = $"选择 {spec.Title}"
        };
        if (folder.ShowDialog(this) == DialogResult.OK)
            field.Text = folder.SelectedPath;
    }

    private static bool DirectoryOrFileExists(string path, bool isFile)
    {
        try
        {
            return isFile ? File.Exists(path) : Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>路径行：标题 + 说明 + 圆角输入框 +（自动 / 自定义 / 无效）状态徽章。</summary>
    private sealed class PathRow : Panel
    {
        private readonly SettingsDialog _owner;
        private readonly Label _titleLabel;
        private readonly Label _hintLabel;
        private readonly Label _statusLabel;
        private readonly SettingsBadge _badge;
        private readonly bool _showSeparator;

        public PathRow(SettingsDialog owner, PathSpec spec, bool showSeparator)
        {
            _owner = owner;
            Spec = spec;
            _showSeparator = showSeparator;
            Height = 114;
            BackColor = CardBg;

            _titleLabel = new Label
            {
                Text = spec.Title,
                AutoSize = true,
                Font = UiTheme.BodyBoldFont,
                ForeColor = UiTheme.TextPrimary,
                BackColor = CardBg,
                Location = new Point(18, 9)
            };

            _hintLabel = new Label
            {
                Text = spec.Hint,
                AutoSize = false,
                AutoEllipsis = true,
                Font = UiTheme.SmallFont,
                ForeColor = UiTheme.TextMuted,
                BackColor = CardBg,
                Location = new Point(18, 30),
                Size = new Size(300, 16)
            };

            Field = new SettingsField
            {
                Location = new Point(18, 50),
                Height = 36,
                Text = spec.GetValue(ModListResolver.PathOptions.Settings),
                PlaceholderText = spec.IsFile ? "留空 = 自动检测 ModsConfig.xml" : "留空 = 自动检测 / 使用默认路径"
            };
            Field.AddAction("浏览…", () => _owner.Browse(spec, Field));
            if (spec.AutoDetect != null)
                Field.AddAction("检测", () => _owner.DetectOne(spec, Field), accent: true);

            _statusLabel = new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                Font = UiTheme.SmallFont,
                ForeColor = UiTheme.TextSecondary,
                BackColor = CardBg,
                Location = new Point(18, 91),
                Size = new Size(300, 16)
            };

            _badge = new SettingsBadge { Location = new Point(300, 8) };

            Controls.Add(_titleLabel);
            Controls.Add(_hintLabel);
            Controls.Add(Field);
            Controls.Add(_statusLabel);
            Controls.Add(_badge);

            Field.ValueChanged += (_, _) => UpdateState();
            UpdateState();
        }

        public PathSpec Spec { get; }

        public SettingsField Field { get; }

        public void UpdateState()
        {
            var value = Field.Text.Trim();
            if (value.Length > 0)
            {
                var exists = DirectoryOrFileExists(value, Spec.IsFile);
                _badge.SetState(exists ? "自定义" : "无效",
                    exists ? UiTheme.Green : UiTheme.Rose,
                    exists ? UiTheme.GreenBg : UiTheme.RoseBg);
                _statusLabel.Text = exists
                    ? "● 已找到：" + value
                    : "○ 路径不存在";
                _statusLabel.ForeColor = exists ? UiTheme.GreenText : UiTheme.RoseText;
            }
            else
            {
                _badge.SetState("自动", UiTheme.TextSecondary, UiTheme.ElevatedBg);
                _statusLabel.Text = "当前生效（自动）：" + Spec.Effective();
                _statusLabel.ForeColor = UiTheme.TextSecondary;
            }

            PlaceBadge();
        }

        private void PlaceBadge()
        {
            _badge.Location = new Point(Math.Max(0, Width - Px(18) - _badge.Width), Px(8));
        }

        private int Px(int value) => _owner._dpiScaled ? UiTheme.Scale(this, value) : value;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Field == null)
                return;

            Field.Width = Math.Max(Px(120), Width - Px(36));
            _hintLabel.Width = Math.Max(Px(120), Width - Px(36));
            _statusLabel.Width = Math.Max(Px(120), Width - Px(36));
            PlaceBadge();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!_showSeparator)
                return;

            using var pen = new Pen(UiTheme.BorderSubtle, 1f);
            e.Graphics.DrawLine(pen, 18, Height - 1, Width - 18, Height - 1);
        }
    }

    // ───────────────────────────── Agent 页 ─────────────────────────────

    private void BuildAgentPage(Panel page, AppSettings settings)
    {
        var modeCard = new RoundedCard { Location = new Point(26, 86) };

        var modeTitle = new Label
        {
            Text = "启用方式",
            AutoSize = true,
            Font = UiTheme.SectionFont,
            ForeColor = UiTheme.TextPrimary,
            BackColor = CardBg,
            Location = new Point(18, 12)
        };
        var modeSubtitle = new Label
        {
            Text = "决定 Agent 是否调用模型接口；本地核查始终可用。",
            AutoSize = true,
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.TextMuted,
            BackColor = CardBg,
            Location = new Point(18, 35)
        };

        _providerInput.Location = new Point(18, 62);
        _providerInput.SelectedIndex = settings.AgentProvider.Trim().ToLowerInvariant() switch
        {
            "openai" => 1,
            "player2" => 2,
            _ => 0
        };
        _providerInput.SelectionChanged += (_, _) => UpdateProviderHint();

        _providerBadge.Location = new Point(300, 13);

        _providerHint.AutoSize = false;
        _providerHint.Font = UiTheme.SmallFont;
        _providerHint.ForeColor = UiTheme.TextSecondary;
        _providerHint.BackColor = CardBg;
        _providerHint.Location = new Point(18, 108);
        _providerHint.Size = new Size(400, 18);

        modeCard.Controls.Add(modeTitle);
        modeCard.Controls.Add(modeSubtitle);
        modeCard.Controls.Add(_providerInput);
        modeCard.Controls.Add(_providerBadge);
        modeCard.Controls.Add(_providerHint);

        var fieldsCard = new RoundedCard { Location = new Point(26, 240) };

        StyleField(_baseUrlInput, settings.AgentBaseUrl, "留空使用服务商默认地址");
        StyleField(_modelInput, settings.AgentModel, "例如 gpt-4.1；Player2 可留空");
        var hasExistingKey = !string.IsNullOrEmpty(_existingProtectedKey);
        StyleField(_apiKeyInput, string.Empty, hasExistingKey
            ? "•••••••••••••••• (已保存密钥，留空保留)"
            : "可选；也可用环境变量 MODSORTER_AGENT_API_KEY");
        _apiKeyInput.UseSystemPasswordChar = true;

        var rows = new (string Title, string Hint, SettingsField Field)[]
        {
            ("接口地址", "OpenAI 兼容 /v1 或 Player2 接口根地址", _baseUrlInput),
            ("模型名称", "需支持长上下文与严格 JSON", _modelInput),
            ("API Key", "DPAPI 加密，仅本用户可解", _apiKeyInput)
        };

        for (var i = 0; i < rows.Length; i++)
        {
            var row = new AgentFieldRow(this, rows[i].Title, rows[i].Hint, rows[i].Field)
            {
                Location = new Point(1, 1 + i * 68),
                ShowSeparator = i < rows.Length - 1
            };

            switch (i)
            {
                case 0: _baseUrlRow = row; break;
                case 1: _modelRow = row; break;
                case 2: _apiKeyRow = row; break;
            }

            fieldsCard.Controls.Add(row);
        }

        _player2AuthButton.Visible = false;
        _player2AuthButton.Click += (_, _) => StartPlayer2WebAuth();

        var patchCard = new RoundedCard { Location = new Point(26, 458) };

        var patchTitle = new Label
        {
            Text = "补丁输出目录",
            AutoSize = true,
            Font = UiTheme.BodyBoldFont,
            ForeColor = UiTheme.TextPrimary,
            BackColor = CardBg,
            Location = new Point(18, 11)
        };
        var patchHint = new Label
        {
            Text = "生成的兼容补丁 mod 包存放位置（留空 = %LOCALAPPDATA%\\ModSorterAgent\\patches）",
            AutoSize = false,
            AutoEllipsis = true,
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.TextMuted,
            BackColor = CardBg,
            Location = new Point(18, 32),
            Size = new Size(400, 16)
        };
        StyleField(_patchDirInput, settings.AgentPatchOutputDir, AgentConfigLoader.DefaultPatchOutputDir);
        _patchDirInput.Location = new Point(18, 54);
        _patchDirInput.AddAction("浏览…", () =>
        {
            using var folder = new FolderBrowserDialog { Description = "选择兼容补丁输出目录" };
            if (folder.ShowDialog(this) == DialogResult.OK)
                _patchDirInput.Text = folder.SelectedPath;
        });

        patchCard.Controls.Add(patchTitle);
        patchCard.Controls.Add(patchHint);
        patchCard.Controls.Add(_patchDirInput);

        void LayoutPage()
        {
            var cardWidth = Math.Max(Px(320), page.ClientSize.Width - Px(52));
            modeCard.Width = cardWidth;
            modeCard.Height = Px(142);
            fieldsCard.Width = cardWidth;
            fieldsCard.Height = Px(206);
            patchCard.Width = cardWidth;
            patchCard.Height = Px(102);

            _providerInput.Width = Math.Max(Px(200), cardWidth - Px(36));
            _providerHint.Width = Math.Max(Px(200), cardWidth - Px(36));
            _providerBadge.Location = new Point(Math.Max(0, cardWidth - Px(18) - _providerBadge.Width), Px(13));

            foreach (var row in fieldsCard.Controls.OfType<AgentFieldRow>())
                row.Width = cardWidth - Px(2);

            _patchDirInput.Width = Math.Max(Px(180), cardWidth - Px(36));
            patchHint.Width = Math.Max(Px(120), cardWidth - Px(36));
        }

        page.Controls.Add(modeCard);
        page.Controls.Add(fieldsCard);
        page.Controls.Add(patchCard);
        page.Resize += (_, _) => LayoutPage();
        _layoutActions.Add(LayoutPage);
        LayoutPage();

        // 构造早期调用 UpdateProviderHint 时字段行尚未建好，这里按已保存的提供方再落一次。
        ApplyProviderFieldLayout(_providerInput.SelectedIndex);
    }

    private static void StyleField(SettingsField field, string text, string placeholder)
    {
        field.Text = text;
        field.PlaceholderText = placeholder;
    }

    private void UpdateProviderHint()
    {
        var index = _providerInput.SelectedIndex;
        var modeled = index != 0;

        _providerHint.Text = index switch
        {
            1 => "调用 OpenAI 兼容接口；需填写模型名称。接口地址留空时使用默认地址。",
            2 => "选中即可用：凭据由本地客户端自动提供，客户端未运行时可在下方发起网页鉴权。",
            _ => "不调用任何模型：Agent 只在本机反编译双方补丁并给出结论，不会生成未经核对的代码。"
        };

        ApplyProviderFieldLayout(index);

        _providerBadge.SetState(modeled ? "模型增强" : "本地优先",
            modeled ? UiTheme.Green : UiTheme.TextSecondary,
            modeled ? UiTheme.GreenBg : UiTheme.ElevatedBg);

        if (_providerBadge.Parent != null)
        {
            _providerBadge.Location = new Point(
                Math.Max(0, _providerBadge.Parent.Width - Px(18) - _providerBadge.Width), Px(13));
        }
    }

    /// <summary>
    /// Player2 不需要接口地址与模型名，把这两行收起来，只留一条授权行；
    /// 其余提供方恢复三行原样。行的显示与位置都限制在本卡片内，卡片高度不变，
    /// 因此不影响下方任何既有控件的位置。
    /// </summary>
    private void ApplyProviderFieldLayout(int index)
    {
        var isPlayer2 = index == 2;

        if (_baseUrlRow != null)
            _baseUrlRow.Visible = !isPlayer2;

        if (_modelRow != null)
            _modelRow.Visible = !isPlayer2;

        if (_apiKeyRow == null)
            return;

        if (isPlayer2)
        {
            var hasKey = !string.IsNullOrEmpty(_existingProtectedKey) || !string.IsNullOrWhiteSpace(_apiKeyInput.Text);
            _apiKeyRow.SetTexts("Player2 授权",
                hasKey ? "已配置 Player2 授权凭据；本地客户端运行时将优先走本地" : "本地客户端自动提供凭据；也可点右侧按钮走网页鉴权");
            _apiKeyRow.Action = _player2AuthButton;
            _apiKeyRow.Location = new Point(1, 1);
        }
        else
        {
            var hasKey = !string.IsNullOrEmpty(_existingProtectedKey) || !string.IsNullOrWhiteSpace(_apiKeyInput.Text);
            _apiKeyRow.SetTexts("API Key",
                hasKey ? "已配置密钥（DPAPI 加密保护，留空保留）" : "DPAPI 加密，仅本用户可解");
            _apiKeyRow.Action = null;
            _apiKeyRow.Location = new Point(1, 1 + 2 * 68);
        }

        if (_baseUrlRow != null)
            _baseUrlRow.Location = new Point(1, 1);
        if (_modelRow != null)
            _modelRow.Location = new Point(1, 1 + 68);
    }

    /// <summary>
    /// 网页鉴权：申请设备码 → 打开授权页 → 轮询换取密钥，成功后填入密钥框，随保存一并落盘。
    /// </summary>
    private async void StartPlayer2WebAuth()
    {
        _player2AuthButton.Enabled = false;
        _player2AuthButton.Text = "等待授权…";
        _apiKeyRow?.SetTexts("Player2 授权", "浏览器已打开，请登录并确认授权");

        try
        {
            var key = await Player2AuthService.AuthorizeViaWebAsync(prompt =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(prompt.ApprovalUrl) { UseShellExecute = true });
                }
                catch
                {
                    // 打不开浏览器不算失败，用户仍可手动访问授权页。
                }
            });

            _apiKeyInput.Text = key;
            _apiKeyRow?.SetTexts("Player2 授权", "已通过网页鉴权获取密钥，保存设置后生效");
            MessageBox.Show(this,
                "已获取 Player2 密钥并填入输入框。\n点「保存设置」后生效。",
                "Player2 授权", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _apiKeyRow?.SetTexts("Player2 授权", "网页鉴权未完成，可重试或改用本地客户端");
            MessageBox.Show(this,
                "Player2 网页鉴权未完成：\n" + ex.Message,
                "Player2 授权", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _player2AuthButton.Text = "网页鉴权";
            _player2AuthButton.Enabled = true;
        }
    }

    private sealed class AgentFieldRow : Panel
    {
        private readonly SettingsDialog _owner;
        private readonly Label _titleLabel;
        private readonly Label _hintLabel;
        private readonly SettingsField _field;
        private Control? _action;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowSeparator { get; set; }

        public AgentFieldRow(SettingsDialog owner, string title, string hint, SettingsField field)
        {
            _owner = owner;
            BackColor = CardBg;
            _field = field;

            _titleLabel = new Label
            {
                Text = title,
                AutoSize = true,
                Font = UiTheme.BodyBoldFont,
                ForeColor = UiTheme.TextPrimary,
                BackColor = CardBg,
                Location = new Point(18, 9)
            };
            _hintLabel = new Label
            {
                Text = hint,
                AutoSize = false,
                AutoEllipsis = true,
                Font = UiTheme.SmallFont,
                ForeColor = UiTheme.TextMuted,
                BackColor = CardBg,
                Location = new Point(18, 30),
                Size = new Size(180, 30)
            };

            Controls.Add(_titleLabel);
            Controls.Add(_hintLabel);
            Controls.Add(field);
            Height = 68;
        }

        /// <summary>改写标题与提示，供提供方切换时复用同一行。</summary>
        public void SetTexts(string title, string hint)
        {
            _titleLabel.Text = title;
            _hintLabel.Text = hint;
        }

        /// <summary>
        /// 行尾的可选操作按钮。未设置时布局与从前完全一致，只在设置了才让出右侧空间。
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Control? Action
        {
            get => _action;
            set
            {
                if (_action != null)
                    Controls.Remove(_action);

                _action = value;
                if (_action != null)
                    Controls.Add(_action);

                PerformLayout();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            var fieldLeft = Px(216);
            var reserved = _action is { Visible: true } ? _action.Width + Px(10) : 0;

            _field.Location = new Point(fieldLeft, (Height - Px(36)) / 2);
            _field.Width = Math.Max(Px(120), Width - fieldLeft - Px(18) - reserved);
            _hintLabel.Width = Math.Max(Px(100), fieldLeft - Px(36));

            if (_action != null)
            {
                _action.Location = new Point(
                    Math.Max(fieldLeft, Width - Px(18) - _action.Width),
                    (Height - _action.Height) / 2);
            }
        }

        private int Px(int value) => _owner._dpiScaled ? UiTheme.Scale(this, value) : value;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!ShowSeparator)
                return;

            using var pen = new Pen(UiTheme.BorderSubtle, 1f);
            e.Graphics.DrawLine(pen, 18, Height - 1, Width - 18, Height - 1);
        }
    }

    // ───────────────────────────── 选项页 ─────────────────────────────

    private void BuildOptionsPage(Panel page, AppSettings settings)
    {
        var verifyCard = BuildOptionCard(
            "分析时核验游戏实况",
            "尝试连接 GABS 或读取 Player.log，确认推荐排序与实机加载一致；失败时自动退回离线核验。",
            _verifyGabs, settings.VerifyWithGabs);

        var workshopCard = BuildOptionCard(
            "纳入 Steam 创意工坊订阅池",
            "把已订阅但未启用的模组一并纳入排序参考。它们只参与排序，不会被自动启用或写入 ModsConfig.xml。",
            _includeWorkshop, settings.IncludeWorkshopSubscriptions);

        var infoCard = BuildInfoCard();

        void LayoutPage()
        {
            var cardWidth = Math.Max(Px(320), page.ClientSize.Width - Px(52));
            verifyCard.Location = new Point(Px(26), Px(86));
            verifyCard.Width = cardWidth;
            verifyCard.Height = Px(96);
            workshopCard.Location = new Point(Px(26), Px(194));
            workshopCard.Width = cardWidth;
            workshopCard.Height = Px(96);
            infoCard.Location = new Point(Px(26), Px(302));
            infoCard.Width = cardWidth;
            infoCard.Height = Px(150);

            foreach (var card in new[] { verifyCard, workshopCard })
                LayoutOptionCard(card);
        }

        page.Controls.Add(verifyCard);
        page.Controls.Add(workshopCard);
        page.Controls.Add(infoCard);
        page.Resize += (_, _) => LayoutPage();
        _layoutActions.Add(LayoutPage);
        LayoutPage();
    }

    private RoundedCard BuildOptionCard(
        string title,
        string description,
        ToggleSwitch toggle,
        bool initial)
    {
        var card = new RoundedCard();
        var titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = UiTheme.SectionFont,
            ForeColor = UiTheme.TextPrimary,
            BackColor = CardBg,
            Location = new Point(18, 16)
        };
        var descLabel = new Label
        {
            Text = description,
            AutoSize = false,
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.TextMuted,
            BackColor = CardBg,
            Location = new Point(18, 42),
            Size = new Size(400, 40)
        };

        var badge = new SettingsBadge { Location = new Point(300, 16) };
        toggle.Checked = initial;
        toggle.Location = new Point(300, 35);

        void Sync()
        {
            var on = toggle.Checked;
            badge.SetState(on ? "已开启" : "已关闭",
                on ? UiTheme.Green : UiTheme.TextSecondary,
                on ? UiTheme.GreenBg : UiTheme.ElevatedBg);
            if (card.Width > 0)
            {
                badge.Location = new Point(
                    Math.Max(0, card.Width - Px(18) - badge.Width), Px(16));
            }
            card.Invalidate();
        }

        toggle.CheckedChanged += (_, _) => Sync();
        Sync();

        card.Controls.Add(titleLabel);
        card.Controls.Add(descLabel);
        card.Controls.Add(toggle);
        card.Controls.Add(badge);

        card.Tag = (descLabel, toggle, badge);
        return card;
    }

    private void LayoutOptionCard(RoundedCard card)
    {
        if (card.Tag is not (Label descLabel, ToggleSwitch toggle, SettingsBadge badge))
            return;

        toggle.Location = new Point(Math.Max(0, card.Width - Px(18) - toggle.Width), Px(35));
        badge.Location = new Point(Math.Max(0, card.Width - Px(18) - badge.Width), Px(16));
        descLabel.Width = Math.Max(Px(160), card.Width - Px(120));
    }

    private RoundedCard BuildInfoCard()
    {
        var card = new RoundedCard();
        var title = new Label
        {
            Text = "存储与安全",
            AutoSize = true,
            Font = UiTheme.SectionFont,
            ForeColor = UiTheme.TextPrimary,
            BackColor = CardBg,
            Location = new Point(18, 14)
        };
        var pathLabel = new Label
        {
            Text = "AppSettings.json · 与程序同目录",
            AutoSize = false,
            AutoEllipsis = true,
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.TextSecondary,
            BackColor = CardBg,
            Location = new Point(18, 44),
            Size = new Size(400, 18)
        };
        var note1 = new Label
        {
            Text = "API Key 使用 Windows DPAPI 加密，仅当前 Windows 用户可解密。",
            AutoSize = false,
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.TextMuted,
            BackColor = CardBg,
            Location = new Point(18, 72),
            Size = new Size(500, 18)
        };
        var note2 = new Label
        {
            Text = "路径解析顺序：用户设置 → 自动检测 → 内置默认。留空即跟随自动检测。",
            AutoSize = false,
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.TextMuted,
            BackColor = CardBg,
            Location = new Point(18, 96),
            Size = new Size(500, 18)
        };

        card.Controls.Add(title);
        card.Controls.Add(pathLabel);
        card.Controls.Add(note1);
        card.Controls.Add(note2);
        card.Resize += (_, _) =>
        {
            pathLabel.Width = Math.Max(Px(120), card.Width - Px(36));
            note1.Width = Math.Max(Px(120), card.Width - Px(36));
            note2.Width = Math.Max(Px(120), card.Width - Px(36));
        };
        return card;
    }

    // ───────────────────────────── 页面切换 ─────────────────────────────

    private void ShowPage(int index, bool updateNav)
    {
        if (index < 0 || index >= _pages.Count)
            return;

        for (var i = 0; i < _pages.Count; i++)
            _pages[i].Visible = i == index;

        if (updateNav)
        {
            for (var i = 0; i < _navItems.Count; i++)
                _navItems[i].Selected = i == index;
        }

        _pages[index].BringToFront();
    }

    // ───────────────────────────── 保存 ─────────────────────────────

    private void Save()
    {
        var invalid = _rows
            .Where(r => r.Field.Text.Trim().Length > 0 &&
                        !DirectoryOrFileExists(r.Field.Text.Trim(), r.Spec.IsFile))
            .Select(r => r.Spec.Title)
            .ToList();

        if (invalid.Count > 0)
        {
            ShowPage(0, updateNav: true);
            var confirm = MessageBox.Show(
                this,
                $"以下路径当前不存在：\n{string.Join("\n", invalid.Select(x => "· " + x))}\n\n仍要保存吗？（保存后相关扫描可能扫不到任何模组）",
                "路径不存在",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1);
            if (confirm != DialogResult.OK)
                return;
        }

        var provider = _providerInput.SelectedIndex switch
        {
            1 => "openai",
            2 => "player2",
            _ => "off"
        };

        // Player2 由服务端选定模型，不要求填模型名。
        if (provider != "off" && provider != "player2" && string.IsNullOrWhiteSpace(_modelInput.Text))
        {
            ShowPage(1, updateNav: true);
            MessageBox.Show(this,
                "已选择模型接口，但模型名称为空。\n模型接口需要同时填写模型名称，否则设置无法保存。",
                "Agent 设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _modelInput.SelectAll();
            return;
        }

        // Player2 的凭据可由本地客户端在调用时自动换取，缺密钥不构成保存障碍。
        if (provider != "off" && provider != "player2" && string.IsNullOrWhiteSpace(_apiKeyInput.Text) &&
            string.IsNullOrEmpty(_existingProtectedKey) &&
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AgentConfigLoader.EnvApiKey)))
        {
            ShowPage(1, updateNav: true);
            var confirm = MessageBox.Show(
                this,
                "没有填写 API Key，也没有检测到环境变量 MODSORTER_AGENT_API_KEY。\n保存后将无法调用模型，Agent 会回退到本地核查。仍要保存吗？",
                "Agent 设置",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1);
            if (confirm != DialogResult.OK)
                return;
        }

        var settings = new AppSettings
        {
            AgentProvider = provider,
            AgentBaseUrl = _baseUrlInput.Text.Trim(),
            AgentModel = _modelInput.Text.Trim(),
            AgentApiKeyProtected = _existingProtectedKey,
            AgentPatchOutputDir = _patchDirInput.Text.Trim(),
            VerifyWithGabs = _verifyGabs.Checked,
            IncludeWorkshopSubscriptions = _includeWorkshop.Checked
        };

        var typedKey = _apiKeyInput.Text.Trim();
        if (typedKey.Length > 0)
            settings.AgentApiKeyProtected = DpapiProtector.Protect(typedKey);

        foreach (var row in _rows)
            row.Spec.SetValue(settings, row.Field.Text.Trim());

        ModListResolver.PathOptions.Update(settings);
        DialogResult = DialogResult.OK;
        Close();
    }

    // ───────────────────────────── 窗口 ─────────────────────────────

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return true;
        }

        if (keyData == (Keys.Control | Keys.S))
        {
            Save();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateWindowRegion();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateWindowRegion();
    }

    private void UpdateWindowRegion()
    {
        if (Width <= 0 || Height <= 0)
            return;

        using var path = UiTheme.RoundedRect(
            new Rectangle(0, 0, Width, Height),
            UiTheme.Scale(this, 12));
        Region = new Region(path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using var pen = new Pen(UiTheme.BorderStrong, 1f);
        using var path = UiTheme.RoundedRect(
            new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)),
            UiTheme.Scale(this, 12));
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.DrawPath(pen, path);
    }
}

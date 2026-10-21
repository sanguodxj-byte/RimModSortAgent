using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ModSorterAgent.Agent;
using ModSorterAgent.Discovery;
using ModSorterAgent.Models;
using ModSorterAgent.Patching;
using ModSorterAgent.Reporting;
using ModSorterAgent.Services;
using ModSorterAgent.UI.Controls;

namespace ModSorterAgent.UI;

public sealed class MainForm : Form
{
    private enum ModQuickFilter
    {
        All,
        Purple,
        Red,
        Yellow,
        Green,
        Moved,
        Inactive
    }

    private readonly IModSortPipeline _pipeline = new ModSortPipelineService();

    private readonly KpiStrip _kpi = new();
    private readonly PipelineProgressBar _progress = new();
    private readonly DetailInspector _inspector = new();

    private readonly VirtualListView _modList = new();

    private readonly SearchBox _modSearch = new("搜索模组名称 / PackageId", 220);

    private readonly FilterChip _chipAll = new("全部", UiTheme.Accent);
    private readonly FilterChip _chipPurple = new("4.紫色", UiTheme.Purple);
    private readonly FilterChip _chipRed = new("3.红色", UiTheme.Rose);
    private readonly FilterChip _chipYellow = new("2.黄色", UiTheme.Amber);
    private readonly FilterChip _chipGreen = new("1.绿色", UiTheme.Green);
    private readonly FilterChip _chipMoved = new("仅看顺位变动", UiTheme.Accent);
    private readonly FilterChip _chipInactive = new("未启用", UiTheme.Amber);


    private readonly ModernButton _btnAnalyze = new("开始分析", ButtonVariant.Primary);
    private readonly ModernButton _btnStop = new("停止", ButtonVariant.Ghost);
    private readonly ModernButton _btnImport = new("导入列表 ▾", ButtonVariant.Secondary);
    private readonly ModernButton _btnApply = new("写入配置", ButtonVariant.Secondary);
    private readonly ModernButton _btnExport = new("导出列表 ▾", ButtonVariant.Ghost);
    private readonly ModernButton _btnLaunchGame = new("启动游戏", ButtonVariant.Secondary);

    private readonly ModernButton _btnSortLists = new("排序列表 ▾", ButtonVariant.Ghost);

    private readonly SortProfileStore _profileStore = new();

    private ContextMenuStrip? _importMenu;
    private ContextMenuStrip? _exportMenu;
    private ContextMenuStrip? _profileMenu;

    private readonly SectionCard _modCard = new("推荐加载顺序", "尚未分析");
    private readonly SectionCard _detailCard = new("冲突详情", "未选择");

    private readonly ModernButton _btnDeepCheck = new("深度核查", ButtonVariant.Secondary);
    private readonly ModernButton _btnBuildPatch = new("兼容生成", ButtonVariant.Primary);

    private readonly PurpleChoicePanel _purpleChoicePanel = new();
    private readonly Dictionary<string, PurpleChoice> _purpleChoices = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _activePackageIdSet = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<PlayerConflict> _playerConflicts = Array.Empty<PlayerConflict>();
    private readonly Dictionary<string, AgentReport> _agentReports = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _bundledConflictKeys = new(StringComparer.OrdinalIgnoreCase);
    private CompatPatchResult? _patchBundle;
    private string? _bundleInstalledPath;
    private readonly HashSet<string> _checkingKeys = new(StringComparer.OrdinalIgnoreCase);

    private AgentInvestigator? _investigator;
    private List<PlayerConflict> _modConflicts = new();
    private bool _agentBusy;
    private bool _includeWorkshopFromCli;
    private CancellationTokenSource? _agentCts;
    private Task _modCheckTask = Task.CompletedTask;

    private readonly Label _statusLabel = new();
    private readonly WindowChromeButton _btnMaximize = new(WindowChromeButton.WindowControlKind.Maximize);
    private readonly ToolTip _chromeTips = new() { InitialDelay = 350, ReshowDelay = 100, AutoPopDelay = 6000 };

    private CancellationTokenSource? _cts;
    private PipelineResult? _lastResult;
    private ModGradeSummary? _selectedMod;

    private List<string> _tokens = new();
    private List<string> _knownExpansions = new();
    private string _gameVersion = "1.7";
    private string _configPath = ModListResolver.DefaultModsConfigPath;
    private ModQuickFilter _modFilter = ModQuickFilter.All;
    private bool _verifyGabs = true;
    private bool _includeWorkshop;
    private bool _busy;
    private bool _splitterInitialized;

    private readonly string? _autoScreenshotPath;
    private readonly bool _exitAfterScreenshot;

    public MainForm(string? autoScreenshotPath = null, bool exitAfterScreenshot = false, bool includeWorkshop = false)
    {
        _autoScreenshotPath = autoScreenshotPath;
        _exitAfterScreenshot = exitAfterScreenshot;
        _includeWorkshopFromCli = includeWorkshop;

        var startupSettings = ModListResolver.PathOptions.Settings;
        _verifyGabs = startupSettings.VerifyWithGabs;
        _includeWorkshop = includeWorkshop || startupSettings.IncludeWorkshopSubscriptions;

        SuspendLayout();

        Text = "RimWorld 模组排序与冲突分析工作台";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = UiTheme.AppBg;
        ForeColor = UiTheme.TextPrimary;
        Font = UiTheme.BodyFont;
        KeyPreview = true;
        DoubleBuffered = true;
        Padding = new Padding(6);

        var workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        MinimumSize = new Size(1180, 720);
        Size = new Size(
            Math.Min(1880, Math.Max(1360, workArea.Width - 60)),
            Math.Min(1040, Math.Max(800, workArea.Height - 60)));

        BuildLayout();
        WireEvents();

        Application.AddMessageFilter(new WheelRedirectFilter());

        ResumeLayout(true);
    }

    // ───────────────────────────── 布局 ─────────────────────────────

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = UiTheme.AppBg,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(_kpi, 0, 1);
        root.Controls.Add(_progress, 0, 2);
        root.Controls.Add(BuildWorkspace(), 0, 3);

        Controls.Add(root);
        Controls.Add(BuildStatusBar());

        _inspector.ShowDocument(null);
        _modList.EmptyText = "尚未加载模组列表";
        _modList.EmptyHint = string.Empty;
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.SurfaceBg,
            Margin = new Padding(0)
        };

        var titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = Color.FromArgb(15, 18, 24)
        };

        titleBar.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var markRect = new Rectangle(UiTheme.Scale(titleBar, 14), UiTheme.Scale(titleBar, 9),
                UiTheme.Scale(titleBar, 23), UiTheme.Scale(titleBar, 23));
            using (var markPath = UiTheme.RoundedRect(markRect, UiTheme.Scale(titleBar, 7)))
            using (var markBrush = new LinearGradientBrush(
                       markRect, UiTheme.Accent, Color.FromArgb(104, 96, 224),
                       LinearGradientMode.ForwardDiagonal))
            {
                g.FillPath(markBrush, markPath);
            }

            using (var glyphPen = new Pen(Color.FromArgb(245, 249, 255), 1.4f)
                   {
                       StartCap = LineCap.Round,
                       EndCap = LineCap.Round
                   })
            {
                var gx = markRect.X + UiTheme.Scale(titleBar, 5);
                for (var i = 0; i < 3; i++)
                {
                    var y = markRect.Y + UiTheme.Scale(titleBar, 7) + i * UiTheme.Scale(titleBar, 5);
                    var width = UiTheme.Scale(titleBar, 13 - i * 3);
                    g.DrawLine(glyphPen, gx, y, gx + width, y);
                }
            }

            var titleLeft = UiTheme.Scale(titleBar, 46);
            var titleWidth = UiTheme.MeasureWidth("RimWorld ModSorterAgent", UiTheme.TitleFont);
            UiTheme.DrawText(g, "RimWorld ModSorterAgent", UiTheme.TitleFont,
                new Rectangle(titleLeft, 0, titleWidth + UiTheme.Scale(titleBar, 8), titleBar.Height),
                UiTheme.TextPrimary);

            var dividerX = titleLeft + titleWidth + UiTheme.Scale(titleBar, 12);
            using (var divider = new Pen(UiTheme.BorderStrong, 1f))
                g.DrawLine(divider, dividerX, UiTheme.Scale(titleBar, 13),
                    dividerX, titleBar.Height - UiTheme.Scale(titleBar, 13));

            var subtitleLeft = dividerX + UiTheme.Scale(titleBar, 11);
            var subtitleWidth = Math.Max(0, titleBar.Width - UiTheme.Scale(titleBar, 210) - subtitleLeft);
            UiTheme.DrawText(g, "静态代码冲突分级 · 分层拓扑排序", UiTheme.SmallFont,
                new Rectangle(subtitleLeft, 0, subtitleWidth, titleBar.Height),
                UiTheme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            using (var edge = new Pen(UiTheme.BorderSubtle, 1f))
                g.DrawLine(edge, titleBar.Width - UiTheme.Scale(titleBar, 188), UiTheme.Scale(titleBar, 11),
                    titleBar.Width - UiTheme.Scale(titleBar, 188), titleBar.Height - UiTheme.Scale(titleBar, 11));

            using var line = new Pen(UiTheme.BorderSubtle, 1f);
            g.DrawLine(line, 0, titleBar.Height - 1, titleBar.Width, titleBar.Height - 1);
        };

        titleBar.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
                return;

            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, NativeMethods.WM_NCLBUTTONDOWN, NativeMethods.HTCAPTION, 0);
        };

        titleBar.MouseDoubleClick += (_, _) => ToggleMaximize();

        var windowButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 172,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = UiTheme.TitleBarBg,
            Padding = new Padding(0, 5, 4, 0)
        };

        var btnSettings = new WindowChromeButton(WindowChromeButton.WindowControlKind.Settings);
        btnSettings.Click += (_, _) => ShowSettingsDialog();

        var btnMinimize = new WindowChromeButton(WindowChromeButton.WindowControlKind.Minimize);

        var btnClose = new WindowChromeButton(WindowChromeButton.WindowControlKind.Close);

        btnMinimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
        _btnMaximize.Click += (_, _) => ToggleMaximize();
        btnClose.Click += (_, _) => Close();

        _chromeTips.SetToolTip(btnSettings, "设置 (Ctrl+,)");
        _chromeTips.SetToolTip(btnMinimize, "最小化");
        _chromeTips.SetToolTip(btnClose, "关闭");
        UpdateWindowButtonTips();

        windowButtons.Controls.Add(btnSettings);
        windowButtons.Controls.Add(btnMinimize);
        windowButtons.Controls.Add(_btnMaximize);
        windowButtons.Controls.Add(btnClose);
        titleBar.Controls.Add(windowButtons);

        var commandBar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.SurfaceBg,
            Padding = new Padding(10, 6, 10, 6)
        };

        var rightFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _btnLaunchGame.Height = 30;
        _btnLaunchGame.Margin = new Padding(0, 0, 0, 0);
        rightFlow.Controls.Add(_btnLaunchGame);

        // RightToLeft 流向：后加入者位于启动按钮左侧
        _btnSortLists.Height = 30;
        _btnSortLists.Margin = new Padding(0, 0, 10, 0);
        rightFlow.Controls.Add(_btnSortLists);

        var commandDivider = new Panel
        {
            Width = 1,
            Height = 20,
            BackColor = UiTheme.BorderStrong,
            Margin = new Padding(6, 5, 12, 0)
        };
        rightFlow.Controls.Add(commandDivider);

        _chromeTips.SetToolTip(_btnLaunchGame, "启动 RimWorld（启动前自动同步激活的排序）");
        _chromeTips.SetToolTip(_btnSortLists, "切换、保存或删除排序列表");

        var leftFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        foreach (var button in new[] { _btnAnalyze, _btnStop, _btnDeepCheck, _btnBuildPatch, _btnImport, _btnExport, _btnApply })
        {
            button.Height = 30;
            button.Margin = new Padding(0, 0, 6, 0);
            leftFlow.Controls.Add(button);
        }

        _btnAnalyze.LeadingGlyph = "▶";
        _btnStop.LeadingGlyph = "■";
        _btnDeepCheck.LeadingGlyph = "◎";
        _btnBuildPatch.LeadingGlyph = "◈";
        _btnApply.LeadingGlyph = "◆";
        _chromeTips.SetToolTip(_btnDeepCheck, "对全部生效冲突逐项做模型深度核查，不只当前模组");
        _chromeTips.SetToolTip(_btnBuildPatch, "对全部生效冲突统一复核，生成一个合并兼容补丁包");
        _btnLaunchGame.LeadingGlyph = "▶";
        _btnLaunchGame.LeadingGlyphColor = UiTheme.Green;

        commandBar.Controls.Add(leftFlow);
        commandBar.Controls.Add(rightFlow);
        rightFlow.BringToFront();

        commandBar.Paint += (_, e) =>
        {
            using var pen = new Pen(UiTheme.BorderSubtle, 1f);
            e.Graphics.DrawLine(pen, 0, commandBar.Height - 1, commandBar.Width, commandBar.Height - 1);
        };

        header.Controls.Add(commandBar);
        header.Controls.Add(titleBar);
        return header;
    }

    private Control BuildWorkspace()
    {
        var outer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 8,
            BackColor = UiTheme.AppBg,
            Margin = new Padding(0),
            BorderStyle = BorderStyle.None
        };
        outer.Panel1.BackColor = UiTheme.AppBg;
        outer.Panel2.BackColor = UiTheme.AppBg;
        outer.Panel1.Padding = new Padding(0, 0, 0, 0);
        outer.Panel2.Padding = new Padding(0, 0, 0, 0);

        BuildModCard();
        outer.Panel1.Controls.Add(_modCard);

        _detailCard.Dock = DockStyle.Fill;

        _inspector.Dock = DockStyle.Fill;
        _detailCard.Body.Controls.Add(_inspector);
        outer.Panel2.Controls.Add(_detailCard);

        outer.Resize += (_, _) => ApplySplitterDistances(outer, force: false);
        Shown += (_, _) => ApplySplitterDistances(outer, force: true);

        return outer;
    }

    private void ApplySplitterDistances(SplitContainer outer, bool force)
    {
        // 只做一次初始比例分配；之后交给用户拖动，避免窗口尺寸变化时把手动调整重置掉
        if (_splitterInitialized && !force)
            return;

        try
        {
            var totalWidth = outer.Width - outer.SplitterWidth;
            if (totalWidth > 400)
                outer.SplitterDistance = (int)(totalWidth * 0.54);

            _splitterInitialized = true;
        }
        catch (InvalidOperationException)
        {
            // 容器尺寸过小时忽略，等下一次布局
        }
    }

    private void BuildModCard()
    {
        _modCard.Dock = DockStyle.Fill;

        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = UiTheme.SurfaceBg,
            Padding = new Padding(10, 7, 10, 7)
        };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        _modSearch.Height = 28;
        _modSearch.Margin = new Padding(0, 0, 8, 0);
        flow.Controls.Add(_modSearch);

        foreach (var chip in new[] { _chipAll, _chipPurple, _chipRed, _chipYellow, _chipGreen, _chipInactive })
        {
            chip.Height = 26;
            chip.Margin = new Padding(0, 0, 6, 0);
            flow.Controls.Add(chip);
        }

        _chipAll.Checked = true;

        toolbar.Controls.Add(flow);

        _modList.Dock = DockStyle.Fill;
        _modList.SetColumns(
            new UiColumn("顺位", 7f, 52, true),
            new UiColumn("变动", 8f, 56, true),
            new UiColumn("评级", 10f, 78),
            new UiColumn("状态", 8f, 58),
            new UiColumn("模组名称", 33f, 160),
            new UiColumn("架构分层", 13f, 92),
            new UiColumn("冲突", 8f, 62),
            new UiColumn("补丁", 7f, 56, true));

        _modCard.Body.Controls.Add(_modList);
        _purpleChoicePanel.Dock = DockStyle.Top;
        _modCard.Body.Controls.Add(_purpleChoicePanel);
        _modCard.Body.Controls.Add(toolbar);
    }

    private Control BuildStatusBar()
    {
        var bar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 26,
            BackColor = Color.FromArgb(15, 18, 24)
        };

        _statusLabel.Text = "就绪";
        _statusLabel.Dock = DockStyle.Left;
        _statusLabel.Width = 700;
        _statusLabel.Padding = new Padding(14, 0, 0, 0);
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.ForeColor = UiTheme.TextSecondary;
        _statusLabel.Font = UiTheme.SmallFont;
        _statusLabel.BackColor = Color.Transparent;

        bar.Controls.Add(_statusLabel);

        bar.Paint += (_, e) =>
        {
            using var pen = new Pen(UiTheme.BorderSubtle, 1f);
            e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
        };

        return bar;
    }

    // ───────────────────────────── 事件 ─────────────────────────────

    private void WireEvents()
    {
        BuildMenus();

        _btnAnalyze.Click += async (_, _) => await RunAnalysisAsync(false);
        _btnStop.Click += (_, _) =>
        {
            _cts?.Cancel();
            _agentCts?.Cancel();
        };
        _btnApply.Click += (_, _) => ApplyToGameConfig();
        _btnLaunchGame.Click += (_, _) => LaunchGame();

        _modSearch.SearchTextChanged += (_, _) => RefreshModList();

        _chipAll.CheckedChanged += (_, _) => OnModFilterChipClicked(ModQuickFilter.All);
        _chipPurple.CheckedChanged += (_, _) => OnModFilterChipClicked(ModQuickFilter.Purple);
        _chipRed.CheckedChanged += (_, _) => OnModFilterChipClicked(ModQuickFilter.Red);
        _chipYellow.CheckedChanged += (_, _) => OnModFilterChipClicked(ModQuickFilter.Yellow);
        _chipGreen.CheckedChanged += (_, _) => OnModFilterChipClicked(ModQuickFilter.Green);
        _chipMoved.CheckedChanged += (_, _) => OnModFilterChipClicked(ModQuickFilter.Moved);
        _chipInactive.CheckedChanged += (_, _) => OnModFilterChipClicked(ModQuickFilter.Inactive);

        _modList.SelectionChanged += (_, _) => OnModSelectionChanged();

        _kpi.CardClicked += (_, card) => OnKpiClicked(card);

        _btnDeepCheck.Click += async (_, _) => await DeepCheckAsync();
        _btnBuildPatch.Click += async (_, _) => await BuildPatchAsync();
        _purpleChoicePanel.ChoiceChanged += (_, e) => OnPurpleChoiceChanged(e);

        KeyDown += OnFormKeyDown;

        Shown += async (_, _) =>
        {
            LoadCurrentConfig();

            if (!string.IsNullOrWhiteSpace(_autoScreenshotPath))
            {
                if (Environment.GetEnvironmentVariable("SKIP_ANALYSIS_FOR_SCREENSHOT") == "1")
                {
                    await CaptureScreenshotAsync(_autoScreenshotPath!);
                    if (_exitAfterScreenshot)
                        Close();
                    return;
                }

                await RunAnalysisAsync(true);
                try
                {
                    await _modCheckTask;
                }
                catch
                {
                    // 截图不因核查失败而中断
                }

                await CaptureScreenshotAsync(_autoScreenshotPath!);
                if (_exitAfterScreenshot)
                    Close();
            }
        };

        FormClosing += (_, _) =>
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            _agentCts?.Cancel();
            _agentCts?.Dispose();
            _agentCts = null;

            _investigator?.Dispose();
            _investigator = null;

            _chromeTips.Dispose();
        };

        UpdateCommandStates();

        // 启动即解析生效路径（用户设置 → 自动检测 → 内置默认），供后续所有扫描使用
        var (initialInstall, initialWorkshop, _) = ModListResolver.PathOptions.Reload();
        _configPath = ModListResolver.DefaultModsConfigPath;
        System.Diagnostics.Debug.WriteLine($"[PathOptions] install={initialInstall} workshop={initialWorkshop}");
    }

    // ───────────────────────────── 设置 ─────────────────────────────

    /// <summary>打开路径设置对话框；保存后刷新路径相关状态。</summary>
    private void ShowSettingsDialog()
    {
        using var dialog = new SettingsDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            SetStatus("已关闭设置");
            return;
        }

        var (installDir, workshopDir, _) = ModListResolver.PathOptions.Reload();
        _configPath = ModListResolver.DefaultModsConfigPath;

        var saved = ModListResolver.PathOptions.Settings;
        _verifyGabs = saved.VerifyWithGabs;
        _includeWorkshop = saved.IncludeWorkshopSubscriptions || _includeWorkshopFromCli;

        if (_lastResult != null)
        {
            _investigator?.Dispose();
            _investigator = new AgentInvestigator(
                AgentConfigLoader.Load(saved),
                _lastResult.Mods,
                ModListResolver.PathOptions.RimWorldInstallDir);

            // 模型配置变化后，旧的模型结论不再可信；本地结论仍可复用
            foreach (var key in _agentReports
                         .Where(pair => !pair.Value.Mode.Equals("本地代码核查", StringComparison.Ordinal))
                         .Select(pair => pair.Key)
                         .ToList())
            {
                _agentReports.Remove(key);
            }

            UpdateDetailForMod();
        }

        SetStatus($"设置已保存：游戏目录 {installDir} · Agent {(_investigator?.ModeLabel ?? "本地代码核查")}");
        if (_lastResult != null)
            SetStatus("路径或 Agent 设置已变更；路径变化时建议重新执行分析");
    }

    private void BuildMenus()
    {
        _importMenu = DarkContextMenu.Create();
        _importMenu.Items.Add(DarkContextMenu.CreateItem("导入当前配置", (_, _) => LoadCurrentConfig(), "Ctrl+R"));
        _importMenu.Items.Add(DarkContextMenu.CreateItem("导入本地列表", (_, _) => LoadLocalModList(), "Ctrl+L"));
        _importMenu.Items.Add(DarkContextMenu.CreateItem("导入工坊订阅列表", (_, _) => LoadWorkshopSubscribedList(), "Ctrl+W"));
        _importMenu.Items.Add(new ToolStripSeparator());
        _importMenu.Items.Add(DarkContextMenu.CreateItem("自定义导入(文件浏览器)", (_, _) => ImportList(), "Ctrl+O"));

        _exportMenu = DarkContextMenu.Create();
        _exportMenu.Items.Add(DarkContextMenu.CreateItem("导出 ModsConfig.xml", (_, _) => ExportModsConfig()));
        _exportMenu.Items.Add(DarkContextMenu.CreateItem("导出 JSON 报告", (_, _) => ExportJson()));
        _exportMenu.Items.Add(DarkContextMenu.CreateItem("导出 Markdown 报告", (_, _) => ExportMarkdown()));

        _btnImport.Click += (_, _) =>
        {
            if (!_busy)
                _importMenu.Show(_btnImport, 0, _btnImport.Height);
        };
        _btnImport.ContextMenuStrip = _importMenu;

        _btnExport.Click += (_, _) =>
        {
            if (_lastResult != null && !_busy)
                _exportMenu.Show(_btnExport, 0, _btnExport.Height);
        };
        _btnExport.ContextMenuStrip = _exportMenu;

        _btnSortLists.Click += (_, _) =>
        {
            if (!_busy)
            {
                RebuildProfileMenu();
                _profileMenu?.Show(_btnSortLists, 0, _btnSortLists.Height);
            }
        };

        RefreshSortListsButton();
    }

    // ───────────────────────────── 排序列表 ─────────────────────────────

    /// <summary>重建"排序列表"下拉菜单：已保存档案（点击切换激活）+ 保存/删除入口。</summary>
    private void RebuildProfileMenu()
    {
        _profileMenu?.Dispose();
        _profileMenu = DarkContextMenu.Create();

        var profiles = _profileStore.Profiles;
        if (profiles.Count == 0)
        {
            var empty = DarkContextMenu.CreateItem("暂无保存的排序，分析后可保存", (_, _) => { });
            empty.Enabled = false;
            _profileMenu.Items.Add(empty);
        }
        else
        {
            foreach (var profile in profiles)
            {
                var isActive = string.Equals(
                    _profileStore.ActiveProfileName, profile.Name, StringComparison.OrdinalIgnoreCase);
                var label = (isActive ? "● " : "○ ") + profile.DisplayName;
                var captured = profile;
                _profileMenu.Items.Add(DarkContextMenu.CreateItem(label, (_, _) => ActivateProfile(captured)));
            }
        }

        _profileMenu.Items.Add(new ToolStripSeparator());
        _profileMenu.Items.Add(DarkContextMenu.CreateItem(
            "保存当前分析为排序…", (_, _) => SaveCurrentResultAsProfile(), "Ctrl+S"));

        if (profiles.Count > 0)
        {
            var deleteMenu = new ToolStripMenuItem("删除排序 ▾")
            {
                ForeColor = UiTheme.TextPrimary,
                BackColor = Color.Transparent,
                Font = UiTheme.BodyFont,
                Padding = new Padding(12, 6, 16, 6)
            };
            foreach (var profile in profiles)
            {
                var captured = profile;
                deleteMenu.DropDownItems.Add(DarkContextMenu.CreateItem(
                    profile.DisplayName, (_, _) => DeleteProfile(captured)));
            }
            _profileMenu.Items.Add(deleteMenu);
        }

        _btnSortLists.ContextMenuStrip = _profileMenu;
    }

    /// <summary>启动区按钮显示当前激活的排序名，无激活时显示占位文案。</summary>
    private void RefreshSortListsButton()
    {
        var active = _profileStore.GetActiveProfile();
        _btnSortLists.Text = active == null
            ? "排序列表 ▾"
            : $"排序: {TrimForButton(active.Name)} ({active.PackageIds.Count}) ▾";
    }

    private static string TrimForButton(string name)
    {
        const int maxChars = 14;
        return name.Length <= maxChars ? name : name[..(maxChars - 1)] + "…";
    }

    /// <summary>
    /// 切换激活排序：更新激活标记后立即按快照写入 ModsConfig.xml（写前自动备份）。
    /// </summary>
    private void ActivateProfile(SortListProfile profile)
    {
        _profileStore.SetActive(profile.Name);
        RefreshSortListsButton();

        var target = ModListResolver.DefaultModsConfigPath;
        try
        {
            var backup = ModSortReportBuilder.ApplyPackageIdsWithBackup(
                target, profile.GameVersion, profile.PackageIds, profile.KnownExpansions);

            SetStatus(backup == null
                ? $"已切换到排序「{profile.Name}」并写入 {target}（原文件不存在，未生成备份）"
                : $"已切换到排序「{profile.Name}」并写入 {target}，备份：{Path.GetFileName(backup)}");
        }
        catch (Exception ex)
        {
            ShowFriendlyError($"切换排序「{profile.Name}」失败", ex);
        }
    }

    /// <summary>把最近一次分析的推荐排序（仅启用项）保存为一份可切换的排序列表，并设为激活。</summary>
    private void SaveCurrentResultAsProfile()
    {
        if (_lastResult == null)
        {
            SetStatus("尚未分析，无法保存排序列表");
            return;
        }

        var activeIds = GetEffectiveActivePackageIds();
        if (activeIds.Count == 0)
        {
            SetStatus("当前分析没有启用模组，未保存排序");
            return;
        }

        var defaultName = $"排序 {DateTime.Now:MM-dd HH:mm}";
        var name = PromptProfileName("保存排序列表", defaultName);
        if (string.IsNullOrWhiteSpace(name))
            return;

        name = name.Trim();
        var overwritten = _profileStore.NameExists(name);
        var profile = new SortListProfile(
            name,
            DateTime.UtcNow,
            _lastResult.EffectiveGameVersion,
            activeIds,
            _lastResult.KnownExpansions.ToList(),
            _lastResult.Report.TotalModsAnalyzed,
            _lastResult.Report.RedModCount,
            _lastResult.Report.YellowModCount);

        _profileStore.Upsert(profile);
        _profileStore.SetActive(profile.Name);
        RefreshSortListsButton();

        SetStatus(overwritten
            ? $"已覆盖排序「{name}」（{activeIds.Count} 项）并设为激活，启动游戏前将自动备份并同步"
            : $"已保存排序「{name}」（{activeIds.Count} 项）并设为激活，启动游戏前将自动备份并同步");
    }

    /// <summary>
    /// 分析完成后自动把推荐排序（仅启用项）保存为固定名「自动保存」的排序档并设为激活。
    /// 覆盖式更新，不弹窗；手动「保存当前分析为排序…」仍可另存命名档。
    /// </summary>
    private bool AutoSaveResultAsProfile()
    {
        if (_lastResult == null)
            return false;

        var activeIds = GetEffectiveActivePackageIds();
        if (activeIds.Count == 0)
            return false;

        const string autoSaveName = "自动保存";
        var profile = new SortListProfile(
            autoSaveName,
            DateTime.UtcNow,
            _lastResult.EffectiveGameVersion,
            activeIds,
            _lastResult.KnownExpansions.ToList(),
            _lastResult.Report.TotalModsAnalyzed,
            _lastResult.Report.RedModCount,
            _lastResult.Report.YellowModCount);

        _profileStore.Upsert(profile);
        _profileStore.SetActive(autoSaveName);
        RefreshSortListsButton();
        RebuildProfileMenu();
        return true;
    }

    private void DeleteProfile(SortListProfile profile)
    {
        var confirm = MessageBox.Show(
            this,
            $"确定删除排序「{profile.Name}」？\n\n此操作只删除工具内的排序快照，不影响游戏配置文件。",
            "删除排序",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (confirm != DialogResult.OK)
            return;

        _profileStore.Delete(profile.Name);
        RefreshSortListsButton();
        SetStatus($"已删除排序「{profile.Name}」");
    }

    /// <summary>暗色主题的文本输入对话框，返回输入内容（取消时返回 null）。</summary>
    private string? PromptProfileName(string title, string defaultName)
    {
        using var form = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(360, 118),
            BackColor = UiTheme.SurfaceBg,
            ForeColor = UiTheme.TextPrimary,
            Font = UiTheme.BodyFont,
            ShowInTaskbar = false
        };

        var label = new Label
        {
            Text = "排序名称：",
            AutoSize = true,
            ForeColor = UiTheme.TextSecondary,
            Location = new Point(14, 18)
        };

        var input = new TextBox
        {
            Text = defaultName,
            ForeColor = UiTheme.TextPrimary,
            BackColor = UiTheme.CardBg,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(14, 40),
            Width = 330,
            SelectionStart = 0,
            SelectionLength = defaultName.Length
        };

        var btnCancel = new ModernButton("取消", ButtonVariant.Ghost)
        {
            Location = new Point(178, 74),
            Height = 30
        };
        var btnOk = new ModernButton("保存", ButtonVariant.Primary)
        {
            Location = new Point(268, 74),
            Height = 30
        };

        string? result = null;
        btnOk.Click += (_, _) =>
        {
            result = input.Text;
            form.Close();
        };
        btnCancel.Click += (_, _) => form.Close();
        input.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                result = input.Text;
                e.SuppressKeyPress = true;
                form.Close();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                form.Close();
            }
        };
        form.AcceptButton = null;

        form.Controls.AddRange(new Control[] { label, input, btnCancel, btnOk });
        form.ShowDialog(this);
        return result;
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        // Agent 复核/补丁生成期间禁止换列表或改设置，避免复盘到一半状态被清空。
        if (_agentBusy && e.Control &&
            e.KeyCode is Keys.O or Keys.R or Keys.L or Keys.W or Keys.Oemcomma)
        {
            e.Handled = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.S)
        {
            SaveCurrentResultAsProfile();
            e.Handled = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.O)
        {
            ImportList();
            e.Handled = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.R)
        {
            LoadCurrentConfig();
            e.Handled = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.L)
        {
            LoadLocalModList();
            e.Handled = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.W)
        {
            LoadWorkshopSubscribedList();
            e.Handled = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.F)
        {
            _modSearch.FocusInput();
            e.Handled = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.Enter)
        {
            ApplyToGameConfig();
            e.Handled = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.Oemcomma)
        {
            ShowSettingsDialog();
            e.Handled = true;
            return;
        }

        switch (e.KeyCode)
        {
            case Keys.F5:
                if (!_busy && !_agentBusy)
                {
                    _ = RunAnalysisAsync(false);
                    e.Handled = true;
                }
                break;

            case Keys.Escape:
                if (_busy)
                {
                    _cts?.Cancel();
                }
                else if (_agentBusy)
                {
                    _agentCts?.Cancel();
                }
                else
                {
                    _modSearch.Clear();
                }
                e.Handled = true;
                break;
        }
    }

    private void OnModFilterChipClicked(ModQuickFilter filter)
    {
        _modFilter = filter;
        SyncModChips();
        RefreshModList();
    }

    /// <summary>
    /// 把筛选状态同步回筛选条。注意"仅看顺位变动"没有常驻 chip（由顶部 KPI 卡触发），
    /// 因此这里按枚举逐一比对，而不是按下标对齐。
    /// </summary>
    private void SyncModChips()
    {
        _chipAll.Checked = _modFilter == ModQuickFilter.All;
        _chipPurple.Checked = _modFilter == ModQuickFilter.Purple;
        _chipRed.Checked = _modFilter == ModQuickFilter.Red;
        _chipYellow.Checked = _modFilter == ModQuickFilter.Yellow;
        _chipGreen.Checked = _modFilter == ModQuickFilter.Green;
        _chipInactive.Checked = _modFilter == ModQuickFilter.Inactive;
        _chipMoved.Checked = _modFilter == ModQuickFilter.Moved;
    }

    private void OnKpiClicked(KpiCard card)
    {
        switch (card)
        {
            case KpiCard.Green:
                OnModFilterChipClicked(ModQuickFilter.Green);
                break;
            case KpiCard.Yellow:
                OnModFilterChipClicked(ModQuickFilter.Yellow);
                break;
            case KpiCard.Red:
                OnModFilterChipClicked(ModQuickFilter.Red);
                break;
            case KpiCard.Purple:
                OnModFilterChipClicked(ModQuickFilter.Purple);
                break;
            case KpiCard.Movement:
                OnModFilterChipClicked(ModQuickFilter.Moved);
                if (_lastResult != null)
                {
                    _detailCard.HeaderTitle = "排序变动预览";
                    _detailCard.HeaderMeta = $"{_lastResult.Report.MovedModCount} 项变动";
                    _inspector.ShowDocument(BuildChangesDocument(_lastResult.Report));
                }
                break;
            default:
                OnModFilterChipClicked(ModQuickFilter.All);
                break;
        }
    }

    // ───────────────────────────── 数据加载 ─────────────────────────────

    /// <summary>导入新列表时彻底清掉上一次的冲突、核查与补丁状态，避免旧结论套到新列表上。</summary>
    private void ClearAnalysisState()
    {
        _lastResult = null;
        _playerConflicts = Array.Empty<PlayerConflict>();
        _activePackageIdSet.Clear();
        _agentReports.Clear();
        _bundledConflictKeys.Clear();
        _patchBundle = null;
        _bundleInstalledPath = null;
        _checkingKeys.Clear();

        _modConflicts = new List<PlayerConflict>();
        _selectedMod = null;
        _modCheckTask = Task.CompletedTask;
        _purpleChoices.Clear();
        _purpleChoicePanel.SetChoices(Array.Empty<PurpleChoiceRow>());

        _investigator?.Dispose();
        _investigator = null;

        _detailCard.HeaderTitle = "冲突详情";
        _detailCard.HeaderMeta = "未选择";
        _inspector.ShowDocument(null);
        UpdateCommandStates();
    }

    private void LoadCurrentConfig()
    {
        try
        {
            if (!File.Exists(ModListResolver.DefaultModsConfigPath))
            {
                SetStatus("未找到 ModsConfig.xml");
                return;
            }

            var resolver = new ModListResolver();
            var (version, mods, expansions) = resolver.ParseModsConfigFile(ModListResolver.DefaultModsConfigPath);

            _gameVersion = version;
            _knownExpansions = expansions;
            _tokens = mods;
            _configPath = ModListResolver.DefaultModsConfigPath;
            ClearAnalysisState();

            _kpi.UpdateState(null);
            _progress.Reset();
            _modCard.HeaderMeta = $"{mods.Count} 项 · 待分析";
            _modList.SetRows(Array.Empty<UiRow>(), preserveSelection: false);
            _modList.EmptyText = "已读取列表，尚未分析";
            _modList.EmptyHint = $"当前配置包含 {mods.Count} 项模组";
            _inspector.ShowDocument(null);

            SetStatus($"已读取 ModsConfig.xml：{mods.Count} 项模组，游戏版本 {version}");
            UpdateCommandStates();
        }
        catch (Exception ex)
        {
            ShowFriendlyError("读取配置失败", ex);
        }
    }

    private void ImportList()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "导入模组列表",
            Filter = "模组列表或配置文件 (*.xml;*.json;*.txt;*.rml)|*.xml;*.json;*.txt;*.rml|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var resolver = new ModListResolver();
            var tokens = resolver.ParseInputFileTokens(dialog.FileName, out var version);

            if (!string.IsNullOrWhiteSpace(version))
                _gameVersion = version!;

            _configPath = dialog.FileName;
            _tokens = tokens;
            ClearAnalysisState();

            _modList.EmptyText = "已导入列表，尚未分析";
            _modList.EmptyHint = $"共 {tokens.Count} 项模组";
            _modCard.HeaderMeta = $"{tokens.Count} 项 · 待分析";

            SetStatus($"已导入 {tokens.Count} 项模组：{Path.GetFileName(dialog.FileName)}");
            UpdateCommandStates();
        }
        catch (Exception ex)
        {
            ShowFriendlyError("导入列表失败", ex);
        }
    }

    private void LoadLocalModList()
    {
        try
        {
            var resolver = new ModListResolver();
            var tokens = resolver.GetLocalModTokens();
            if (tokens.Count == 0)
            {
                SetStatus("未扫描到本地模组，请检查 RimWorld 安装目录与 Mods 文件夹");
                return;
            }

            _gameVersion = "1.7";
            _knownExpansions = resolver.GetInstalledOfficialExpansions();
            _tokens = tokens;
            _configPath = Path.Combine(ModListResolver.PathOptions.RimWorldInstallDir, "Mods");
            ClearAnalysisState();

            _kpi.UpdateState(null);
            _progress.Reset();
            _modCard.HeaderMeta = $"{tokens.Count} 项 · 待分析";
            _modList.SetRows(Array.Empty<UiRow>(), preserveSelection: false);
            _modList.EmptyText = "已导入本地模组列表，尚未分析";
            _modList.EmptyHint = $"共扫描到 {tokens.Count} 项本地模组（含官方内容及 Mods 目录模组）";
            _inspector.ShowDocument(null);

            SetStatus($"已导入本地模组列表：共 {tokens.Count} 项模组（含官方内容及本地 Mods 目录模组）");
            UpdateCommandStates();
        }
        catch (Exception ex)
        {
            ShowFriendlyError("导入本地列表失败", ex);
        }
    }

    private void LoadWorkshopSubscribedList()
    {
        try
        {
            var resolver = new ModListResolver();
            var tokens = resolver.GetWorkshopSubscribedTokens();
            if (tokens.Count == 0)
            {
                SetStatus("未扫描到创意工坊已订阅模组，请检查 Steam 创意工坊目录或订阅状态");
                return;
            }

            _gameVersion = "1.7";
            _knownExpansions = resolver.GetInstalledOfficialExpansions();
            _tokens = tokens;
            _configPath = ModListResolver.PathOptions.WorkshopDir;
            ClearAnalysisState();

            _kpi.UpdateState(null);
            _progress.Reset();
            _modCard.HeaderMeta = $"{tokens.Count} 项 · 待分析";
            _modList.SetRows(Array.Empty<UiRow>(), preserveSelection: false);
            _modList.EmptyText = "已导入工坊订阅列表，尚未分析";
            _modList.EmptyHint = $"共扫描到 {tokens.Count} 项工坊订阅模组（含官方内容）";
            _inspector.ShowDocument(null);

            SetStatus($"已导入工坊订阅列表：共 {tokens.Count} 项模组（含官方内容及 Steam 创意工坊已订阅模组）");
            UpdateCommandStates();
        }
        catch (Exception ex)
        {
            ShowFriendlyError("导入工坊订阅列表失败", ex);
        }
    }

    private void LaunchGame()
    {
        // 启动前处理激活排序列表：需要同步时写入（自动备份），已同步时也做一次进游戏前快照
        var syncMessage = SyncActiveProfileBeforeLaunch();
        if (syncMessage == AbortLaunch)
            return;

        var (success, message) = ModListResolver.LaunchGame();
        var finalMessage = syncMessage.Length > 0 ? $"{syncMessage} · {message}" : message;
        SetStatus(finalMessage);
        if (!success)
        {
            MessageBox.Show(this, finalMessage, "启动游戏", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>特殊的同步结果标记：同步阶段已弹窗告知错误，启动流程中止。</summary>
    private const string AbortLaunch = "\u0001ABORT";

    /// <summary>
    /// 启动游戏前同步激活排序：
    /// 1. 无激活排序 → 仅对当前 ModsConfig.xml 做一次启动前快照备份；
    /// 2. 磁盘已与激活排序一致 → 直接启动（不再重复写盘）；
    /// 3. 不一致 → 先备份再写入快照。
    /// 返回状态栏附加消息（无需处理时为空串）；失败时弹窗并返回 <see cref="AbortLaunch"/>。
    /// </summary>
    private string SyncActiveProfileBeforeLaunch()
    {
        var target = ModListResolver.DefaultModsConfigPath;
        var profile = _profileStore.GetActiveProfile();

        try
        {
            // 无激活排序：仍然做启动前快照，保证每次进游戏都有可回滚的备份
            if (profile == null)
            {
                var snapshot = ModSortReportBuilder.BackupModsConfigFile(target);
                return snapshot == null
                    ? string.Empty
                    : $"启动前备份：{Path.GetFileName(snapshot)}";
            }

            var expected = profile.PackageIds
                .Select(id => id.Trim().ToLowerInvariant())
                .Where(id => id.Length > 0)
                .ToList();

            ModSortReportBuilder.TryReadActiveMods(target, out _, out var current);
            var currentSet = current;
            if (currentSet.Count == expected.Count &&
                currentSet.SequenceEqual(expected))
            {
                return $"排序「{profile.Name}」已同步，无需写入";
            }

            var backup = ModSortReportBuilder.ApplyPackageIdsWithBackup(
                target, profile.GameVersion, profile.PackageIds, profile.KnownExpansions);

            return backup == null
                ? $"已按排序「{profile.Name}」写入 ModsConfig.xml（原文件不存在，未生成备份）"
                : $"已按排序「{profile.Name}」写入 ModsConfig.xml，备份：{Path.GetFileName(backup)}";
        }
        catch (Exception ex)
        {
            ShowFriendlyError("启动前同步排序失败", ex);
            return AbortLaunch;
        }
    }

    // ───────────────────────────── 分析 ─────────────────────────────

    private async Task RunAnalysisAsync(bool autoSelectForScreenshot)
    {
        if (_busy)
            return;

        if (_tokens.Count == 0)
        {
            SetStatus("列表为空");
            return;
        }

        _busy = true;
        UpdateCommandStates();

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _progress.Begin("正在启动分析…");
        SetStatus("正在分析…");

        var request = new PipelineRequest(
            ModTokens: _tokens.ToList(),
            TargetGameVersion: _gameVersion,
            KnownExpansions: _knownExpansions.ToList(),
            VerifyWithGabs: _verifyGabs,
            ModsConfigPath: _configPath,
            IncludeWorkshopSubscriptions: _includeWorkshop);

        var progress = new Progress<PipelineProgressReport>(report =>
        {
            if (IsDisposed)
                return;

            _progress.UpdateProgress(report);
            SetStatus(report.StatusText);
        });

        try
        {
            var result = await Task.Run(() => _pipeline.Run(request, progress, null, token), token);

            _lastResult = result;
            PrepareConflictWorkspace(result);
            BindReport(result.Report);
            _progress.Finish($"分析完成 · {result.Report.TotalModsAnalyzed} 项模组 · 绿色 {result.Report.GreenModCount} / 黄色 {result.Report.YellowModCount} / 红色 {result.Report.RedModCount}",
                result.Report.AnalysisDurationMs);

            var autoSaved = AutoSaveResultAsProfile();
            SetStatus(BuildCompletionSummary(result.Report) +
                      (autoSaved ? " · 已自动保存排序档「自动保存」" : string.Empty));

            if (autoSelectForScreenshot)
                SelectInterestingModForScreenshot();
        }
        catch (OperationCanceledException)
        {
            _progress.Fail("分析已中止");
            SetStatus("分析已中止，未写入任何文件");
        }
        catch (Exception ex)
        {
            _progress.Fail("分析失败");
            SetStatus("分析失败");
            ShowFriendlyError("分析失败", ex);
        }
        finally
        {
            _busy = false;
            _cts?.Dispose();
            _cts = null;
            UpdateCommandStates();
        }
    }

    private string BuildCompletionSummary(ModSortReport report)
    {
        var gabs = report.GabsVerification?.Connected == true ? "实机已核验" : "离线日志核验";
        var purple = _playerConflicts.Count(c => c.Grade == ConflictGrade.Purple);
        var red = _playerConflicts.Count(c => c.Grade == ConflictGrade.Red);
        var yellow = _playerConflicts.Count(c => c.Grade == ConflictGrade.Yellow);
        var conflictText = _playerConflicts.Count == 0
            ? "没有发现需要处理的冲突"
            : $"需要处理 {_playerConflicts.Count} 项冲突（作者声明 {purple} / 明确冲突 {red} / 需要确认 {yellow}）";

        return $"分析完成：{report.TotalModsAnalyzed} 项模组 · {conflictText} · {gabs}";
    }

    /// <summary>
    /// 分析完成后准备右侧详情所需的数据：玩家视角冲突清单 + 带真实代码取证的 Agent。
    /// 列表仍保持原有排序/评级视图，不在这里改动。
    /// </summary>
    private void PrepareConflictWorkspace(PipelineResult result)
    {
        _playerConflicts = ConflictNarrative.Build(result.Report);
        _activePackageIdSet.Clear();
        foreach (var mod in result.Report.SortedMods.Where(m => m.IsActive))
            _activePackageIdSet.Add(mod.PackageId);

        _agentReports.Clear();
        _bundledConflictKeys.Clear();
        _patchBundle = null;
        _bundleInstalledPath = null;
        _checkingKeys.Clear();

        _modConflicts = new List<PlayerConflict>();
        _modCheckTask = Task.CompletedTask;
        InitializePurpleChoices();

        _investigator?.Dispose();
        _investigator = new AgentInvestigator(
            AgentConfigLoader.Load(ModListResolver.PathOptions.Settings),
            result.Mods,
            ModListResolver.PathOptions.RimWorldInstallDir);
    }

    private void BindReport(ModSortReport report)
    {
        _kpi.UpdateState(report);
        RefreshModList();

        _modCard.HeaderMeta = report.WorkshopPoolModCount > 0
            ? $"{report.TotalModsAnalyzed} 项 · 启用 {report.TotalModsAnalyzed - report.WorkshopPoolModCount} / 订阅池 {report.WorkshopPoolModCount}"
            : $"{report.TotalModsAnalyzed} 项 · 变动 {report.MovedModCount} · 分层错位 {report.TierInversionCount}";

        UpdateCommandStates();
    }

    private void SelectInterestingModForScreenshot()
    {
        var target = _modList;
        var rows = _lastResult?.Report.SortedMods;
        if (rows == null)
            return;

        var candidate = rows.FirstOrDefault(m => m.RedConflictCount >= 1) ??
                        rows.FirstOrDefault(m => m.YellowUncertaintyCount >= 1) ??
                        rows.FirstOrDefault();

        if (candidate == null)
            return;

        var index = rows.ToList().FindIndex(m => ReferenceEquals(m, candidate));
        if (index >= 0)
            target.SelectIndex(index);
    }

    // ───────────────────────────── 列表绑定 ─────────────────────────────

    private void RefreshModList()
    {
        if (_lastResult == null)
            return;

        var query = _modSearch.SearchText.Trim();
        var rows = new List<UiRow>();
        var summaries = _lastResult.Report.SortedMods;
        var excludedIds = BuildPurpleExclusionSet();

        foreach (var mod in summaries)
        {
            if (_modFilter == ModQuickFilter.Purple && mod.Grade != ConflictGrade.Purple)
                continue;
            if (_modFilter == ModQuickFilter.Red && mod.Grade != ConflictGrade.Red)
                continue;
            if (_modFilter == ModQuickFilter.Yellow && mod.Grade != ConflictGrade.Yellow)
                continue;
            if (_modFilter == ModQuickFilter.Green && mod.Grade != ConflictGrade.Green)
                continue;
            if (_modFilter == ModQuickFilter.Moved && (!mod.IsActive || excludedIds.Contains(mod.PackageId) || mod.OrderDelta == 0))
                continue;
            if (_modFilter == ModQuickFilter.Inactive && mod.IsActive)
                continue;

            if (query.Length > 0 &&
                mod.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                mod.PackageId.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                mod.ArchitectureTierLabel.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                (mod.WorkshopId ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            var excluded = excludedIds.Contains(mod.PackageId);
            var delta = !mod.IsActive
                ? "新增"
                : excluded
                    ? "排除"
                    : mod.OrderDelta switch
                    {
                        > 0 => $"▲{mod.OrderDelta}",
                        < 0 => $"▼{Math.Abs(mod.OrderDelta)}",
                        _ => "—"
                    };

            var conflictText = mod.PurpleConflictCount > 0
                ? $"{mod.PurpleConflictCount} 紫"
                : mod.RedConflictCount > 0
                    ? $"{mod.RedConflictCount} 红"
                    : mod.YellowUncertaintyCount > 0
                        ? $"{mod.YellowUncertaintyCount} 黄"
                        : "—";

            rows.Add(new UiRow(new[]
            {
                new UiCell(mod.RecommendedOrderIndex.ToString(), CellKind.Number),
                new UiCell(delta, CellKind.Delta),
                new UiCell(reportGradeText(mod.Grade), CellKind.Grade),
                new UiCell(excluded ? "已排除" : mod.IsActive ? "已启用" : "未启用", CellKind.Status),
                new UiCell(mod.Name, CellKind.Emphasis),
                new UiCell(mod.ArchitectureTierLabel, CellKind.Tier),
                new UiCell(conflictText, CellKind.Text),
                new UiCell(mod.HarmonyPatchCount.ToString(), CellKind.Number)
            }, mod));
        }

        _modList.EmptyText = query.Length > 0 || _modFilter != ModQuickFilter.All
            ? "没有符合条件的模组"
            : "尚无排序结果";
        _modList.EmptyHint = string.Empty;

        _modList.SetRows(rows);

        var report = _lastResult.Report;
        _modCard.HeaderMeta = report.WorkshopPoolModCount > 0
            ? $"{rows.Count}/{report.TotalModsAnalyzed} 项 · 启用 {report.TotalModsAnalyzed - report.WorkshopPoolModCount} / 订阅池 {report.WorkshopPoolModCount}"
            : $"{rows.Count}/{report.TotalModsAnalyzed} 项 · 变动 {report.MovedModCount}";

        if (rows.Count > 0 && _modList.SelectedIndex < 0)
            _modList.SelectIndex(0);
    }

    private static string reportGradeText(ConflictGrade grade)
    {
        return grade switch
        {
            ConflictGrade.Purple => "4.紫色",
            ConflictGrade.Red => "3.红色",
            ConflictGrade.Yellow => "2.黄色",
            _ => "1.绿色"
        };
    }

    // ───────────────────────────── 选择联动 ─────────────────────────────

    private void OnModSelectionChanged()
    {
        if (_modList.SelectedRow?.Tag is not ModGradeSummary mod)
            return;

        _selectedMod = mod;
        _modConflicts = _playerConflicts
            .Where(c => Involves(c, mod.PackageId))
            .OrderByDescending(c => c.Grade)
            .ThenBy(c => c.InvolvesInactiveMod)
            .ToList();

        UpdateDetailForMod();
        UpdateCommandStates();

        _modCheckTask = EnsureModConflictsCheckedAsync(mod);
    }

    private static bool Involves(PlayerConflict conflict, string packageId)
        => conflict.ModAPackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase) ||
           conflict.ModBPackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase);

    private static string OtherModName(PlayerConflict conflict, string packageId)
        => conflict.ModAPackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase)
            ? conflict.ModBName
            : conflict.ModAName;

    // ───────────────────────────── 紫色冲突选择 ─────────────────────────────

    private void InitializePurpleChoices()
    {
        _purpleChoices.Clear();
        foreach (var conflict in _playerConflicts.Where(IsPurpleChoiceApplicable))
            _purpleChoices[conflict.Key] = PurpleChoice.Both;
    }

    /// <summary>只有双方都在启用列表里，作者声明的互斥才需要玩家做取舍。</summary>
    private bool IsPurpleChoiceApplicable(PlayerConflict conflict)
    {
        if (conflict.Grade != ConflictGrade.Purple)
            return false;

        return !conflict.ModAPackageId.Equals(conflict.ModBPackageId, StringComparison.OrdinalIgnoreCase) &&
               _activePackageIdSet.Contains(conflict.ModAPackageId) &&
               _activePackageIdSet.Contains(conflict.ModBPackageId);
    }

    private PurpleChoice GetPurpleChoice(string conflictKey)
        => _purpleChoices.TryGetValue(conflictKey, out var choice) ? choice : PurpleChoice.Both;

    /// <summary>只选一边时，被放弃的一方从生效列表里排除；两边都选则都不排除。</summary>
    private HashSet<string> BuildPurpleExclusionSet()
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var conflict in _playerConflicts)
        {
            if (!IsPurpleChoiceApplicable(conflict))
                continue;

            switch (GetPurpleChoice(conflict.Key))
            {
                case PurpleChoice.A:
                    excluded.Add(conflict.ModBPackageId);
                    break;
                case PurpleChoice.B:
                    excluded.Add(conflict.ModAPackageId);
                    break;
            }
        }

        return excluded;
    }

    /// <summary>
    /// 深度核查与兼容生成的作用域：全部生效冲突。
    /// 紫色冲突只有选择"两边都保留"且双方都仍生效时才纳入；
    /// 只保留一方时冲突已通过排除解决，不再进入补丁范围。
    /// </summary>
    private List<PlayerConflict> GetAgentScopeConflicts()
    {
        if (_lastResult == null)
            return new List<PlayerConflict>();

        var excluded = BuildPurpleExclusionSet();
        var effective = new HashSet<string>(
            _lastResult.Report.SortedMods
                .Where(m => m.IsActive && !excluded.Contains(m.PackageId))
                .Select(m => m.PackageId),
            StringComparer.OrdinalIgnoreCase);

        return _playerConflicts
            .GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Where(c => c.Grade != ConflictGrade.Green)
            .Where(c =>
            {
                if (c.Grade == ConflictGrade.Purple)
                    return IsPurpleChoiceApplicable(c) &&
                           GetPurpleChoice(c.Key) == PurpleChoice.Both &&
                           effective.Contains(c.ModAPackageId) &&
                           effective.Contains(c.ModBPackageId);

                if (c.Category.Equals("MissingDependency", StringComparison.Ordinal))
                    return effective.Contains(c.ModAPackageId);

                return effective.Contains(c.ModAPackageId) && effective.Contains(c.ModBPackageId);
            })
            .OrderByDescending(c => c.Grade)
            .ThenBy(c => c.PairLabel, StringComparer.CurrentCulture)
            .ToList();
    }

    private HashSet<string> GetApprovedExplicitKeys(IEnumerable<PlayerConflict> conflicts)
        => conflicts
            .Where(CompatPatchEligibility.IsAuthorDeclared)
            .Select(c => c.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private void UpdatePurplePanel()
    {
        if (_selectedMod == null)
        {
            _purpleChoicePanel.SetChoices(Array.Empty<PurpleChoiceRow>());
            return;
        }

        var rows = _playerConflicts
            .Where(c => IsPurpleChoiceApplicable(c) && Involves(c, _selectedMod.PackageId))
            .OrderBy(c => c.ModAName, StringComparer.CurrentCulture)
            .Select(c => new PurpleChoiceRow(c.Key, c.ModAName, c.ModBName, GetPurpleChoice(c.Key)))
            .ToList();

        _purpleChoicePanel.SetChoices(rows);
    }

    private void OnPurpleChoiceChanged(PurpleChoiceChangedEventArgs e)
    {
        _purpleChoices[e.ConflictKey] = e.Choice;

        var conflict = _playerConflicts.FirstOrDefault(c =>
            c.Key.Equals(e.ConflictKey, StringComparison.OrdinalIgnoreCase));

        RefreshModList();
        UpdateCommandStates();
        PersistEffectiveSelectionToAutoProfile();

        if (conflict != null)
        {
            var kept = e.Choice switch
            {
                PurpleChoice.A => conflict.ModAName,
                PurpleChoice.B => conflict.ModBName,
                _ => string.Empty
            };

            SetStatus(e.Choice == PurpleChoice.Both
                ? $"「{conflict.PairLabel}」双方都保留，已纳入兼容生成范围"
                : $"仅保留「{kept}」，另一方将从排序写入与导出中排除");
        }

        // 点击事件来自选择条内部，等它返回后再重建详情，避免销毁正在处理事件的控件
        if (!IsDisposed)
            BeginInvoke(new Action(UpdateDetailForMod));
    }

    private void UpdateDetailForMod()
    {
        if (_selectedMod == null)
        {
            _detailCard.HeaderTitle = "冲突详情";
            _detailCard.HeaderMeta = "未选择";
            UpdatePurplePanel();
            _inspector.ShowDocument(null);
            UpdateCommandStates();
            return;
        }

        var verified = _modConflicts.Count(c => _agentReports.ContainsKey(c.Key));
        var checking = _modConflicts.Count(c => _checkingKeys.Contains(c.Key));
        _detailCard.HeaderTitle = $"详情 · {_selectedMod.Name}";
        _detailCard.HeaderMeta = _modConflicts.Count == 0
            ? "没有需要处理的冲突"
            : checking > 0
                ? $"共 {_modConflicts.Count} 项 · 核查中 {checking}"
                : $"共 {_modConflicts.Count} 项 · 已核查 {verified}/{_modConflicts.Count}";

        UpdatePurplePanel();
        _inspector.ShowDocument(BuildModDocument(_selectedMod));
        UpdateCommandStates();
    }

    /// <summary>
    /// 选中模组后自动对其冲突做本地代码核查：反编译双方补丁、读取目标方法、抽取 XML 原文。
    /// 结论先落到详情里，玩家不需要手动点"开始核查"。
    /// </summary>
    private async Task EnsureModConflictsCheckedAsync(ModGradeSummary mod)
    {
        if (_investigator == null)
            return;

        var targets = _playerConflicts
            .Where(c => Involves(c, mod.PackageId) && !_agentReports.ContainsKey(c.Key))
            .OrderByDescending(c => c.Grade)
            .Take(8)
            .ToList();

        var completed = 0;
        var failed = 0;

        foreach (var conflict in targets)
        {
            if (!_checkingKeys.Add(conflict.Key))
                continue;

            try
            {
                SetStatus($"Agent 正在读取双方代码：{conflict.PairLabel}…");
                var report = await _investigator.InvestigateAsync(conflict, CancellationToken.None, useModel: false);

                // 模型结论优先：本地核查结果不得覆盖已经完成的模型核查
                if (!_agentReports.TryGetValue(report.ConflictKey, out var existing) ||
                    existing.Mode.Equals("本地代码核查", StringComparison.Ordinal))
                {
                    _agentReports[report.ConflictKey] = report;
                }

                completed++;
            }
            catch (OperationCanceledException)
            {
                // 关闭窗口或切换列表时忽略
            }
            catch (Exception ex)
            {
                failed++;
                SetStatus($"代码核查失败：{ex.Message}");
            }
            finally
            {
                _checkingKeys.Remove(conflict.Key);

                if (_selectedMod != null)
                    UpdateDetailForMod();
            }
        }

        if (completed > 0 && _selectedMod != null)
        {
            SetStatus(failed > 0
                ? $"本地代码核查完成 {completed} 项，{failed} 项失败"
                : $"本地代码核查完成：{completed} 项冲突已给出结论");
        }

        UpdateCommandStates();
    }

    // ───────────────────────────── Agent 与补丁动作 ─────────────────────────────

    private async Task DeepCheckAsync()
    {
        var conflicts = GetAgentScopeConflicts();
        if (_lastResult == null || conflicts.Count == 0)
        {
            SetStatus("当前没有需要深度核查的生效冲突");
            return;
        }

        if (_investigator?.UsesModel != true)
        {
            var answer = MessageBox.Show(
                this,
                "深度核查需要配置模型接口。\n\n本地核查已经会反编译双方补丁并读取目标方法；配置模型后，Agent 会逐项复核全部生效冲突，进一步分析隐藏隐患，并生成可编译的兼容补丁代码。\n\n是否现在打开设置？",
                "Agent 深度核查",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (answer == DialogResult.Yes)
                ShowSettingsDialog();

            return;
        }

        _agentBusy = true;
        UpdateCommandStates();

        _agentCts?.Dispose();
        _agentCts = new CancellationTokenSource();

        var completed = 0;
        var failed = 0;

        try
        {
            for (var i = 0; i < conflicts.Count; i++)
            {
                _agentCts.Token.ThrowIfCancellationRequested();
                var conflict = conflicts[i];
                SetStatus($"模型深度核查 {i + 1}/{conflicts.Count}：{conflict.PairLabel}…");

                try
                {
                    var report = await _investigator.InvestigateAsync(conflict, _agentCts.Token, useModel: true);
                    _agentReports[report.ConflictKey] = report;
                    completed++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    SetStatus($"深度核查失败：{conflict.PairLabel} · {ex.Message}");
                }

                if (_selectedMod != null)
                    UpdateDetailForMod();
            }

            SetStatus(failed > 0
                ? $"深度核查完成 {completed}/{conflicts.Count} 项，{failed} 项失败"
                : $"深度核查完成：{completed} 项生效冲突已由模型复核");
        }
        catch (OperationCanceledException)
        {
            SetStatus("已停止深度核查");
        }
        finally
        {
            _agentBusy = false;
            _agentCts?.Dispose();
            _agentCts = null;
            UpdateCommandStates();
            if (_selectedMod != null)
                UpdateDetailForMod();
        }
    }

    private async Task BuildPatchAsync()
    {
        if (_lastResult == null || _investigator == null)
            return;

        var conflicts = GetAgentScopeConflicts();

        // 是否复核、能否生成补丁由 Agent 编排决定；这里只拦完全没有候选的列表。
        if (conflicts.Count == 0)
        {
            SetStatus("当前没有需要复核的生效冲突");
            return;
        }

        if (_investigator.UsesModel != true)
        {
            var answer = MessageBox.Show(
                this,
                "兼容生成由内部 Agent 复核全部生效冲突（包含紫色冲突中玩家选择两边都保留的项），并统一生成补丁方案。\n\n当前尚未配置模型接口，Agent 只能给出本地结论和顺序建议，无法自动编写补丁代码。\n\n是否现在打开设置？",
                "生成合并兼容补丁",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (answer == DialogResult.Yes)
                ShowSettingsDialog();

            return;
        }

        var reviewCount = conflicts.Count;
        var approvedExplicitKeys = GetApprovedExplicitKeys(conflicts);

        _agentBusy = true;
        _agentCts?.Dispose();
        _agentCts = new CancellationTokenSource();
        UpdateCommandStates();
        SetStatus($"内部 Agent 正在复核 {reviewCount} 项生效冲突…");

        CompatPatchResult? patch = null;
        try
        {
            var progress = new Progress<(int Done, int Total, string Label)>(p =>
                SetStatus($"Agent 复核 {p.Done}/{p.Total} · {p.Label}"));

            var coordinator = new CompatBundleAgent(_investigator);
            var work = await coordinator.ReviewAsync(
                conflicts,
                progress,
                _agentCts.Token,
                onReport: null,
                approvedAuthorDeclared: approvedExplicitKeys);

            foreach (var report in work.Reports)
                _agentReports[report.ConflictKey] = report;

            UpdateDetailForMod();

            if (work.Entries.Count == 0)
            {
                var reasons = work.Skipped
                    .Take(8)
                    .Select(s => $"· {s.Conflict.PairLabel}：{s.Reason}");
                SetStatus("Agent 复核完成，但没有可生成补丁的冲突");
                MessageBox.Show(
                    this,
                    "Agent 已复核全部生效冲突，但没有任何一项同时满足：有代码或 XML 原文证据、给出可编译的补丁方案。\n\n" +
                    string.Join("\n", reasons),
                    "生成合并兼容补丁",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            SetStatus($"Agent 复核完成：{work.Entries.Count} 项可修复，正在编译合并补丁…");

            var settings = ModListResolver.PathOptions.Settings;
            var outputRoot = string.IsNullOrWhiteSpace(settings.AgentPatchOutputDir)
                ? AgentConfigLoader.DefaultPatchOutputDir
                : settings.AgentPatchOutputDir;

            var builder = new CompatPatchBuilder(
                ModListResolver.PathOptions.RimWorldInstallDir,
                outputRoot,
                _lastResult.EffectiveGameVersion,
                approvedExplicitKeys,
                _lastResult.Mods);

            IProgress<string> repairStatus = new Progress<string>(SetStatus);
            var build = await coordinator.BuildWithRepairAsync(
                builder,
                work,
                repairStatus.Report,
                _agentCts.Token);

            patch = build.Patch;
            work = build.Work;

            foreach (var report in work.Reports)
                _agentReports[report.ConflictKey] = report;

            UpdateDetailForMod();
        }
        catch (OperationCanceledException)
        {
            SetStatus("已停止合并补丁生成");
            return;
        }
        catch (Exception ex)
        {
            SetStatus($"生成合并补丁失败：{ex.Message}");
            MessageBox.Show(this, ex.Message, "生成合并兼容补丁", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        finally
        {
            _agentBusy = false;
            _agentCts?.Dispose();
            _agentCts = null;
            UpdateCommandStates();
        }

        if (patch == null)
            return;

        _patchBundle = patch;
        _bundledConflictKeys.Clear();
        foreach (var key in patch.IncludedConflictKeys)
            _bundledConflictKeys.Add(key);
        _bundleInstalledPath = null;
        UpdateDetailForMod();

        if (!patch.Success || !patch.IsModPackage)
        {
            var output = string.IsNullOrWhiteSpace(patch.CompilerOutput)
                ? string.Empty
                : "\n\n编译器输出（末尾）：\n" + Tail(patch.CompilerOutput!, 900);

            MessageBox.Show(this, patch.Message + output, "补丁未能封装", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            SetStatus("补丁源码已保留，但未能生成可安装 mod 包");
            return;
        }

        SetStatus($"合并兼容补丁已生成：{patch.OutputDirectory}");

        var skippedNote = patch.SkippedReasons.Count == 0
            ? string.Empty
            : $"\n另有 {patch.SkippedReasons.Count} 项冲突未并入，原因见包内 README。";
        var confirm = MessageBox.Show(
            this,
            $"合并兼容补丁已生成：\n{patch.OutputDirectory}\n\n" +
            $"收录 {patch.IncludedConflictKeys.Count} 项冲突，共一个补丁 mod。{skippedNote}\n\n" +
            "是否立即安装到游戏 Mods 目录，并在 ModsConfig.xml 中启用？\n\n安装前会自动备份 ModsConfig.xml。",
            "安装合并兼容补丁",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (confirm == DialogResult.Yes)
            InstallPatchCore();
    }

    private void InstallPatchCore()
    {
        var patch = _patchBundle;
        if (patch == null)
            return;

        var result = CompatPatchInstaller.Install(
            patch,
            ModListResolver.PathOptions.RimWorldInstallDir,
            ModListResolver.DefaultModsConfigPath);

        if (!result.Success)
        {
            SetStatus("安装合并补丁失败：" + result.Message);
            MessageBox.Show(this, result.Message, "安装合并兼容补丁", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _bundleInstalledPath = result.InstalledPath ?? patch.OutputDirectory;
        SetStatus(result.Message);

        if (result.Warnings.Count > 0)
            MessageBox.Show(this, string.Join("\n", result.Warnings), "安装提示", MessageBoxButtons.OK, MessageBoxIcon.Information);

        UpdateDetailForMod();
        UpdateCommandStates();
    }

    private static string Tail(string text, int max)
        => text.Length <= max ? text : "…" + text[^max..];

    private static string SeverityText(string severity) => severity switch
    {
        "high" => "高",
        "low" => "低",
        _ => "中"
    };

    // ───────────────────────────── 详情文档 ─────────────────────────────

    private DetailDocument BuildModDocument(ModGradeSummary mod)
    {
        var blocks = new List<DetailBlock>();
        const int maxDetailedConflicts = 8;

        if (_modConflicts.Count == 0)
        {
            var items = new List<DetailItem>
            {
                new("结论", "本次分析没有发现该模组与其他已启用模组之间存在需要处理的冲突。", DetailValueKind.Strong),
                new("当前评级", mod.GradeLabel),
                new("推荐顺位", mod.IsActive
                    ? $"#{mod.RecommendedOrderIndex}（{(mod.OrderDelta == 0 ? "与当前顺序一致" : "较当前顺序有调整")}）"
                    : $"#{mod.RecommendedOrderIndex}（当前未启用）")
            };

            blocks.Add(new DetailBlock("没有需要处理的冲突", items, "可正常使用", ConflictGrade.Green));
        }
        else
        {
            foreach (var conflict in _modConflicts.Take(maxDetailedConflicts))
            {
                var items = new List<DetailItem>
                {
                    new("对方模组", OtherModName(conflict, mod.PackageId), DetailValueKind.Strong),
                    new("会发生什么", conflict.What),
                    new("可能表现", conflict.Impact),
                    new("建议", conflict.Action)
                };

                if (conflict.InvolvesInactiveMod)
                    items.Add(new DetailItem("注意", "其中一方当前未启用；启用后这项冲突才会真正发生。", DetailValueKind.Bullet));

                if (conflict.Grade == ConflictGrade.Purple && IsPurpleChoiceApplicable(conflict))
                {
                    var other = OtherModName(conflict, mod.PackageId);
                    items.Add(new DetailItem(
                        "取舍",
                        GetPurpleChoice(conflict.Key) == PurpleChoice.Both
                            ? $"当前双方都保留。若只保留其中一方，另一方（{other}）会从排序写入与导出中排除。"
                            : $"当前只保留一方；双方都保留时双方都会进入兼容生成范围。",
                        DetailValueKind.Bullet));
                }

                blocks.Add(new DetailBlock(
                    Title: conflict.Headline,
                    Meta: conflict.SeverityLabel,
                    Accent: conflict.Grade,
                    Items: items));

                blocks.Add(BuildAgentBlock(conflict));

                var patchBlock = BuildPatchBlock(conflict);
                if (patchBlock != null)
                    blocks.Add(patchBlock);
            }

            if (_modConflicts.Count > maxDetailedConflicts)
            {
                blocks.Add(new DetailBlock(
                    "其余冲突",
                    new[]
                    {
                        new DetailItem(
                            string.Empty,
                            $"还有 {_modConflicts.Count - maxDetailedConflicts} 项冲突未展开。",
                            DetailValueKind.Bullet)
                    }));
            }
        }

        var (_, _, _, shortLabel) = UiTheme.GradeStyle(mod.Grade);
        var subtitle = _modConflicts.Count == 0
            ? $"{shortLabel} · {mod.ArchitectureTierLabel} · 推荐顺位 #{mod.RecommendedOrderIndex}"
            : $"{shortLabel} · {mod.ArchitectureTierLabel} · {_modConflicts.Count} 项冲突";

        return new DetailDocument(mod.Name, subtitle, blocks, _modConflicts.Count > 0 ? _modConflicts[0].Grade : mod.Grade);
    }

    /// <summary>Agent 结论块：先说结论，再给玩家看得懂的隐患；技术证据留在 Badge 与「技术细节」里。</summary>
    private DetailBlock BuildAgentBlock(PlayerConflict conflict)
    {
        if (_agentReports.TryGetValue(conflict.Key, out var report))
        {
            var items = new List<DetailItem>();

            if (!string.IsNullOrWhiteSpace(report.Headline))
                items.Add(new DetailItem("结论", report.Headline, DetailValueKind.Strong));
            if (!string.IsNullOrWhiteSpace(report.Explanation))
                items.Add(new DetailItem("依据", report.Explanation));

            items.Add(new DetailItem("把握", $"{Math.Round(report.Confidence * 100)}% · {report.Mode}"));

            foreach (var risk in report.HiddenRisks.Take(5))
                items.Add(new DetailItem("隐患", $"[{SeverityText(risk.Severity)}] {risk.Title}：{risk.Detail}", DetailValueKind.Bullet));

            if (!string.IsNullOrWhiteSpace(report.EvidenceSummary))
                items.Add(new DetailItem("证据", report.EvidenceSummary));
            if (!string.IsNullOrWhiteSpace(report.Error))
                items.Add(new DetailItem("提示", report.Error));

            if (items.Count == 0)
                items.Add(new DetailItem(string.Empty, "核查没有返回有效结论。", DetailValueKind.Bullet));

            var accent = report.Verdict switch
            {
                AgentVerdictKind.RealConflict => (ConflictGrade?)ConflictGrade.Red,
                AgentVerdictKind.ConditionDependent => ConflictGrade.Yellow,
                AgentVerdictKind.FalsePositive => ConflictGrade.Green,
                _ => null
            };

            return new DetailBlock("Agent 代码核查", items, report.VerdictLabel, accent);
        }

        if (_checkingKeys.Contains(conflict.Key))
        {
            return new DetailBlock(
                "Agent 代码核查",
                new[] { new DetailItem(string.Empty, "正在反编译双方补丁、读取目标方法并比对修改逻辑…", DetailValueKind.Bullet) },
                "核查中");
        }

        return new DetailBlock(
            "Agent 代码核查",
            new[]
            {
                new DetailItem(string.Empty,
                    _investigator == null ? "Agent 尚未就绪。" : "该冲突尚未核查，选中模组后会自动开始。",
                    DetailValueKind.Bullet)
            },
            "待核查");
    }

    private DetailBlock? BuildPatchBlock(PlayerConflict conflict)
    {
        if (!_agentReports.TryGetValue(conflict.Key, out var report))
            return null;

        var plan = report.PatchPlan;
        var items = new List<DetailItem>();

        if (plan == null)
        {
            items.Add(new DetailItem("处理方式", FixStrategyLabel(report.FixStrategy), DetailValueKind.Strong));
            items.Add(new DetailItem("说明", report.FixStrategy == "disable_one"
                ? "这类冲突无法靠补丁安全修复，建议二选一。"
                : "当前结论没有提供可生成的补丁方案。"));

            if (_patchBundle != null)
            {
                var skipReason = BundleSkipReason(conflict);
                items.Add(new DetailItem("合并补丁", skipReason == null
                    ? "该冲突未并入本次合并补丁。"
                    : "未并入本次合并补丁：" + skipReason));
            }

            return new DetailBlock("兼容修复", items, _patchBundle == null ? "无需补丁包" : "未并入");
        }

        items.Add(new DetailItem("方案", plan.Summary, DetailValueKind.Strong));
        items.Add(new DetailItem("做法", FixStrategyLabel(report.FixStrategy)));

        if (!string.IsNullOrWhiteSpace(plan.RiskNote))
            items.Add(new DetailItem("风险", plan.RiskNote!));

        if (plan.Files.Count > 0)
            items.Add(new DetailItem("文件", string.Join("、", plan.Files.Select(f => f.RelativePath)), DetailValueKind.Mono));

        ConflictGrade? accent = null;
        var bundled = _bundledConflictKeys.Contains(conflict.Key);

        if (_patchBundle != null)
        {
            if (bundled)
            {
                accent = _patchBundle.Success && _patchBundle.IsModPackage
                    ? ConflictGrade.Green
                    : ConflictGrade.Red;

                if (_patchBundle.Success && _patchBundle.IsModPackage)
                {
                    items.Add(new DetailItem("合并补丁包", _patchBundle.OutputDirectory, DetailValueKind.Mono));
                    items.Add(new DetailItem("收录状态", "该冲突已并入合并补丁"));
                    if (!string.IsNullOrWhiteSpace(_bundleInstalledPath))
                        items.Add(new DetailItem("安装状态", "已安装：" + _bundleInstalledPath, DetailValueKind.Mono));
                }
                else
                {
                    items.Add(new DetailItem("生成结果", _patchBundle.Message));
                    if (!string.IsNullOrWhiteSpace(_patchBundle.CompilerOutput))
                        items.Add(new DetailItem("编译输出", Tail(_patchBundle.CompilerOutput!, 800), DetailValueKind.Mono));
                }
            }
            else
            {
                accent = ConflictGrade.Yellow;
                var skipReason = BundleSkipReason(conflict);
                items.Add(new DetailItem(
                    "合并补丁",
                    skipReason == null
                        ? "该冲突未并入本次合并补丁。"
                        : "未并入本次合并补丁：" + skipReason));
            }
        }
        else if (plan.Kind is "harmony" or "xml")
        {
            items.Add(new DetailItem(
                "下一步",
                "可生成兼容补丁：内部 Agent 会复核全部生效冲突，并把可修复项统一编译进一个补丁 mod。"));
        }
        else if (plan.Kind == "order_only")
        {
            items.Add(new DetailItem("下一步", "按推荐顺序排列即可，无需生成补丁包。"));
        }
        else
        {
            items.Add(new DetailItem("下一步", report.FixStrategy == "compat_patch"
                ? "本地核查不会生成未经核对的代码；配置模型后可生成并编译兼容补丁。"
                : "当前方案需要人工处理。"));
        }

        var meta = plan.Kind switch
        {
            "harmony" => "Harmony 补丁",
            "xml" => "XML 补丁",
            "order_only" => "仅调整顺序",
            _ => "人工处理"
        };

        if (_patchBundle != null)
            meta = bundled ? "已并入合并包" : "未并入";

        if (bundled && !string.IsNullOrWhiteSpace(_bundleInstalledPath))
            meta = "已安装";

        return new DetailBlock("兼容修复", items, meta, accent);
    }

    private string? BundleSkipReason(PlayerConflict conflict)
    {
        var note = _patchBundle?.SkippedReasons.FirstOrDefault(r =>
            r.StartsWith(conflict.PairLabel + "：", StringComparison.Ordinal));
        return note == null ? null : note[(conflict.PairLabel.Length + 1)..];
    }

    private static string FixStrategyLabel(string strategy) => strategy switch
    {
        "order" => "调整加载顺序",
        "disable_one" => "二选一（禁用其一）",
        "compat_patch" => "生成兼容补丁",
        "config" => "修改模组设置",
        _ => "人工处理"
    };

    private static DetailDocument BuildChangesDocument(ModSortReport report)
    {
        var moved = report.SortedMods.Where(m => m.OrderDelta != 0).ToList();
        var earlier = moved.Where(m => m.OrderDelta > 0).OrderByDescending(m => m.OrderDelta).ToList();
        var later = moved.Where(m => m.OrderDelta < 0).OrderBy(m => m.OrderDelta).ToList();

        var blocks = new List<DetailBlock>();

        if (earlier.Count == 0 && later.Count == 0)
        {
            blocks.Add(new DetailBlock(
                "顺序未发生变动",
                new[]
                {
                    new DetailItem(string.Empty, "所有模组的推荐顺位与原始顺位一致，现有加载顺序已满足全部约束。", DetailValueKind.Text)
                }));
        }

        if (earlier.Count > 0)
        {
            blocks.Add(new DetailBlock(
                "提前加载",
                earlier.Take(30)
                    .Select(m => new DetailItem(
                        $"▲{m.OrderDelta}",
                        $"{m.Name}：#{m.OriginalOrderIndex} → #{m.RecommendedOrderIndex}（{m.GradeLabel.Split(' ')[0]}）",
                        DetailValueKind.Bullet))
                    .ToArray(),
                $"{earlier.Count} 项"));
        }

        if (later.Count > 0)
        {
            blocks.Add(new DetailBlock(
                "延后加载",
                later.Take(30)
                    .Select(m => new DetailItem(
                        $"▼{Math.Abs(m.OrderDelta)}",
                        $"{m.Name}：#{m.OriginalOrderIndex} → #{m.RecommendedOrderIndex}（{m.GradeLabel.Split(' ')[0]}）",
                        DetailValueKind.Bullet))
                    .ToArray(),
                $"{later.Count} 项"));
        }

        return new DetailDocument(
            "排序变动预览",
            $"共 {moved.Count} 项模组顺位调整 · 总计 {report.TotalModsAnalyzed} 项",
            blocks);
    }

    // ───────────────────────────── 导出与应用 ─────────────────────────────

    /// <summary>当前真正要写入游戏的启用列表：原始启用项减去紫色冲突里被放弃的一侧。</summary>
    private List<string> GetEffectiveActivePackageIds()
    {
        if (_lastResult == null)
            return new List<string>();

        var excluded = BuildPurpleExclusionSet();
        return _lastResult.Report.SortedMods
            .Where(m => m.IsActive && !excluded.Contains(m.PackageId))
            .Select(m => m.PackageId)
            .ToList();
    }

    /// <summary>紫色取舍变化后，把当前生效列表同步进自动保存的排序档，避免启动游戏时写回旧选择。</summary>
    private void PersistEffectiveSelectionToAutoProfile()
    {
        const string autoSaveName = "自动保存";
        if (_lastResult == null || !_profileStore.NameExists(autoSaveName))
            return;

        var activeIds = GetEffectiveActivePackageIds();
        if (activeIds.Count == 0)
            return;

        var profile = new SortListProfile(
            autoSaveName,
            DateTime.UtcNow,
            _lastResult.EffectiveGameVersion,
            activeIds,
            _lastResult.KnownExpansions.ToList(),
            _lastResult.Report.TotalModsAnalyzed,
            _lastResult.Report.RedModCount,
            _lastResult.Report.YellowModCount);

        _profileStore.Upsert(profile);
        RefreshSortListsButton();
        RebuildProfileMenu();
    }

    private void ApplyToGameConfig()
    {
        if (_lastResult == null)
        {
            SetStatus("尚未分析，无法写入游戏配置");
            return;
        }

        var target = ModListResolver.DefaultModsConfigPath;
        var redCount = _lastResult.Report.RedModCount;
        var warning = redCount > 0
            ? $"\n\n注意：当前仍有 {redCount} 项模组处于 3.红色（明确冲突），写入后进入游戏仍可能报错。"
            : string.Empty;

        if (_lastResult.Report.GabsVerification?.GameRunning == true)
        {
            warning += "\n\n注意：检测到 RimWorld 正在运行。游戏退出时会用内存里的模组列表回写" +
                       " ModsConfig.xml，本次写入可能被覆盖。建议先退出游戏再写入。";
        }

        var poolCount = _lastResult.Report.WorkshopPoolModCount;
        if (poolCount > 0)
        {
            warning += $"\n\n说明：工坊订阅池中的 {poolCount} 项未启用模组只参与排序参考，" +
                       "不会写入 ModsConfig.xml（避免意外激活数百个模组）。";
        }

        var effectiveIds = GetEffectiveActivePackageIds();
        var excludedCount = _lastResult.Report.SortedMods.Count(m => m.IsActive) - effectiveIds.Count;
        if (excludedCount > 0)
        {
            warning += $"\n\n紫色冲突取舍：{excludedCount} 项被放弃的模组不会写入配置。" +
                       "双方都保留时这两项都会进入兼容生成范围。";
        }

        var openPurple = GetAgentScopeConflicts().Count(c => c.Grade == ConflictGrade.Purple);
        if (openPurple > 0)
        {
            warning += $"\n\n注意：仍有 {openPurple} 对作者声明不兼容的模组双方都保留。" +
                       "写入后进入游戏可能报错或损坏存档，建议先在右侧完成兼容生成。";
        }

        var confirm = MessageBox.Show(
            this,
            $"即将把推荐排序写入：\n{target}\n\n原文件会先自动备份为 ModsConfig.backup.时间戳.xml。{warning}\n\n是否继续？",
            "写入游戏配置",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (confirm != DialogResult.OK)
        {
            SetStatus("已取消写入");
            return;
        }

        try
        {
            var backup = ModSortReportBuilder.ApplyPackageIdsWithBackup(
                target,
                _lastResult.EffectiveGameVersion,
                effectiveIds,
                _lastResult.KnownExpansions);

            SetStatus(backup == null
                ? $"已写入 {target}（原文件不存在，未生成备份）"
                : $"已写入 {target}，备份：{Path.GetFileName(backup)}");
        }
        catch (Exception ex)
        {
            ShowFriendlyError("写入游戏配置失败", ex);
        }
    }

    private void ExportModsConfig()
    {
        if (_lastResult == null)
        {
            SetStatus("尚未分析，无法导出 ModsConfig.xml");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "导出 ModsConfig.xml",
            Filter = "RimWorld ModsConfig (ModsConfig.xml)|*.xml",
            FileName = "ModsConfig.xml"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            ModSortReportBuilder.ExportPackageIdsAsModsConfigXml(
                dialog.FileName,
                _lastResult.EffectiveGameVersion,
                GetEffectiveActivePackageIds(),
                _lastResult.KnownExpansions);

            SetStatus($"已导出 ModsConfig.xml：{dialog.FileName}");
        }
        catch (Exception ex)
        {
            ShowFriendlyError("导出 ModsConfig.xml 失败", ex);
        }
    }

    private void ExportJson()
    {
        if (_lastResult == null)
        {
            SetStatus("尚未分析，无法导出 JSON");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "导出 JSON 报告",
            Filter = "JSON 文件 (*.json)|*.json",
            FileName = "mod_sort_report.json"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            File.WriteAllText(dialog.FileName,
                JsonSerializer.Serialize(_lastResult.Report, options),
                new UTF8Encoding(false));

            SetStatus($"已导出 JSON：{dialog.FileName}");
        }
        catch (Exception ex)
        {
            ShowFriendlyError("导出 JSON 失败", ex);
        }
    }

    private void ExportMarkdown()
    {
        if (_lastResult == null)
        {
            SetStatus("尚未分析，无法导出报告");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "导出 Markdown 报告",
            Filter = "Markdown 文件 (*.md)|*.md",
            FileName = "mod_sort_report.md"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            File.WriteAllText(dialog.FileName, _lastResult.MarkdownReport, new UTF8Encoding(false));
            SetStatus($"已导出报告：{dialog.FileName}");
        }
        catch (Exception ex)
        {
            ShowFriendlyError("导出报告失败", ex);
        }
    }

    // ───────────────────────────── 辅助 ─────────────────────────────

    private void UpdateCommandStates()
    {
        var hasResult = _lastResult != null;
        var hasTokens = _tokens.Count > 0;

        _btnAnalyze.Enabled = !_busy && !_agentBusy && hasTokens;
        _btnStop.Enabled = _busy || _agentBusy;
        _btnImport.Enabled = !_busy && !_agentBusy;
        _btnApply.Enabled = hasResult && !_busy;
        _btnExport.Enabled = hasResult && !_busy;
        _btnLaunchGame.Enabled = !_busy;
        _btnSortLists.Enabled = !_busy;

        _btnAnalyze.Text = _busy ? "分析进行中…" : "开始分析";

        var hasAgentTargets = GetAgentScopeConflicts().Count > 0;
        var hasPackage = _patchBundle is { Success: true, IsModPackage: true };

        _btnDeepCheck.Enabled = !_busy && !_agentBusy && hasAgentTargets;
        _btnBuildPatch.Enabled = !_busy && !_agentBusy && hasAgentTargets;
        _btnBuildPatch.Text = hasPackage ? "重新兼容生成" : "兼容生成";

        _btnMaximize.IsMaximized = WindowState == FormWindowState.Maximized;
        _btnMaximize.Invalidate();
        UpdateWindowButtonTips();
    }

    private void UpdateWindowButtonTips()
    {
        _chromeTips.SetToolTip(_btnMaximize,
            WindowState == FormWindowState.Maximized ? "还原窗口" : "最大化");
    }

    private void SetStatus(string text)
    {
        if (IsDisposed)
            return;

        _statusLabel.Text = text;
    }

    private void ShowFriendlyError(string title, Exception ex)
    {
        var message = TranslateError(ex);
        SetStatus($"{title}：{message}");

        MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string TranslateError(Exception ex)
    {
        return ex switch
        {
            FileNotFoundException => $"找不到所需文件：{ex.Message}",
            DirectoryNotFoundException => "找不到指定目录，请检查 RimWorld 安装路径与创意工坊路径是否正确。",
            UnauthorizedAccessException => "没有写入权限，请尝试以管理员身份运行本程序。",
            IOException io => $"文件读写失败：{io.Message}",
            InvalidOperationException => ex.Message,
            _ => $"分析过程中出现未预期的问题：{ex.Message}"
        };
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;

        _btnMaximize.IsMaximized = WindowState == FormWindowState.Maximized;
        _btnMaximize.Invalidate();
    }

    private async Task CaptureScreenshotAsync(string outputPath)
    {
        try
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            await Task.Delay(300);
            Application.DoEvents();
            Refresh();

            using var bitmap = new Bitmap(Width, Height);
            DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
            bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);

            SetStatus($"界面截图已保存：{outputPath}");
        }
        catch (Exception ex)
        {
            SetStatus($"界面截图保存失败：{ex.Message}");
        }
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg != NativeMethods.WM_NCHITTEST || (int)m.Result != NativeMethods.HTCLIENT)
            return;

        var screenPoint = new Point((short)((long)m.LParam & 0xFFFF), (short)((long)m.LParam >> 16));
        var point = PointToClient(screenPoint);
        var grip = 8;

        var onLeft = point.X <= grip;
        var onRight = point.X >= ClientSize.Width - grip;
        var onTop = point.Y <= grip;
        var onBottom = point.Y >= ClientSize.Height - grip;

        m.Result = (IntPtr)((onTop, onBottom, onLeft, onRight) switch
        {
            (true, _, true, _) => NativeMethods.HTTOPLEFT,
            (true, _, _, true) => NativeMethods.HTTOPRIGHT,
            (_, true, true, _) => NativeMethods.HTBOTTOMLEFT,
            (_, true, _, true) => NativeMethods.HTBOTTOMRIGHT,
            (true, _, _, _) => NativeMethods.HTTOP,
            (_, true, _, _) => NativeMethods.HTBOTTOM,
            (_, _, true, _) => NativeMethods.HTLEFT,
            (_, _, _, true) => NativeMethods.HTRIGHT,
            _ => NativeMethods.HTCLIENT
        });
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using var pen = new Pen(UiTheme.BorderStrong, 1f);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}

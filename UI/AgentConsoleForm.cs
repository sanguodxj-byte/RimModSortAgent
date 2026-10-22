using System.ComponentModel;
using System.Drawing.Drawing2D;
using ModSorterAgent.Agent;
using ModSorterAgent.Reporting;
using ModSorterAgent.UI.Controls;

namespace ModSorterAgent.UI;

/// <summary>
/// Agent 接管窗口：补丁生成与实机验证期间浮在主窗口之上，
/// 实时展示 Agent 正在做什么（推理、工具调用、逐条结论），
/// 并允许玩家在运行中投递要求（交给 <see cref="AgentGuidanceChannel"/> 带进后续提示词）。
///
/// 只在 Agent 运行时存在，结束后等用户点「返回」再关闭，主窗口不受任何改动。
/// 运行期间随时可以停止、缩小或直接退出，退出路径不受运行状态阻挡。
/// </summary>
internal sealed class AgentConsoleForm : Form, IAgentLiveSink
{
    private const int HeaderHeight = 74;
    private const int FooterHeight = 74;
    private const int ControlHeight = 36;

    // 尺寸都按 96 DPI 记，落到窗口上的数值由 ApplyScale / FitAndCenter 换算到当前 DPI：
    // Design* 是上限，Minimum* 是下限，实际大小取主窗口的固定比例。
    private const int DesignWidth = 1040;
    private const int DesignHeight = 720;
    private const int MinimumWidth = 680;
    private const int MinimumHeight = 460;

    private readonly List<Action> _pendingUi = new();
    private readonly ToolTip _chromeTips = new();

    private readonly Panel _header = new();
    private readonly Panel _footer = new();
    private readonly Panel _body = new();
    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly RichTextBox _output = new();
    private readonly Composer _composer = new();
    private readonly TranscriptCard _card = new();

    private readonly WindowChromeButton _btnMinimize =
        new(WindowChromeButton.WindowControlKind.Minimize) { BackColor = UiTheme.SurfaceBg };

    private readonly WindowChromeButton _btnExit =
        new(WindowChromeButton.WindowControlKind.Close) { BackColor = UiTheme.SurfaceBg };

    private readonly ModernButton _btnReturn = new("返回", ButtonVariant.Primary);
    private readonly ModernButton _btnStop = new("停止", ButtonVariant.Secondary);
    private readonly ModernButton _btnSend = new("发送消息", ButtonVariant.Primary);

    private IWin32Window? _owner;
    private int _total;
    private int _done;
    private bool _finished;
    private bool _cancelRequested;
    private float _progress;
    private Color _railColor = UiTheme.Accent;
    private Color _railTrack = UiTheme.BorderSubtle;

    public AgentConsoleForm(string title)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.None;

        // 接管窗口的位置只能自己算：StartPosition=CenterParent 对非模态 Show(owner) 不生效，
        // 实测会落到默认级联位置（屏幕左上区域），既不居中于宿主也不居中于屏幕。
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = true;
        KeyPreview = true;

        // 一律不做自动缩放：面板高度、内边距、按钮尺寸都在 ApplyScale 里按 DPI 手算，
        // 混两套缩放会把文字压在边框底下。
        AutoScaleMode = AutoScaleMode.None;
        BackColor = UiTheme.AppBg;
        ForeColor = UiTheme.TextPrimary;
        Font = UiTheme.BodyFont;
        DoubleBuffered = true;
        Padding = new Padding(1);
        Size = new Size(DesignWidth, DesignHeight);

        BuildHeader(title);
        BuildBody();
        BuildFooter();

        Controls.Add(_header);
        Controls.Add(_footer);

        LayoutChrome();
        LayoutFooter();

        KeyDown += OnFormKeyDown;
    }

    /// <summary>用户点了停止或退出时触发，调用方据此取消 Agent。</summary>
    public event EventHandler? CancelRequested;

    /// <summary>用户在运行中投递了一条要求，参数为消息正文。</summary>
    public event EventHandler<string>? MessageSent;

    private int Px(int value) => UiTheme.Scale(this, value);

    // ───────────────────────────── 结构 ─────────────────────────────

    private void BuildHeader(string title)
    {
        _header.Dock = DockStyle.Top;
        _header.Height = HeaderHeight;
        _header.BackColor = UiTheme.SurfaceBg;
        _header.Paint += (_, e) => PaintHeader(e.Graphics);

        _title.Text = title;
        _title.AutoSize = false;
        _title.AutoEllipsis = true;
        _title.BackColor = Color.Transparent;
        _title.ForeColor = UiTheme.TextPrimary;
        _title.Font = UiTheme.DialogTitleFont;
        _title.TextAlign = ContentAlignment.MiddleLeft;

        _subtitle.Text = "准备中";
        _subtitle.AutoSize = false;
        _subtitle.AutoEllipsis = true;
        _subtitle.BackColor = Color.Transparent;
        _subtitle.ForeColor = UiTheme.TextSecondary;
        _subtitle.Font = UiTheme.SmallFont;
        _subtitle.TextAlign = ContentAlignment.MiddleLeft;

        _btnMinimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
        _btnExit.Click += (_, _) => ExitConsole();

        _chromeTips.SetToolTip(_btnMinimize, "最小化");
        _chromeTips.SetToolTip(_btnExit, "退出");

        _header.Controls.Add(_title);
        _header.Controls.Add(_subtitle);
        _header.Controls.Add(_btnMinimize);
        _header.Controls.Add(_btnExit);

        var drag = new MouseEventHandler((_, e) =>
        {
            if (e.Button != MouseButtons.Left)
                return;

            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, NativeMethods.WM_NCLBUTTONDOWN, NativeMethods.HTCAPTION, 0);
        });

        _header.MouseDown += drag;
        _title.MouseDown += drag;
        _subtitle.MouseDown += drag;
    }

    private void BuildBody()
    {
        _body.Dock = DockStyle.Fill;
        _body.BackColor = UiTheme.AppBg;

        _card.Dock = DockStyle.Fill;
        _card.BackColor = UiTheme.CardBg;

        _output.Dock = DockStyle.Fill;
        _output.BackColor = UiTheme.CardBg;
        _output.ForeColor = UiTheme.TextPrimary;
        _output.Font = UiTheme.MonoFont;
        _output.BorderStyle = BorderStyle.None;
        _output.ReadOnly = true;
        _output.WordWrap = true;
        _output.ScrollBars = RichTextBoxScrollBars.Vertical;
        _output.DetectUrls = false;

        NativeMethods.UseDarkScrollbars(_output);

        _card.Controls.Add(_output);
        _body.Controls.Add(_card);
        Controls.Add(_body);
    }

    private void BuildFooter()
    {
        _footer.Dock = DockStyle.Bottom;
        _footer.Height = FooterHeight;
        _footer.BackColor = UiTheme.SurfaceBg;
        _footer.Paint += (_, e) =>
        {
            using var pen = new Pen(UiTheme.BorderSubtle, 1f);
            e.Graphics.DrawLine(pen, 0, 0, _footer.Width, 0);
        };

        _composer.Height = ControlHeight;
        _composer.ValueChanged += (_, _) => UpdateSendState();
        _composer.Submitted += (_, _) => SendMessage();

        _btnSend.Height = ControlHeight;
        _btnSend.LeadingGlyph = "▶";
        _btnSend.Enabled = false;
        _btnSend.Click += (_, _) => SendMessage();

        _btnStop.Height = ControlHeight;
        _btnStop.LeadingGlyph = "■";
        _btnStop.Click += (_, _) => RequestCancel();

        _btnReturn.Height = ControlHeight;
        _btnReturn.Visible = false;
        _btnReturn.Click += (_, _) => Close();

        _chromeTips.SetToolTip(_btnSend, "发送消息");
        _chromeTips.SetToolTip(_btnStop, "停止");
        _chromeTips.SetToolTip(_btnReturn, "返回");

        _btnStop.SizeChanged += (_, _) => LayoutFooter();
        _btnReturn.SizeChanged += (_, _) => LayoutFooter();

        _footer.Controls.Add(_composer);
        _footer.Controls.Add(_btnSend);
        _footer.Controls.Add(_btnStop);
        _footer.Controls.Add(_btnReturn);
    }

    // ───────────────────────────── 布局 ─────────────────────────────

    /// <summary>
    /// 固定尺寸统一按当前 DPI 换算。字号是磅值、随 DPI 自己变大，
    /// 面板高度与内边距不会，两者必须一起走，否则文字会被边框压住、按钮贴边。
    /// </summary>
    private void ApplyScale()
    {
        _header.Height = Px(HeaderHeight);
        _footer.Height = Px(FooterHeight);

        var chrome = new Size(Px(42), Px(30));
        _btnMinimize.Size = chrome;
        _btnExit.Size = chrome;

        _body.Padding = new Padding(Px(16), Px(14), Px(16), Px(12));
        _card.Padding = new Padding(Px(16), Px(12), Px(8), Px(10));

        _btnSend.RefreshMetrics();
        _btnStop.RefreshMetrics();
        _btnReturn.RefreshMetrics();

        MinimumSize = new Size(Px(MinimumWidth), Px(MinimumHeight));

        LayoutChrome();
        LayoutFooter();
        _header.Invalidate();
        _footer.Invalidate();
    }

    private void LayoutChrome()
    {
        var top = Math.Max(UiTheme.Scale(_header, 8), (_header.Height - Px(30)) / 2);
        _btnExit.Location = new Point(_header.ClientSize.Width - Px(14) - _btnExit.Width, top);
        _btnMinimize.Location = new Point(_btnExit.Left - _btnMinimize.Width, top);

        var textLeft = Px(64);
        var textRight = Math.Max(textLeft + Px(160), _btnMinimize.Left - Px(14));
        _title.Location = new Point(textLeft, Px(11));
        _title.Size = new Size(textRight - textLeft, Px(26));
        _subtitle.Location = new Point(textLeft + Px(1), _title.Bottom + Px(1));
        _subtitle.Size = new Size(textRight - textLeft, Px(20));
    }

    private void LayoutFooter()
    {
        var height = Px(ControlHeight);
        var y = Math.Max(Px(4), (_footer.Height - height) / 2);

        _btnSend.Height = height;
        _btnStop.Height = height;
        _btnReturn.Height = height;
        _composer.Height = height;

        _btnSend.Location = new Point(_footer.ClientSize.Width - Px(18) - _btnSend.Width, y);

        // 停止与返回共用同一个位置：运行时放停止，结束后换成返回
        var slot = Math.Max(_btnStop.Width, _btnReturn.Width);
        var slotLeft = _btnSend.Left - Px(10) - slot;
        _btnStop.Location = new Point(slotLeft, y);
        _btnReturn.Location = new Point(slotLeft, y);

        _composer.Location = new Point(Px(18), y);
        _composer.Width = Math.Max(Px(160), slotLeft - Px(12) - Px(18));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutChrome();
        LayoutFooter();
        _header.Invalidate();
        _footer.Invalidate();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        // 运行期间只留「停止」，结束后由 End/Fail 换成「返回」。
        _btnReturn.Visible = _finished;
        _btnStop.Visible = !_finished;
        _btnSend.Enabled = false;
        _composer.FocusInput();
    }

    // ───────────────────────────── 绘制 ─────────────────────────────

    private void PaintHeader(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var mark = new Rectangle(Px(22), Px(17), Px(30), Px(30));
        using (var path = UiTheme.RoundedRect(mark, Px(9)))
        using (var brush = new LinearGradientBrush(
                   mark, UiTheme.Accent, Color.FromArgb(96, 92, 220), LinearGradientMode.ForwardDiagonal))
        {
            g.FillPath(brush, path);
        }

        using (var pen = new Pen(Color.FromArgb(245, 249, 255), 1.4f)
               {
                   StartCap = LineCap.Round,
                   EndCap = LineCap.Round
               })
        {
            var left = mark.X + Px(8);
            for (var i = 0; i < 3; i++)
            {
                var y = mark.Y + Px(10) + i * Px(5);
                g.DrawLine(pen, left, y, left + Px(14 - i * 4), y);
            }
        }

        // 进度轨：底轨随状态着色，前景按完成比例填充
        var railHeight = Math.Max(2, Px(3));
        var railY = _header.Height - railHeight;

        using (var track = new SolidBrush(_railTrack))
            g.FillRectangle(track, 0, railY, _header.Width, railHeight);

        if (_progress > 0.001f)
        {
            var width = Math.Max(railHeight, (int)Math.Round(_header.Width * Math.Clamp(_progress, 0f, 1f)));
            using var fill = new SolidBrush(_railColor);
            g.FillRectangle(fill, 0, railY, width, railHeight);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(UiTheme.BorderStrong, 1f);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg != NativeMethods.WM_NCHITTEST || (int)m.Result != NativeMethods.HTCLIENT)
            return;

        var screenPoint = new Point((short)((long)m.LParam & 0xFFFF), (short)((long)m.LParam >> 16));
        var point = PointToClient(screenPoint);
        var grip = Px(6);

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

    // ───────────────────────────── 接管与退出 ─────────────────────────────

    /// <summary>
    /// 接管主窗口：运行期间主窗口不可操作，窗口关闭后自动恢复。
    /// 用模态 ShowDialog 的等价效果，但保持异步不被阻塞。
    /// </summary>
    public void TakeOver(IWin32Window owner)
    {
        _owner = owner;

        if (owner is Form ownerForm)
            ownerForm.Enabled = false;

        FormClosed += (_, _) =>
        {
            if (owner is Form form && !form.IsDisposed)
                form.Enabled = true;
        };

        CenterOnOwner();
        Show(owner);
        CenterOnOwner();
    }

    /// <summary>
    /// 按主窗口比例定尺并摆到正中，再收进可用桌面区域。
    /// 主窗口自身尺寸不随 DPI 换算，所以窗口大小也跟着主窗口走，比固定像素更稳。
    /// </summary>
    private void FitAndCenter(bool resize)
    {
        var ownerForm = _owner as Form;
        var area = ownerForm != null
            ? Screen.FromRectangle(ownerForm.Bounds).WorkingArea
            : Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);

        var reference = ownerForm is { Visible: true, WindowState: not FormWindowState.Minimized }
            ? ownerForm.Bounds
            : area;

        if (resize)
        {
            var targetWidth = Math.Clamp((int)(reference.Width * 0.82), Px(MinimumWidth), Px(DesignWidth));
            var targetHeight = Math.Clamp((int)(reference.Height * 0.86), Px(MinimumHeight), Px(DesignHeight));

            Size = new Size(
                Math.Min(targetWidth, Math.Max(Px(360), area.Width - Px(32))),
                Math.Min(targetHeight, Math.Max(Px(280), area.Height - Px(32))));
        }

        var x = reference.X + (reference.Width - Width) / 2;
        var y = reference.Y + (reference.Height - Height) / 2;

        Location = new Point(
            Math.Clamp(x, area.X, Math.Max(area.X, area.Right - Width)),
            Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - Height)));
    }

    private void CenterOnOwner() => FitAndCenter(resize: false);

    /// <summary>运行中退出＝先取消 Agent 再关窗，保证任何时刻都有一步可走的退路。</summary>
    private void ExitConsole()
    {
        RequestCancel();
        Close();
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Escape)
            return;

        e.Handled = true;
        ExitConsole();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_finished)
            RequestCancel();

        base.OnFormClosing(e);
    }

    private void RequestCancel()
    {
        if (_finished || _cancelRequested)
            return;

        _cancelRequested = true;
        _btnStop.Enabled = false;
        _btnStop.Text = "正在停止…";
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SendMessage()
    {
        if (_finished)
            return;

        var text = _composer.Value.Trim();
        if (text.Length == 0)
            return;

        _composer.Clear();
        AppendUserMessage(text);
        MessageSent?.Invoke(this, text);
        _composer.FocusInput();
    }

    private void UpdateSendState()
    {
        _btnSend.Enabled = !_finished && _composer.Value.Trim().Length > 0;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_pendingUi)
            {
                _pendingUi.Clear();
            }

            _chromeTips.Dispose();
        }

        base.Dispose(disposing);
    }

    // ── IAgentLiveSink ──────────────────────────────

    public void Begin(string title, int conflictCount)
    {
        _total = conflictCount;
        _done = 0;
        OnUi(() =>
        {
            _title.Text = title;
            _subtitle.ForeColor = UiTheme.TextSecondary;
            _subtitle.Text = $"共 {conflictCount} 项";
            _railColor = UiTheme.Accent;
            _railTrack = UiTheme.BorderSubtle;
            UpdateProgressBar();
        });
    }

    public void BeginConflict(PlayerConflict conflict, int index, int total)
    {
        OnUi(() =>
        {
            _total = total;
            _subtitle.Text = $"{index + 1}/{total} · {conflict.PairLabel}";
            AppendBlock(string.Empty, UiTheme.TextMuted);
            AppendBlock($"[{index + 1}/{total}] {conflict.PairLabel} · {conflict.CategoryLabel}",
                UiTheme.Accent, bold: true);
        });
    }

    public void Reasoning(PlayerConflict conflict, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var trimmed = text.Trim();
        // 机器解析的原始 JSON 报文（包含 verdict、hidden_risks 等内部字段）严禁直接显示在界面上
        if (IsRawJsonPayload(trimmed))
            return;

        OnUi(() => AppendBlock(trimmed, UiTheme.TextSecondary));
    }

    private static bool IsRawJsonPayload(string text)
    {
        return (text.StartsWith('{') && text.Contains("\"verdict\"")) ||
               (text.Contains("```") && text.Contains("\"verdict\""));
    }

    public void ToolCall(PlayerConflict conflict, AgentToolCall call)
    {
        OnUi(() =>
        {
            var summary = DescribeToolCall(call);
            AppendBlock("  " + summary, UiTheme.TextMuted);
        });
    }

    public void ConflictCompleted(PlayerConflict conflict, AgentReport report)
    {
        OnUi(() =>
        {
            _done++;
            UpdateProgressBar();

            var color = report.Verdict switch
            {
                AgentVerdictKind.RealConflict => UiTheme.RoseText,
                AgentVerdictKind.ConditionDependent => UiTheme.AmberText,
                AgentVerdictKind.FalsePositive => UiTheme.GreenText,
                _ => UiTheme.TextMuted
            };

            AppendBlock($"  → {report.VerdictLabel}（置信度 {report.Confidence:P0}）{report.Headline}",
                color, bold: true);
        });
    }

    public void End(IReadOnlyList<AgentReport> reports)
    {
        OnUi(() =>
        {
            _finished = true;
            _total = reports.Count;
            _done = reports.Count;
            UpdateProgressBar();

            _subtitle.ForeColor = UiTheme.GreenText;
            _subtitle.Text = $"完成 · 共 {reports.Count} 项";
            _railColor = UiTheme.Green;
            _railTrack = UiTheme.GreenBg;

            CloseRunState();
            _btnReturn.Focus();

            var real = reports.Count(r => r.Verdict == AgentVerdictKind.RealConflict);
            var conditional = reports.Count(r => r.Verdict == AgentVerdictKind.ConditionDependent);
            var falsePositive = reports.Count(r => r.Verdict == AgentVerdictKind.FalsePositive);
            var insufficient = reports.Count(r => r.Verdict == AgentVerdictKind.InsufficientEvidence);

            AppendBlock(string.Empty, UiTheme.TextMuted);
            AppendBlock(
                $"确认冲突 {real} · 条件性 {conditional} · 静态误报 {falsePositive} · 证据不足 {insufficient}",
                UiTheme.TextPrimary, bold: true);
        });
    }

    /// <summary>调用方在 Agent 抛错时调用，保证窗口不会永远停在运行态。</summary>
    public void Fail(string message)
    {
        OnUi(() =>
        {
            _finished = true;
            _progress = 1f;
            _subtitle.ForeColor = UiTheme.RoseText;
            _subtitle.Text = "已中断";
            _railColor = UiTheme.Rose;
            _railTrack = UiTheme.RoseBg;

            CloseRunState();
            _btnReturn.Focus();

            AppendBlock(string.Empty, UiTheme.TextMuted);
            AppendBlock(message, UiTheme.RoseText, bold: true);
        });
    }

    /// <summary>运行结束后的收尾：停止换成返回，输入行收起。</summary>
    private void CloseRunState()
    {
        _btnStop.Visible = false;
        _btnReturn.Visible = true;
        _composer.SetInteractive(false);
        UpdateSendState();
        LayoutFooter();
    }

    private void UpdateProgressBar()
    {
        _progress = _total <= 0 ? 0f : Math.Clamp(_done / (float)_total, 0f, 1f);
        _header.Invalidate();
    }

    // ───────────────────────────── 文本流 ─────────────────────────────

    private void AppendBlock(
        string text,
        Color color,
        bool bold = false,
        bool rightAligned = false,
        Color? highlight = null)
    {
        _output.SelectionStart = _output.TextLength;
        _output.SelectionLength = 0;
        _output.SelectionColor = color;
        _output.SelectionFont = bold ? UiTheme.MonoBoldFont : UiTheme.MonoFont;
        _output.SelectionAlignment = rightAligned ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        _output.SelectionBackColor = highlight ?? _output.BackColor;
        _output.AppendText(text + Environment.NewLine);

        ResetRunStyle();
        TrimHistory();
        _output.SelectionStart = _output.TextLength;
        _output.ScrollToCaret();
    }

    /// <summary>玩家消息：整行右对齐并加一层主色底片，与 Agent 输出区分开。</summary>
    private void AppendUserMessage(string text)
    {
        AppendBlock(string.Empty, UiTheme.TextMuted);
        AppendBlock("  " + text + "  ", UiTheme.TextPrimary, rightAligned: true, highlight: UiTheme.AccentSoft);
        AppendBlock(string.Empty, UiTheme.TextMuted);
    }

    /// <summary>写入下一段前的复位：清掉对齐与底片，避免被上一段继承。</summary>
    private void ResetRunStyle()
    {
        _output.SelectionStart = _output.TextLength;
        _output.SelectionLength = 0;
        _output.SelectionColor = UiTheme.TextPrimary;
        _output.SelectionFont = UiTheme.MonoFont;
        _output.SelectionAlignment = HorizontalAlignment.Left;
        _output.SelectionBackColor = _output.BackColor;
    }

    private void TrimHistory()
    {
        // 只保留尾部，避免长时间运行时无界增长
        if (_output.TextLength <= 400_000)
            return;

        _output.Select(0, 100_000);
        _output.SelectedText = string.Empty;
    }

    private static string DescribeToolCall(AgentToolCall call)
    {
        var args = SummarizeArguments(call.ArgumentsJson);
        return string.IsNullOrWhiteSpace(args) ? $"· {call.Name}" : $"· {call.Name} {args}";
    }

    /// <summary>把工具参数压成一行摘要，保留可读的关键字段。</summary>
    private static string SummarizeArguments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return string.Empty;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
                return Trim(json, 120);

            var parts = new List<string>();
            foreach (var name in new[] { "assembly", "type", "member", "query", "tool", "path" })
            {
                if (!root.TryGetProperty(name, out var value))
                    continue;

                var text = value.ValueKind == System.Text.Json.JsonValueKind.String
                    ? value.GetString()
                    : value.ToString();

                if (!string.IsNullOrWhiteSpace(text))
                    parts.Add(Trim(text, 80));
            }

            return string.Join(" ", parts);
        }
        catch
        {
            return Trim(json, 120);
        }
    }

    private static string Trim(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";

    private void OnUi(Action action)
    {
        if (IsDisposed)
            return;

        // 接管之前句柄还没有：复核其实已经在推进了，这些更新先存着，建好句柄再补。
        if (!IsHandleCreated)
        {
            lock (_pendingUi)
            {
                if (IsDisposed)
                    return;

                if (!IsHandleCreated)
                {
                    _pendingUi.Add(action);
                    return;
                }
            }
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(action);
            }
            catch (ObjectDisposedException)
            {
                // 窗口已关闭，丢弃后续更新
            }
            return;
        }

        action();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // 句柄建好才知道真实 DPI：先把尺寸与位置摆正，再补上接管之前累积的过程更新。
        ApplyScale();
        FitAndCenter(resize: true);

        Action[] pending;
        lock (_pendingUi)
        {
            pending = _pendingUi.ToArray();
            _pendingUi.Clear();
        }

        foreach (var action in pending)
            action();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ApplyScale();
        FitAndCenter(resize: true);
    }

    // ───────────────────────────── 内嵌控件 ─────────────────────────────

    /// <summary>记录区底板：圆角卡片，把 RichTextBox 包在边框里。</summary>
    [DesignerCategory("Code")]
    private sealed class TranscriptCard : Panel
    {
        public TranscriptCard()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            UiTheme.DrawCard(
                e.Graphics,
                new Rectangle(0, 0, Width - 1, Height - 1),
                UiTheme.CardBg,
                UiTheme.BorderSubtle,
                UiTheme.Scale(this, 10));
        }
    }

    /// <summary>圆角输入框：宿主自绘边框与底色，内部一个无边框 TextBox 负责录入。</summary>
    [DesignerCategory("Code")]
    private sealed class Composer : Panel
    {
        private readonly TextBox _input = new();
        private bool _hovered;

        public Composer()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);

            BackColor = UiTheme.InputBg;
            Cursor = Cursors.IBeam;

            _input.BorderStyle = BorderStyle.None;
            _input.BackColor = UiTheme.InputBg;
            _input.ForeColor = UiTheme.TextPrimary;
            _input.Font = UiTheme.BodyFont;
            _input.TextChanged += (_, _) => ValueChanged?.Invoke(this, EventArgs.Empty);
            _input.GotFocus += (_, _) => Invalidate();
            _input.LostFocus += (_, _) => Invalidate();
            _input.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter)
                    return;

                e.Handled = true;
                e.SuppressKeyPress = true;
                Submitted?.Invoke(this, EventArgs.Empty);
            };

            Controls.Add(_input);
            LayoutInput();
        }

        public event EventHandler? ValueChanged;

        public event EventHandler? Submitted;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Value
        {
            get => _input.Text;
            set => _input.Text = value;
        }

        public void Clear() => _input.Text = string.Empty;

        public void FocusInput() => _input.Focus();

        /// <summary>运行结束后收起输入：文本框只读，边框回到静默色。</summary>
        public void SetInteractive(bool interactive)
        {
            _input.ReadOnly = !interactive;
            _input.TabStop = interactive;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutInput();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _input.Focus();
        }

        private void LayoutInput()
        {
            var inset = UiTheme.Scale(this, 12);
            _input.Left = inset;
            _input.Width = Math.Max(UiTheme.Scale(this, 40), Width - inset * 2);
            _input.Top = Math.Max(0, (Height - _input.Height) / 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            var radius = UiTheme.Scale(this, 8);
            var border = !_input.ReadOnly
                ? _input.Focused ? UiTheme.Accent : _hovered ? UiTheme.InputHoverBorder : UiTheme.BorderSubtle
                : UiTheme.BorderSubtle;

            UiTheme.FillRounded(g, rect, radius, UiTheme.InputBg);
            UiTheme.StrokeRounded(g, rect, radius, border, 1f);
        }
    }
}

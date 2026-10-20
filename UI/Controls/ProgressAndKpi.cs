using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using ModSorterAgent.Models;

namespace ModSorterAgent.UI.Controls;

/// <summary>
/// 管线进度条：显示"当前阶段 / 总阶段 + 百分比 + 状态文案 + 耗时"。
/// 分段刻度让用户明确知道还剩几个阶段，而不是只看到一个抽象的百分比。
/// </summary>
[DesignerCategory("Code")]
internal sealed class PipelineProgressBar : Control
{
    private int _percentage;
    private int _stageIndex;
    private int _totalStages = 7;
    private long _elapsedMs;
    private string _status = "等待开始分析";
    private bool _running;

    public PipelineProgressBar()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        Height = 56;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.SurfaceBg;
    }

    public void Reset()
    {
        _percentage = 0;
        _stageIndex = 0;
        _elapsedMs = 0;
        _running = false;
        _status = "等待开始分析";
        Invalidate();
    }

    public void Begin(string status)
    {
        _percentage = 0;
        _stageIndex = 0;
        _elapsedMs = 0;
        _running = true;
        _status = status;
        Invalidate();
    }

    public void UpdateProgress(PipelineProgressReport report)
    {
        _percentage = report.Percentage;
        _stageIndex = report.StageIndex;
        _totalStages = Math.Max(1, report.TotalStages);
        _elapsedMs = report.ElapsedMs;
        _status = report.StatusText;
        _running = report.Stage is not PipelineStage.Completed and not PipelineStage.Cancelled and not PipelineStage.Faulted;
        Invalidate();
    }

    public void Finish(string status, long elapsedMs)
    {
        _percentage = 100;
        _elapsedMs = elapsedMs;
        _status = status;
        _running = false;
        Invalidate();
    }

    public void Fail(string status)
    {
        _running = false;
        _status = status;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var pad = UiTheme.Scale(this, 14);
        var topLine = new Rectangle(pad, UiTheme.Scale(this, 6), Width - pad * 2, UiTheme.Scale(this, 20));

        var statusColor = _running ? UiTheme.TextPrimary : UiTheme.TextSecondary;
        UiTheme.DrawText(g, _status, UiTheme.SmallFont, topLine, statusColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        var metaText = _running
            ? $"阶段 {Math.Max(1, _stageIndex)}/{_totalStages} · {_percentage}% · {_elapsedMs / 1000.0:F1}s"
            : $"耗时 {_elapsedMs / 1000.0:F1}s";

        UiTheme.DrawText(g, metaText, UiTheme.MonoSmallFont, topLine, UiTheme.TextMuted,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

        var barHeight = UiTheme.Scale(this, 8);
        var barRect = new Rectangle(pad, UiTheme.Scale(this, 32), Width - pad * 2, barHeight);
        var radius = barHeight / 2;

        UiTheme.FillRounded(g, barRect, radius, UiTheme.ElevatedBg);

        var filledWidth = (int)(barRect.Width * Math.Clamp(_percentage, 0, 100) / 100.0);
        if (filledWidth > 2)
        {
            var fillRect = new Rectangle(barRect.X, barRect.Y, filledWidth, barHeight);
            using var path = UiTheme.RoundedRect(fillRect, radius);
            using var gradient = new LinearGradientBrush(
                new Rectangle(barRect.X, barRect.Y, Math.Max(2, barRect.Width), barHeight),
                UiTheme.Accent,
                UiTheme.Green,
                LinearGradientMode.Horizontal);
            g.FillPath(gradient, path);
        }

        // 阶段刻度：把"还剩几步"可视化
        if (_totalStages > 1)
        {
            for (var i = 1; i < _totalStages; i++)
            {
                var x = barRect.X + barRect.Width * i / _totalStages;
                var reached = _percentage >= i * 100 / _totalStages;
                using var pen = new Pen(reached ? Color.FromArgb(120, 12, 14, 19) : UiTheme.BorderStrong, 1f);
                g.DrawLine(pen, x, barRect.Y + 1, x, barRect.Bottom - 1);
            }
        }
    }
}

internal enum KpiCard
{
    Overall,
    Green,
    Yellow,
    Red,
    Movement
}

/// <summary>
/// 顶部 KPI 指标卡条。卡片可点击，直接联动下方筛选，减少"看到数字后还要手动筛选"的往返操作。
/// </summary>
[DesignerCategory("Code")]
internal sealed class KpiStrip : Control
{
    private ModSortReport? _report;
    private int _hoverCard = -1;

    public KpiStrip()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        Height = 84;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.AppBg;
    }

    public event EventHandler<KpiCard>? CardClicked;

    public void UpdateState(ModSortReport? report)
    {
        _report = report;
        Invalidate();
    }

    private Rectangle[] GetCardRects()
    {
        var pad = UiTheme.Scale(this, 12);
        var gap = UiTheme.Scale(this, 10);
        var available = Width - pad * 2 - gap * 4;
        var height = Height - UiTheme.Scale(this, 10);

        var weights = new[] { 0.24, 0.16, 0.18, 0.18, 0.24 };
        var rects = new Rectangle[5];
        var x = pad;

        for (var i = 0; i < 5; i++)
        {
            var w = (int)(available * weights[i]);
            rects[i] = new Rectangle(x, UiTheme.Scale(this, 5), w, height);
            x += w + gap;
        }

        return rects;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var rects = GetCardRects();
        var hover = -1;
        for (var i = 0; i < rects.Length; i++)
        {
            if (rects[i].Contains(e.Location))
            {
                hover = i;
                break;
            }
        }

        if (hover != _hoverCard)
        {
            _hoverCard = hover;
            Cursor = hover >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverCard != -1)
        {
            _hoverCard = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
            return;

        var rects = GetCardRects();
        for (var i = 0; i < rects.Length; i++)
        {
            if (rects[i].Contains(e.Location))
            {
                CardClicked?.Invoke(this, (KpiCard)i);
                return;
            }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var rects = GetCardRects();

        if (_report == null)
        {
            DrawCard(g, rects[0], 0, "总体评级", "待分析", "读取列表后生成报告", UiTheme.Accent, "—");
            DrawCard(g, rects[1], 1, "1.绿色 · 无冲突", "0", "占总数 0%", UiTheme.Green, "0");
            DrawCard(g, rects[2], 2, "2.黄色 · 代码存疑", "0", "占总数 0%", UiTheme.Amber, "0");
            DrawCard(g, rects[3], 3, "3.红色 · 明确冲突", "0", "占总数 0%", UiTheme.Rose, "0");
            DrawCard(g, rects[4], 4, "顺位调整 / 实机核验", "—", "尚未执行分析", UiTheme.TextMuted, "—");
            return;
        }

        var report = _report;
        var total = Math.Max(1, report.TotalModsAnalyzed);
        var (overallAccent, _, _, _) = UiTheme.GradeStyle(report.OverallGrade);

        var overallText = report.OverallGrade switch
        {
            ConflictGrade.Green => "1.绿色 · 无冲突",
            ConflictGrade.Yellow => "2.黄色 · 存疑",
            ConflictGrade.Red => "3.红色 · 有冲突",
            _ => "未知"
        };

        // 三档统计覆盖排序集合里的全部模组（启用 + 订阅池同一分析口径），分母即总数
        var overallSub = report.WorkshopPoolModCount > 0
            ? $"共 {report.TotalModsAnalyzed} 项 · 含订阅池 {report.WorkshopPoolModCount} 项"
            : $"共 {report.TotalModsAnalyzed} 项 · 版本 {report.TargetGameVersion}";

        DrawCard(g, rects[0], 0, "总体评级", report.OverallGradeLabel.Length > 0 ? overallText : "未知",
            overallSub,
            overallAccent, report.OverallGradeLabel.Split(' ')[0]);

        DrawCard(g, rects[1], 1, "1.绿色 · 无冲突", report.GreenModCount.ToString(),
            $"占总数 {report.GreenModCount * 100 / total}%", UiTheme.Green, report.GreenModCount.ToString());

        DrawCard(g, rects[2], 2, "2.黄色 · 代码存疑", report.YellowModCount.ToString(),
            $"占总数 {report.YellowModCount * 100 / total}%", UiTheme.Amber, report.YellowModCount.ToString());

        DrawCard(g, rects[3], 3, "3.红色 · 明确冲突", report.RedModCount.ToString(),
            $"占总数 {report.RedModCount * 100 / total}% · {report.ConflictFindings.Count(f => f.Grade == ConflictGrade.Red)} 对",
            UiTheme.Rose, report.RedModCount.ToString());

        var gabsText = report.GabsVerification?.Connected == true
            ? "实机已连接"
            : report.GabsVerification != null ? "离线日志核验" : "未核验";
        DrawCard(g, rects[4], 4, "顺位调整 / 实机状态", $"{report.MovedModCount} 项变动",
            $"{gabsText} · 补丁 {report.TotalHarmonyPatchCount} 处",
            report.MovedModCount > 0 ? UiTheme.Accent : UiTheme.TextMuted, report.MovedModCount.ToString());
    }

    private void DrawCard(Graphics g, Rectangle rect, int index, string label, string value, string sub, Color accent, string badge)
    {
        var hovered = _hoverCard == index;
        var fill = hovered ? Color.FromArgb(26, 31, 41) : UiTheme.SurfaceBg;
        var border = hovered ? UiTheme.BorderStrong : UiTheme.BorderSubtle;

        UiTheme.DrawCard(g, rect, fill, border, UiTheme.Scale(this, 8));

        using (var bar = new SolidBrush(accent))
            g.FillRectangle(bar, rect.X + UiTheme.Scale(this, 1), rect.Y + UiTheme.Scale(this, 1),
                UiTheme.Scale(this, 3), rect.Height - UiTheme.Scale(this, 2));

        var contentX = rect.X + UiTheme.Scale(this, 14);
        var contentW = rect.Width - UiTheme.Scale(this, 24);

        UiTheme.DrawText(g, label, UiTheme.MetricLabelFont,
            new Rectangle(contentX, rect.Y + UiTheme.Scale(this, 7), contentW, UiTheme.Scale(this, 18)),
            UiTheme.TextSecondary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        var compact = UiTheme.MeasureWidth(value, UiTheme.MetricValueFont) > contentW - UiTheme.Scale(this, 30);
        var valueFont = compact ? UiTheme.MetricValueSmallFont : UiTheme.MetricValueFont;

        UiTheme.DrawText(g, value, valueFont,
            new Rectangle(contentX, rect.Y + UiTheme.Scale(this, 24), contentW, UiTheme.Scale(this, 30)),
            UiTheme.TextPrimary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        UiTheme.DrawText(g, sub, UiTheme.SmallFont,
            new Rectangle(contentX, rect.Bottom - UiTheme.Scale(this, 22), contentW, UiTheme.Scale(this, 18)),
            UiTheme.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

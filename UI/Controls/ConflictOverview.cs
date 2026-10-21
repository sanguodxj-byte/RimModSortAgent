using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ModSorterAgent.UI.Controls;

internal enum ConflictCard
{
    All,
    Red,
    Yellow,
    Verified
}

/// <summary>
/// 顶部冲突概览：只统计玩家真正需要处理的东西——冲突数、明确冲突、需要确认、已核查。
/// 卡片可点击，直接筛选下方冲突清单。
/// </summary>
[DesignerCategory("Code")]
internal sealed class ConflictOverview : Control
{
    private int _total;
    private int _red;
    private int _yellow;
    private int _verified;
    private int _patchable;
    private int _affectedMods;
    private string _agentMode = "尚未核查";
    private bool _hasReport;
    private int _hoverCard = -1;

    public ConflictOverview()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        Height = 86;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.AppBg;
    }

    public event EventHandler<ConflictCard>? CardClicked;

    public void UpdateState(
        bool hasReport,
        int total,
        int red,
        int yellow,
        int verified,
        int patchable,
        int affectedMods,
        string agentMode)
    {
        _hasReport = hasReport;
        _total = total;
        _red = red;
        _yellow = yellow;
        _verified = verified;
        _patchable = patchable;
        _affectedMods = affectedMods;
        _agentMode = agentMode;
        Invalidate();
    }

    private Rectangle[] GetCardRects()
    {
        var pad = UiTheme.Scale(this, 12);
        var gap = UiTheme.Scale(this, 10);
        var available = Width - pad * 2 - gap * 3;
        var height = Height - UiTheme.Scale(this, 10);

        var weights = new[] { 0.24, 0.19, 0.19, 0.38 };
        var rects = new Rectangle[4];
        var x = pad;

        for (var i = 0; i < 4; i++)
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
                CardClicked?.Invoke(this, (ConflictCard)i);
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

        if (!_hasReport)
        {
            DrawCard(g, rects[0], 0, "需要处理", "—", "尚未检查", UiTheme.Accent, showDot: false);
            DrawCard(g, rects[1], 1, "明确冲突", "0", "会让模组失效", UiTheme.Rose, showDot: false);
            DrawCard(g, rects[2], 2, "需要确认", "0", "取决于运行条件", UiTheme.Amber, showDot: false);
            DrawCard(g, rects[3], 3, "Agent 核查", "—", _agentMode, UiTheme.TextMuted, showDot: false);
            return;
        }

        DrawCard(g, rects[0], 0, "需要处理", _total.ToString(),
            _total == 0 ? "没有发现冲突" : $"{_affectedMods} 个模组涉及冲突", UiTheme.Accent, showDot: _total > 0);

        DrawCard(g, rects[1], 1, "明确冲突", _red.ToString(),
            _red == 0 ? "没有会让模组失效的问题" : "会让其中一方失效", UiTheme.Rose, showDot: _red > 0);

        DrawCard(g, rects[2], 2, "需要确认", _yellow.ToString(),
            _yellow == 0 ? "没有存疑项" : "只在特定条件下出错", UiTheme.Amber, showDot: _yellow > 0);

        var agentValue = _total == 0 ? "—" : $"{_verified} / {_total}";
        var agentSub = _total == 0
            ? _agentMode
            : _verified >= _total
                ? _patchable > 0 ? $"已核查 · 可生成 {_patchable} 个补丁" : "已全部核查"
                : _agentMode;
        DrawCard(g, rects[3], 3, "Agent 核查", agentValue, agentSub, UiTheme.Green, showDot: _verified > 0);
    }

    private void DrawCard(
        Graphics g,
        Rectangle rect,
        int index,
        string label,
        string value,
        string sub,
        Color accent,
        bool showDot)
    {
        var hovered = _hoverCard == index;
        var fill = hovered ? Color.FromArgb(27, 32, 43) : UiTheme.SurfaceBg;
        var border = hovered ? UiTheme.BorderStrong : UiTheme.BorderSubtle;

        UiTheme.DrawCard(g, rect, fill, border, UiTheme.Scale(this, 8));

        using (var bar = new SolidBrush(accent))
            g.FillRectangle(bar, rect.X + 1, rect.Y + 1, UiTheme.Scale(this, 3), rect.Height - 2);

        var contentX = rect.X + UiTheme.Scale(this, 15);
        var contentW = rect.Width - UiTheme.Scale(this, 26);

        UiTheme.DrawText(g, label, UiTheme.MetricLabelFont,
            new Rectangle(contentX, rect.Y + UiTheme.Scale(this, 7), contentW, UiTheme.Scale(this, 18)),
            showDot ? accent : UiTheme.TextSecondary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        var compact = UiTheme.MeasureWidth(value, UiTheme.MetricValueFont) > contentW - UiTheme.Scale(this, 24);
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

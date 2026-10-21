using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using ModSorterAgent.Models;

namespace ModSorterAgent.UI.Controls;

internal enum DetailValueKind
{
    Text,
    Mono,
    Strong,
    Bullet
}

internal sealed record DetailItem(string Label, string Value, DetailValueKind Kind = DetailValueKind.Text);

internal sealed record DetailBlock(string Title, IReadOnlyList<DetailItem> Items, string? Meta = null, ConflictGrade? Accent = null);

internal sealed record DetailDocument(string Title, string Subtitle, IReadOnlyList<DetailBlock> Blocks, ConflictGrade? Accent = null);

/// <summary>
/// 详情检查器：以"卡片块 + 标签值"结构呈现冲突证据、模组画像与排序变动。
/// 采用先测量后绘制的方式，滚动时不做重复排版，长文本自动换行且不裁切。
/// </summary>
[DesignerCategory("Code")]
internal sealed class DetailInspector : Control, IWheelScrollable
{
    private readonly List<BlockLayout> _layout = new();
    private DetailDocument? _document;
    private int _scrollOffset;
    private int _contentHeight;
    private bool _draggingThumb;
    private int _dragStartY;
    private int _dragStartOffset;
    private int _lastMeasuredWidth = -1;

    public DetailInspector()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = UiTheme.SurfaceBg;
        Dock = DockStyle.Fill;
    }

    private sealed record ItemLayout(Rectangle Bounds, DetailItem Item, int TextHeight);

    private sealed record BlockLayout(Rectangle Bounds, DetailBlock Block, List<ItemLayout> Items);

    public void ShowDocument(DetailDocument? document)
    {
        _document = document;
        _scrollOffset = 0;
        _lastMeasuredWidth = -1;
        RebuildLayout();
        Invalidate();
    }

    private int ScrollbarWidth => _contentHeight > ClientSize.Height ? UiTheme.Scale(this, 12) : 0;

    private int MaxScroll => Math.Max(0, _contentHeight - ClientSize.Height);

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_document != null && Math.Abs(Width - _lastMeasuredWidth) > 2)
            RebuildLayout();

        _scrollOffset = Math.Clamp(_scrollOffset, 0, MaxScroll);
        Invalidate();
    }

    private void RebuildLayout()
    {
        _layout.Clear();
        _contentHeight = 0;

        if (_document == null || Width <= 0)
            return;

        using var g = CreateGraphics();
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var pad = UiTheme.Scale(this, 12);
        var cardWidth = Math.Max(120, ClientSize.Width - pad * 2 - ScrollbarWidth);
        var labelWidth = UiTheme.Scale(this, 84);
        var innerPadX = UiTheme.Scale(this, 14);
        var valueWidth = Math.Max(80, cardWidth - labelWidth - innerPadX * 2 - UiTheme.Scale(this, 8));
        var lineHeight = UiTheme.Scale(this, 18);
        var y = pad;

        foreach (var block in _document.Blocks)
        {
            var titleHeight = UiTheme.Scale(this, 24);
            var items = new List<ItemLayout>(block.Items.Count);
            var contentY = y + UiTheme.Scale(this, 8) + titleHeight;

            foreach (var item in block.Items)
            {
                var font = item.Kind switch
                {
                    DetailValueKind.Mono => UiTheme.MonoSmallFont,
                    DetailValueKind.Strong => UiTheme.BodyBoldFont,
                    _ => UiTheme.SmallFont
                };

                var textHeight = Math.Max(lineHeight,
                    UiTheme.WrapHeight(g, item.Value, font, valueWidth, lineHeight));

                items.Add(new ItemLayout(
                    new Rectangle(pad + cardWidth - valueWidth - innerPadX, contentY, valueWidth, textHeight),
                    item,
                    textHeight));

                contentY += textHeight + UiTheme.Scale(this, 5);
            }

            var blockHeight = contentY - y + UiTheme.Scale(this, 10);
            _layout.Add(new BlockLayout(
                new Rectangle(pad, y, cardWidth, blockHeight),
                block,
                items));

            y += blockHeight + UiTheme.Scale(this, 8);
        }

        _contentHeight = y + pad;
        _lastMeasuredWidth = Width;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ScrollBy(-Math.Sign(e.Delta) * UiTheme.Scale(this, 72));
    }

    public void ScrollByWheel(int delta)
    {
        ScrollBy(-Math.Sign(delta) * UiTheme.Scale(this, 68));
    }

    private void ScrollBy(int delta)
    {
        if (MaxScroll <= 0)
            return;

        _scrollOffset = Math.Clamp(_scrollOffset + delta, 0, MaxScroll);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (e.Button != MouseButtons.Left || ScrollbarWidth == 0)
            return;

        if (e.X < ClientSize.Width - ScrollbarWidth)
            return;

        var trackTop = UiTheme.Scale(this, 4);
        var trackHeight = Math.Max(1, ClientSize.Height - trackTop * 2);
        var thumbHeight = GetThumbHeight(trackHeight);
        var thumbTop = trackTop + (int)((double)_scrollOffset / Math.Max(1, MaxScroll) * (trackHeight - thumbHeight));

        if (e.Y >= thumbTop && e.Y <= thumbTop + thumbHeight)
        {
            _draggingThumb = true;
            _dragStartY = e.Y;
            _dragStartOffset = _scrollOffset;
            Capture = true;
        }
        else
        {
            ScrollBy(e.Y < thumbTop ? -ClientSize.Height : ClientSize.Height);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_draggingThumb)
            return;

        var trackTop = UiTheme.Scale(this, 4);
        var trackHeight = Math.Max(1, ClientSize.Height - trackTop * 2);
        var thumbHeight = GetThumbHeight(trackHeight);
        var scrollable = Math.Max(1, trackHeight - thumbHeight);
        var target = _dragStartOffset + (int)((double)(e.Y - _dragStartY) / scrollable * MaxScroll);
        _scrollOffset = Math.Clamp(target, 0, MaxScroll);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_draggingThumb)
        {
            _draggingThumb = false;
            Capture = false;
        }
    }

    private int GetThumbHeight(int trackHeight)
    {
        if (_contentHeight <= 0)
            return trackHeight;

        return Math.Max(UiTheme.Scale(this, 30),
            (int)((double)ClientSize.Height / _contentHeight * trackHeight));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using (var bg = new SolidBrush(UiTheme.SurfaceBg))
            g.FillRectangle(bg, ClientRectangle);

        if (_document == null)
        {
            PaintPlaceholder(g);
            return;
        }

        if (_layout.Count == 0 || Math.Abs(Width - _lastMeasuredWidth) > 2)
            RebuildLayout();

        var previous = g.Clip;
        g.Clip = new Region(ClientRectangle);

        foreach (var block in _layout)
        {
            var rect = new Rectangle(
                block.Bounds.X,
                block.Bounds.Y - _scrollOffset,
                block.Bounds.Width,
                block.Bounds.Height);

            if (rect.Bottom < -20 || rect.Top > ClientSize.Height + 20)
                continue;

            PaintBlock(g, block, rect);
        }

        g.Clip = previous;
        PaintScrollbar(g);
    }

    private void PaintPlaceholder(Graphics g)
    {
        var area = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
        UiTheme.DrawText(g, "未选中模组", UiTheme.SectionFont,
            new Rectangle(area.X + 20, area.Y + area.Height / 2 - 24, area.Width - 40, 26),
            UiTheme.TextSecondary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private void PaintBlock(Graphics g, BlockLayout block, Rectangle rect)
    {
        var accent = block.Block.Accent.HasValue
            ? UiTheme.GradeStyle(block.Block.Accent.Value).Accent
            : UiTheme.Accent;

        UiTheme.DrawCard(g, rect, UiTheme.CardBg, UiTheme.BorderSubtle, UiTheme.Scale(this, 7));

        using (var bar = new SolidBrush(accent))
            g.FillRectangle(bar, rect.X + 1, rect.Y + UiTheme.Scale(this, 10),
                UiTheme.Scale(this, 3), UiTheme.Scale(this, 14));

        var metaWidth = string.IsNullOrEmpty(block.Block.Meta)
            ? 0
            : UiTheme.MeasureWidth(block.Block.Meta, UiTheme.SmallFont) + UiTheme.Scale(this, 14);

        var titleRect = new Rectangle(
            rect.X + UiTheme.Scale(this, 12),
            rect.Y + UiTheme.Scale(this, 6),
            Math.Max(UiTheme.Scale(this, 60), rect.Width - UiTheme.Scale(this, 24) - metaWidth),
            UiTheme.Scale(this, 22));

        UiTheme.DrawText(g, block.Block.Title, UiTheme.BodyBoldFont, titleRect, UiTheme.TextPrimary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (metaWidth > 0)
        {
            var metaRect = new Rectangle(
                rect.Right - metaWidth - UiTheme.Scale(this, 10),
                rect.Y + UiTheme.Scale(this, 6),
                metaWidth,
                UiTheme.Scale(this, 22));

            UiTheme.DrawText(g, block.Block.Meta ?? string.Empty, UiTheme.SmallFont, metaRect, UiTheme.TextMuted,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }

        foreach (var item in block.Items)
        {
            var valueRect = new Rectangle(
                item.Bounds.X,
                item.Bounds.Y - _scrollOffset,
                item.Bounds.Width,
                item.TextHeight);

            if (valueRect.Bottom < rect.Y || valueRect.Top > rect.Bottom)
                continue;

            var labelRect = new Rectangle(
                rect.X + UiTheme.Scale(this, 14),
                valueRect.Y,
                UiTheme.Scale(this, 78),
                UiTheme.Scale(this, 18));

            if (!string.IsNullOrEmpty(item.Item.Label))
            {
                UiTheme.DrawText(g, item.Item.Label, UiTheme.SmallFont, labelRect, UiTheme.TextMuted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            var (font, color) = item.Item.Kind switch
            {
                DetailValueKind.Mono => (UiTheme.MonoSmallFont, UiTheme.TextSecondary),
                DetailValueKind.Strong => (UiTheme.BodyBoldFont, UiTheme.TextPrimary),
                DetailValueKind.Bullet => (UiTheme.SmallFont, UiTheme.TextSecondary),
                _ => (UiTheme.SmallFont, UiTheme.TextPrimary)
            };

            var text = item.Item.Kind == DetailValueKind.Bullet ? $"• {item.Item.Value}" : item.Item.Value;
            UiTheme.DrawWrapped(g, text, font, valueRect, color, UiTheme.Scale(this, 18));
        }
    }

    private void PaintScrollbar(Graphics g)
    {
        if (ScrollbarWidth == 0)
            return;

        var margin = UiTheme.Scale(this, 4);
        var trackRect = new Rectangle(ClientSize.Width - ScrollbarWidth, margin, ScrollbarWidth,
            Math.Max(1, ClientSize.Height - margin * 2));

        using (var track = new SolidBrush(UiTheme.ScrollTrack))
            g.FillRectangle(track, trackRect);

        var thumbHeight = GetThumbHeight(trackRect.Height);
        var thumbTop = trackRect.Y + (int)((double)_scrollOffset / Math.Max(1, MaxScroll) * (trackRect.Height - thumbHeight));

        UiTheme.FillRounded(g,
            new Rectangle(trackRect.X + 2, thumbTop, ScrollbarWidth - 4, thumbHeight),
            (ScrollbarWidth - 4) / 2,
            _draggingThumb ? UiTheme.ScrollThumbHover : UiTheme.ScrollThumb);
    }
}

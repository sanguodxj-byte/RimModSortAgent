using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using ModSorterAgent.Models;

namespace ModSorterAgent.UI.Controls;

internal enum CellKind
{
    Text,
    Emphasis,
    Mono,
    Number,
    Grade,
    Delta,
    Tier,
    Paired,

    /// <summary>启用状态：已启用显示为低对比灰，未启用显示为琥珀色，便于一眼区分订阅池。</summary>
    Status,

    /// <summary>弱化的说明性文本（例如"仅 About"），不参与语义着色。</summary>
    Note
}

internal sealed record UiCell(string Text, CellKind Kind = CellKind.Text, string? SecondaryText = null);

internal sealed record UiRow(IReadOnlyList<UiCell> Cells, object? Tag = null);

internal sealed record UiColumn(string Header, float Weight, int MinWidth = 60, bool RightAligned = false);

/// <summary>
/// 自绘虚拟列表。相比 <see cref="DataGridView"/>：
/// 1) 只绘制可见行，千行列表也能保持 60fps 滚动；
/// 2) 滚动条、选中、悬停、键盘导航全部可控，深浅色主题统一；
/// 3) 单元格按语义类型渲染（评级徽章、顺位变动、等宽代码），不需要在业务层拼字符串。
/// </summary>
[DesignerCategory("Code")]
internal sealed class VirtualListView : Control, IWheelScrollable
{
    private readonly List<UiColumn> _columns = new();
    private readonly List<UiRow> _rows = new();
    private readonly List<int> _rowOffsets = new();

    private int _scrollOffset;
    private int _hoverIndex = -1;
    private int _selectedIndex = -1;
    private bool _draggingThumb;
    private int _dragStartY;
    private int _dragStartOffset;
    private int _contentHeight;

    public VirtualListView()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        BackColor = UiTheme.SurfaceBg;
        ForeColor = UiTheme.TextPrimary;
        Font = UiTheme.BodyFont;
        TabStop = true;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyDpiMetrics();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyDpiMetrics();
    }

    private void ApplyDpiMetrics()
    {
        RowHeight = UiTheme.Scale(this, 34);
        HeaderHeight = UiTheme.Scale(this, 32);
        RecalculateOffsets();
        Invalidate();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

    public int RowHeight { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

    public int HeaderHeight { get; set; } = 34;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

    public string EmptyText { get; set; } = "暂无数据";

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

    public string EmptyHint { get; set; } = string.Empty;

    public int SelectedIndex => _selectedIndex;

    public UiRow? SelectedRow => _selectedIndex >= 0 && _selectedIndex < _rows.Count ? _rows[_selectedIndex] : null;

    public event EventHandler? SelectionChanged;

    public event EventHandler? RowActivated;

    public void SetColumns(params UiColumn[] columns)
    {
        _columns.Clear();
        _columns.AddRange(columns);
        Invalidate();
    }

    public void SetRows(IReadOnlyList<UiRow> rows, bool preserveSelection = true)
    {
        var previousTag = preserveSelection ? SelectedRow?.Tag : null;

        _rows.Clear();
        _rows.AddRange(rows);
        RecalculateOffsets();

        _selectedIndex = -1;
        if (previousTag != null)
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                if (ReferenceEquals(_rows[i].Tag, previousTag))
                {
                    _selectedIndex = i;
                    break;
                }
            }
        }

        _scrollOffset = Math.Clamp(_scrollOffset, 0, MaxScrollOffset);
        _hoverIndex = -1;

        Invalidate();
        if (preserveSelection && previousTag != null)
            SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectIndex(int index, bool scrollIntoView = true)
    {
        if (_rows.Count == 0)
            return;

        index = Math.Clamp(index, 0, _rows.Count - 1);
        if (index == _selectedIndex)
            return;

        _selectedIndex = index;

        if (scrollIntoView)
            EnsureVisible(index);

        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectFirst()
    {
        if (_rows.Count > 0)
            SelectIndex(0);
    }

    public void ClearSelection()
    {
        if (_selectedIndex < 0)
            return;

        _selectedIndex = -1;
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private int MaxScrollOffset => Math.Max(0, _contentHeight - BodyHeight);

    private int BodyHeight => Math.Max(0, ClientSize.Height - HeaderHeight);

    private int EffectiveScrollbarWidth => _contentHeight > BodyHeight ? UiTheme.Scale(this, 12) : 0;

    private void RecalculateOffsets()
    {
        _rowOffsets.Clear();
        var y = 0;
        foreach (var row in _rows)
        {
            _rowOffsets.Add(y);
            y += GetRowHeight(row);
        }
        _contentHeight = y;
    }

    private int GetRowHeight(UiRow row)
    {
        return row.Cells.Any(c => c.Kind == CellKind.Paired) ? RowHeight + UiTheme.Scale(this, 16) : RowHeight;
    }

    private void EnsureVisible(int index)
    {
        if (index < 0 || index >= _rowOffsets.Count)
            return;

        var top = _rowOffsets[index];
        var bottom = top + GetRowHeight(_rows[index]);

        if (top < _scrollOffset)
            _scrollOffset = top;
        else if (bottom > _scrollOffset + BodyHeight)
            _scrollOffset = bottom - BodyHeight;

        _scrollOffset = Math.Clamp(_scrollOffset, 0, MaxScrollOffset);
    }

    private int HitTestRow(int y)
    {
        if (y < HeaderHeight)
            return -1;

        var local = y - HeaderHeight + _scrollOffset;
        for (var i = 0; i < _rowOffsets.Count; i++)
        {
            var height = GetRowHeight(_rows[i]);
            if (local >= _rowOffsets[i] && local < _rowOffsets[i] + height)
                return i;
        }

        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_draggingThumb)
        {
            var trackHeight = Math.Max(1, BodyHeight - 2 * UiTheme.Scale(this, 4));
            var thumbHeight = GetThumbHeight(trackHeight);
            var scrollable = Math.Max(1, trackHeight - thumbHeight);
            var delta = e.Y - _dragStartY;
            var target = _dragStartOffset + (int)((double)delta / scrollable * MaxScrollOffset);
            _scrollOffset = Math.Clamp(target, 0, MaxScrollOffset);
            Invalidate();
            return;
        }

        var index = HitTestRow(e.Y);
        if (index != _hoverIndex)
        {
            _hoverIndex = index;
            Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverIndex != -1)
        {
            _hoverIndex = -1;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (e.Button != MouseButtons.Left)
            return;

        var scrollbarWidth = EffectiveScrollbarWidth;
        if (scrollbarWidth > 0 && e.X >= ClientSize.Width - scrollbarWidth)
        {
            var trackHeight = Math.Max(1, BodyHeight - 2 * UiTheme.Scale(this, 4));
            var thumbHeight = GetThumbHeight(trackHeight);
            var thumbTop = HeaderHeight + UiTheme.Scale(this, 4) + (int)((double)_scrollOffset / Math.Max(1, MaxScrollOffset) * (trackHeight - thumbHeight));
            var thumbRect = new Rectangle(ClientSize.Width - scrollbarWidth + 2, thumbTop, scrollbarWidth - 4, thumbHeight);

            if (thumbRect.Contains(e.Location))
            {
                _draggingThumb = true;
                _dragStartY = e.Y;
                _dragStartOffset = _scrollOffset;
                Capture = true;
                return;
            }

            // 点击轨道：翻页
            var direction = e.Y < thumbTop ? -1 : 1;
            _scrollOffset = Math.Clamp(_scrollOffset + direction * BodyHeight, 0, MaxScrollOffset);
            Invalidate();
            return;
        }

        var index = HitTestRow(e.Y);
        if (index >= 0)
            SelectIndex(index, scrollIntoView: false);
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

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        var index = HitTestRow(e.Y);
        if (index >= 0)
        {
            SelectIndex(index, scrollIntoView: false);
            RowActivated?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ScrollBy(-Math.Sign(e.Delta) * Math.Max(RowHeight, UiTheme.Scale(this, 56)));
    }

    public void ScrollByWheel(int delta)
    {
        ScrollBy(-Math.Sign(delta) * Math.Max(RowHeight, UiTheme.Scale(this, 54)));
    }

    private void ScrollBy(int delta)
    {
        if (MaxScrollOffset <= 0)
            return;

        _scrollOffset = Math.Clamp(_scrollOffset + delta, 0, MaxScrollOffset);
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return keyData switch
        {
            Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End => true,
            _ => base.IsInputKey(keyData)
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var pageSize = Math.Max(1, BodyHeight / Math.Max(1, RowHeight) - 1);

        switch (e.KeyCode)
        {
            case Keys.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Keys.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Keys.PageDown:
                MoveSelection(pageSize);
                e.Handled = true;
                break;
            case Keys.PageUp:
                MoveSelection(-pageSize);
                e.Handled = true;
                break;
            case Keys.Home:
                if (_rows.Count > 0)
                {
                    SelectIndex(0);
                    e.Handled = true;
                }
                break;
            case Keys.End:
                if (_rows.Count > 0)
                {
                    SelectIndex(_rows.Count - 1);
                    e.Handled = true;
                }
                break;
            case Keys.Enter:
                if (_selectedIndex >= 0)
                {
                    RowActivated?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                }
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        if (_rows.Count == 0)
            return;

        var next = _selectedIndex < 0
            ? 0
            : Math.Clamp(_selectedIndex + delta, 0, _rows.Count - 1);

        SelectIndex(next);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _scrollOffset = Math.Clamp(_scrollOffset, 0, MaxScrollOffset);
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using (var bg = new SolidBrush(UiTheme.SurfaceBg))
            g.FillRectangle(bg, ClientRectangle);

        if (_columns.Count == 0)
            return;

        var widths = ComputeColumnWidths();
        PaintHeader(g, widths);

        if (_rows.Count == 0)
        {
            PaintEmptyState(g);
            return;
        }

        var first = FindFirstVisibleRow();
        var y = HeaderHeight - (_scrollOffset - (first < _rowOffsets.Count ? _rowOffsets[first] : 0));

        using var clip = new Region(new Rectangle(0, HeaderHeight, ClientSize.Width, BodyHeight));
        var previousClip = g.Clip;
        g.Clip = clip;

        for (var i = first; i < _rows.Count; i++)
        {
            var rowHeight = GetRowHeight(_rows[i]);
            if (y > ClientSize.Height)
                break;

            if (y + rowHeight >= HeaderHeight)
                PaintRow(g, i, y, rowHeight, widths);

            y += rowHeight;
        }

        g.Clip = previousClip;
        PaintScrollbar(g);
        PaintFocusRing(g);
    }

    private int FindFirstVisibleRow()
    {
        for (var i = 0; i < _rowOffsets.Count; i++)
        {
            if (_rowOffsets[i] + GetRowHeight(_rows[i]) > _scrollOffset)
                return i;
        }
        return 0;
    }

    private int[] ComputeColumnWidths()
    {
        var scrollbarWidth = EffectiveScrollbarWidth;
        var padding = UiTheme.Scale(this, 12);
        var available = Math.Max(0, ClientSize.Width - scrollbarWidth - padding);

        var widths = new int[_columns.Count];
        var totalWeight = _columns.Sum(c => c.Weight);
        if (totalWeight <= 0)
            totalWeight = _columns.Count;

        var remaining = available;
        var flexible = new List<int>();
        for (var i = 0; i < _columns.Count; i++)
        {
            var min = UiTheme.Scale(this, _columns[i].MinWidth);
            var desired = (int)(available * (_columns[i].Weight / totalWeight));
            if (desired < min)
            {
                widths[i] = min;
                remaining -= min;
            }
            else
            {
                widths[i] = -1;
                flexible.Add(i);
            }
        }

        if (flexible.Count > 0)
        {
            var flexibleWeight = flexible.Sum(i => _columns[i].Weight);
            if (flexibleWeight <= 0)
                flexibleWeight = flexible.Count;

            foreach (var i in flexible)
            {
                widths[i] = Math.Max(
                    UiTheme.Scale(this, _columns[i].MinWidth),
                    (int)(Math.Max(0, remaining) * (_columns[i].Weight / flexibleWeight)));
            }
        }

        return widths;
    }

    private void PaintHeader(Graphics g, int[] widths)
    {
        var headerRect = new Rectangle(0, 0, ClientSize.Width, HeaderHeight);
        using (var bg = new SolidBrush(UiTheme.ElevatedBg))
            g.FillRectangle(bg, headerRect);

        var padding = UiTheme.Scale(this, 12);
        var x = padding;
        var scrollbarWidth = EffectiveScrollbarWidth;

        for (var i = 0; i < _columns.Count; i++)
        {
            var column = _columns[i];
            var rect = new Rectangle(x, 0, widths[i], HeaderHeight);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                        (column.RightAligned ? TextFormatFlags.Right : TextFormatFlags.Left);

            UiTheme.DrawText(g, column.Header, UiTheme.SmallBoldFont, rect, UiTheme.TextSecondary, flags);
            x += widths[i];
        }

        using var pen = new Pen(UiTheme.BorderSubtle, 1f);
        g.DrawLine(pen, 0, HeaderHeight - 1, ClientSize.Width - scrollbarWidth, HeaderHeight - 1);
    }

    private void PaintRow(Graphics g, int index, int y, int height, int[] widths)
    {
        var row = _rows[index];
        var isSelected = index == _selectedIndex;
        var isHovered = index == _hoverIndex;

        var background = isSelected
            ? UiTheme.RowSelectedBg
            : isHovered
                ? UiTheme.RowHoverBg
                : index % 2 == 1
                    ? UiTheme.RowAltBg
                    : UiTheme.SurfaceBg;

        var rowRect = new Rectangle(0, y, ClientSize.Width, height);
        using (var brush = new SolidBrush(background))
            g.FillRectangle(brush, rowRect);

        if (isSelected)
        {
            using var accent = new SolidBrush(UiTheme.Accent);
            g.FillRectangle(accent, 0, y, UiTheme.Scale(this, 3), height);
        }

        var padding = UiTheme.Scale(this, 12);
        var x = padding;

        for (var i = 0; i < _columns.Count && i < row.Cells.Count; i++)
        {
            var cell = row.Cells[i];
            var rect = new Rectangle(x, y, widths[i], height);
            PaintCell(g, cell, rect, isSelected, _columns[i].RightAligned);
            x += widths[i];
        }

        using var separator = new Pen(Color.FromArgb(28, 33, 43), 1f);
        g.DrawLine(separator, 0, y + height - 1, ClientSize.Width, y + height - 1);
    }

    private void PaintCell(Graphics g, UiCell cell, Rectangle rect, bool isSelected, bool rightAligned)
    {
        var inset = UiTheme.Scale(this, 4);

        switch (cell.Kind)
        {
            case CellKind.Grade:
            {
                var grade = ParseGrade(cell.Text);
                var (accent, pillBg, pillText, gradeLabel) = UiTheme.GradeStyle(grade);
                var label = cell.Text.StartsWith("1.", StringComparison.Ordinal) ||
                            cell.Text.StartsWith("2.", StringComparison.Ordinal) ||
                            cell.Text.StartsWith("3.", StringComparison.Ordinal)
                    ? gradeLabel
                    : cell.Text;
                var pillWidth = Math.Min(
                    rect.Width - inset * 2,
                    Math.Max(UiTheme.Scale(this, 62), UiTheme.MeasureWidth(label, UiTheme.PillFont) + UiTheme.Scale(this, 22)));
                var pillHeight = Math.Min(rect.Height - UiTheme.Scale(this, 12), UiTheme.Scale(this, 22));
                var pillRect = new Rectangle(
                    rect.X + inset,
                    rect.Y + (rect.Height - pillHeight) / 2,
                    Math.Max(UiTheme.Scale(this, 48), pillWidth),
                    pillHeight);

                UiTheme.DrawPill(g, pillRect, label, accent, pillBg, pillText, UiTheme.PillFont);
                return;
            }

            case CellKind.Delta:
            {
                var text = cell.Text;
                var color = text.StartsWith('▲') ? UiTheme.GreenText
                    : text.StartsWith('▼') ? UiTheme.AmberText
                    : UiTheme.TextMuted;

                UiTheme.DrawText(g, text, UiTheme.SmallBoldFont,
                    new Rectangle(rect.X + inset, rect.Y, rect.Width - inset * 2, rect.Height),
                    color,
                    rightAligned ? TextFormatFlags.Right : TextFormatFlags.Left);
                return;
            }

            case CellKind.Status:
            {
                var color = cell.Text switch
                {
                    "未启用" or "待核查" or "可生成" or "条件性冲突" => UiTheme.AmberText,
                    "已核查" or "已生成" or "已安装" or "静态误报" => UiTheme.GreenText,
                    "确认冲突" or "生成失败" => UiTheme.RoseText,
                    _ => isSelected ? UiTheme.TextSecondary : UiTheme.TextMuted
                };

                UiTheme.DrawText(g, cell.Text, UiTheme.SmallFont,
                    new Rectangle(rect.X + inset, rect.Y, rect.Width - inset * 2, rect.Height),
                    color,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                return;
            }

            case CellKind.Note:
            {
                UiTheme.DrawText(g, cell.Text, UiTheme.SmallFont,
                    new Rectangle(rect.X + inset, rect.Y, rect.Width - inset * 2, rect.Height),
                    isSelected ? UiTheme.TextSecondary : UiTheme.TextMuted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                return;
            }

            case CellKind.Tier:
            {
                UiTheme.DrawText(g, cell.Text, UiTheme.SmallFont,
                    new Rectangle(rect.X + inset, rect.Y, rect.Width - inset * 2, rect.Height),
                    isSelected ? UiTheme.TextPrimary : UiTheme.Accent,
                    TextFormatFlags.Left);
                return;
            }

            case CellKind.Mono:
            {
                var color = isSelected ? UiTheme.TextPrimary : UiTheme.TextSecondary;
                UiTheme.DrawText(g, cell.Text, UiTheme.MonoSmallFont,
                    new Rectangle(rect.X + inset, rect.Y, rect.Width - inset * 2, rect.Height),
                    color,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                return;
            }

            case CellKind.Number:
            {
                var color = isSelected ? UiTheme.TextPrimary : UiTheme.TextSecondary;
                UiTheme.DrawText(g, cell.Text, UiTheme.MonoSmallFont,
                    new Rectangle(rect.X, rect.Y, rect.Width - inset, rect.Height),
                    color,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                return;
            }

            case CellKind.Paired:
            {
                var primaryColor = isSelected ? UiTheme.TextPrimary : UiTheme.TextPrimary;
                var secondaryColor = isSelected ? UiTheme.TextSecondary : UiTheme.TextMuted;
                var lineHeight = UiTheme.Scale(this, 18);
                var top = rect.Y + (rect.Height - lineHeight * 2) / 2;

                UiTheme.DrawText(g, cell.Text, UiTheme.SmallBoldFont,
                    new Rectangle(rect.X + inset, top, rect.Width - inset * 2, lineHeight),
                    primaryColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                UiTheme.DrawText(g, cell.SecondaryText ?? string.Empty, UiTheme.SmallFont,
                    new Rectangle(rect.X + inset, top + lineHeight, rect.Width - inset * 2, lineHeight),
                    secondaryColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                return;
            }

            case CellKind.Emphasis:
            {
                var color = isSelected ? Color.White : UiTheme.TextPrimary;
                UiTheme.DrawText(g, cell.Text, UiTheme.BodyBoldFont,
                    new Rectangle(rect.X + inset, rect.Y, rect.Width - inset * 2, rect.Height),
                    color,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                return;
            }

            default:
            {
                var color = isSelected ? UiTheme.TextPrimary : UiTheme.TextSecondary;
                UiTheme.DrawText(g, cell.Text, UiTheme.BodyFont,
                    new Rectangle(rect.X + inset, rect.Y, rect.Width - inset * 2, rect.Height),
                    color,
                    (rightAligned ? TextFormatFlags.Right : TextFormatFlags.Left) |
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                return;
            }
        }
    }

    private static ConflictGrade ParseGrade(string text)
    {
        if (text.StartsWith("3.", StringComparison.Ordinal) || text == "明确冲突")
            return ConflictGrade.Red;
        if (text.StartsWith("2.", StringComparison.Ordinal) || text == "需要确认")
            return ConflictGrade.Yellow;
        return ConflictGrade.Green;
    }

    private void PaintEmptyState(Graphics g)
    {
        var area = new Rectangle(0, HeaderHeight, ClientSize.Width, BodyHeight);
        var centerY = area.Y + area.Height / 2;

        var titleRect = new Rectangle(area.X + 24, centerY - UiTheme.Scale(this, 28), area.Width - 48, UiTheme.Scale(this, 26));
        UiTheme.DrawText(g, EmptyText, UiTheme.SectionFont, titleRect, UiTheme.TextSecondary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        if (string.IsNullOrWhiteSpace(EmptyHint))
            return;

        var hintRect = new Rectangle(area.X + 24, centerY + UiTheme.Scale(this, 2), area.Width - 48, UiTheme.Scale(this, 44));
        UiTheme.DrawText(g, EmptyHint, UiTheme.SmallFont, hintRect, UiTheme.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
    }

    private int GetThumbHeight(int trackHeight)
    {
        if (_contentHeight <= 0)
            return trackHeight;

        return Math.Max(UiTheme.Scale(this, 30), (int)((double)BodyHeight / _contentHeight * trackHeight));
    }

    private void PaintScrollbar(Graphics g)
    {
        var scrollbarWidth = EffectiveScrollbarWidth;
        if (scrollbarWidth <= 0)
            return;

        var margin = UiTheme.Scale(this, 4);
        var trackRect = new Rectangle(ClientSize.Width - scrollbarWidth, HeaderHeight + margin, scrollbarWidth, Math.Max(1, BodyHeight - margin * 2));
        using (var track = new SolidBrush(UiTheme.ScrollTrack))
            g.FillRectangle(track, trackRect);

        var thumbHeight = GetThumbHeight(trackRect.Height);
        var maxOffset = Math.Max(1, MaxScrollOffset);
        var thumbTop = trackRect.Y + (int)((double)_scrollOffset / maxOffset * (trackRect.Height - thumbHeight));
        var thumbRect = new Rectangle(trackRect.X + 2, thumbTop, scrollbarWidth - 4, thumbHeight);

        var thumbColor = _draggingThumb ? UiTheme.ScrollThumbHover
            : _hoverIndex >= 0 ? UiTheme.ScrollThumb
            : UiTheme.ScrollThumb;

        UiTheme.FillRounded(g, thumbRect, (scrollbarWidth - 4) / 2, thumbColor);
    }

    private void PaintFocusRing(Graphics g)
    {
        if (!Focused)
            return;

        using var pen = new Pen(Color.FromArgb(90, UiTheme.Accent), 1f);
        g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }
}

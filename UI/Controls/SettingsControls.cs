using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ModSorterAgent.UI.Controls;

/// <summary>
/// 圆角卡片容器：设置窗口中所有分区共用。子控件按卡片背景色绘制，
/// 由 Region 负责裁切圆角，避免 WinForms 透明背景的嵌套绘制问题。
/// </summary>
[DesignerCategory("Code")]
internal sealed class RoundedCard : Panel
{
    private int _cornerRadius = 10;

    public RoundedCard()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = UiTheme.CardBg;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = value;
            UpdateRegion();
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = UiTheme.BorderSubtle;

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRegion();
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
            return;

        using var path = UiTheme.RoundedRect(
            new Rectangle(0, 0, Width, Height),
            UiTheme.Scale(this, _cornerRadius));
        Region = new Region(path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using var path = UiTheme.RoundedRect(
            new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)),
            UiTheme.Scale(this, _cornerRadius));
        using var pen = new Pen(BorderColor, 1f);
        g.DrawPath(pen, path);
    }
}

/// <summary>
/// 设置页输入框容器：圆角边框 + 内嵌无边框 TextBox + 右侧文字动作（浏览 / 检测）。
/// 动作区自己绘制并处理点击，避免在深色输入框里嵌套透明子控件的绘制缺陷。
/// </summary>
[DesignerCategory("Code")]
internal sealed class SettingsField : Control
{
    private sealed class FieldAction
    {
        public required string Text { get; init; }
        public required Action Invoke { get; init; }
        public bool Accent { get; init; }
        public Rectangle Bounds;
        public bool Pressed;
    }

    private readonly TextBox _input;
    private readonly Label _placeholder;
    private readonly List<FieldAction> _actions = new();
    private bool _hovered;
    private int _hoverAction = -1;
    private int _pressedAction = -1;

    public SettingsField()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        Height = 36;
        BackColor = UiTheme.InputBg;
        Cursor = Cursors.IBeam;

        _input = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = UiTheme.InputBg,
            ForeColor = UiTheme.TextPrimary,
            Font = UiTheme.BodyFont,
            TabIndex = 0
        };
        _input.TextChanged += (_, _) =>
        {
            UpdatePlaceholder();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        };
        _input.GotFocus += (_, _) => { UpdatePlaceholder(); Invalidate(); };
        _input.LostFocus += (_, _) => { UpdatePlaceholder(); Invalidate(); };

        _placeholder = new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            BackColor = UiTheme.InputBg,
            ForeColor = UiTheme.TextMuted,
            Font = UiTheme.BodyFont,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _placeholder.Click += (_, _) => _input.Focus();
        _placeholder.Cursor = Cursors.IBeam;

        Controls.Add(_input);
        Controls.Add(_placeholder);
        UpdateRegion();
    }

    public TextBox Input => _input;

    [AllowNull]
    public override string Text
    {
        get => _input.Text;
        set => _input.Text = value;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string PlaceholderText
    {
        get => _placeholder.Text;
        set
        {
            _placeholder.Text = value;
            UpdatePlaceholder();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool UseSystemPasswordChar
    {
        get => _input.UseSystemPasswordChar;
        set => _input.UseSystemPasswordChar = value;
    }

    public event EventHandler? ValueChanged;

    public void AddAction(string text, Action invoke, bool accent = false)
    {
        _actions.Add(new FieldAction { Text = text, Invoke = invoke, Accent = accent });
        LayoutContent();
        Invalidate();
    }

    public void SelectAll()
    {
        _input.Focus();
        _input.SelectAll();
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
            return;

        using var path = UiTheme.RoundedRect(
            new Rectangle(0, 0, Width, Height),
            UiTheme.Scale(this, 8));
        Region = new Region(path);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRegion();
        LayoutContent();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateRegion();
        LayoutContent();
    }

    private void LayoutContent()
    {
        if (Width <= 0 || Height <= 0)
            return;

        var right = Width - UiTheme.Scale(this, 7);
        for (var i = _actions.Count - 1; i >= 0; i--)
        {
            var action = _actions[i];
            var width = UiTheme.MeasureWidth(action.Text, UiTheme.SmallBoldFont) + UiTheme.Scale(this, 20);
            var height = UiTheme.Scale(this, 24);
            action.Bounds = new Rectangle(right - width, (Height - height) / 2, width, height);
            right -= width + UiTheme.Scale(this, 5);
        }

        var left = UiTheme.Scale(this, 12);
        var inputWidth = Math.Max(UiTheme.Scale(this, 24), right - left);
        _input.SetBounds(left, (Height - _input.Height) / 2, inputWidth, _input.Height);
        _placeholder.SetBounds(left, (Height - UiTheme.Scale(this, 20)) / 2,
            inputWidth, UiTheme.Scale(this, 20));
        _placeholder.BringToFront();
    }

    private void UpdatePlaceholder()
    {
        _placeholder.Visible = _input.Text.Length == 0 && !_input.Focused;
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
        _hoverAction = -1;
        Cursor = Cursors.IBeam;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var hover = HitAction(e.Location);
        if (hover != _hoverAction)
        {
            _hoverAction = hover;
            Cursor = hover >= 0 ? Cursors.Hand : Cursors.IBeam;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;

        var index = HitAction(e.Location);
        if (index >= 0)
        {
            _pressedAction = index;
            _actions[index].Pressed = true;
            Capture = true;
            Invalidate();
            return;
        }

        _input.Focus();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_pressedAction >= 0)
        {
            var index = _pressedAction;
            var action = _actions[index];
            action.Pressed = false;
            _pressedAction = -1;
            Capture = false;
            Invalidate();

            if (action.Bounds.Contains(e.Location))
                action.Invoke();
        }
    }

    private int HitAction(Point point)
    {
        for (var i = 0; i < _actions.Count; i++)
        {
            if (_actions[i].Bounds.Contains(point))
                return i;
        }
        return -1;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new SolidBrush(UiTheme.InputBg);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var focused = _input.Focused;
        var border = focused
            ? UiTheme.Accent
            : _hovered || _hoverAction >= 0
                ? UiTheme.InputHoverBorder
                : UiTheme.BorderSubtle;

        var rect = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        using (var pen = new Pen(border, 1f))
        using (var path = UiTheme.RoundedRect(rect, UiTheme.Scale(this, 8)))
        {
            g.DrawPath(pen, path);
        }

        if (focused)
        {
            var glowRect = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
            using var glow = new Pen(Color.FromArgb(70, UiTheme.Accent), 1f);
            using var glowPath = UiTheme.RoundedRect(glowRect, UiTheme.Scale(this, 7));
            g.DrawPath(glow, glowPath);
        }

        for (var i = 0; i < _actions.Count; i++)
            PaintAction(g, _actions[i], i == _hoverAction);
    }

    private void PaintAction(Graphics g, FieldAction action, bool hovered)
    {
        var radius = UiTheme.Scale(this, 6);
        var bounds = action.Bounds;

        Color fill;
        Color border;
        Color text;

        if (action.Accent)
        {
            fill = action.Pressed ? UiTheme.AccentSoft : hovered ? Color.FromArgb(38, 58, 96) : UiTheme.AccentSoft;
            border = hovered ? Color.FromArgb(76, 108, 168) : Color.FromArgb(48, 74, 122);
            text = UiTheme.AccentHover;
        }
        else
        {
            fill = action.Pressed ? UiTheme.RowSelectedBg : hovered ? UiTheme.RowHoverBg : UiTheme.ElevatedBg;
            border = hovered ? UiTheme.BorderStrong : UiTheme.BorderSubtle;
            text = hovered ? UiTheme.TextPrimary : UiTheme.TextSecondary;
        }

        UiTheme.FillRounded(g, bounds, radius, fill);
        UiTheme.StrokeRounded(g, bounds, radius, border, 1f);

        UiTheme.DrawText(g, action.Text, UiTheme.SmallBoldFont,
            new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

/// <summary>设置页状态徽章：自动 / 自定义 / 无效。</summary>
[DesignerCategory("Code")]
internal sealed class SettingsBadge : Control
{
    private string _text = string.Empty;
    private Color _accent = UiTheme.TextSecondary;
    private Color _fill = UiTheme.ElevatedBg;

    public SettingsBadge()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        Height = 22;
        Width = 60;
        BackColor = UiTheme.CardBg;
    }

    public void SetState(string text, Color accent, Color fill)
    {
        _text = text;
        _accent = accent;
        _fill = fill;
        Width = UiTheme.MeasureWidth(text, UiTheme.SmallBoldFont) + UiTheme.Scale(this, 26);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        UiTheme.FillRounded(g, rect, rect.Height / 2, _fill);
        UiTheme.StrokeRounded(g, rect, rect.Height / 2, Color.FromArgb(60, _accent), 1f);

        var dot = UiTheme.Scale(this, 6);
        var dotX = UiTheme.Scale(this, 9);
        using (var brush = new SolidBrush(_accent))
            g.FillEllipse(brush, dotX, (Height - dot) / 2, dot, dot);

        UiTheme.DrawText(g, _text, UiTheme.SmallBoldFont,
            new Rectangle(dotX + dot + UiTheme.Scale(this, 5), 0,
                Math.Max(0, Width - dotX - dot - UiTheme.Scale(this, 12)), Height),
            _accent,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

/// <summary>分段选择器：用于 Agent 启用方式等少量互斥选项。</summary>
[DesignerCategory("Code")]
internal sealed class SegmentedControl : Control
{
    private readonly string[] _items;
    private int _selectedIndex;
    private int _hoverIndex = -1;

    public SegmentedControl(params string[] items)
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        _items = items.Length > 0 ? items : new[] { "默认" };
        Height = 36;
        BackColor = UiTheme.CardBg;
        TabStop = true;
        Cursor = Cursors.Hand;
    }

    public event EventHandler? SelectionChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var clamped = Math.Clamp(value, 0, _items.Length - 1);
            if (_selectedIndex == clamped)
                return;

            _selectedIndex = clamped;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    private (int X, int Width) SegmentBounds(int index)
    {
        var inset = UiTheme.Scale(this, 3);
        var usable = Width - inset * 2;
        var baseWidth = usable / _items.Length;
        var x = inset + index * baseWidth;
        var width = index == _items.Length - 1 ? inset + usable - x : baseWidth;
        return (x, width);
    }

    private int HitSegment(Point point)
    {
        if (point.Y < 0 || point.Y > Height)
            return -1;

        for (var i = 0; i < _items.Length; i++)
        {
            var (x, width) = SegmentBounds(i);
            if (point.X >= x && point.X < x + width)
                return i;
        }
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hover = HitSegment(e.Location);
        if (hover != _hoverIndex)
        {
            _hoverIndex = hover;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverIndex = -1;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        Focus();
        var index = HitSegment(e.Location);
        if (index >= 0)
            SelectedIndex = index;
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Left)
        {
            SelectedIndex = Math.Max(0, _selectedIndex - 1);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Right)
        {
            SelectedIndex = Math.Min(_items.Length - 1, _selectedIndex + 1);
            e.Handled = true;
        }
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

        var rect = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        var radius = UiTheme.Scale(this, 9);
        UiTheme.FillRounded(g, rect, radius, UiTheme.InputBg);
        UiTheme.StrokeRounded(g, rect, radius, Focused ? UiTheme.Accent : UiTheme.BorderSubtle, 1f);

        for (var i = 0; i < _items.Length; i++)
        {
            var (x, width) = SegmentBounds(i);
            var bounds = new Rectangle(x, UiTheme.Scale(this, 3), width, Height - UiTheme.Scale(this, 6));
            var selected = i == _selectedIndex;

            if (selected)
            {
                UiTheme.FillRounded(g, bounds, UiTheme.Scale(this, 7), UiTheme.Accent);
            }
            else if (i == _hoverIndex)
            {
                UiTheme.FillRounded(g, bounds, UiTheme.Scale(this, 7), UiTheme.RowHoverBg);
            }

            UiTheme.DrawText(g, _items[i], UiTheme.BodyFont,
                new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height),
                selected ? Color.White : i == _hoverIndex ? UiTheme.TextPrimary : UiTheme.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}

/// <summary>开关控件：替代 WinForms 原生 CheckBox，选中态与深色主题一致。</summary>
[DesignerCategory("Code")]
internal sealed class ToggleSwitch : Control
{
    private bool _checked;
    private bool _hovered;

    public ToggleSwitch()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        Size = new Size(48, 26);
        BackColor = UiTheme.CardBg;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public event EventHandler? CheckedChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
                return;

            _checked = value;
            CheckedChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
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

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !ClientRectangle.Contains(e.Location))
            return;

        Focus();
        Checked = !Checked;
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            Checked = !Checked;
            e.Handled = true;
        }
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

        var trackHeight = Height - UiTheme.Scale(this, 4);
        var track = new Rectangle(0, UiTheme.Scale(this, 2), Width - 1, trackHeight);
        var radius = track.Height / 2;

        var trackColor = _checked
            ? _hovered ? UiTheme.AccentHover : UiTheme.Accent
            : _hovered ? Color.FromArgb(64, 75, 95) : Color.FromArgb(48, 56, 70);
        UiTheme.FillRounded(g, track, radius, trackColor);

        if (Focused)
            UiTheme.StrokeRounded(g, track, radius, Color.FromArgb(170, UiTheme.Accent), 1f);

        var knobSize = trackHeight - UiTheme.Scale(this, 6);
        var knobX = _checked
            ? track.Right - knobSize - UiTheme.Scale(this, 3)
            : track.X + UiTheme.Scale(this, 3);
        var knob = new Rectangle(knobX, track.Y + (track.Height - knobSize) / 2, knobSize, knobSize);
        using (var shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
            g.FillEllipse(shadow, knob.X, knob.Y + 1, knob.Width, knob.Height);
        using (var brush = new SolidBrush(_checked ? Color.White : Color.FromArgb(214, 222, 234)))
            g.FillEllipse(brush, knob);
    }
}

internal enum SettingsNavIcon
{
    Paths,
    Agent,
    Options
}

/// <summary>设置窗口左侧导航项：图标 + 标题 + 说明，选中态带主色竖条。</summary>
[DesignerCategory("Code")]
internal sealed class SettingsNavItem : Control
{
    private bool _selected;
    private bool _hovered;

    public SettingsNavItem(SettingsNavIcon icon, string title, string subtitle)
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        Icon = icon;
        Title = title;
        Subtitle = subtitle;
        Height = 58;
        BackColor = UiTheme.NavBg;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public SettingsNavIcon Icon { get; }

    public string Title { get; }

    public string Subtitle { get; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
                return;

            _selected = value;
            Invalidate();
        }
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

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !ClientRectangle.Contains(e.Location))
            return;

        Focus();
        OnClick(EventArgs.Empty);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }
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

        var bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        var radius = UiTheme.Scale(this, 8);

        if (_selected)
        {
            UiTheme.FillRounded(g, bounds, radius, UiTheme.NavActiveBg);
            using var bar = new SolidBrush(UiTheme.Accent);
            g.FillRectangle(bar, bounds.X + 1, bounds.Y + UiTheme.Scale(this, 14),
                UiTheme.Scale(this, 3), Height - UiTheme.Scale(this, 28));
        }
        else if (_hovered)
        {
            UiTheme.FillRounded(g, bounds, radius, UiTheme.NavHoverBg);
        }

        if (Focused)
            UiTheme.StrokeRounded(g, bounds, radius, Color.FromArgb(120, UiTheme.Accent), 1f);

        var iconColor = _selected ? UiTheme.AccentHover : UiTheme.TextMuted;
        var iconBack = _selected ? UiTheme.NavActiveBg : UiTheme.NavBg;
        PaintIcon(g, new Rectangle(UiTheme.Scale(this, 16), (Height - UiTheme.Scale(this, 22)) / 2,
            UiTheme.Scale(this, 22), UiTheme.Scale(this, 22)), iconColor, iconBack);

        var textX = UiTheme.Scale(this, 50);
        UiTheme.DrawText(g, Title, UiTheme.NavTitleFont,
            new Rectangle(textX, UiTheme.Scale(this, 10), Width - textX - UiTheme.Scale(this, 8), UiTheme.Scale(this, 20)),
            _selected ? UiTheme.TextPrimary : UiTheme.TextSecondary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        UiTheme.DrawText(g, Subtitle, UiTheme.NavSubFont,
            new Rectangle(textX, UiTheme.Scale(this, 30), Width - textX - UiTheme.Scale(this, 8), UiTheme.Scale(this, 18)),
            _selected ? UiTheme.TextSecondary : UiTheme.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void PaintIcon(Graphics g, Rectangle rect, Color color, Color backColor)
    {
        using var pen = new Pen(color, 1.4f);
        var cx = rect.X + rect.Width / 2f;
        var cy = rect.Y + rect.Height / 2f;
        var s = UiTheme.Scale(this, 1);

        switch (Icon)
        {
            case SettingsNavIcon.Paths:
            {
                var w = UiTheme.Scale(this, 16);
                var h = UiTheme.Scale(this, 12);
                var left = cx - w / 2f;
                var top = cy - h / 2f + s;
                using var path = new GraphicsPath();
                path.AddLine(left, top + h, left, top + s * 2);
                path.AddLine(left, top + s * 2, left + s * 5, top + s * 2);
                path.AddLine(left + s * 5, top + s * 2, left + s * 7, top + s * 4);
                path.AddLine(left + s * 7, top + s * 4, left + w, top + s * 4);
                path.AddLine(left + w, top + s * 4, left + w, top + h);
                path.CloseFigure();
                g.DrawPath(pen, path);
                break;
            }

            case SettingsNavIcon.Agent:
            {
                var box = new Rectangle((int)(cx - UiTheme.Scale(this, 6)), (int)(cy - UiTheme.Scale(this, 6)),
                    UiTheme.Scale(this, 12), UiTheme.Scale(this, 12));
                g.DrawRectangle(pen, box);
                for (var i = -1; i <= 1; i++)
                {
                    var offset = i * UiTheme.Scale(this, 4);
                    g.DrawLine(pen, cx + offset, box.Top - UiTheme.Scale(this, 3), cx + offset, box.Top);
                    g.DrawLine(pen, cx + offset, box.Bottom, cx + offset, box.Bottom + UiTheme.Scale(this, 3));
                    g.DrawLine(pen, box.Left - UiTheme.Scale(this, 3), cy + offset, box.Left, cy + offset);
                    g.DrawLine(pen, box.Right, cy + offset, box.Right + UiTheme.Scale(this, 3), cy + offset);
                }
                using var dot = new SolidBrush(color);
                g.FillEllipse(dot, cx - UiTheme.Scale(this, 2), cy - UiTheme.Scale(this, 2),
                    UiTheme.Scale(this, 4), UiTheme.Scale(this, 4));
                break;
            }

            case SettingsNavIcon.Options:
            {
                var left = cx - UiTheme.Scale(this, 8);
                var right = cx + UiTheme.Scale(this, 8);
                var knob = UiTheme.Scale(this, 3);
                for (var i = 0; i < 3; i++)
                {
                    var y = cy + (i - 1) * UiTheme.Scale(this, 5);
                    g.DrawLine(pen, left, y, right, y);
                    var knobX = left + (i == 0 ? UiTheme.Scale(this, 4) : i == 1 ? UiTheme.Scale(this, 11) : UiTheme.Scale(this, 7));
                    using var brush = new SolidBrush(backColor);
                    g.FillEllipse(brush, knobX - knob, y - knob, knob * 2, knob * 2);
                    g.DrawEllipse(pen, knobX - knob, y - knob, knob * 2, knob * 2);
                }
                break;
            }
        }
    }
}

using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ModSorterAgent.UI.Controls;

internal enum ButtonVariant
{
    Primary,
    Secondary,
    Ghost,
    Danger
}

/// <summary>
/// 统一风格按钮：支持主/次/幽灵/危险四种语义，含悬停、按下、焦点与禁用态。
/// 键盘可用（Space / Enter 触发），便于全键盘操作。
/// </summary>
[DesignerCategory("Code")]
internal sealed class ModernButton : Control
{
    private bool _hovered;
    private bool _pressed;

    public ModernButton(string text, ButtonVariant variant = ButtonVariant.Secondary)
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.SupportsTransparentBackColor,
            true);

        Text = text;
        Variant = variant;
        Height = 32;
        Cursor = Cursors.Hand;
        TabStop = true;
        Font = UiTheme.BodyFont;
        BackColor = Color.Transparent;
        AutoFitWidth();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

    public ButtonVariant Variant { get; set; }

    private string? _leadingGlyph;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

    public string? LeadingGlyph
    {
        get => _leadingGlyph;
        set
        {
            _leadingGlyph = value;
            AutoFitWidth();
            Invalidate();
        }
    }

    private void AutoFitWidth()
    {
        var textWidth = UiTheme.MeasureWidth(Text, Font);
        var glyphWidth = string.IsNullOrEmpty(_leadingGlyph) ? 0 : UiTheme.Scale(this, 20);
        Width = Math.Max(UiTheme.Scale(this, 56), textWidth + glyphWidth + UiTheme.Scale(this, 26));
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
        _pressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Focus();
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left && _pressed)
        {
            _pressed = false;
            Invalidate();
            if (ClientRectangle.Contains(e.Location))
                OnClick(EventArgs.Empty);
        }
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

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        AutoFitWidth();
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        AutoFitWidth();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var enabled = Enabled;
        var (fill, border, textColor) = ResolveColors(enabled);
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        var radius = UiTheme.Scale(this, 6);

        using (var path = UiTheme.RoundedRect(rect, radius))
        using (var brush = new SolidBrush(fill))
        using (var pen = new Pen(border, 1f))
        {
            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }

        if (Focused && enabled)
            UiTheme.StrokeRounded(g, rect, radius, Color.FromArgb(150, UiTheme.Accent), 1f);

        var content = Text;
        var contentWidth = UiTheme.MeasureWidth(content, Font);
        var totalWidth = contentWidth + (string.IsNullOrEmpty(LeadingGlyph) ? 0 : UiTheme.Scale(this, 18));
        var startX = rect.X + Math.Max(8, (rect.Width - totalWidth) / 2);
        var textRect = new Rectangle(startX, rect.Y, Math.Max(0, rect.Right - startX - 8), rect.Height);

        if (!string.IsNullOrEmpty(LeadingGlyph))
        {
            var glyphRect = new Rectangle(startX, rect.Y, UiTheme.Scale(this, 16), rect.Height);
            UiTheme.DrawText(g, LeadingGlyph, UiTheme.SmallBoldFont, glyphRect, textColor);
            textRect = new Rectangle(
                startX + UiTheme.Scale(this, 18),
                rect.Y,
                Math.Max(0, rect.Right - startX - UiTheme.Scale(this, 18) - 8),
                rect.Height);
        }

        UiTheme.DrawText(g, content, Font, textRect, textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private (Color Fill, Color Border, Color Text) ResolveColors(bool enabled)
    {
        if (!enabled)
        {
            return Variant switch
            {
                ButtonVariant.Primary => (Color.FromArgb(40, 52, 72), UiTheme.BorderSubtle, UiTheme.TextMuted),
                ButtonVariant.Danger => (Color.FromArgb(52, 34, 38), UiTheme.BorderSubtle, UiTheme.TextMuted),
                _ => (Color.FromArgb(26, 30, 39), UiTheme.BorderSubtle, UiTheme.TextMuted)
            };
        }

        return Variant switch
        {
            ButtonVariant.Primary => (
                _pressed ? UiTheme.AccentPressed : _hovered ? UiTheme.AccentHover : UiTheme.Accent,
                UiTheme.Accent,
                UiTheme.TextOnAccent),
            ButtonVariant.Danger => (
                _pressed ? Color.FromArgb(190, 54, 54) : _hovered ? Color.FromArgb(226, 92, 92) : Color.FromArgb(196, 62, 62),
                Color.FromArgb(214, 78, 78),
                Color.White),
            ButtonVariant.Ghost => (
                _pressed ? Color.FromArgb(38, 44, 57) : _hovered ? Color.FromArgb(30, 35, 45) : Color.Transparent,
                Color.Transparent,
                _hovered ? UiTheme.TextPrimary : UiTheme.TextSecondary),
            _ => (
                _pressed ? UiTheme.ElevatedBg : _hovered ? Color.FromArgb(33, 39, 50) : UiTheme.ElevatedBg,
                _hovered ? UiTheme.BorderStrong : UiTheme.BorderSubtle,
                UiTheme.TextPrimary)
        };
    }
}

/// <summary>
/// 无边框窗口的最小化/最大化/还原/关闭按钮。用线条绘制，避免"红黄绿圆点"被误读为状态灯。
/// </summary>
[DesignerCategory("Code")]
internal sealed class WindowChromeButton : Control
{
    private bool _hovered;

    public WindowChromeButton(WindowControlKind kind)
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        Kind = kind;
        Width = 40;
        Height = 30;
        Cursor = Cursors.Hand;
        BackColor = Color.FromArgb(15, 18, 24);
    }

    public enum WindowControlKind
    {
        Minimize,
        Maximize,
        Close,
        Settings
    }

    public WindowControlKind Kind { get; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

    public bool IsMaximized { get; set; }

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

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (_hovered)
        {
            var bg = Kind == WindowControlKind.Close
                ? Color.FromArgb(196, 62, 62)
                : Color.FromArgb(38, 44, 57);
            using var brush = new SolidBrush(bg);
            g.FillRectangle(brush, ClientRectangle);
        }

        var color = _hovered && Kind == WindowControlKind.Close ? Color.White : UiTheme.TextSecondary;
        using var pen = new Pen(color, 1.2f);

        var cx = Width / 2f;
        var cy = Height / 2f;
        var half = UiTheme.Scale(this, 5);

        switch (Kind)
        {
            case WindowControlKind.Minimize:
                g.DrawLine(pen, cx - half, cy + UiTheme.Scale(this, 2), cx + half, cy + UiTheme.Scale(this, 2));
                break;

            case WindowControlKind.Maximize when !IsMaximized:
                g.DrawRectangle(pen, cx - half, cy - half, half * 2, half * 2);
                break;

            case WindowControlKind.Maximize:
                g.DrawRectangle(pen, cx - half, cy - half, half * 2, half * 2);
                using (var inner = new Pen(color, 1f))
                    g.DrawLine(inner, cx - half + 2, cy - half + 2, cx + half, cy - half + 2);
                break;

            case WindowControlKind.Close:
                g.DrawLine(pen, cx - half, cy - half, cx + half, cy + half);
                g.DrawLine(pen, cx + half, cy - half, cx - half, cy + half);
                break;

            case WindowControlKind.Settings:
                // 齿轮：中心圆 + 8 齿辐条 + 内孔
                g.DrawEllipse(pen, cx - half, cy - half, half * 2, half * 2);
                for (var i = 0; i < 8; i++)
                {
                    var angle = i * Math.PI / 4;
                    var inner = half + UiTheme.Scale(this, 1);
                    var outer = half + UiTheme.Scale(this, 4);
                    g.DrawLine(pen,
                        cx + (float)Math.Cos(angle) * inner,
                        cy + (float)Math.Sin(angle) * inner,
                        cx + (float)Math.Cos(angle) * outer,
                        cy + (float)Math.Sin(angle) * outer);
                }
                using (var holePen = new Pen(color, 1f))
                {
                    var holeR = UiTheme.Scale(this, 2);
                    g.DrawEllipse(holePen, cx - holeR, cy - holeR, holeR * 2, holeR * 2);
                }
                break;
        }
    }
}

/// <summary>
/// 带前置放大镜图标与占位提示的搜索框。用于模组列表与冲突列表的即时过滤。
/// </summary>
[DesignerCategory("Code")]
internal sealed class SearchBox : Panel
{
    private readonly TextBox _input;
    private readonly string _placeholder;

    public SearchBox(string placeholder, int width = 240)
    {
        _placeholder = placeholder;
        Width = width;
        Height = 30;
        BackColor = UiTheme.ElevatedBg;
        Padding = new Padding(UiTheme.Scale(this, 28), UiTheme.Scale(this, 5), UiTheme.Scale(this, 8), 0);

        _input = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = UiTheme.ElevatedBg,
            ForeColor = UiTheme.TextPrimary,
            Font = UiTheme.BodyFont,
            Dock = DockStyle.Fill,
            PlaceholderText = placeholder
        };

        _input.TextChanged += (_, _) => SearchTextChanged?.Invoke(this, EventArgs.Empty);
        _input.GotFocus += (_, _) => Invalidate();
        _input.LostFocus += (_, _) => Invalidate();

        Controls.Add(_input);
    }

    public event EventHandler? SearchTextChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

    public string SearchText
    {
        get => _input.Text;
        set => _input.Text = value;
    }

    public void Clear()
    {
        if (_input.Text.Length > 0)
            _input.Text = string.Empty;
    }

    public void FocusInput()
    {
        _input.Focus();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        UiTheme.FillRounded(g, rect, UiTheme.Scale(this, 6), UiTheme.ElevatedBg);
        UiTheme.StrokeRounded(g, rect, UiTheme.Scale(this, 6),
            _input.Focused ? UiTheme.Accent : UiTheme.BorderSubtle, 1f);

        // 放大镜：圆 + 手柄
        var iconColor = _input.Focused ? UiTheme.Accent : UiTheme.TextMuted;
        using var pen = new Pen(iconColor, 1.3f);
        var diameter = UiTheme.Scale(this, 9);
        var originX = UiTheme.Scale(this, 10);
        var originY = (Height - diameter) / 2 - 1;
        g.DrawEllipse(pen, originX, originY, diameter, diameter);
        g.DrawLine(pen, originX + diameter - 1, originY + diameter - 1,
            originX + diameter + 2, originY + diameter + 2);
    }
}

/// <summary>
/// 过滤胶囊。用于"全部/红/黄/绿/仅看顺位变动"等多维筛选，选中态用实心主色。
/// </summary>
[DesignerCategory("Code")]
internal sealed class FilterChip : Control
{
    private bool _checked;
    private bool _hovered;

    public FilterChip(string text, Color? accent = null)
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.SupportsTransparentBackColor,
            true);

        Text = text;
        Accent = accent ?? UiTheme.Accent;
        BackColor = Color.Transparent;
        Height = 26;
        Width = UiTheme.MeasureWidth(text, UiTheme.SmallFont) + UiTheme.Scale(this, 26);
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public Color Accent { get; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
                return;
            _checked = value;
            Invalidate();
        }
    }

    public event EventHandler? CheckedChanged;

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Width = UiTheme.MeasureWidth(Text, UiTheme.SmallFont) + UiTheme.Scale(this, 34);
        Invalidate();
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

        Checked = !Checked;
        CheckedChanged?.Invoke(this, EventArgs.Empty);
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
            CheckedChanged?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        var radius = Height / 2;
        var fill = !Enabled
            ? Color.FromArgb(24, 28, 36)
            : _checked ? Accent
            : _hovered ? Color.FromArgb(34, 40, 52) : UiTheme.ElevatedBg;
        var border = !Enabled ? UiTheme.BorderSubtle : _checked ? Accent : UiTheme.BorderSubtle;
        var textColor = !Enabled ? UiTheme.TextMuted : _checked ? Color.White : UiTheme.TextSecondary;

        UiTheme.FillRounded(g, rect, radius, fill);
        UiTheme.StrokeRounded(g, rect, radius, border, 1f);

        var dotSize = UiTheme.Scale(this, 6);
        var dotX = UiTheme.Scale(this, 10);
        using (var dot = new SolidBrush(!Enabled ? UiTheme.TextMuted : _checked ? Color.White : Accent))
            g.FillEllipse(dot, dotX, (Height - dotSize) / 2, dotSize, dotSize);

        UiTheme.DrawText(g, Text, UiTheme.SmallFont,
            new Rectangle(dotX + dotSize + UiTheme.Scale(this, 6), 0,
                Width - dotX - dotSize - UiTheme.Scale(this, 14), Height),
            textColor);
    }
}

/// <summary>
/// 分区卡片：标题栏 + 内容区。标题栏左侧有主色竖条，右侧可显示计数或辅助说明。
/// </summary>
[DesignerCategory("Code")]
internal sealed class SectionCard : Panel
{
    private readonly Label _titleLabel;
    private readonly Label _metaLabel;

    public SectionCard(string title, string meta = "")
    {
        BackColor = UiTheme.SurfaceBg;
        Margin = new Padding(0);
        Padding = new Padding(1);

        Header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = UiTheme.ElevatedBg
        };

        Header.Paint += (_, e) =>
        {
            using var bar = new SolidBrush(UiTheme.Accent);
            e.Graphics.FillRectangle(bar, UiTheme.Scale(Header, 10), UiTheme.Scale(Header, 11),
                UiTheme.Scale(Header, 3), UiTheme.Scale(Header, 14));

            using var line = new Pen(UiTheme.BorderSubtle, 1f);
            e.Graphics.DrawLine(line, 0, Header.Height - 1, Header.Width, Header.Height - 1);
        };

        _titleLabel = new Label
        {
            Text = title,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = UiTheme.TextPrimary,
            Font = UiTheme.SectionFont,
            BackColor = Color.Transparent,
            AutoEllipsis = true
        };

        _metaLabel = new Label
        {
            Text = meta,
            AutoSize = false,
            Dock = DockStyle.Right,
            Width = 190,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 12, 0),
            ForeColor = UiTheme.TextMuted,
            Font = UiTheme.SmallFont,
            BackColor = Color.Transparent
        };

        Header.Controls.Add(_titleLabel);
        Header.Controls.Add(_metaLabel);

        Body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.SurfaceBg
        };

        Controls.Add(Body);
        Controls.Add(Header);

        Paint += (_, e) =>
        {
            using var pen = new Pen(UiTheme.BorderSubtle, 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };
    }

    public Panel Header { get; }

    public Panel Body { get; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

    public string HeaderTitle
    {
        get => _titleLabel.Text;
        set => _titleLabel.Text = value;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

    public string HeaderMeta
    {
        get => _metaLabel.Text;
        set => _metaLabel.Text = value;
    }
}


/// <summary>
/// 深色下拉菜单渲染器，与工作台整体视觉设计完全统一。
/// </summary>
internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkMenuColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Enabled)
            return;

        var g = e.Graphics;
        var rect = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);

        if (e.Item.Selected || e.Item.Pressed)
        {
            using var brush = new SolidBrush(UiTheme.RowHoverBg);
            g.FillRectangle(brush, rect);
            using var borderPen = new Pen(UiTheme.BorderStrong, 1f);
            g.DrawRectangle(borderPen, rect);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? UiTheme.TextPrimary : UiTheme.TextMuted;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(UiTheme.BorderStrong, 1f);
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(UiTheme.CardBg);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var g = e.Graphics;
        var y = e.Item.Height / 2;
        using var pen = new Pen(UiTheme.BorderSubtle, 1f);
        g.DrawLine(pen, 6, y, e.Item.Width - 6, y);
    }
}

internal sealed class DarkMenuColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => UiTheme.CardBg;
    public override Color ImageMarginGradientBegin => UiTheme.CardBg;
    public override Color ImageMarginGradientMiddle => UiTheme.CardBg;
    public override Color ImageMarginGradientEnd => UiTheme.CardBg;
    public override Color MenuBorder => UiTheme.BorderStrong;
    public override Color MenuItemBorder => Color.Transparent;
    public override Color MenuItemSelected => UiTheme.RowHoverBg;
    public override Color MenuItemSelectedGradientBegin => UiTheme.RowHoverBg;
    public override Color MenuItemSelectedGradientEnd => UiTheme.RowHoverBg;
    public override Color MenuItemPressedGradientBegin => UiTheme.RowSelectedBg;
    public override Color MenuItemPressedGradientMiddle => UiTheme.RowSelectedBg;
    public override Color MenuItemPressedGradientEnd => UiTheme.RowSelectedBg;
    public override Color SeparatorDark => UiTheme.BorderSubtle;
    public override Color SeparatorLight => Color.Transparent;
    public override Color CheckBackground => UiTheme.RowSelectedBg;
    public override Color CheckSelectedBackground => UiTheme.RowSelectedBg;
}

/// <summary>
/// 深色右键/下拉菜单快速构造器。
/// </summary>
internal static class DarkContextMenu
{
    public static ContextMenuStrip Create()
    {
        return new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            BackColor = UiTheme.CardBg,
            ForeColor = UiTheme.TextPrimary,
            Font = UiTheme.BodyFont,
            ShowImageMargin = false,
            ShowCheckMargin = false,
            DropShadowEnabled = true
        };
    }

    public static ToolStripMenuItem CreateItem(string text, EventHandler onClick, string? shortcut = null)
    {
        var item = new ToolStripMenuItem(text, null, onClick)
        {
            ForeColor = UiTheme.TextPrimary,
            BackColor = Color.Transparent,
            Font = UiTheme.BodyFont,
            Padding = new Padding(12, 6, 16, 6)
        };
        if (!string.IsNullOrEmpty(shortcut))
        {
            item.ShortcutKeyDisplayString = shortcut;
        }
        return item;
    }
}

using System.ComponentModel;
using System.Drawing.Drawing2D;
using ModSorterAgent.Models;

namespace ModSorterAgent.UI.Controls;

internal enum PurpleChoice
{
    A,
    B,
    Both
}

internal sealed record PurpleChoiceRow(
    string ConflictKey,
    string ModAName,
    string ModBName,
    PurpleChoice Choice);

internal sealed record PurpleChoiceChangedEventArgs(string ConflictKey, PurpleChoice Choice);

/// <summary>
/// 作者声明不兼容的双向选择条：只选一边 = 正常排序保留该边；
/// 两边都选 = 保留双方并纳入兼容生成。
/// </summary>
[DesignerCategory("Code")]
internal sealed class PurpleChoicePanel : Panel
{
    private readonly Label _title;
    private readonly Label _hint;
    private readonly FlowLayoutPanel _rows;
    private readonly ToolTip _tips = new() { InitialDelay = 350, AutoPopDelay = 8000 };
    private bool _updating;

    public PurpleChoicePanel()
    {
        BackColor = UiTheme.SurfaceBg;
        Padding = new Padding(12, 6, 12, 8);

        _title = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = UiTheme.Scale(this, 20),
            Text = "作者声明不兼容 · 保留哪一方",
            ForeColor = UiTheme.PurpleText,
            Font = UiTheme.SmallBoldFont,
            BackColor = Color.Transparent
        };

        _hint = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = UiTheme.Scale(this, 18),
            Text = "只选一边：正常排序并排除另一边　·　两边都选：保留双方并纳入兼容生成",
            ForeColor = UiTheme.TextMuted,
            Font = UiTheme.SmallFont,
            BackColor = Color.Transparent
        };

        _rows = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        _rows.Resize += (_, _) =>
        {
            foreach (Control row in _rows.Controls)
                row.Width = Math.Max(220, _rows.ClientSize.Width - 4);
        };

        Controls.Add(_rows);
        Controls.Add(_hint);
        Controls.Add(_title);

        Visible = false;
    }

    public event EventHandler<PurpleChoiceChangedEventArgs>? ChoiceChanged;

    private int RowHeight => UiTheme.Scale(this, 52);

    public int PreferredHeight => _rows.Controls.Count == 0
        ? 0
        : UiTheme.Scale(this, 52) + _rows.Controls.Count * UiTheme.Scale(this, 58) + UiTheme.Scale(this, 16);

    public void SetChoices(IReadOnlyList<PurpleChoiceRow> choices)
    {
        _updating = true;
        SuspendLayout();
        try
        {
            foreach (var control in _rows.Controls.Cast<Control>().ToArray())
                control.Dispose();
            _rows.Controls.Clear();

            foreach (var choice in choices)
                _rows.Controls.Add(BuildRow(choice));

            Visible = choices.Count > 0;
            Height = choices.Count > 0 ? PreferredHeight : 0;
        }
        finally
        {
            _updating = false;
            ResumeLayout(true);
            PerformLayout();
            Invalidate();
        }
    }

    private Control BuildRow(PurpleChoiceRow row)
    {
        var panel = new Panel
        {
            Height = RowHeight,
            Width = Math.Max(220, _rows.ClientSize.Width - 4),
            BackColor = UiTheme.CardBg,
            Margin = new Padding(0, 0, 0, 6)
        };

        panel.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var border = new Pen(UiTheme.BorderSubtle, 1f);
            g.DrawRectangle(border, 0, 0, panel.Width - 1, panel.Height - 1);
            using var bar = new SolidBrush(UiTheme.Purple);
            g.FillRectangle(bar, 1, UiTheme.Scale(panel, 7), UiTheme.Scale(panel, 3), panel.Height - UiTheme.Scale(panel, 14));
        };

        var chipA = CreateChip(row.ModAName, row.Choice is PurpleChoice.A or PurpleChoice.Both);
        var chipB = CreateChip(row.ModBName, row.Choice is PurpleChoice.B or PurpleChoice.Both);
        _tips.SetToolTip(chipA, "保留「" + row.ModAName + "」");
        _tips.SetToolTip(chipB, "保留「" + row.ModBName + "」");

        var connector = new Label
        {
            AutoSize = false,
            Width = UiTheme.Scale(this, 24),
            Height = UiTheme.Scale(this, 26),
            Text = "或",
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = UiTheme.TextMuted,
            Font = UiTheme.SmallFont,
            BackColor = Color.Transparent
        };

        var status = new Label
        {
            AutoSize = false,
            Height = UiTheme.Scale(this, 16),
            ForeColor = StatusColor(row.Choice),
            Font = UiTheme.SmallFont,
            BackColor = Color.Transparent,
            Text = StatusText(row)
        };

        panel.Controls.Add(chipA);
        panel.Controls.Add(connector);
        panel.Controls.Add(chipB);
        panel.Controls.Add(status);

        void LayoutRow()
        {
            var gap = UiTheme.Scale(this, 8);
            var pad = UiTheme.Scale(this, 12);
            var usable = Math.Max(180, panel.ClientSize.Width - pad * 2 - connector.Width - gap * 2);
            var chipWidth = Math.Max(UiTheme.Scale(this, 90), usable / 2);

            chipA.Width = chipWidth;
            chipB.Width = chipWidth;

            chipA.Location = new Point(pad, UiTheme.Scale(this, 5));
            connector.Location = new Point(
                panel.ClientSize.Width / 2 - connector.Width / 2,
                UiTheme.Scale(this, 5));
            chipB.Location = new Point(
                panel.ClientSize.Width - pad - chipWidth,
                UiTheme.Scale(this, 5));

            status.Location = new Point(UiTheme.Scale(this, 14), UiTheme.Scale(this, 33));
            status.Width = Math.Max(60, panel.ClientSize.Width - UiTheme.Scale(this, 24));
        }

        panel.Resize += (_, _) => LayoutRow();
        LayoutRow();

        chipA.CheckedChanged += (_, _) => OnChipToggled(row, chipA, chipA, chipB, status);
        chipB.CheckedChanged += (_, _) => OnChipToggled(row, chipB, chipA, chipB, status);

        return panel;
    }

    private FilterChip CreateChip(string name, bool isChecked)
    {
        var chip = new FilterChip(TrimName(name), UiTheme.Purple)
        {
            Checked = isChecked,
            Height = 26
        };
        return chip;
    }

    private void OnChipToggled(PurpleChoiceRow row, FilterChip toggled, FilterChip chipA, FilterChip chipB, Label status)
    {
        if (_updating)
            return;

        // 至少保留一侧：点击唯一勾选项时不进入“两边都不选”，只把这一项恢复
        if (!chipA.Checked && !chipB.Checked)
        {
            _updating = true;
            toggled.Checked = true;
            _updating = false;
            return;
        }

        var choice = chipA.Checked && chipB.Checked
            ? PurpleChoice.Both
            : chipA.Checked ? PurpleChoice.A : PurpleChoice.B;

        status.Text = StatusText(row.ModAName, row.ModBName, choice);
        status.ForeColor = StatusColor(choice);
        ChoiceChanged?.Invoke(this, new PurpleChoiceChangedEventArgs(row.ConflictKey, choice));
    }

    private static string StatusText(PurpleChoiceRow row) => StatusText(row.ModAName, row.ModBName, row.Choice);

    private static string StatusText(string a, string b, PurpleChoice choice) => choice switch
    {
        PurpleChoice.A => $"仅保留「{a}」 · 「{b}」将从写入与导出中排除",
        PurpleChoice.B => $"仅保留「{b}」 · 「{a}」将从写入与导出中排除",
        _ => "两边都保留 · 已纳入兼容生成范围"
    };

    private static Color StatusColor(PurpleChoice choice)
        => choice == PurpleChoice.Both ? UiTheme.PurpleText : UiTheme.AmberText;

    private static string TrimName(string name)
    {
        const int maxChars = 32;
        return name.Length <= maxChars ? name : name[..(maxChars - 1)] + "…";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(UiTheme.BorderSubtle, 1f);
        e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _tips.Dispose();
        base.Dispose(disposing);
    }
}

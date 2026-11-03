using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace ModSorterAgent.UI;

/// <summary>
/// 设计令牌（Design Tokens）：颜色、字体、间距、圆角统一收口在这里，
/// 避免颜色与字号散落在各个绘制代码里。
/// </summary>
internal static class UiTheme
{
    // 背景层级：App &lt; Surface &lt; Elevated &lt; Card
    public static readonly Color AppBg = Color.FromArgb(12, 14, 19);
    public static readonly Color SurfaceBg = Color.FromArgb(18, 21, 28);
    public static readonly Color ElevatedBg = Color.FromArgb(24, 28, 37);
    public static readonly Color CardBg = Color.FromArgb(22, 26, 34);
    public static readonly Color RowAltBg = Color.FromArgb(21, 25, 32);
    public static readonly Color RowHoverBg = Color.FromArgb(32, 38, 50);
    public static readonly Color RowSelectedBg = Color.FromArgb(31, 58, 94);

    public static readonly Color BorderSubtle = Color.FromArgb(38, 44, 57);
    public static readonly Color BorderStrong = Color.FromArgb(56, 65, 84);

    public static readonly Color TextPrimary = Color.FromArgb(238, 242, 248);
    public static readonly Color TextSecondary = Color.FromArgb(150, 163, 183);
    public static readonly Color TextMuted = Color.FromArgb(104, 116, 136);
    public static readonly Color TextOnAccent = Color.FromArgb(245, 249, 255);

    public static readonly Color Accent = Color.FromArgb(72, 132, 245);
    public static readonly Color AccentHover = Color.FromArgb(94, 152, 255);
    public static readonly Color AccentPressed = Color.FromArgb(56, 110, 214);
    public static readonly Color AccentSoft = Color.FromArgb(30, 48, 82);

    public static readonly Color Green = Color.FromArgb(45, 190, 130);
    public static readonly Color GreenBg = Color.FromArgb(17, 58, 45);
    public static readonly Color GreenText = Color.FromArgb(122, 226, 176);

    public static readonly Color Amber = Color.FromArgb(238, 166, 44);
    public static readonly Color AmberBg = Color.FromArgb(68, 49, 16);
    public static readonly Color AmberText = Color.FromArgb(246, 205, 118);

    public static readonly Color Rose = Color.FromArgb(232, 84, 84);
    public static readonly Color RoseBg = Color.FromArgb(70, 26, 30);
    public static readonly Color RoseText = Color.FromArgb(248, 163, 163);

    public static readonly Color Purple = Color.FromArgb(168, 112, 240);
    public static readonly Color PurpleBg = Color.FromArgb(45, 30, 68);
    public static readonly Color PurpleText = Color.FromArgb(210, 178, 255);

    public static readonly Color ScrollTrack = Color.FromArgb(24, 28, 36);
    public static readonly Color ScrollThumb = Color.FromArgb(62, 72, 90);
    public static readonly Color ScrollThumbHover = Color.FromArgb(86, 99, 122);

    // 按钮"暗"态：底片比标题栏更沉、文字与边框一起降对比，与"亮"态拉开亮度差
    public static readonly Color DisabledFill = Color.FromArgb(15, 18, 24);
    public static readonly Color DisabledBorder = Color.FromArgb(27, 32, 41);
    public static readonly Color DisabledText = Color.FromArgb(82, 92, 108);

    // 设置窗口专用层级：导航栏比正文更沉，输入框比卡片更亮
    public static readonly Color NavBg = Color.FromArgb(15, 18, 25);
    public static readonly Color NavHoverBg = Color.FromArgb(23, 28, 38);
    public static readonly Color NavActiveBg = Color.FromArgb(26, 36, 53);
    public static readonly Color InputBg = Color.FromArgb(20, 24, 32);
    public static readonly Color InputHoverBorder = Color.FromArgb(66, 77, 99);
    public static readonly Color TitleBarBg = Color.FromArgb(15, 18, 24);

    // 字体：全静态复用，避免在 OnPaint 里反复 new Font 造成 GDI 句柄泄漏
    public static readonly Font TitleFont = new("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
    public static readonly Font DialogTitleFont = new("Microsoft YaHei UI", 13.5f, FontStyle.Bold);
    public static readonly Font PageTitleFont = new("Microsoft YaHei UI", 14.5f, FontStyle.Bold);
    public static readonly Font NavTitleFont = new("Microsoft YaHei UI", 9.4f, FontStyle.Bold);
    public static readonly Font NavSubFont = new("Microsoft YaHei UI", 8.0f, FontStyle.Regular);
    public static readonly Font SubtitleFont = new("Microsoft YaHei UI", 8.6f, FontStyle.Regular);
    public static readonly Font SectionFont = new("Microsoft YaHei UI", 9.6f, FontStyle.Bold);
    public static readonly Font BodyFont = new("Microsoft YaHei UI", 9.0f, FontStyle.Regular);
    public static readonly Font BodyBoldFont = new("Microsoft YaHei UI", 9.0f, FontStyle.Bold);
    public static readonly Font SmallFont = new("Microsoft YaHei UI", 8.4f, FontStyle.Regular);
    public static readonly Font SmallBoldFont = new("Microsoft YaHei UI", 8.4f, FontStyle.Bold);
    public static readonly Font PillFont = new("Microsoft YaHei UI", 8.4f, FontStyle.Bold);
    public static readonly Font MetricLabelFont = new("Microsoft YaHei UI", 8.4f, FontStyle.Regular);
    public static readonly Font MetricValueFont = new("Microsoft YaHei UI", 15.0f, FontStyle.Bold);
    public static readonly Font MetricValueSmallFont = new("Microsoft YaHei UI", 11.0f, FontStyle.Bold);
    public static readonly Font MonoFont = new("Consolas", 8.8f, FontStyle.Regular);
    public static readonly Font MonoBoldFont = new("Consolas", 8.8f, FontStyle.Bold);
    public static readonly Font MonoSmallFont = new("Consolas", 8.2f, FontStyle.Regular);

    public static (Color Accent, Color PillBg, Color PillText, string ShortLabel) GradeStyle(Models.ConflictGrade grade)
    {
        return grade switch
        {
            Models.ConflictGrade.Green => (Green, GreenBg, GreenText, "1.绿色"),
            Models.ConflictGrade.Yellow => (Amber, AmberBg, AmberText, "2.黄色"),
            Models.ConflictGrade.Red => (Rose, RoseBg, RoseText, "3.红色"),
            Models.ConflictGrade.Purple => (Purple, PurpleBg, PurpleText, "4.紫色"),
            _ => (TextSecondary, ElevatedBg, TextPrimary, "未知")
        };
    }

    public static string CategoryLabel(string raw)
    {
        return Reporting.ConflictPlainText.CategoryLabel(raw);
    }

    public static string TierLabel(int tierRank)
    {
        return tierRank switch
        {
            0 => "核心底座",
            1 => "系统与诊断",
            2 => "前置框架",
            3 => "种族与阵营",
            4 => "玩法与扩展",
            5 => "渲染与动画",
            6 => "纯 XML 补丁",
            7 => "语言汉化包",
            _ => "其他"
        };
    }

    public static int Scale(Control control, int value)
    {
        return Math.Max(1, (int)Math.Round(value * control.DeviceDpi / 96.0));
    }

    public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return path;

        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 1)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics g, Rectangle bounds, int radius, Color color)
    {
        using var path = RoundedRect(bounds, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void StrokeRounded(Graphics g, Rectangle bounds, int radius, Color color, float width = 1f)
    {
        using var path = RoundedRect(bounds, radius);
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }

    public static void DrawCard(Graphics g, Rectangle bounds, Color fill, Color border, int radius = 8)
    {
        using var path = RoundedRect(bounds, radius);
        using var brush = new SolidBrush(fill);
        using var pen = new Pen(border, 1f);
        g.FillPath(brush, path);
        g.DrawPath(pen, path);
    }

    public static void DrawPill(
        Graphics g,
        Rectangle bounds,
        string text,
        Color accent,
        Color pillBg,
        Color textColor,
        Font font,
        bool withDot = true)
    {
        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var path = RoundedRect(bounds, bounds.Height / 2))
        using (var brush = new SolidBrush(pillBg))
        {
            g.FillPath(brush, path);
        }

        var dotSize = Math.Max(6, bounds.Height / 3);
        var dotLeft = bounds.X + Math.Max(7, bounds.Height / 3);
        var textLeft = dotLeft;

        if (withDot)
        {
            using var dotBrush = new SolidBrush(accent);
            g.FillEllipse(dotBrush, dotLeft, bounds.Y + (bounds.Height - dotSize) / 2, dotSize, dotSize);
            textLeft = dotLeft + dotSize + Math.Max(4, bounds.Height / 6);
        }
        else
        {
            textLeft = bounds.X + 8;
        }

        g.SmoothingMode = previous;

        var textRect = new Rectangle(
            textLeft,
            bounds.Y,
            Math.Max(0, bounds.Right - textLeft - 6),
            bounds.Height);

        TextRenderer.DrawText(
            g, text, font, textRect, textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    public static string[] Wrap(Graphics g, string text, Font font, int maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 8)
            return Array.Empty<string>();

        var lines = new List<string>();
        foreach (var paragraph in text.Split('\n'))
        {
            var current = paragraph.TrimEnd('\r');
            if (current.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            if (MeasureWidth(current, font) <= maxWidth)
            {
                lines.Add(current);
                continue;
            }

            var start = 0;
            while (start < current.Length)
            {
                var remaining = current.Length - start;
                var take = FitLength(current, start, remaining, font, maxWidth);
                if (take <= 0)
                    take = 1;

                var breakAt = FindBreakPoint(current, start, take);
                var segment = current.Substring(start, breakAt).TrimEnd();
                lines.Add(segment);

                start += breakAt;
                while (start < current.Length && current[start] == ' ')
                    start++;
            }
        }

        return lines.ToArray();
    }

    private static int FitLength(string text, int start, int length, Font font, int maxWidth)
    {
        var low = 1;
        var high = length;
        var best = 1;

        while (low <= high)
        {
            var mid = (low + high) / 2;
            var width = MeasureWidth(text.Substring(start, mid), font);
            if (width <= maxWidth)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return best;
    }

    private static int FindBreakPoint(string text, int start, int take)
    {
        for (var i = take; i > Math.Max(0, take / 2); i--)
        {
            var ch = text[start + i - 1];
            if (ch is ' ' or '/' or '\\' or '-' or '_' or ',' or ';' or ':' or '；' or '，' or '、' or '.' or '·')
                return i;
        }
        return take;
    }

    public static int MeasureWidth(string text, Font font)
    {
        return TextRenderer.MeasureText(
            text, font, new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
    }

    public static int WrapHeight(Graphics g, string text, Font font, int maxWidth, int lineHeight)
    {
        var lines = Wrap(g, text, font, maxWidth);
        return Math.Max(lineHeight, lines.Length * lineHeight);
    }

    public static int DrawWrapped(
        Graphics g,
        string text,
        Font font,
        Rectangle bounds,
        Color color,
        int lineHeight)
    {
        var lines = Wrap(g, text, font, bounds.Width);
        var y = bounds.Y;

        foreach (var line in lines)
        {
            if (y + lineHeight > bounds.Bottom + lineHeight / 2)
                break;

            TextRenderer.DrawText(
                g, line, font,
                new Rectangle(bounds.X, y, bounds.Width, lineHeight + 2),
                color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            y += lineHeight;
        }

        return lines.Length * lineHeight;
    }

    public static void DrawText(Graphics g, string text, Font font, Rectangle bounds, Color color, TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix)
    {
        TextRenderer.DrawText(g, text, font, bounds, color, flags | TextFormatFlags.NoPadding);
    }
}

/// <summary>
/// Win32 互操作：用于无边框窗口的拖动、边缘缩放与暗色滚动条。
/// </summary>
internal static class NativeMethods
{
    public const int WM_NCHITTEST = 0x0084;
    public const int WM_NCLBUTTONDOWN = 0x00A1;

    public const int HTCLIENT = 1;
    public const int HTCAPTION = 2;
    public const int HTLEFT = 10;
    public const int HTRIGHT = 11;
    public const int HTTOP = 12;
    public const int HTTOPLEFT = 13;
    public const int HTTOPRIGHT = 14;
    public const int HTBOTTOM = 15;
    public const int HTBOTTOMLEFT = 16;
    public const int HTBOTTOMRIGHT = 17;

    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(Point point);

    [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string subAppName, string? subIdList);

    public static void UseDarkScrollbars(Control control)
    {
        if (control.IsHandleCreated)
            TryApply(control.Handle);

        control.HandleCreated += (_, _) => TryApply(control.Handle);

        foreach (Control child in control.Controls)
            UseDarkScrollbars(child);
    }

    private static void TryApply(IntPtr handle)
    {
        try
        {
            _ = SetWindowTheme(handle, "DarkMode_Explorer", null);
        }
        catch
        {
            // 旧版 Windows 不支持，忽略
        }
    }
}

/// <summary>
/// 支持鼠标滚轮滚动的自绘控件。WinForms 的滚轮消息只会送到焦点控件，
/// 若不重定向，用户把鼠标悬停在列表上滚动会毫无反应，属于常见可用性缺陷。
/// </summary>
internal interface IWheelScrollable
{
    void ScrollByWheel(int delta);
}

internal sealed class WheelRedirectFilter : IMessageFilter
{
    private const int WmMouseWheel = 0x020A;

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != WmMouseWheel)
            return false;

        var handle = NativeMethods.WindowFromPoint(Cursor.Position);
        if (handle == IntPtr.Zero)
            return false;

        var control = Control.FromHandle(handle);
        while (control != null && control is not IWheelScrollable)
            control = control.Parent;

        if (control is not IWheelScrollable scroller)
            return false;

        var delta = (short)((long)m.WParam >> 16);
        scroller.ScrollByWheel(delta);
        return true;
    }
}

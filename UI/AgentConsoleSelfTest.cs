using System.Drawing.Imaging;
using System.Reflection;
using ModSorterAgent.Agent;
using ModSorterAgent.Models;
using ModSorterAgent.Reporting;

namespace ModSorterAgent.UI;

/// <summary>
/// Agent 接管窗口的合成夹具自检：按键齐备、接管前的过程更新不丢、
/// 完全居中、运行中可投递消息、任何时刻都能一步退出。
///
/// 运行态与结束态各留一张截图在 %TEMP%\ModSorterAgent-UiSelfTest，供人工看观感。
/// </summary>
public static class AgentConsoleSelfTest
{
    private static string OutputDir => Path.Combine(Path.GetTempPath(), "ModSorterAgent-UiSelfTest");

    public static IReadOnlyList<string> Run()
    {
        var failures = new List<string>();
        Form? owner = null;
        AgentConsoleForm? console = null;

        // 没有交互桌面（无人值守环境）时开不了窗口，跳过而不是报失败。
        if (!Environment.UserInteractive)
        {
            Console.WriteLine("    [INFO] 无交互桌面，跳过窗口自检。");
            return failures;
        }

        try
        {
            TryEnableVisualStyles();

            // 门禁：ModernButton 单次鼠标点击必须且只能触发一次 Click 事件，
            // 严禁因 ControlStyles.StandardClick 与 OnMouseUp 双发导致 Agent 窗口重复弹出。
            using (var testBtn = new ModSorterAgent.UI.Controls.ModernButton("测试"))
            using (var testForm = new Form())
            {
                testForm.Controls.Add(testBtn);
                testForm.Show();
                Pump(50);
                var clickCount = 0;
                testBtn.Click += (_, _) => clickCount++;

                const int WM_LBUTTONDOWN = 0x0201;
                const int WM_LBUTTONUP = 0x0202;
                var lParam = 10 | (10 << 16);
                NativeMethods.SendMessage(testBtn.Handle, WM_LBUTTONDOWN, 1, lParam);
                NativeMethods.SendMessage(testBtn.Handle, WM_LBUTTONUP, 0, lParam);
                Pump(50);

                if (clickCount != 1)
                    failures.Add($"ModernButton 单次点击触发了 {clickCount} 次 Click 事件（必须为 1，多发会导致 Agent 窗口重复弹出）。");
            }

            var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            owner = new Form
            {
                Text = "自检宿主窗口",
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(area.X + 40, area.Y + 40),
                Size = new Size(Math.Min(1400, area.Width - 160), Math.Min(900, area.Height - 160)),
                BackColor = UiTheme.AppBg
            };
            owner.Show();
            Pump();

            var title = "Agent · 兼容补丁生成";
            console = new AgentConsoleForm(title);

            var cancelCount = 0;
            var posted = new List<string>();
            console.CancelRequested += (_, _) => cancelCount++;
            console.MessageSent += (_, text) => posted.Add(text);

            // 复核是从 TakeOver 之前就开始推进的：这些更新一条都不能丢。
            var first = FakeConflict("case-1", "Pick Up And Haul", "Simple Sidearms");
            var second = FakeConflict("case-2", "RimHUD", "Dubs Mint Menus", "HarmonyBlockingPrefixConflict");
            console.Begin(title, 2);
            console.BeginConflict(first, 0, 2);

            console.TakeOver(owner);
            Pump();

            AssertControls(console, failures);
            AssertCentered(console, owner, failures);
            AssertOwnerLocked(owner, failures);

            var output = Field<RichTextBox>(console, "_output");
            if (output == null || !output.Text.Contains("Pick Up And Haul", StringComparison.Ordinal))
                failures.Add("接管前写入的冲突过程没有补进记录区。");

            console.Reasoning(first,
                "两个模组都在 Verb.TryStartCastOn 上挂了 Prefix。\n" +
                "Simple Sidearms 的 Prefix 直接返回 false 截断原方法，Pick Up And Haul 的改写因此永远不生效。");
            console.ToolCall(first, new AgentToolCall("call-1", "decompile_method",
                "{\"assembly\":\"game\",\"type\":\"Verse.Verb\",\"member\":\"TryStartCastOn\",\"parameter_count\":3}"));
            console.ToolCall(first, new AgentToolCall("call-2", "find_members",
                "{\"assembly\":\"simple_sidearms\",\"query\":\"TryStartCastOn\"}"));
            console.ConflictCompleted(first,
                FakeReport(first.Key, AgentVerdictKind.RealConflict, 0.86, "Prefix 截断让另一方的改写失效"));
            Capture(console, "01-运行中", failures);

            // 运行中投递要求
            SetComposerText(console, "优先用顺序方案，不要改原版方法");
            var send = Field<Control>(console, "_btnSend");
            if (send is not { Enabled: true })
                failures.Add("输入非空后「发送消息」仍不可点。");

            Invoke(console, "SendMessage");
            Pump();
            if (posted.Count != 1 || posted[0] != "优先用顺序方案，不要改原版方法")
                failures.Add($"投递消息没有按预期触发：{(posted.Count == 0 ? "没有收到事件" : string.Join(" | ", posted))}");
            if (output == null || !output.Text.Contains("优先用顺序方案", StringComparison.Ordinal))
                failures.Add("投递的消息没有出现在记录区。");
            if (ComposerText(console).Length != 0)
                failures.Add("投递之后输入框没有清空。");

            Invoke(console, "SendMessage");
            if (posted.Count != 1)
                failures.Add("空输入也触发了投递。");

            console.BeginConflict(second, 1, 2);
            console.ConflictCompleted(second,
                FakeReport(second.Key, AgentVerdictKind.ConditionDependent, 0.62, "只有同时开某个选项时才会撞车"));
            console.End(new[]
            {
                FakeReport(first.Key, AgentVerdictKind.RealConflict, 0.86, "Prefix 截断让另一方的改写失效"),
                FakeReport(second.Key, AgentVerdictKind.ConditionDependent, 0.62, "只有同时开某个选项时才会撞车")
            });
            Pump();

            AssertFinishedState(console, failures);
            Capture(console, "02-已完成", failures);

            // 结束后退出：不再取消 Agent，主窗口恢复
            Invoke(console, "ExitConsole");
            Pump();
            if (cancelCount != 0)
                failures.Add($"结束后的退出不应再取消 Agent，实际取消 {cancelCount} 次。");
            if (!owner.Enabled)
                failures.Add("结束后退出没有恢复主窗口可操作。");

            AssertStopAndExit(owner, failures, (form, list) => Capture(form, "03-已停止", list));
            AssertMinimize(owner, failures);
            AssertEscapeExit(owner, failures);
            AssertGuidanceInjection(failures);
            AssertPlacementOverRealMainForm(failures);
        }
        catch (Exception ex)
        {
            failures.Add($"自检异常：{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            console?.Dispose();
            owner?.Dispose();
        }

        return failures;
    }

    // ── 断言 ──────────────────────────────

    private static void AssertControls(AgentConsoleForm console, List<string> failures)
    {
        var minimize = Field<Control>(console, "_btnMinimize");
        var exit = Field<Control>(console, "_btnExit");
        var send = Field<Control>(console, "_btnSend");
        var stop = Field<Control>(console, "_btnStop");
        var back = Field<Control>(console, "_btnReturn");
        var composer = Field<Control>(console, "_composer");

        if (minimize is not { Visible: true })
            failures.Add("标题栏缺少可用的「最小化」按钮。");
        if (exit is not { Visible: true, Enabled: true })
            failures.Add("标题栏缺少可用的「退出」按钮。");
        if (send is not { Visible: true })
            failures.Add("底部缺少「发送消息」按钮。");
        if (stop is not { Visible: true, Enabled: true })
            failures.Add("运行中缺少可用的「停止」按钮。");
        if (back is { Visible: true })
            failures.Add("运行中不该出现「返回」按钮。");
        if (composer is not { Visible: true })
            failures.Add("底部缺少消息输入框。");
        if (!console.ShowInTaskbar)
            failures.Add("窗口不在任务栏：最小化之后没有确定的恢复入口。");
    }

    private static void AssertFinishedState(AgentConsoleForm console, List<string> failures)
    {
        var send = Field<Control>(console, "_btnSend");
        var stop = Field<Control>(console, "_btnStop");
        var back = Field<Control>(console, "_btnReturn");

        if (stop is { Visible: true })
            failures.Add("结束后「停止」仍在显示。");
        if (back is not { Visible: true, Enabled: true })
            failures.Add("结束后没有出现可用的「返回」按钮。");
        if (send is { Enabled: true })
            failures.Add("结束后「发送消息」仍可点：会让玩家以为消息还有人收。");

        var rail = FieldValue<float>(console, "_progress");
        if (rail < 0.999f)
            failures.Add($"结束后进度轨没有走满，实际 {rail:P0}。");
    }

    private static void AssertCentered(AgentConsoleForm console, Form owner, List<string> failures)
    {
        var area = Screen.FromRectangle(owner.Bounds).WorkingArea;
        if (!area.Contains(console.Bounds))
            failures.Add($"窗口超出可用桌面区域：窗口 {console.Bounds}，可用 {area}。");

        var areaCenter = new Point(area.X + area.Width / 2, area.Y + area.Height / 2);
        var ownerCenter = new Point(owner.Bounds.X + owner.Width / 2, owner.Bounds.Y + owner.Height / 2);
        var consoleCenter = new Point(console.Bounds.X + console.Width / 2, console.Bounds.Y + console.Height / 2);

        var onOwner = Math.Abs(consoleCenter.X - ownerCenter.X) <= 2 && Math.Abs(consoleCenter.Y - ownerCenter.Y) <= 2;
        var onArea = Math.Abs(consoleCenter.X - areaCenter.X) <= 2 && Math.Abs(consoleCenter.Y - areaCenter.Y) <= 2;

        Console.WriteLine(
            $"    [INFO] Agent 窗口 {console.Width}x{console.Height} @ {console.Bounds.Location}，" +
            $"主窗口中心 {ownerCenter}，窗口中心 {consoleCenter}，可用区中心 {areaCenter}，DPI {console.DeviceDpi}");
        Console.WriteLine(
            $"    [INFO] 标题栏 {Field<Control>(console, "_header")?.Height}px，" +
            $"底栏 {Field<Control>(console, "_footer")?.Height}px，" +
            $"输入框 {Field<Control>(console, "_composer")?.Height}px，" +
            $"发送键 {Field<Control>(console, "_btnSend")?.Width}x{Field<Control>(console, "_btnSend")?.Height}，" +
            $"停止键 {Field<Control>(console, "_btnStop")?.Width}x{Field<Control>(console, "_btnStop")?.Height}");

        if (!onOwner && !onArea)
            failures.Add($"窗口没有完全居中：相对主窗口偏差 {consoleCenter.X - ownerCenter.X}/" +
                         $"{consoleCenter.Y - ownerCenter.Y}，相对可用区偏差 {consoleCenter.X - areaCenter.X}/" +
                         $"{consoleCenter.Y - areaCenter.Y}。");
    }

    private static void AssertOwnerLocked(Form owner, List<string> failures)
    {
        if (owner.Enabled)
            failures.Add("接管期间主窗口仍可操作。");
    }

    /// <summary>停止按钮与退出按钮：运行中先停后关，且不重复取消。</summary>
    private static void AssertStopAndExit(Form owner, List<string> failures, Action<Form, List<string>> captureStopping)
    {
        using var running = new AgentConsoleForm("Agent · 实机验证");
        var cancels = 0;
        running.CancelRequested += (_, _) => cancels++;

        running.Begin("Agent · 实机验证", 2);
        running.BeginConflict(FakeConflict("case-9", "RimHUD", "Dubs Mint Menus"), 0, 2);
        running.TakeOver(owner);
        Pump();

        ClickControl(Field<Control>(running, "_btnStop"));
        Pump();
        var stop = Field<Control>(running, "_btnStop");
        if (cancels != 1)
            failures.Add($"点「停止」应取消一次，实际 {cancels} 次。");
        if (stop is { Enabled: true })
            failures.Add("点过「停止」之后按钮仍可重复点。");
        if (stop?.Text != "正在停止…")
            failures.Add($"停止中的按钮文案不对：{stop?.Text}");

        running.Fail("已停止");
        Pump();
        captureStopping(running, failures);

        Invoke(running, "ExitConsole");
        Pump();
        if (cancels != 1)
            failures.Add($"停过之后再退出不应重复取消，实际 {cancels} 次。");
        if (running.Visible)
            failures.Add("运行中点「退出」没有关掉窗口。");
        if (!owner.Enabled)
            failures.Add("退出之后主窗口没有恢复可操作。");
    }

    /// <summary>最小化按钮：能最小化，并且能靠任务栏找回来。</summary>
    private static void AssertMinimize(Form owner, List<string> failures)
    {
        using var console = new AgentConsoleForm("Agent · 兼容补丁生成");
        console.TakeOver(owner);
        Pump();

        ClickControl(Field<Control>(console, "_btnMinimize"));
        Pump();
        if (console.WindowState != FormWindowState.Minimized)
            failures.Add("点「最小化」之后窗口没有最小化。");
        if (!console.ShowInTaskbar)
            failures.Add("最小化之后没有任务栏入口，无法恢复。");

        console.WindowState = FormWindowState.Normal;
        Pump();
        Invoke(console, "ExitConsole");
        Pump();
        if (!owner.Enabled)
            failures.Add("最小化测试结束后主窗口没有恢复可操作。");
    }

    /// <summary>Esc 也是一步可达的退路：运行中按下即停止并关窗。</summary>
    private static void AssertEscapeExit(Form owner, List<string> failures)
    {
        using var console = new AgentConsoleForm("Agent · 兼容补丁生成");
        var cancels = 0;
        console.CancelRequested += (_, _) => cancels++;

        console.TakeOver(owner);
        Pump();

        var keyDown = typeof(AgentConsoleForm)
            .GetMethod("OnFormKeyDown", BindingFlags.Instance | BindingFlags.NonPublic);
        keyDown?.Invoke(console, new object?[] { console, new KeyEventArgs(Keys.Escape) });
        Pump();

        if (cancels != 1)
            failures.Add($"运行中按 Esc 应停止一次，实际 {cancels} 次。");
        if (console.Visible)
            failures.Add("运行中按 Esc 没有关掉窗口。");
        if (!owner.Enabled)
            failures.Add("按 Esc 退出之后主窗口没有恢复可操作。");
    }

    // ── 夹具 ──────────────────────────────

    /// <summary>窗口投递的要求必须真的进提示词：空白忽略、去首尾空白、超出条数挤掉最早的、清空后回到原样。</summary>
    private static void AssertGuidanceInjection(List<string> failures)
    {
        var channel = new AgentGuidanceChannel();
        using var investigator = new AgentInvestigator(
            new AgentConfig("off", string.Empty, string.Empty, string.Empty),
            Array.Empty<ModDescriptor>(),
            Path.GetTempPath());
        investigator.Guidance = channel;

        const string prompt = "基础提示词";
        if (PromptFor(investigator, prompt) != prompt)
            failures.Add("没有投递要求时提示词被改动了。");

        channel.Post("  优先用顺序方案  ");
        var withOne = PromptFor(investigator, prompt);
        if (!withOne.Contains("- 优先用顺序方案\n", StringComparison.Ordinal))
            failures.Add("投递的要求没有去掉首尾空白后进入提示词。");
        if (!withOne.StartsWith(prompt, StringComparison.Ordinal))
            failures.Add("注入要求破坏了原有提示词的正文。");

        channel.Post("   ");
        if (channel.Count != 1)
            failures.Add($"空白消息不该入列，实际 {channel.Count} 条。");

        for (var i = 1; i <= 9; i++)
            channel.Post($"第 {i} 条");

        var many = PromptFor(investigator, prompt);
        if (many.Contains("优先用顺序方案", StringComparison.Ordinal))
            failures.Add("超过条数上限后，最早的要求没有被挤掉。");
        if (!many.Contains("第 9 条", StringComparison.Ordinal))
            failures.Add("最新投递的要求没有进入提示词。");

        channel.Clear();
        if (PromptFor(investigator, prompt) != prompt)
            failures.Add("清空要求之后提示词没有回到原样。");
    }

    private static string PromptFor(AgentInvestigator investigator, string prompt)
    {
        var method = typeof(AgentInvestigator)
            .GetMethod("AppendGuidance", BindingFlags.Instance | BindingFlags.NonPublic);
        return method?.Invoke(investigator, new object[] { prompt }) as string ?? prompt;
    }

    /// <summary>
    /// 生产路径复现：宿主换成正牌主窗口，并把整块屏幕拍下来——
    /// 「窗口落在屏幕哪里」必须是能对着图核对的事实，只看裁到窗口边界的截图看不出来。
    /// </summary>
    private static void AssertPlacementOverRealMainForm(List<string> failures)
    {
        using var main = new MainForm();
        AssertAgentIsOptional(main, failures);
        main.Show();
        Pump(300);

        var conflict = FakeConflict("case-real", "Pick Up And Haul", "Simple Sidearms");
        using var console = new AgentConsoleForm("Agent · 兼容补丁生成");
        console.Begin("Agent · 兼容补丁生成", 1);
        console.BeginConflict(conflict, 0, 1);
        console.Reasoning(conflict, "宿主是正式主窗口，这里是落点复现。");
        console.TakeOver(main);
        Pump(500);

        var screen = Screen.FromControl(main);
        var mode = Environment.GetEnvironmentVariable("MODSORTER_UIPROBE_DPI") ?? "permonitorv2";
        Console.WriteLine(
            $"    [INFO:{mode}] 主窗口 {main.Bounds}（中心 {Center(main.Bounds)}），" +
            $"控制台 {console.Bounds}（中心 {Center(console.Bounds)}），可见={console.Visible}，" +
            $"屏幕 {screen.Bounds}，可用 {screen.WorkingArea}，DPI {console.DeviceDpi}");

        CaptureWholeScreen(console, "30-整屏-" + mode, failures);
        Capture(console, "31-真实宿主-窗口", failures);

        var dx = Center(console.Bounds).X - Center(main.Bounds).X;
        var dy = Center(console.Bounds).Y - Center(main.Bounds).Y;
        if (Math.Abs(dx) > 2 || Math.Abs(dy) > 2)
            failures.Add($"真实主窗口宿主下窗口没有居中：偏差 {dx}/{dy}。");

        if (!console.ShowInTaskbar)
            failures.Add("真实主窗口宿主下窗口不在任务栏。");

        Invoke(console, "ExitConsole");
        Pump();
        if (!main.Enabled)
            failures.Add("真实主窗口宿主：退出后主窗口没有恢复可操作。");

        main.Close();
        Pump();
    }

    private static void AssertAgentIsOptional(MainForm main, List<string> failures)
    {
        var mod = new ModDescriptor { PackageId = "selftest.optional.agent", Name = "Optional", RootDirectory = Path.GetTempPath() };
        var report = ModSortReportBuilder.BuildReport("1.7", new[] { mod },
            Array.Empty<PairwiseConflictFinding>(), Array.Empty<OrderingEdge>(),
            Array.Empty<string>(), Array.Empty<string>(), null);
        var result = new PipelineResult(report, string.Empty, Array.Empty<string>(), "1.7", new[] { mod });
        var type = typeof(MainForm);
        type.GetField("_lastResult", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, result);
        type.GetMethod("PrepareConflictWorkspace", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, new object[] { result });
        type.GetField("_selectedMod", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, report.SortedMods[0]);
        Invoke(main, "UpdateDetailForMod");
        Invoke(main, "OnModSelectionChanged");
        if (type.GetField("_investigator", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main) != null)
            failures.Add("普通结果准备与模组选择不得创建 Agent。");
        foreach (var field in new[] { "_btnDeepCheck", "_btnBuildPatch", "_btnLiveVerify" })
        {
            var button = Field<Control>(main, field);
            if (button == null || button.Parent == null)
                failures.Add($"可选 Agent 按钮未保留：{field}");
        }
    }

    private static Point Center(Rectangle rect)
        => new(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

    /// <summary>整屏截图：一眼能看出窗口落在屏幕的哪里、跟主窗口是什么相对位置。</summary>
    private static void CaptureWholeScreen(Form form, string name, List<string> failures)
    {
        try
        {
            Directory.CreateDirectory(OutputDir);
            var path = Path.Combine(OutputDir, name + ".png");

            // 自检进程不在前台，不置顶就会被别的窗口盖住，截出来看不到自己
            form.TopMost = true;
            form.Activate();
            Pump(260);
            form.Refresh();
            Pump(160);

            var bounds = SystemInformation.VirtualScreen;
            using (var bitmap = new Bitmap(bounds.Width, bounds.Height))
            {
                using (var g = Graphics.FromImage(bitmap))
                    g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bitmap.Size);

                bitmap.Save(path, ImageFormat.Png);
            }

            form.TopMost = false;
            Console.WriteLine($"    [INFO] 整屏截图：{path}");
        }
        catch (Exception ex)
        {
            failures.Add($"整屏截图失败（{name}）：{ex.Message}");
        }
    }

    private static PlayerConflict FakeConflict(
        string key,
        string modA,
        string modB,
        string category = "MultipleTranspilersSameTarget")
    {
        return new PlayerConflict(
            Key: key,
            Grade: ConflictGrade.Red,
            SeverityLabel: "红",
            ModAName: modA,
            ModAPackageId: modA.Replace(' ', '_').ToLowerInvariant(),
            ModBName: modB,
            ModBPackageId: modB.Replace(' ', '_').ToLowerInvariant(),
            Category: category,
            CategoryLabel: UiTheme.CategoryLabel(category),
            Headline: "两个模组改写同一个方法",
            What: "落点方法相同。",
            Impact: "其中一方的改写会失效。",
            Action: "复核补丁落点。",
            TargetLabel: "Verse.Verb.TryStartCastOn",
            Targets: new[] { "Verse.Verb.TryStartCastOn" },
            Findings: Array.Empty<PairwiseConflictFinding>(),
            InvolvesInactiveMod: false);
    }

    private static AgentReport FakeReport(
        string key,
        AgentVerdictKind verdict,
        double confidence,
        string headline)
    {
        return new AgentReport(
            ConflictKey: key,
            GeneratedAtUtc: DateTime.UtcNow,
            Mode: "自检夹具",
            Verdict: verdict,
            Confidence: confidence,
            Headline: headline,
            Explanation: "夹具说明。",
            HiddenRisks: Array.Empty<AgentHiddenRisk>(),
            FixStrategy: "order",
            PatchPlan: null,
            CodeSources: Array.Empty<string>(),
            EvidenceSummary: "夹具证据。",
            RawResponse: null,
            Error: null);
    }

    // ── 工具 ──────────────────────────────

    private static void TryEnableVisualStyles()
    {
        // 与正式 GUI 保持同一种 DPI 模式：不做这一步，自检窗口在带缩放的显示器上
        // 会按逻辑坐标定位，截图会错位，量出来的居中也不可信。
        // MODSORTER_UIPROBE_DPI 可在自检里换一种模式（unaware / system），
        // 用来复现"进程被系统缩放"时的落点。
        var mode = Environment.GetEnvironmentVariable("MODSORTER_UIPROBE_DPI");
        try
        {
            Application.SetHighDpiMode(mode?.ToLowerInvariant() switch
            {
                "unaware" => HighDpiMode.DpiUnaware,
                "system" => HighDpiMode.SystemAware,
                _ => HighDpiMode.PerMonitorV2
            });
        }
        catch
        {
            // 进程 DPI 模式已定死时忽略
        }

        try
        {
            Application.EnableVisualStyles();
        }
        catch
        {
            // 自检不因外观初始化失败而中断
        }
    }

    /// <summary>把窗口实际画到屏幕上的样子截下来，供人工看观感。</summary>
    private static void Capture(Form form, string name, List<string> failures)
    {
        try
        {
            Directory.CreateDirectory(OutputDir);
            var path = Path.Combine(OutputDir, name + ".png");

            form.TopMost = true;
            form.Activate();
            Pump(240);
            form.Refresh();
            Pump(120);

            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                using (var g = Graphics.FromImage(bitmap))
                    g.CopyFromScreen(form.Bounds.Location, Point.Empty, form.Size);

                bitmap.Save(path, ImageFormat.Png);
            }

            form.TopMost = false;
            Console.WriteLine($"    [INFO] 截图：{path}");
        }
        catch (Exception ex)
        {
            failures.Add($"截图失败（{name}）：{ex.Message}");
        }
    }

    private static void Pump(int milliseconds = 80)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        do
        {
            Application.DoEvents();
            Thread.Sleep(15);
        }
        while (Environment.TickCount64 < deadline);
    }

    private static T? Field<T>(object target, string name) where T : class
    {
        return target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(target) as T;
    }

    private static T FieldValue<T>(object target, string name) where T : struct
    {
        var value = target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(target);
        return value is T typed ? typed : default;
    }

    private static void Invoke(object target, string method)
    {
        target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?
            .Invoke(target, null);
    }

    private static void ClickControl(Control? control)
    {
        if (control == null)
            return;

        typeof(Control)
            .GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)?
            .Invoke(control, new object[] { EventArgs.Empty });
    }

    private static string ComposerText(AgentConsoleForm console)
    {
        var composer = Field<Control>(console, "_composer");
        var value = composer?.GetType().GetProperty("Value")?.GetValue(composer);
        return value as string ?? string.Empty;
    }

    private static void SetComposerText(AgentConsoleForm console, string text)
    {
        var composer = Field<Control>(console, "_composer");
        composer?.GetType().GetProperty("Value")?.SetValue(composer, text);
    }
}

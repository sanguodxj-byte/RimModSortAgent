using ModSorterAgent.Agent;
using ModSorterAgent.Discovery;
using ModSorterAgent.Services;
using ModSorterAgent.UI.Controls;

namespace ModSorterAgent.UI;

/// <summary>
/// 设置对话框：游戏/工坊/工作区/ModsConfig 路径，以及 Agent 核查所用模型与补丁输出目录。
/// 留空的路径会跟随自动检测；Agent 配置留空则始终使用本地代码核查。
/// </summary>
internal sealed class SettingsDialog : Form
{
    private sealed record RowSpec(
        string Title,
        string Hint,
        Func<AppSettings, string> GetValue,
        Action<AppSettings, string> SetValue,
        Func<string?> AutoDetect,
        Func<string, string> GetEffective,
        bool IsFile);

    private readonly List<(RowSpec Spec, TextBox Input, Label Status)> _rows = new();

    private readonly ComboBox _providerInput = new();
    private readonly TextBox _baseUrlInput = new();
    private readonly TextBox _modelInput = new();
    private readonly TextBox _apiKeyInput = new();
    private readonly TextBox _patchDirInput = new();
    private readonly CheckBox _verifyGabs = new();
    private readonly CheckBox _includeWorkshop = new();
    private readonly string _existingProtectedKey;

    public SettingsDialog()
    {
        Text = "设置";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(650, 640);
        BackColor = UiTheme.SurfaceBg;
        ForeColor = UiTheme.TextPrimary;
        Font = UiTheme.BodyFont;
        ShowInTaskbar = false;

        var settings = ModListResolver.PathOptions.Settings;
        _existingProtectedKey = settings.AgentApiKeyProtected;

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 66,
            BackColor = UiTheme.SurfaceBg
        };

        var title = new Label
        {
            Text = "设置",
            AutoSize = true,
            Font = UiTheme.SectionFont,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(20, 13)
        };
        var subtitle = new Label
        {
            Text = "路径决定扫描范围；Agent 决定冲突核查与补丁生成能力。所有内容即时保存在 AppSettings.json。",
            AutoSize = true,
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.TextSecondary,
            Location = new Point(20, 38)
        };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);

        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            BackColor = UiTheme.SurfaceBg
        };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(UiTheme.BorderSubtle, 1f);
            e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
        };

        var content = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = UiTheme.SurfaceBg,
            Padding = new Padding(0)
        };

        var y = 4;

        var pathHeader = new Label
        {
            Text = "游戏与模组路径",
            AutoSize = true,
            Font = UiTheme.BodyBoldFont,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(20, y)
        };
        content.Controls.Add(pathHeader);
        y += 28;

        var specs = new[]
        {
            new RowSpec(
                "RimWorld 游戏安装目录",
                "包含 RimWorldWin64.exe / Data / Mods（Steam 库 steamapps\\common\\RimWorld）",
                s => s.GameInstallDir,
                (s, v) => s.GameInstallDir = v,
                PathAutoDetector.DetectGameInstallDir,
                _ => ModListResolver.PathOptions.RimWorldInstallDir,
                IsFile: false),
            new RowSpec(
                "Steam 创意工坊内容目录",
                "steamapps\\workshop\\content\\294100（本地 mod 与订阅池扫描根目录）",
                s => s.WorkshopContentDir,
                (s, v) => s.WorkshopContentDir = v,
                PathAutoDetector.DetectWorkshopContentDir,
                _ => ModListResolver.PathOptions.WorkshopDir,
                IsFile: false),
            new RowSpec(
                "本地模组工作区目录",
                "存放若干未打包 mod 文件夹的工作区（留空 = D:\\rim mod）",
                s => s.LocalModWorkspaceDir,
                (s, v) => s.LocalModWorkspaceDir = v,
                () => null,
                _ => ModListResolver.PathOptions.WorkspaceDir,
                IsFile: false),
            new RowSpec(
                "ModsConfig.xml 路径",
                "游戏模组加载配置（留空 = 自动检测 LocalLow\\Ludeon Studios\\…\\Config）",
                s => s.ModsConfigPath,
                (s, v) => s.ModsConfigPath = v,
                PathAutoDetector.DetectModsConfigPath,
                _ => ModListResolver.DefaultModsConfigPath,
                IsFile: true)
        };

        foreach (var spec in specs)
        {
            content.Controls.Add(BuildPathRow(spec, y, out var input, out var status));
            _rows.Add((spec, input, status));
            y += 82;
        }

        y += 10;
        content.Controls.Add(new Panel
        {
            Location = new Point(20, y),
            Size = new Size(590, 1),
            BackColor = UiTheme.BorderSubtle
        });
        y += 18;

        var agentHeader = new Label
        {
            Text = "Agent 代码核查",
            AutoSize = true,
            Font = UiTheme.BodyBoldFont,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(20, y)
        };
        content.Controls.Add(agentHeader);

        var agentHint = new Label
        {
            Text = "不配置模型时，Agent 仍会在本地反编译冲突补丁、读取目标方法并给出结论；配置模型后可获得更深入的隐患分析与自动补丁代码。",
            AutoSize = false,
            Size = new Size(590, 34),
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.TextMuted,
            Location = new Point(20, y + 22)
        };
        content.Controls.Add(agentHint);
        y += 64;

        _providerInput.DropDownStyle = ComboBoxStyle.DropDownList;
        _providerInput.FlatStyle = FlatStyle.Flat;
        _providerInput.BackColor = UiTheme.CardBg;
        _providerInput.ForeColor = UiTheme.TextPrimary;
        _providerInput.Font = UiTheme.SmallFont;
        _providerInput.Width = 384;
        _providerInput.Items.AddRange(new object[]
        {
            "仅本地核查（不联网）",
            "OpenAI 兼容接口",
            "Anthropic 接口"
        });
        _providerInput.SelectedIndex = settings.AgentProvider.Trim().ToLowerInvariant() switch
        {
            "openai" => 1,
            "anthropic" => 2,
            _ => 0
        };

        StyleInput(_baseUrlInput);
        _baseUrlInput.Text = settings.AgentBaseUrl;
        _baseUrlInput.PlaceholderText = "留空使用默认地址";

        StyleInput(_modelInput);
        _modelInput.Text = settings.AgentModel;
        _modelInput.PlaceholderText = "例如 gpt-4.1 或 claude-sonnet-4-5";

        StyleInput(_apiKeyInput);
        _apiKeyInput.UseSystemPasswordChar = true;
        _apiKeyInput.PlaceholderText = string.IsNullOrEmpty(_existingProtectedKey)
            ? "可选；也可用环境变量 MODSORTER_AGENT_API_KEY"
            : "已保存密钥，留空保留";

        StyleInput(_patchDirInput);
        _patchDirInput.Text = settings.AgentPatchOutputDir;
        _patchDirInput.PlaceholderText = AgentConfigLoader.DefaultPatchOutputDir;

        AddAgentRow(content, y, "启用方式", "本地核查永远可用；模型只增强结论与补丁代码。", _providerInput);
        y += 54;
        AddAgentRow(content, y, "接口地址", "OpenAI 兼容 /v1 或 Anthropic 根地址。", _baseUrlInput);
        y += 54;
        AddAgentRow(content, y, "模型名称", "需支持长上下文，返回严格 JSON。", _modelInput);
        y += 54;
        AddAgentRow(content, y, "API Key", "使用 Windows DPAPI 加密，仅当前用户可解密。", _apiKeyInput);
        y += 54;

        var patchButton = new ModernButton("浏览…", ButtonVariant.Secondary)
        {
            Location = new Point(540, y + 7),
            Height = 26,
            Width = 70
        };
        patchButton.Click += (_, _) =>
        {
            using var folder = new FolderBrowserDialog { Description = "选择兼容补丁输出目录" };
            if (folder.ShowDialog(this) == DialogResult.OK)
                _patchDirInput.Text = folder.SelectedPath;
        };
        _patchDirInput.Width = 314;
        AddAgentRow(content, y, "补丁输出目录", "生成的兼容补丁 mod 包存放位置。", _patchDirInput);
        content.Controls.Add(patchButton);
        y += 58;

        _verifyGabs.Text = "分析时尝试核验游戏实况（GABS / Player.log）";
        _verifyGabs.ForeColor = UiTheme.TextSecondary;
        _verifyGabs.Font = UiTheme.SmallFont;
        _verifyGabs.AutoSize = true;
        _verifyGabs.BackColor = Color.Transparent;
        _verifyGabs.Checked = settings.VerifyWithGabs;
        _verifyGabs.Location = new Point(20, y);
        content.Controls.Add(_verifyGabs);
        y += 28;

        _includeWorkshop.Text = "把 Steam 创意工坊订阅池一并纳入分析（仅参考，不自动启用）";
        _includeWorkshop.ForeColor = UiTheme.TextSecondary;
        _includeWorkshop.Font = UiTheme.SmallFont;
        _includeWorkshop.AutoSize = true;
        _includeWorkshop.BackColor = Color.Transparent;
        _includeWorkshop.Checked = settings.IncludeWorkshopSubscriptions;
        _includeWorkshop.Location = new Point(20, y);
        content.Controls.Add(_includeWorkshop);
        y += 36;

        content.AutoScrollMinSize = new Size(0, y + 8);

        var btnDetectAll = new ModernButton("全部自动检测", ButtonVariant.Ghost)
        {
            Location = new Point(20, 12),
            Height = 30
        };
        btnDetectAll.Click += (_, _) =>
        {
            var any = false;
            foreach (var (spec, input, status) in _rows.Where(r => r.Spec.AutoDetect != null!))
            {
                var detected = spec.AutoDetect();
                if (detected == null)
                    continue;
                input.Text = detected;
                any = true;
                UpdateRowStatus(spec, input, status);
            }

            if (!any)
                MessageBox.Show(this, "未检测到任何可自动识别的路径，请手动浏览选择。", "自动检测",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var btnReset = new ModernButton("恢复默认路径", ButtonVariant.Ghost)
        {
            Location = new Point(20 + btnDetectAll.Width + 8, 12),
            Height = 30
        };
        btnReset.Click += (_, _) =>
        {
            foreach (var (spec, input, status) in _rows)
            {
                input.Text = string.Empty;
                UpdateRowStatus(spec, input, status);
            }
        };

        var btnCancel = new ModernButton("取消", ButtonVariant.Ghost)
        {
            Location = new Point(ClientSize.Width - 270, 12),
            Height = 30
        };
        var btnSave = new ModernButton("保存", ButtonVariant.Primary)
        {
            Location = new Point(ClientSize.Width - 140, 12),
            Height = 30
        };
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        btnSave.Click += (_, _) => Save();

        footer.Controls.Add(btnDetectAll);
        footer.Controls.Add(btnReset);
        footer.Controls.Add(btnCancel);
        footer.Controls.Add(btnSave);

        Controls.Add(content);
        Controls.Add(footer);
        Controls.Add(header);
        AcceptButton = null;
    }

    private static void StyleInput(TextBox input)
    {
        input.Font = UiTheme.SmallFont;
        input.ForeColor = UiTheme.TextPrimary;
        input.BackColor = UiTheme.CardBg;
        input.BorderStyle = BorderStyle.FixedSingle;
        input.Width = 384;
    }

    private static void AddAgentRow(Control parent, int y, string title, string hint, Control input)
    {
        var titleLabel = new Label
        {
            Text = title,
            AutoSize = false,
            Size = new Size(200, 18),
            Font = UiTheme.BodyBoldFont,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(20, y + 6)
        };
        var hintLabel = new Label
        {
            Text = hint,
            AutoSize = false,
            Size = new Size(200, 30),
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.TextMuted,
            Location = new Point(20, y + 24)
        };

        input.Location = new Point(226, y + 7);
        parent.Controls.Add(titleLabel);
        parent.Controls.Add(hintLabel);
        parent.Controls.Add(input);
    }

    private Control BuildPathRow(RowSpec spec, int y, out TextBox input, out Label status)
    {
        var panel = new Panel { Location = new Point(20, y), Size = new Size(590, 78), BackColor = Color.Transparent };

        var title = new Label
        {
            Text = spec.Title,
            AutoSize = true,
            Font = UiTheme.BodyBoldFont,
            ForeColor = UiTheme.TextPrimary,
            Location = new Point(0, 0)
        };

        var hint = new Label
        {
            Text = spec.Hint,
            AutoSize = false,
            Size = new Size(590, 16),
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.TextMuted,
            Location = new Point(0, 18)
        };

        input = new TextBox
        {
            Text = spec.GetValue(ModListResolver.PathOptions.Settings),
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.TextPrimary,
            BackColor = UiTheme.CardBg,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(0, 40),
            Width = 468
        };

        var btnBrowse = new ModernButton("浏览…", ButtonVariant.Secondary)
        {
            Location = new Point(476, 39),
            Height = 26
        };
        btnBrowse.Click += (_, _) => Browse(spec);

        status = new Label
        {
            AutoSize = false,
            Size = new Size(590, 16),
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.TextSecondary,
            Location = new Point(0, 60),
            Text = string.Empty
        };

        if (spec.AutoDetect != null!)
        {
            var btnDetect = new ModernButton("检测", ButtonVariant.Ghost)
            {
                Location = new Point(476 + btnBrowse.Width + 6, 39),
                Height = 26
            };
            var capturedInput = input;
            var capturedStatus = status;
            btnDetect.Click += (_, _) =>
            {
                var detected = spec.AutoDetect();
                if (detected == null)
                {
                    MessageBox.Show(this, $"未能自动检测到「{spec.Title}」，请手动浏览选择。", "自动检测",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                capturedInput.Text = detected;
                UpdateRowStatus(spec, capturedInput, capturedStatus);
            };
            panel.Controls.Add(btnDetect);
        }

        panel.Controls.Add(title);
        panel.Controls.Add(hint);
        panel.Controls.Add(input);
        panel.Controls.Add(btnBrowse);
        panel.Controls.Add(status);

        UpdateRowStatus(spec, input, status);
        return panel;
    }

    private void UpdateRowStatus(RowSpec spec, TextBox input, Label status)
    {
        var value = input.Text.Trim();
        if (value.Length > 0)
        {
            var exists = DirectoryOrFileExists(value, spec.IsFile);
            status.Text = exists ? $"✓ 自定义：{value}" : $"⚠ 路径不存在：{value}";
            status.ForeColor = exists ? UiTheme.GreenText : UiTheme.RoseText;
        }
        else
        {
            var effective = spec.GetEffective(value);
            status.Text = $"当前生效（自动）：{effective}";
            status.ForeColor = UiTheme.TextSecondary;
        }
    }

    private static bool DirectoryOrFileExists(string path, bool isFile)
    {
        try
        {
            return isFile ? File.Exists(path) : Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private void Browse(RowSpec spec)
    {
        if (spec.IsFile)
        {
            using var dialog = new OpenFileDialog
            {
                Title = $"选择 {spec.Title}",
                Filter = "ModsConfig.xml|ModsConfig.xml|XML 文件 (*.xml)|*.xml",
                FileName = "ModsConfig.xml"
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                ApplyBrowseResult(spec, dialog.FileName);
            return;
        }

        using var folder = new FolderBrowserDialog
        {
            ShowNewFolderButton = false,
            Description = $"选择 {spec.Title}"
        };
        if (folder.ShowDialog(this) == DialogResult.OK)
            ApplyBrowseResult(spec, folder.SelectedPath);
    }

    private void ApplyBrowseResult(RowSpec spec, string path)
    {
        foreach (var (rowSpec, input, status) in _rows.Where(r => ReferenceEquals(r.Spec, spec)))
        {
            input.Text = path;
            UpdateRowStatus(rowSpec, input, status);
        }
    }

    private void Save()
    {
        var invalid = new List<string>();
        foreach (var (spec, input, _) in _rows)
        {
            var value = input.Text.Trim();
            if (value.Length > 0 && !DirectoryOrFileExists(value, spec.IsFile))
                invalid.Add(spec.Title);
        }

        if (invalid.Count > 0)
        {
            var confirm = MessageBox.Show(
                this,
                $"以下路径当前不存在：\n{string.Join("\n", invalid.Select(x => "· " + x))}\n\n仍要保存吗？（保存后相关扫描可能扫不到任何模组）",
                "路径不存在",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.OK)
                return;
        }

        var provider = _providerInput.SelectedIndex switch
        {
            1 => "openai",
            2 => "anthropic",
            _ => "off"
        };

        if (provider != "off" && string.IsNullOrWhiteSpace(_modelInput.Text))
        {
            MessageBox.Show(this, "已选择模型接口，但模型名称为空。\n请填写模型名称，或把启用方式改回「仅本地核查」。",
                "Agent 设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _modelInput.Focus();
            return;
        }

        if (provider != "off" && string.IsNullOrWhiteSpace(_apiKeyInput.Text) &&
            string.IsNullOrEmpty(_existingProtectedKey) &&
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AgentConfigLoader.EnvApiKey)))
        {
            var confirm = MessageBox.Show(
                this,
                "没有填写 API Key，也没有检测到环境变量 MODSORTER_AGENT_API_KEY。\n保存后将无法调用模型，Agent 会回退到本地核查。仍要保存吗？",
                "Agent 设置",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.OK)
                return;
        }

        var settings = new AppSettings
        {
            AgentProvider = provider,
            AgentBaseUrl = _baseUrlInput.Text.Trim(),
            AgentModel = _modelInput.Text.Trim(),
            AgentApiKeyProtected = _existingProtectedKey,
            AgentPatchOutputDir = _patchDirInput.Text.Trim(),
            VerifyWithGabs = _verifyGabs.Checked,
            IncludeWorkshopSubscriptions = _includeWorkshop.Checked
        };

        var typedKey = _apiKeyInput.Text.Trim();
        if (typedKey.Length > 0)
            settings.AgentApiKeyProtected = DpapiProtector.Protect(typedKey);

        foreach (var (spec, input, _) in _rows)
            spec.SetValue(settings, input.Text.Trim());

        ModListResolver.PathOptions.Update(settings);
        DialogResult = DialogResult.OK;
        Close();
    }
}

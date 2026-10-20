using System.Text.Json;
using ModSorterAgent.Models;

namespace ModSorterAgent.Services;

/// <summary>
/// 排序列表（SortListProfile）的 JSON 持久化存储，含"当前激活"标记。
/// 存储文件位于程序目录旁的 SortProfiles.json，随叫随用，不需要时静默降级为空列表。
/// </summary>
public sealed class SortProfileStore
{
    private sealed class StoreState
    {
        public List<SortListProfile> Profiles { get; set; } = new();
        public string? ActiveProfileName { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _storePath;
    private StoreState _state = new();

    public string StorePath => _storePath;
    public IReadOnlyList<SortListProfile> Profiles => _state.Profiles;
    public string? ActiveProfileName => _state.ActiveProfileName;

    public SortProfileStore(string? storePath = null)
    {
        _storePath = storePath ?? Path.Combine(AppContext.BaseDirectory, "SortProfiles.json");
        Load();
    }

    public SortListProfile? GetActiveProfile() =>
        _state.Profiles.FirstOrDefault(p =>
            string.Equals(p.Name, _state.ActiveProfileName, StringComparison.OrdinalIgnoreCase));

    public bool NameExists(string name) =>
        _state.Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>保存或覆盖（同名，忽略大小写）一份排序档，并立即持久化。</summary>
    public void Upsert(SortListProfile profile)
    {
        var index = _state.Profiles.FindIndex(p =>
            string.Equals(p.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            _state.Profiles[index] = profile;
        else
            _state.Profiles.Add(profile);

        _state.Profiles = _state.Profiles
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToList();
        Save();
    }

    /// <summary>删除指定排序档（若它是激活档则同时清除激活标记），并立即持久化。</summary>
    public bool Delete(string name)
    {
        var index = _state.Profiles.FindIndex(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return false;

        _state.Profiles.RemoveAt(index);
        if (string.Equals(_state.ActiveProfileName, name, StringComparison.OrdinalIgnoreCase))
            _state.ActiveProfileName = null;
        Save();
        return true;
    }

    public void SetActive(string? name)
    {
        _state.ActiveProfileName = string.IsNullOrWhiteSpace(name) ? null : name;
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_storePath))
                return;

            var state = JsonSerializer.Deserialize<StoreState>(File.ReadAllText(_storePath), JsonOptions);
            if (state == null)
                return;

            _state = state;

            // 激活名必须指向真实存在的档，防止手工编辑 JSON 后悬空
            if (_state.ActiveProfileName != null && GetActiveProfile() == null)
                _state.ActiveProfileName = null;
        }
        catch (Exception)
        {
            // 存储文件损坏时不让工具起不来：保持空状态，下次保存会重写文件
            _state = new StoreState();
        }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_storePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(_storePath, JsonSerializer.Serialize(_state, JsonOptions));
        }
        catch (Exception)
        {
            // 写不进去（只读目录等）时静默失败，功能退化为本次会话内存态
        }
    }
}

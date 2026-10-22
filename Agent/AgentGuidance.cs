using System.Collections.Concurrent;

namespace ModSorterAgent.Agent;

/// <summary>
/// 运行中玩家投递要求的通道：Agent 窗口收到消息后写入这里，
/// 调查器每次组装提示词时取出全部在案要求，作为玩家本人的指示一并交给模型。
/// 一次运行用一个实例，跑完即弃，不跨运行累积。
/// </summary>
public sealed class AgentGuidanceChannel
{
    // 要求会进入之后每一次调用，条数与长度都要收口，避免提示词被单条消息撑爆。
    private const int MaxMessages = 8;
    private const int MaxMessageLength = 1000;

    private readonly ConcurrentQueue<string> _messages = new();

    public int Count => _messages.Count;

    /// <summary>投递一条玩家消息；空白消息忽略，超出条数上限时挤掉最早的一条。</summary>
    public void Post(string? message)
    {
        var text = (message ?? string.Empty).Trim();
        if (text.Length == 0)
            return;

        if (text.Length > MaxMessageLength)
            text = text[..MaxMessageLength];

        _messages.Enqueue(text);
        while (_messages.Count > MaxMessages && _messages.TryDequeue(out _))
        {
        }
    }

    /// <summary>取当前全部在案要求的快照，供提示词组装使用。</summary>
    public IReadOnlyList<string> Snapshot() => _messages.ToArray();

    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
        }
    }
}

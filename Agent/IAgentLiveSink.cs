using ModSorterAgent.Reporting;

namespace ModSorterAgent.Agent;

/// <summary>
/// Agent 运行过程的实时出口：推理文本、工具调用、单条结论。
/// 由界面实现，把过程展示在 Agent 窗口里；不需要展示时留空即可。
/// </summary>
public interface IAgentLiveSink
{
    /// <summary>Agent 开始的整体任务，用于窗口标题与进度范围。</summary>
    void Begin(string title, int conflictCount);

    /// <summary>开始处理某一条冲突。</summary>
    void BeginConflict(PlayerConflict conflict, int index, int total);

    /// <summary>模型给出的推理文本。</summary>
    void Reasoning(PlayerConflict conflict, string text);

    /// <summary>模型请求调用某个工具。</summary>
    void ToolCall(PlayerConflict conflict, AgentToolCall call);

    /// <summary>单条冲突的结论产出。</summary>
    void ConflictCompleted(PlayerConflict conflict, AgentReport report);

    /// <summary>整体任务结束。</summary>
    void End(IReadOnlyList<AgentReport> reports);
}

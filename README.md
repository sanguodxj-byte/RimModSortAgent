# ModSorterAgent — RimWorld 模组排序与冲突分析工作台

读取当前 ModsConfig.xml、本地模组目录与 Steam 工坊订阅池，扫描 XML Def 与 Patch，反编译程序集提取 Harmony 补丁特征与动态注入点，做三级冲突定级（绿色 / 黄色 / 红色，另有作者声明不兼容的紫色）与分层拓扑排序，输出推荐加载顺序与冲突报告。可选接入模型接口做逐项深度核查，并把可修复的冲突统一编译成一个合并兼容补丁 mod。

界面与报告为中文。

## 运行前提

| 项目 | 要求 | 说明 |
| --- | --- | --- |
| 系统 | Windows 10 及以上（x64） | |
| 主程序运行时 | .NET 9 桌面运行时 | 安装自包含发布包则无需单独安装 |
| 兼容生成 | .NET 10 SDK | 仅在生成合并兼容补丁时需要，用于编译补丁 mod |
| 游戏 | RimWorld 安装目录 + Steam 工坊内容目录 | 自动检测（Steam 注册表与 libraryfolders.vdf），检测不到时在「设置」中手动指定 |
| 实机核验（可选） | GABS | 未安装时自动退回离线扫描 Player.log，不影响排序与冲突分析 |
| 模型接口（可选） | OpenAI 或 Anthropic 兼容接口 | 默认关闭，关闭时全程本地核查 |

游戏版本从 ModsConfig.xml 读取；主程序与命令行默认 1.7。

## 启动

图形界面：直接运行 ModSorterAgent.exe（不带参数即进界面）。

命令行（不开界面，适合批处理）：

```
ModSorterAgent.exe --from-mods-config                         读取当前配置并输出报告到控制台
ModSorterAgent.exe --local --output-report report.md          扫描本地模组
ModSorterAgent.exe --workshop --include-workshop              并入工坊订阅池
ModSorterAgent.exe --mods "a.b,c.d" --output-json out.json    直接给定模组列表
ModSorterAgent.exe --export-mods-config ModsConfig.xml        按推荐顺序导出配置
ModSorterAgent.exe --export-rml list.rml                      导出 RimSort 列表
ModSorterAgent.exe --no-verify-gabs                           跳过 GABS 核验
ModSorterAgent.exe --selftest                                 跑内置自检（分析器合成夹具）
```

其余参数：`--gui`、`--input-file`（`--input`）、`--game-version`、`--output-report`、`--output-json`、`--workshop-dir`、`--verify-gabs`。

## 数据与写入位置

- 设置 `AppSettings.json`、排序档案 `SortProfiles.json`：与 exe 同目录。**该目录必须可写**，放在 Program Files 等只读位置时设置与排序档案会保存失败（程序不会报错，只在本次会话内有效）。
- 补丁工作区：%LOCALAPPDATA%\ModSorterAgent\patches；Agent 案卷：%LOCALAPPDATA%\ModSorterAgent\agent-cases。
- 写入 ModsConfig.xml 前自动备份为 `ModsConfig.backup.<时间戳>.xml`，与配置同目录。
- 安装合并兼容补丁会把补丁目录复制到 游戏目录\Mods\，并写入 ModsConfig.xml。若目标位置已有同名文件夹且不是本工具生成的（缺少 .modsorteragent 标记），安装会被拒绝，不做覆盖。

## 隐私

- 默认 `agentProvider=off`，所有分析在本机完成，不发起网络请求。
- 启用模型接口后，「深度核查」与「兼容生成」会把相关模组的 XML 片段、反编译代码片段与冲突上下文发送到所配置的接口地址。所配置的是自备的 OpenAI 或 Anthropic 兼容接口。
- API Key 经 Windows DPAPI 加密后存放在同目录 `AppSettings.json`，只有当前 Windows 用户能解密；也可用环境变量 `MODSORTER_AGENT_API_KEY` 在运行期注入。

## 从源码构建

本项目引用同级目录的上游项目 DecompilerServer，单独克隆本仓库无法构建。把上游克隆为同级目录后再构建：

```
git clone https://github.com/pardeike/DecompilerServer.git
git clone <本仓库地址> RimModSortAgent
cd RimModSortAgent
dotnet build ModSorterAgent.csproj -c Release
```

需要 .NET 9 SDK 或更高版本。DecompilerServer 的依赖（ICSharpCode.Decompiler、ModelContextProtocol、Microsoft.Extensions.*）会随项目引用自动还原。

## 发布打包

```
powershell -ExecutionPolicy Bypass -File publish.ps1 -Zip
```

脚本编译发布、剔除个人数据与运行非必需文件、内附本说明与第三方许可、对产物跑一次自检，并在 artifacts\publish\ 输出目录与 zip（含 SHA256）。加 -SelfContained 生成自包含包（用户无需预装 .NET 9 桌面运行时）。

## 许可

本项目以 MIT 许可发布，见 [LICENSE](LICENSE)。

随包分发的第三方组件（ILSpy 的 ICSharpCode.Decompiler、DecompilerServer、ModelContextProtocol、Microsoft.Extensions.* 系列）许可类型与原文见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

# RimWorld 模组自动排序与代码冲突分级报告

- 生成时间: 2026-10-20 17:16:56
- 目标游戏版本: 1.7.4981 rev136
- 总体评级: 3.红色 (明确冲突)
- 模组总数: 37（1.绿色: 33，2.黄色: 2，3.红色: 2）
- 顺位调整模组数: 25 / 37
- 扫描统计: Harmony 补丁 513 处（其中动态反射点 70 处），XML Def 6095 项，XML Patch 221 项
- 分析耗时: 5.7 秒
- 实机状态: 静态代码与离线日志核验

## 架构分层分布

- 核心底座 7
- 系统与诊断 3
- 前置框架 2
- 种族与阵营 6
- 玩法与扩展 9
- 渲染与动画 6
- 语言汉化包 4

## 一、推荐加载顺序（含三级定级）

| 推荐顺位 | 原顺位 | 变动 | 架构分层 | 状态 | 分级 | 模组名称 | PackageId | Harmony | Def/Patch | 关键依据 |
|---:|---:|:--:|---|---|---|---|---|---:|---:|---|
| 1 | 1 | — | 核心底座 | 已启用 | 1.绿色 (确认无冲突) | Harmony | brrainz.harmony | 3 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 2 | 2 | — | 核心底座 | 已启用 | 1.绿色 (确认无冲突) | Core | ludeon.rimworld | 0 | 0/0 | 前置约束：排在 Harmony 之后（官方底座顺序） |
| 3 | 3 | — | 核心底座 | 已启用 | 1.绿色 (确认无冲突) | Royalty | ludeon.rimworld.royalty | 0 | 0/0 | 前置约束：排在 Core 之后（官方底座顺序） |
| 4 | 4 | — | 核心底座 | 已启用 | 1.绿色 (确认无冲突) | Ideology | ludeon.rimworld.ideology | 0 | 0/0 | 前置约束：排在 Royalty 之后（官方底座顺序）；前置约束：排在 Core 之后（About.loadBefore） |
| 5 | 5 | — | 核心底座 | 已启用 | 1.绿色 (确认无冲突) | Biotech | ludeon.rimworld.biotech | 0 | 0/0 | 前置约束：排在 Ideology 之后（官方底座顺序）；前置约束：排在 Royalty 之后（About.loadBefore） |
| 6 | 6 | — | 核心底座 | 已启用 | 1.绿色 (确认无冲突) | Anomaly | ludeon.rimworld.anomaly | 0 | 0/0 | 前置约束：排在 Biotech 之后（官方底座顺序）；前置约束：排在 Royalty 之后（About.loadBefore） |
| 7 | 7 | — | 核心底座 | 已启用 | 1.绿色 (确认无冲突) | Odyssey | ludeon.rimworld.odyssey | 0 | 0/0 | 前置约束：排在 Anomaly 之后（官方底座顺序）；前置约束：排在 Royalty 之后（About.loadBefore） |
| 8 | 8 | — | 系统与诊断 | 已启用 | 1.绿色 (确认无冲突) | RimBridgeServer | brrainz.rimbridgeserver | 90 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 9 | 9 | — | 系统与诊断 | 已启用 | 1.绿色 (确认无冲突) | RimAgentTestHarness | rimmod.rimagenttestharness | 0 | 0/0 | 前置约束：排在 RimBridgeServer 之后（About 强依赖） |
| 10 | 28 | ▲18 | 系统与诊断 | 已启用 | 1.绿色 (确认无冲突) | Dubs Performance Analyzer | dubwise.dubsperformanceanalyzer.steam | 0 | 5/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 11 | 11 | — | 前置框架 | 已启用 | 1.绿色 (确认无冲突) | Humanoid Alien Races | erdelf.humanoidalienraces | 0 | 100/5 | 前置约束：排在 Ideology 之后（条件兼容目录）；前置约束：排在 Odyssey 之后（条件兼容目录） |
| 12 | 14 | ▲2 | 前置框架 | 已启用 | 1.绿色 (确认无冲突) | Ancot Library | ancot.ancotlibrary | 25 | 143/4 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Biotech 之后（条件兼容目录） |
| 13 | 10 | ▼3 | 玩法与扩展 | 已启用 | 1.绿色 (确认无冲突) | The Second Seat | sanguo.thesecondseat | 4 | 93/0 | 前置约束：排在 Core 之后（About.loadAfter）；前置约束：排在 Royalty 之后（About.loadAfter） |
| 14 | 12 | ▼2 | 玩法与扩展 | 已启用 | 1.绿色 (确认无冲突) | Interaction Bubbles | jaxe.bubbles | 4 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 15 | 13 | ▼2 | 玩法与扩展 | 已启用 | 1.绿色 (确认无冲突) | RimTalk | cj.rimtalk | 31 | 9/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Interaction Bubbles 之后（About.loadAfter） |
| 16 | 15 | ▼1 | 玩法与扩展 | 已启用 | 1.绿色 (确认无冲突) | [NL] Facial Animation - WIP | nals.facialanimation | 0 | 73/3 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 17 | 35 | ▲18 | 玩法与扩展 | 已启用 | 1.绿色 (确认无冲突) | RimTalk - Expand Memory | cj.rimtalk.expandmemory | 19 | 1/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 RimTalk 之后（About 强依赖） |
| 18 | 36 | ▲18 | 玩法与扩展 | 已启用 | 1.绿色 (确认无冲突) | 厨间百艺 (Culinary Arts 100) | sanguo.culinaryarts100 | 5 | 152/4 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 19 | 17 | ▼2 | 渲染与动画 | 已启用 | 1.绿色 (确认无冲突) | [NL] Facial Animation - Experimentals | nals.facialanimationexperimentals | 0 | 93/3 | 前置约束：排在 [NL] Facial Animation - WIP 之后（About 强依赖） |
| 20 | 26 | ▲6 | 渲染与动画 | 已启用 | 1.绿色 (确认无冲突) | Pawn Universal Adjuster | sanguo.pawnuniversaladjuster | 6 | 1/0 | 前置约束：排在 Humanoid Alien Races 之后（动态反射修补） |
| 21 | 27 | ▲6 | 渲染与动画 | 已启用 | 2.黄色 (无法通过代码分析判断) | PUA Body Animation | lea.puabodyanimation | 11 | 4/0 | [2.黄色] NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 PUA Body Animation 的前置补丁先执行；前置约束：排在 Pawn Universal Adjuster 之后（About.loadAfter） |
| 22 | 31 | ▲9 | 渲染与动画 | 已启用 | 3.红色 (明确冲突) | Yayo's Animation (Continued) | com.yayo.yayoani.continued | 14 | 0/0 | [3.红色] NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上无条件返回 false 跳过原方法，会使 Yayo's Animation (Continued) 针对原方法体的补丁无法生效；前置约束：排在 Harmony 之后（About 强依赖） |
| 23 | 23 | — | 种族与阵营 | 已启用 | 2.黄色 (无法通过代码分析判断) | Nivarian Race | keeptpa.nivarianrace_steam | 193 | 1051/22 | [2.黄色] NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Nivarian Race 的前置补丁先执行；前置约束：排在 Humanoid Alien Races 之后（About 强依赖） |
| 24 | 24 | — | 种族与阵营 | 已启用 | 1.绿色 (确认无冲突) | Milira Race | ancot.milirarace | 35 | 1103/44 | 前置约束：排在 Humanoid Alien Races 之后（About 强依赖）；前置约束：排在 Ancot Library 之后（About 强依赖） |
| 25 | 18 | ▼7 | 种族与阵营 | 已启用 | 1.绿色 (确认无冲突) | Kiiro Race | ancot.kiirorace | 8 | 622/42 | 前置约束：排在 Humanoid Alien Races 之后（About 强依赖）；前置约束：排在 Ancot Library 之后（About 强依赖） |
| 26 | 33 | ▲7 | 种族与阵营 | 已启用 | 1.绿色 (确认无冲突) | Rim Girls' Frontline | rimgirlsfrontline.main | 51 | 1590/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 27 | 22 | ▼5 | 种族与阵营 | 已启用 | 3.红色 (明确冲突) | NewRatkinPlus | solaris.ratkinracemod_steam | 3 | 500/68 | [3.红色] NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上无条件返回 false 跳过原方法，会使 Yayo's Animation (Continued) 针对原方法体的补丁无法生效；[2.黄色] NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Nivarian Race 的前置补丁先执行 |
| 28 | 19 | ▼9 | 玩法与扩展 | 已启用 | 1.绿色 (确认无冲突) | Kiiro Race Gene Patch | ancot.kiiroracegenepatch | 0 | 14/6 | 前置约束：排在 Kiiro Race 之后（About 强依赖） |
| 29 | 20 | ▼9 | 种族与阵营 | 已启用 | 1.绿色 (确认无冲突) | Kiiro Story: Events Expanded | ancot.kiirostoryeventsexpanded | 8 | 327/6 | 前置约束：排在 Kiiro Race 之后（About 强依赖）；前置约束：排在 Ancot Library 之后（About 强依赖） |
| 30 | 21 | ▼9 | 玩法与扩展 | 已启用 | 1.绿色 (确认无冲突) | 绮罗蹭饭扩展 (Kiiro Bum Meals Off Expanded) | ricehaoci.kiirobummealsoffexpanded | 0 | 48/7 | 前置约束：排在 Kiiro Race 之后（About 强依赖）；前置约束：排在 Ancot Library 之后（About 强依赖） |
| 31 | 37 | ▲6 | 玩法与扩展 | 已启用 | 1.绿色 (确认无冲突) | 涅瓦莲：繁星之路 | sanguo.nivarianstarryroad | 3 | 87/1 | 前置约束：排在 Nivarian Race 之后（About.loadAfter）；前置约束：排在 Humanoid Alien Races 之后（About.loadAfter） |
| 32 | 25 | ▼7 | 渲染与动画 | 已启用 | 1.绿色 (确认无冲突) | Milira Race Facial Animation | ancot.miliraracefacialanimation | 0 | 64/3 | 前置约束：排在 Milira Race 之后（About 强依赖）；前置约束：排在 [NL] Facial Animation - WIP 之后（About 强依赖） |
| 33 | 34 | ▲1 | 渲染与动画 | 已启用 | 1.绿色 (确认无冲突) | Kiiro Race Facial Animation | ancot.kiirofacialanimation | 0 | 15/3 | 前置约束：排在 [NL] Facial Animation - WIP 之后（About 强依赖）；前置约束：排在 Kiiro Race 之后（About 强依赖） |
| 34 | 16 | ▼18 | 语言汉化包 | 已启用 | 1.绿色 (确认无冲突) | [NL] Facial Animation - WIP-zh | day29zh.facialanimation | 0 | 0/0 | 前置约束：排在 [NL] Facial Animation - WIP 之后（About.loadAfter） |
| 35 | 29 | ▼6 | 语言汉化包 | 已启用 | 1.绿色 (确认无冲突) | Dubs Performance Analyzer_zh | zh.dubwise.dubsperformanceanalyzer.steam | 0 | 0/0 | 前置约束：排在 Dubs Performance Analyzer 之后（About 强依赖） |
| 36 | 30 | ▼6 | 语言汉化包 | 已启用 | 1.绿色 (确认无冲突) | Interaction Bubbles_zh | jaxe.bubbles.zh.hc | 0 | 0/0 | 前置约束：排在 Interaction Bubbles 之后（About.loadAfter） |
| 37 | 32 | ▼5 | 语言汉化包 | 已启用 | 1.绿色 (确认无冲突) | Yayo's Animation_zh | zh.com.yayo.yayoani | 0 | 0/0 | 前置约束：排在 Yayo's Animation (Continued) 之后（About.loadAfter） |

## 二、3.红色（明确冲突）明细（共 1 项）

### [HarmonyBlockingPrefixConflict] NewRatkinPlus ↔ Yayo's Animation (Continued)
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上无条件返回 false 跳过原方法，会使 Yayo's Animation (Continued) 针对原方法体的补丁无法生效
- 处置建议: 无条件截断 Prefix 会跳过原方法体，导致针对原方法 IL 的 Transpiler 失效
  - 证据: NewRatkinPlus: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Yayo's Animation (Continued): [Prefix] YayoAnimation.HarmonyPatches.PawnPatches.AimingPatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Yayo's Animation (Continued): [Finalizer] YayoAnimation.HarmonyPatches.PawnPatches.AimingPatch.Finalizer (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Yayo's Animation (Continued): [Transpiler] YayoAnimation.HarmonyPatches.PawnPatches.AimingPatch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

## 三、2.黄色（无法通过代码分析完全判断）明细（共 2 项）

### [ConditionalPrefixOrResultMutationOverlap] NewRatkinPlus ↔ Nivarian Race
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Nivarian Race 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Nivarian Race 排在截断型 Prefix 模组 NewRatkinPlus 之前，确保双方前置逻辑依次执行
  - 证据: NewRatkinPlus: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Nivarian Race: [Prefix] Nivarian_Race.Code.Patches.Patch_BathHideApparelExtras.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] NewRatkinPlus ↔ PUA Body Animation
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 PUA Body Animation 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 PUA Body Animation 排在截断型 Prefix 模组 NewRatkinPlus 之前，确保双方前置逻辑依次执行
  - 证据: NewRatkinPlus: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: PUA Body Animation: [Prefix] PUABodyAnimation.PUAAnimRenderPatch.DrawEquipmentAndApparelExtrasPrefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: PUA Body Animation: [Finalizer] PUABodyAnimation.PUAAnimRenderPatch.DrawEquipmentAndApparelExtrasFinalizer (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)

## 四、1.绿色（共同作用于同一目标但确认无冲突）明细（共 149 项）

- [AdditiveXmlPatchOverlap] [NL] Facial Animation - WIP + [NL] Facial Animation - Experimentals 于 /Defs/ThingDef[defName="Human"]: [NL] Facial Animation - WIP 与 [NL] Facial Animation - Experimentals 存在明确上下游依赖，按顺序对同一 XPath 节点执行扩展更新
- [AdditiveXmlPatchOverlap] [NL] Facial Animation - WIP + [NL] Facial Animation - Experimentals 于 /Defs/ThingDef[defName="Human"]/comps: [NL] Facial Animation - WIP 与 [NL] Facial Animation - Experimentals 存在明确上下游依赖，按顺序对同一 XPath 节点执行扩展更新
- [AdditiveXmlPatchOverlap] Kiiro Race + Kiiro Story: Events Expanded 于 Defs/AlienRace.ThingDef_AlienRace[defName="Kiiro_Race"]/alienRace/raceRestriction/whiteApparelList: Kiiro Race 与 Kiiro Story: Events Expanded 存在明确上下游依赖，按顺序对同一 XPath 节点执行扩展更新
- [AdditiveXmlPatchOverlap] Kiiro Race + Kiiro Race Gene Patch 于 Defs/AlienRace.ThingDef_AlienRace[defName="Kiiro_Race"]/statBases: Kiiro Race 与 Kiiro Race Gene Patch 存在明确上下游依赖，按顺序对同一 XPath 节点执行扩展更新
- [AdditiveXmlPatchOverlap] NewRatkinPlus + Nivarian Race 于 Defs/FactionDef[defName="Empire"]/permanentEnemyToEveryoneExcept: NewRatkinPlus 与 Nivarian Race 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Kiiro Race + Milira Race 于 Defs/ResearchProjectDef[defName="ComplexClothing"]/tags: Kiiro Race 与 Milira Race 存在明确上下游依赖，按顺序对同一 XPath 节点执行扩展更新
- [AdditiveXmlPatchOverlap] Kiiro Race + Milira Race 于 Defs/ResearchProjectDef[defName="ComplexFurniture"]/tags: Kiiro Race 与 Milira Race 存在明确上下游依赖，按顺序对同一 XPath 节点执行扩展更新
- [AdditiveXmlPatchOverlap] Kiiro Race + Milira Race 于 Defs/ResearchProjectDef[defName="PassiveCooler"]/tags: Kiiro Race 与 Milira Race 存在明确上下游依赖，按顺序对同一 XPath 节点执行扩展更新
- [AdditiveXmlPatchOverlap] Kiiro Race + Milira Race 于 Defs/ResearchProjectDef[defName="Smithing"]/tags: Kiiro Race 与 Milira Race 存在明确上下游依赖，按顺序对同一 XPath 节点执行扩展更新
- [AdditiveXmlPatchOverlap] Kiiro Race + Milira Race 于 Defs/ResearchProjectDef[defName="Stonecutting"]/tags: Kiiro Race 与 Milira Race 存在明确上下游依赖，按顺序对同一 XPath 节点执行扩展更新
- [AdditiveXmlPatchOverlap] Kiiro Story: Events Expanded + Milira Race 于 Defs/StorytellerDef[@Name="BaseStoryteller"]/comps: Kiiro Story: Events Expanded 与 Milira Race 存在明确上下游依赖，按顺序对同一 XPath 节点执行扩展更新
- [AdditiveXmlPatchOverlap] Kiiro Race + NewRatkinPlus 于 Defs/WorkGiverDef[defName="DoBillsMakeApparel"]/fixedBillGiverDefs: Kiiro Race 与 NewRatkinPlus 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Kiiro Race + Milira Race 于 Defs/WorkGiverDef[defName="DoBillsMakeApparel"]/fixedBillGiverDefs: Kiiro Race 与 Milira Race 存在明确上下游依赖，按顺序对同一 XPath 节点执行扩展更新
- [AdditiveXmlPatchOverlap] NewRatkinPlus + Milira Race 于 Defs/WorkGiverDef[defName="DoBillsMakeApparel"]/fixedBillGiverDefs: NewRatkinPlus 与 Milira Race 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Kiiro Race + NewRatkinPlus 于 Defs/WorkGiverDef[defName="DoBillsMakeWeapons"]/fixedBillGiverDefs: Kiiro Race 与 NewRatkinPlus 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Ancot Library + Kiiro Story: Events Expanded 于 Defs/WorldObjectDef[defName="Settlement"]/comps: Ancot Library 与 Kiiro Story: Events Expanded 存在明确上下游依赖，按顺序对同一 XPath 节点执行扩展更新
- [CompatibleHarmonyOverlap] Kiiro Race + Nivarian Race 于 AlienRace.RaceRestrictionSettings.CanWear: Kiiro Race 与 Nivarian Race 共同扩展 AlienRace.RaceRestrictionSettings.CanWear，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Kiiro Race + Milira Race 于 AlienRace.RaceRestrictionSettings.CanWear: Kiiro Race 与 Milira Race 共同扩展 AlienRace.RaceRestrictionSettings.CanWear，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Nivarian Race + Milira Race 于 AlienRace.RaceRestrictionSettings.CanWear: Nivarian Race 与 Milira Race 共同扩展 AlienRace.RaceRestrictionSettings.CanWear，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Ancot Library + Rim Girls' Frontline 于 MechanitorUtility.CanDraftMech: Ancot Library 与 Rim Girls' Frontline 共同扩展 MechanitorUtility.CanDraftMech，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Ancot Library + Milira Race 于 MechanitorUtility.InMechanitorCommandRange: Ancot Library 与 Milira Race 共同扩展 MechanitorUtility.InMechanitorCommandRange，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Ancot Library + Rim Girls' Frontline 于 MechanitorUtility.InMechanitorCommandRange: Ancot Library 与 Rim Girls' Frontline 共同扩展 MechanitorUtility.InMechanitorCommandRange，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Milira Race + Rim Girls' Frontline 于 MechanitorUtility.InMechanitorCommandRange: Milira Race 与 Rim Girls' Frontline 共同扩展 MechanitorUtility.InMechanitorCommandRange，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] 涅瓦莲：繁星之路 + 涅瓦莲：繁星之路 于 NivarianStarryRoad.NivarianDialogueOverlayPatch.Postfix: 涅瓦莲：繁星之路 通过动态反射桥接前置模组方法（Postfix），对应前置模组已启用并排在前面
- [CompatibleHarmonyOverlap] 涅瓦莲：繁星之路 + 涅瓦莲：繁星之路 于 NivarianStarryRoad.NivarianEndingCUIPatch.Prefix: 涅瓦莲：繁星之路 通过动态反射桥接前置模组方法（Prefix），对应前置模组已启用并排在前面
- [CompatibleHarmonyOverlap] 涅瓦莲：繁星之路 + 涅瓦莲：繁星之路 于 NivarianStarryRoad.NivarianStarryRoadTopLayerPatch.Postfix: 涅瓦莲：繁星之路 通过动态反射桥接前置模组方法（Postfix），对应前置模组已启用并排在前面
- [CompatibleHarmonyOverlap] Nivarian Race + Nivarian Race 于 Nivarian_Race.Code.Features.PawnRendering.Patches.Patch_SelectedPawnPortraitOverlay_PortraitRenderCache.Prefix: Nivarian Race 通过动态反射桥接前置模组方法（Prefix），对应前置模组已启用并排在前面
- [CompatibleHarmonyOverlap] Nivarian Race + Nivarian Race 于 Nivarian_Race.Code.Features.SelfBuilding.Patches.Patch_Blueprints_BuildableInfo_FromThing.Postfix: Nivarian Race 通过动态反射桥接前置模组方法（Postfix），对应前置模组已启用并排在前面
- [CompatibleHarmonyOverlap] Nivarian Race + Nivarian Race 于 Nivarian_Race.Code.Features.SelfBuilding.Patches.Patch_Blueprints_IsValidBlueprintThing.Postfix: Nivarian Race 通过动态反射桥接前置模组方法（Postfix），对应前置模组已启用并排在前面
- [CompatibleHarmonyOverlap] Nivarian Race + Nivarian Race 于 Nivarian_Race.Code.Features.UplinkResearch.Patches.Patch_YART_ResearchGraph_CreateNodes.Postfix: Nivarian Race 通过动态反射桥接前置模组方法（Postfix），对应前置模组已启用并排在前面
- [CompatibleHarmonyOverlap] Nivarian Race + Nivarian Race 于 Nivarian_Race.Code.Patches.Patch_NivarianNpcApparelFallback.Postfix: Nivarian Race 通过动态反射桥接前置模组方法（Postfix），对应前置模组已启用并排在前面
- [CompatibleHarmonyOverlap] Nivarian Race + Nivarian Race 于 Nivarian_Race.Code.Patches.Patch_RJW_GiveBirth_DragonEgg.Postfix: Nivarian Race 通过动态反射桥接前置模组方法（Postfix），对应前置模组已启用并排在前面
- [CompatibleHarmonyOverlap] Nivarian Race + Nivarian Race 于 Nivarian_Race.Code.SaveCompatibility.Patches.Patch_FA_ExposableString_Normal2Remap.Postfix: Nivarian Race 通过动态反射桥接前置模组方法（Postfix），对应前置模组已启用并排在前面
- [CompatibleHarmonyOverlap] RimTalk - Expand Memory + RimTalk - Expand Memory 于 RimTalk.Memory.Patches.Patch_GenerateAndProcessTalkAsync.Prefix: RimTalk - Expand Memory 通过动态反射桥接前置模组方法（Prefix），对应前置模组已启用并排在前面
- [CompatibleHarmonyOverlap] Nivarian Race + Milira Race 于 RimWorld.Designator_Deconstruct.CanDesignateThing: Nivarian Race 与 Milira Race 共同扩展 RimWorld.Designator_Deconstruct.CanDesignateThing，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Kiiro Race + Kiiro Story: Events Expanded 于 RimWorld.Faction.Notify_RelationKindChanged: Kiiro Race 与 Kiiro Story: Events Expanded 共同扩展 RimWorld.Faction.Notify_RelationKindChanged，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Kiiro Story: Events Expanded + Milira Race 于 RimWorld.FactionDialogMaker.FactionDialogFor: Kiiro Story: Events Expanded 与 Milira Race 共同扩展 RimWorld.FactionDialogMaker.FactionDialogFor，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] The Second Seat + RimTalk - Expand Memory 于 RimWorld.IncidentWorker.TryExecute: The Second Seat 与 RimTalk - Expand Memory 共同扩展 RimWorld.IncidentWorker.TryExecute，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Nivarian Race + Milira Race 于 RimWorld.IncidentWorker_Raid.AdjustedRaidPoints: Nivarian Race 与 Milira Race 共同扩展 RimWorld.IncidentWorker_Raid.AdjustedRaidPoints，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Ancot Library + Milira Race 于 RimWorld.MainTabWindow_Mechs.DoWindowContents: Ancot Library 与 Milira Race 共同扩展 RimWorld.MainTabWindow_Mechs.DoWindowContents，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Interaction Bubbles + RimTalk 于 RimWorld.MapInterface.MapInterfaceOnGUI_BeforeMainTabs: Interaction Bubbles 与 RimTalk 共同扩展 RimWorld.MapInterface.MapInterfaceOnGUI_BeforeMainTabs，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Ancot Library + Rim Girls' Frontline 于 RimWorld.PawnComponentsUtility.AddAndRemoveDynamicComponents: Ancot Library 与 Rim Girls' Frontline 共同扩展 RimWorld.PawnComponentsUtility.AddAndRemoveDynamicComponents，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Kiiro Race + Nivarian Race 于 RimWorld.Planet.SettlementProximityGoodwillUtility.AppendProximityGoodwillOffsets: Kiiro Race 与 Nivarian Race 共同扩展 RimWorld.Planet.SettlementProximityGoodwillUtility.AppendProximityGoodwillOffsets，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Ancot Library + Nivarian Race 于 RimWorld.Planet.TravellingTransporters.TraveledPctStepPerTick[Getter]: Ancot Library 与 Nivarian Race 共同扩展 RimWorld.Planet.TravellingTransporters.TraveledPctStepPerTick[Getter]，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Interaction Bubbles + RimTalk 于 RimWorld.PlaySettings.DoPlaySettingsGlobalControls: Interaction Bubbles 与 RimTalk 共同扩展 RimWorld.PlaySettings.DoPlaySettingsGlobalControls，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Interaction Bubbles + 厨间百艺 (Culinary Arts 100) 于 RimWorld.PlaySettings.DoPlaySettingsGlobalControls: Interaction Bubbles 与 厨间百艺 (Culinary Arts 100) 共同扩展 RimWorld.PlaySettings.DoPlaySettingsGlobalControls，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] RimTalk + 厨间百艺 (Culinary Arts 100) 于 RimWorld.PlaySettings.DoPlaySettingsGlobalControls: RimTalk 与 厨间百艺 (Culinary Arts 100) 共同扩展 RimWorld.PlaySettings.DoPlaySettingsGlobalControls，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Ancot Library + Rim Girls' Frontline 于 RimWorld.QualityUtility.GenerateQualityCreatedByPawn: Ancot Library 与 Rim Girls' Frontline 共同扩展 RimWorld.QualityUtility.GenerateQualityCreatedByPawn，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] The Second Seat + Nivarian Race 于 RimWorld.StorytellerComp.IncidentChanceFinal: The Second Seat 与 Nivarian Race 共同扩展 RimWorld.StorytellerComp.IncidentChanceFinal，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] RimBridgeServer + RimTalk 于 RimWorld.UIRoot_Play.UIRootOnGUI: RimBridgeServer 与 RimTalk 共同扩展 RimWorld.UIRoot_Play.UIRootOnGUI，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] RimBridgeServer + Rim Girls' Frontline 于 RimWorld.UIRoot_Play.UIRootOnGUI: RimBridgeServer 与 Rim Girls' Frontline 共同扩展 RimWorld.UIRoot_Play.UIRootOnGUI，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] RimTalk + Rim Girls' Frontline 于 RimWorld.UIRoot_Play.UIRootOnGUI: RimTalk 与 Rim Girls' Frontline 共同扩展 RimWorld.UIRoot_Play.UIRootOnGUI，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Kiiro Race + Milira Race 于 Verse.AI.JobDriver_Wait.CheckForAutoAttack: Kiiro Race 与 Milira Race 共同扩展 Verse.AI.JobDriver_Wait.CheckForAutoAttack，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Kiiro Race + Rim Girls' Frontline 于 Verse.AI.JobDriver_Wait.CheckForAutoAttack: Kiiro Race 与 Rim Girls' Frontline 共同扩展 Verse.AI.JobDriver_Wait.CheckForAutoAttack，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Milira Race + Rim Girls' Frontline 于 Verse.AI.JobDriver_Wait.CheckForAutoAttack: Milira Race 与 Rim Girls' Frontline 共同扩展 Verse.AI.JobDriver_Wait.CheckForAutoAttack，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] RimTalk + RimTalk - Expand Memory 于 Verse.AI.Pawn_JobTracker.StartJob: RimTalk 与 RimTalk - Expand Memory 共同扩展 Verse.AI.Pawn_JobTracker.StartJob，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Nivarian Race + Rim Girls' Frontline 于 Verse.Game.FinalizeInit: Nivarian Race 与 Rim Girls' Frontline 共同扩展 Verse.Game.FinalizeInit，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] RimBridgeServer + Rim Girls' Frontline 于 Verse.Game.InitNewGame: RimBridgeServer 与 Rim Girls' Frontline 共同扩展 Verse.Game.InitNewGame，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] RimTalk + Ancot Library 于 Verse.Pawn.GetGizmos: RimTalk 与 Ancot Library 共同扩展 Verse.Pawn.GetGizmos，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] RimTalk + Nivarian Race 于 Verse.Pawn.GetGizmos: RimTalk 与 Nivarian Race 共同扩展 Verse.Pawn.GetGizmos，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] RimTalk + PUA Body Animation 于 Verse.Pawn.GetGizmos: RimTalk 与 PUA Body Animation 共同扩展 Verse.Pawn.GetGizmos，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] RimTalk + Rim Girls' Frontline 于 Verse.Pawn.GetGizmos: RimTalk 与 Rim Girls' Frontline 共同扩展 Verse.Pawn.GetGizmos，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Ancot Library + Nivarian Race 于 Verse.Pawn.GetGizmos: Ancot Library 与 Nivarian Race 共同扩展 Verse.Pawn.GetGizmos，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Ancot Library + PUA Body Animation 于 Verse.Pawn.GetGizmos: Ancot Library 与 PUA Body Animation 共同扩展 Verse.Pawn.GetGizmos，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Ancot Library + Rim Girls' Frontline 于 Verse.Pawn.GetGizmos: Ancot Library 与 Rim Girls' Frontline 共同扩展 Verse.Pawn.GetGizmos，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Nivarian Race + PUA Body Animation 于 Verse.Pawn.GetGizmos: Nivarian Race 与 PUA Body Animation 共同扩展 Verse.Pawn.GetGizmos，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Nivarian Race + Rim Girls' Frontline 于 Verse.Pawn.GetGizmos: Nivarian Race 与 Rim Girls' Frontline 共同扩展 Verse.Pawn.GetGizmos，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] PUA Body Animation + Rim Girls' Frontline 于 Verse.Pawn.GetGizmos: PUA Body Animation 与 Rim Girls' Frontline 共同扩展 Verse.Pawn.GetGizmos，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] RimTalk + Nivarian Race 于 Verse.Pawn.Kill: RimTalk 与 Nivarian Race 共同扩展 Verse.Pawn.Kill，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] RimTalk + Rim Girls' Frontline 于 Verse.Pawn.Kill: RimTalk 与 Rim Girls' Frontline 共同扩展 Verse.Pawn.Kill，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Nivarian Race + Rim Girls' Frontline 于 Verse.Pawn.Kill: Nivarian Race 与 Rim Girls' Frontline 共同扩展 Verse.Pawn.Kill，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Nivarian Race + Rim Girls' Frontline 于 Verse.Pawn.SpawnSetup: Nivarian Race 与 Rim Girls' Frontline 共同扩展 Verse.Pawn.SpawnSetup，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Nivarian Race + Milira Race 于 Verse.PawnGenerator.GeneratePawn: Nivarian Race 与 Milira Race 共同扩展 Verse.PawnGenerator.GeneratePawn，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Nivarian Race + Yayo's Animation (Continued) 于 Verse.PawnRenderUtility.DrawCarriedWeapon: Nivarian Race 与 Yayo's Animation (Continued) 共同扩展 Verse.PawnRenderUtility.DrawCarriedWeapon，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Nivarian Race + Pawn Universal Adjuster 于 Verse.PawnRenderUtility.DrawEquipmentAiming: Nivarian Race 与 Pawn Universal Adjuster 共同扩展 Verse.PawnRenderUtility.DrawEquipmentAiming，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Nivarian Race + PUA Body Animation 于 Verse.PawnRenderUtility.DrawEquipmentAiming: Nivarian Race 与 PUA Body Animation 共同扩展 Verse.PawnRenderUtility.DrawEquipmentAiming，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Pawn Universal Adjuster + PUA Body Animation 于 Verse.PawnRenderUtility.DrawEquipmentAiming: Pawn Universal Adjuster 与 PUA Body Animation 共同扩展 Verse.PawnRenderUtility.DrawEquipmentAiming，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Nivarian Race + PUA Body Animation 于 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras: Nivarian Race 与 PUA Body Animation 共同扩展 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] Nivarian Race + Yayo's Animation (Continued) 于 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras: Nivarian Race 与 Yayo's Animation (Continued) 共同扩展 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras，均带有条件守卫或增量计算，作用域正交
- [CompatibleHarmonyOverlap] PUA Body Animation + Yayo's Animation (Continued) 于 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras: PUA Body Animation 与 Yayo's Animation (Continued) 共同扩展 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras，均带有条件守卫或增量计算，作用域正交
- …（另有 69 项绿色重叠记录）


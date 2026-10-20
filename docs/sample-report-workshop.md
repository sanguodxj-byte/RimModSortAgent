# RimWorld 模组自动排序与代码冲突分级报告

- 生成时间: 2026-10-20 17:18:06
- 目标游戏版本: 1.7.4981 rev136
- 总体评级: 3.红色 (明确冲突)
- 排序集合: 692 项（已启用 37 + 工坊订阅池 655）
- 工坊订阅: 680 项，其中 2 项未下载完成
- 全量定级: 1.绿色 606，2.黄色 41，3.红色 45（全部模组同一分析口径）
- 顺位调整模组数: 35 / 37（订阅池无原始顺位，不计入）
- 扫描统计: Harmony 补丁 5476 处（其中动态反射点 337 处），XML Def 50394 项，XML Patch 4404 项
- 分析耗时: 34.3 秒
- 实机状态: 静态代码与离线日志核验

## 架构分层分布

- 核心底座 11
- 系统与诊断 15
- 前置框架 41
- 种族与阵营 72
- 玩法与扩展 341
- 渲染与动画 53
- 纯 XML 补丁 24
- 语言汉化包 135

## 工坊订阅检测

- 订阅清单: D:\Steam\steamapps\workshop\appworkshop_294100.acf
- 订阅条目: 680 项
- 并入排序: 655 项未启用模组（与启用列表同口径：XML Def 扫描 + 程序集反编译 + Harmony 特征提取）
- 未下载完成: 2 项（未纳入排序）
- 提示: 2 项已订阅工坊模组尚未完整下载，本次不参与排序。

订阅池分层分布：核心底座 4，系统与诊断 12，前置框架 30，种族与阵营 69，玩法与扩展 336，渲染与动画 49，纯 XML 补丁 24，语言汉化包 131

## 缺失前置依赖（3.红色）
- 模组 kemomimihouse Kz (moo.kemomimihouse.kz) 缺少必需前置依赖: kemomimihouse (psyche.kemomimihouse)
- 模组 Moyo Facial Animation Patch (aw.moyo.fac) 缺少必需前置依赖: Moyo2.0-Blood stained business (nemonian.my2)
- 模组 [1.7]边缘仙路-神机天纵 RimImmortal-Forge (ri.rimimmortal.forge) 缺少必需前置依赖: 边缘仙路-羽化登仙 RimImmortal-Core[1.6] (ri.rimimmortal.core)
- 模组 LEVIATHANS:SANDWORM (chezhou.creature.sandworm) 缺少必需前置依赖: RimUnity (chezhou.rimunity)
- 模组 Raven Race - Embers in Shadow (zuoyao.ravenrace) 缺少必需前置依赖: RimUnity (chezhou.rimunity)

## 硬性排序环路告警（3.红色）
- 检测到硬性排序循环依赖: Layered Wall Destruction (keshash.layeredwalldestruction) → Layered Wall Destruction - Additional Patches (seromas.layereddestruction.patches)，已断开该边以保证排序可解（Layered Wall Destruction 声明 loadBefore Layered Wall Destruction - Additional Patches；Layered Wall Destruction - Additional Patches 强依赖前置 Layered Wall Destruction；Layered Wall Destruction - Additional Patches 声明 loadAfter Layered Wall Destruction）

## 一、推荐加载顺序（含三级定级）

> 顺位是「已启用 + 工坊订阅池」统一求解后的绝对位置：若要写入 ModsConfig.xml，未启用项会被自动跳过，只按其中「已启用」的行重排。

| 推荐顺位 | 原顺位 | 变动 | 架构分层 | 状态 | 分级 | 模组名称 | PackageId | Harmony | Def/Patch | 关键依据 |
|---:|---:|:--:|---|---|---|---|---|---:|---:|---|
| 1 | 1 | — | 核心底座 | 已启用 | 1.绿色 (确认无冲突) | Harmony | brrainz.harmony | 3 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 2 | — | 新增 | 核心底座 | 未启用 | 3.红色 (明确冲突) | Mod Options Sort | superniquito.modoptionssort | 2 | 0/0 | [3.红色] Mod Options Sort 在 RimWorld.Dialog_Options.DoModOptions 上无条件返回 false 跳过原方法，会使 HugsLib 针对原方法体的补丁无法生效；[2.黄色] Mod Options Sort 在 RimWorld.Dialog_Options.DoModOptions 上包含返回 false 的接管型 Prefix，需确保 RimWorld Together 的前置补丁先执行 |
| 3 | — | 新增 | 核心底座 | 未启用 | 1.绿色 (确认无冲突) | Prepatcher | zetrith.prepatcher | 1 | 0/0 | 前置约束：排在 Mod Options Sort 之后（官方底座顺序） |
| 4 | — | 新增 | 核心底座 | 未启用 | 1.绿色 (确认无冲突) | Loading Progress | ilyvion.loadingprogress | 138 | 0/0 | 前置约束：排在 Prepatcher 之后（官方底座顺序）；前置约束：排在 Harmony 之后（About 强依赖） |
| 5 | — | 新增 | 核心底座 | 未启用 | 1.绿色 (确认无冲突) | Better Stacktraces | aleksey.betterstacktraces | 4 | 0/0 | 前置约束：排在 Loading Progress 之后（官方底座顺序）；前置约束：排在 Harmony 之后（About 强依赖） |
| 6 | 2 | ▼4 | 核心底座 | 已启用 | 1.绿色 (确认无冲突) | Core | ludeon.rimworld | 0 | 0/0 | 前置约束：排在 Better Stacktraces 之后（官方底座顺序）；前置约束：排在 Harmony 之后（About.loadBefore） |
| 7 | 3 | ▼4 | 核心底座 | 已启用 | 1.绿色 (确认无冲突) | Royalty | ludeon.rimworld.royalty | 0 | 0/0 | 前置约束：排在 Core 之后（官方底座顺序） |
| 8 | 4 | ▼4 | 核心底座 | 已启用 | 1.绿色 (确认无冲突) | Ideology | ludeon.rimworld.ideology | 0 | 0/0 | 前置约束：排在 Royalty 之后（官方底座顺序）；前置约束：排在 Core 之后（About.loadBefore） |
| 9 | 5 | ▼4 | 核心底座 | 已启用 | 1.绿色 (确认无冲突) | Biotech | ludeon.rimworld.biotech | 0 | 0/0 | 前置约束：排在 Ideology 之后（官方底座顺序）；前置约束：排在 Royalty 之后（About.loadBefore） |
| 10 | 6 | ▼4 | 核心底座 | 已启用 | 1.绿色 (确认无冲突) | Anomaly | ludeon.rimworld.anomaly | 0 | 0/0 | 前置约束：排在 Biotech 之后（官方底座顺序）；前置约束：排在 Royalty 之后（About.loadBefore） |
| 11 | 7 | ▼4 | 核心底座 | 已启用 | 1.绿色 (确认无冲突) | Odyssey | ludeon.rimworld.odyssey | 0 | 0/0 | 前置约束：排在 Anomaly 之后（官方底座顺序）；前置约束：排在 Royalty 之后（About.loadBefore） |
| 12 | — | 新增 | 前置框架 | 未启用 | 3.红色 (明确冲突) | Gunplay | automatic.gunplay | 4 | 47/0 | [3.红色] Mechanoids: Total Warfare 在 About.xml 中显式声明与 Gunplay 不兼容 |
| 13 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Music Manager (Continued) | zal.musicmanager | 8 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 14 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | Image Opt | dev.soeur.imageopt | 3 | 0/0 | [3.红色] Graphics Settings+ 在 Verse.ModContentLoader.LoadTexture 上无条件返回 false 跳过原方法，会使 Image Opt 针对原方法体的补丁无法生效；前置约束：排在 Harmony 之后（About 强依赖） |
| 15 | — | 新增 | 渲染与动画 | 未启用 | 3.红色 (明确冲突) | Graphics Settings+ | telefonmast.graphicssettings | 3 | 0/0 | [3.红色] Graphics Settings+ 在 Verse.ModContentLoader.LoadTexture 上无条件返回 false 跳过原方法，会使 Image Opt 针对原方法体的补丁无法生效；[2.黄色] Graphics Settings+ 与 HugsLib 同时对 RimWorld.MainMenuDrawer.DoMainMenuControls 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响 |
| 16 | — | 新增 | 前置框架 | 未启用 | 3.红色 (明确冲突) | HugsLib | unlimitedhugs.hugslib | 17 | 6/0 | [3.红色] Mod Options Sort 在 RimWorld.Dialog_Options.DoModOptions 上无条件返回 false 跳过原方法，会使 HugsLib 针对原方法体的补丁无法生效；[2.黄色] Graphics Settings+ 与 HugsLib 同时对 RimWorld.MainMenuDrawer.DoMainMenuControls 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响 |
| 17 | 31 | ▲14 | 前置框架 | 已启用 | 3.红色 (明确冲突) | Yayo's Animation (Continued) | com.yayo.yayoani.continued | 14 | 0/0 | [3.红色] NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上无条件返回 false 跳过原方法，会使 Yayo's Animation (Continued) 针对原方法体的补丁无法生效；[3.红色] Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上无条件返回 false 跳过原方法，会使 Yayo's Animation (Continued) 针对原方法体的补丁无法生效 |
| 18 | — | 新增 | 系统与诊断 | 未启用 | 1.绿色 (确认无冲突) | OgreStack | ogre.ogrestack | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 19 | — | 新增 | 系统与诊断 | 未启用 | 1.绿色 (确认无冲突) | [Ling]Move Steam Geyser | lingluo.movesteamgeyser | 0 | 0/2 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 20 | — | 新增 | 系统与诊断 | 未启用 | 1.绿色 (确认无冲突) | Auto links | automatic.autolinks | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 21 | — | 新增 | 系统与诊断 | 未启用 | 1.绿色 (确认无冲突) | FSharp.Core | latta.fsharp.core | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 22 | — | 新增 | 系统与诊断 | 未启用 | 1.绿色 (确认无冲突) | Smarter Construction | dhultgren.smarterconstruction | 5 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 23 | — | 新增 | 系统与诊断 | 未启用 | 1.绿色 (确认无冲突) | Map Preview | m00nl1ght.mappreview | 0 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 HugsLib 之后（About 强依赖） |
| 24 | — | 新增 | 系统与诊断 | 未启用 | 1.绿色 (确认无冲突) | Show mechanoid weapons | issaczhuang.showmechanoidweapon | 0 | 0/0 | 前置约束：排在 Core 之后（About.loadAfter）；前置约束：排在 Biotech 之后（About.loadAfter） |
| 25 | — | 新增 | 系统与诊断 | 未启用 | 3.红色 (明确冲突) | Pick Up at Home (Fork) | rw.mod.pickupathome | 0 | 0/0 | [3.红色] Pick Up at Home (Fork) 在 About.xml 中显式声明与 Pick Up And Haul 不兼容；前置约束：排在 Core 之后（About.loadAfter） |
| 26 | 8 | ▼18 | 系统与诊断 | 已启用 | 1.绿色 (确认无冲突) | RimBridgeServer | brrainz.rimbridgeserver | 90 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 27 | 9 | ▼18 | 系统与诊断 | 已启用 | 1.绿色 (确认无冲突) | RimAgentTestHarness | rimmod.rimagenttestharness | 0 | 0/0 | 前置约束：排在 RimBridgeServer 之后（About 强依赖） |
| 28 | 28 | — | 系统与诊断 | 已启用 | 3.红色 (明确冲突) | Dubs Performance Analyzer | dubwise.dubsperformanceanalyzer.steam | 0 | 5/0 | [3.红色] Butter++ 在 About.xml 中显式声明与 Dubs Performance Analyzer 不兼容；前置约束：排在 HugsLib 之后（动态反射修补） |
| 29 | — | 新增 | 系统与诊断 | 未启用 | 2.黄色 (无法通过代码分析判断) | Visible Raid Points | visibleraidpoints.1trickpwnyta | 50 | 0/0 | [2.黄色] Visible Raid Points 与 Mechanoids: Total Warfare 同时对 RimWorld.StorytellerUtility.DefaultThreatPointsNow 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；前置约束：排在 Harmony 之后（About 强依赖） |
| 30 | 12 | ▼18 | 前置框架 | 已启用 | 1.绿色 (确认无冲突) | Interaction Bubbles | jaxe.bubbles | 4 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 31 | 14 | ▼17 | 前置框架 | 已启用 | 1.绿色 (确认无冲突) | Ancot Library | ancot.ancotlibrary | 25 | 143/4 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Biotech 之后（条件兼容目录） |
| 32 | 15 | ▼17 | 前置框架 | 已启用 | 1.绿色 (确认无冲突) | [NL] Facial Animation - WIP | nals.facialanimation | 0 | 73/3 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 33 | 17 | ▼16 | 前置框架 | 已启用 | 1.绿色 (确认无冲突) | [NL] Facial Animation - Experimentals | nals.facialanimationexperimentals | 0 | 93/3 | 前置约束：排在 [NL] Facial Animation - WIP 之后（About 强依赖） |
| 34 | — | 新增 | 前置框架 | 未启用 | 1.绿色 (确认无冲突) | Dubs Rimatomics | dubwise.rimatomics | 19 | 400/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 35 | — | 新增 | 前置框架 | 未启用 | 1.绿色 (确认无冲突) | Architect Icons | com.bymarcin.architecticons | 0 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 36 | — | 新增 | 前置框架 | 未启用 | 1.绿色 (确认无冲突) | Character Editor | void.charactereditor | 0 | 41/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 HugsLib 之后（About.loadAfter） |
| 37 | 13 | ▼24 | 前置框架 | 已启用 | 2.黄色 (无法通过代码分析判断) | RimTalk | cj.rimtalk | 31 | 9/0 | [2.黄色] RimWorld Together 在 Verse.GameDataSaveLoader.SaveGame 上包含返回 false 的接管型 Prefix，需确保 RimTalk 的前置补丁先执行；[2.黄色] RimTalk 与 MoeLotl: Rigor Mortis 同时对 RimWorld.CharacterCardUtility.DoTopStack 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响 |
| 38 | — | 新增 | 系统与诊断 | 未启用 | 1.绿色 (确认无冲突) | RimTalk Event+ | saltgin.rimtalkeventmemory | 16 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 RimTalk 之后（About 强依赖） |
| 39 | — | 新增 | 前置框架 | 未启用 | 1.绿色 (确认无冲突) | What's That Mod | co.uk.epicguru.whatsthatmod | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 40 | — | 新增 | 前置框架 | 未启用 | 1.绿色 (确认无冲突) | XML Extensions | imranfish.xmlextensions | 14 | 1/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 41 | 11 | ▼30 | 前置框架 | 已启用 | 3.红色 (明确冲突) | Humanoid Alien Races | erdelf.humanoidalienraces | 0 | 100/5 | [3.红色] Humanoid Alien Races 与 Humanoid Alien Races ~ Dev 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突；前置约束：排在 XML Extensions 之后（About.loadAfter） |
| 42 | — | 新增 | 前置框架 | 未启用 | 1.绿色 (确认无冲突) | Muzzle Flash | issaczhuang.muzzleflash | 2 | 5/1 | 前置约束：排在 Core 之后（About.loadAfter） |
| 43 | — | 新增 | 前置框架 | 未启用 | 1.绿色 (确认无冲突) | Engine Industries | aoba.motorization.engine | 0 | 38/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 44 | — | 新增 | 前置框架 | 未启用 | 1.绿色 (确认无冲突) | MoeLotl Race | hentailoliteam.axolotl | 22 | 739/37 | 前置约束：排在 Humanoid Alien Races 之后（About 强依赖） |
| 45 | — | 新增 | 前置框架 | 未启用 | 1.绿色 (确认无冲突) | Map Mode Framework | nozome.mapmodeframework | 9 | 10/1 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 46 | — | 新增 | 前置框架 | 未启用 | 1.绿色 (确认无冲突) | Wolfein Race | melondove.wolfeinrace | 17 | 1005/15 | 前置约束：排在 Humanoid Alien Races 之后（About 强依赖）；前置约束：排在 Ancot Library 之后（About 强依赖） |
| 47 | — | 新增 | 前置框架 | 未启用 | 1.绿色 (确认无冲突) | [OA]Oberonia Aurea Framework | oark.oberoniaaurea.framework | 2 | 7/1 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 48 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Sparkling Worlds - Full Mod | albion.sparklingworlds.full | 0 | 197/106 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 49 | — | 新增 | 种族与阵营 | 未启用 | 2.黄色 (无法通过代码分析判断) | Anty the war ant race | roo.antyracemod | 1 | 825/10 | [2.黄色] Anty the war ant race 与 Miho, the celestial fox 同时对 Verse.Explosion.TrySpawnExplosionThing 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；前置约束：排在 Humanoid Alien Races 之后（About 强依赖） |
| 50 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | [K4G] RimWorld War 2 | k4g.rww2 | 0 | 1082/73 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 51 | — | 新增 | 种族与阵营 | 未启用 | 3.红色 (明确冲突) | Humanoid Alien Races ~ Dev | erdelf.humanoidalienraces.dev | 0 | 95/6 | [3.红色] Humanoid Alien Races 与 Humanoid Alien Races ~ Dev 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突；前置约束：排在 XML Extensions 之后（About.loadAfter） |
| 52 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Biotech xenotype expanded - Rotfish | gene.rotfish | 8 | 50/12 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 53 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Custom Quest Framework | hailuan.customquestframework | 0 | 155/1 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Odyssey 之后（条件兼容目录） |
| 54 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Misstall's Armory | misstall.nisstallsarmoryz | 0 | 141/0 | 前置约束：排在 Muzzle Flash 之后（About.loadAfter） |
| 55 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Moyo2.0-Blood stained business | nemonian.my2.beta | 2 | 399/23 | 前置约束：排在 Humanoid Alien Races 之后（About 强依赖）；前置约束：排在 Royalty 之后（条件兼容目录） |
| 56 | — | 新增 | 种族与阵营 | 未启用 | 3.红色 (明确冲突) | [1.7]边缘仙路-神机天纵 RimImmortal-Forge | ri.rimimmortal.forge | 2 | 646/3 | [3.红色] 缺少必需前置模组 边缘仙路-羽化登仙 RimImmortal-Core[1.6] (ri.rimimmortal.core)；前置约束：排在 Biotech 之后（About 强依赖） |
| 57 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Misstall's Armor and Uniforms | misstall.nisstallsoutfitszzz | 0 | 66/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 58 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Korossa: scorched brass | gyun.0.6.3.1 | 1 | 554/0 | 前置约束：排在 Humanoid Alien Races 之后（About 强依赖）；前置约束：排在 Biotech 之后（条件兼容目录） |
| 59 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Misstall's Vigil Race | misstalls.vigilracemodmain | 0 | 159/4 | 前置约束：排在 Humanoid Alien Races 之后（About 强依赖）；前置约束：排在 Misstall's Armory 之后（About 强依赖） |
| 60 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Rustspark Crisis | hailuan.allsparkcrisis | 3 | 64/1 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 61 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Fleshbeast Colony | fb.demobroka | 1 | 349/14 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 62 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Gloria A Las Plagas | ccdevelopment.glorialasplagas.0003 | 5 | 69/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Biotech 之后（About 强依赖） |
| 63 | 36 | ▼27 | 玩法与扩展 | 已启用 | 1.绿色 (确认无冲突) | 厨间百艺 (Culinary Arts 100) | sanguo.culinaryarts100 | 5 | 152/4 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 64 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | Pick Up And Haul | mehni.pickupandhaul | 0 | 3/1 | [3.红色] Pick Up at Home (Fork) 在 About.xml 中显式声明与 Pick Up And Haul 不兼容；前置约束：排在 Harmony 之后（About 强依赖） |
| 65 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Snap Out! | weilbyte.snapout | 0 | 7/1 | 前置约束：排在 Dubs Rimatomics 之后（DLL 程序集引用） |
| 66 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Range Finder | brrainz.rangefinder | 3 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 67 | 35 | ▼32 | 玩法与扩展 | 已启用 | 1.绿色 (确认无冲突) | RimTalk - Expand Memory | cj.rimtalk.expandmemory | 19 | 1/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 RimTalk 之后（About 强依赖） |
| 68 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [FSF] Growable Ambrosia | frozensnowfox.growableambrosia | 0 | 1/5 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 69 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Better Roads | wastelandr.betterroads | 0 | 5/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 70 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Smart Speed | sarg.smartspeed | 3 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 71 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Door Mat | dracoix.doormat.r12a | 0 | 43/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 72 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | RimHUD | jaxe.rimhud | 15 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 RimBridgeServer 之后（守卫 Prefix 顺位） |
| 73 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Trap Disable | velc.trapdisable | 0 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 74 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Sometimes Raids Go Wrong | marvinkosh.sometimesraidsgowrong | 0 | 5/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 75 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Real Ruins | woolstrand.realruins | 12 | 33/4 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 76 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | GloomyFurniture | solaris.furniturebase | 0 | 141/49 | [3.红色] Replace Stuff - Continued 在 About.xml 中显式声明与 GloomyFurniture 不兼容；前置约束：排在 Harmony 之后（About 强依赖） |
| 77 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [NL] Custom Portraits | nals.customportraits | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 78 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Dub's Paint Shop | dubwise.dubspaintshop | 8 | 18/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 79 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Dubs Mint Minimap | dubwise.dubsmintminimap | 4 | 8/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 80 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Bionic icons | automatic.bionicicons | 0 | 185/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 81 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Gradient Hair | automatic.gradienthair | 13 | 25/2 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 82 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Trait Rarity Colors | carnysenpai.traitraritycolors | 4 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 83 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Non uno Pinata (don't drop items) | avilmask.nonunopinata | 13 | 2/2 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 84 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Go Explore! | albion.goexplore | 0 | 60/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 85 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Won hair_men | won.hair | 0 | 37/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 86 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Won hair_women | won2.hair | 0 | 40/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 87 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Do Something for Idle | gguake.ai.dsfi | 0 | 29/1 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 88 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | CM Color Coded Mood Bar [1.1+] | crashm.colorcodedmoodbar.11 | 3 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 89 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | VGP Vegetable Garden | dismarzero.vgp.vgpvegetablegarden | 0 | 201/92 | [3.红色] VGP Vegetable Garden 与 VGP Garden Fabrics Simplified 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突；前置约束：排在 Ideology 之后（条件兼容目录） |
| 90 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | VGP Garden Dyes | dismarzero.vgp.vgpgardendyes | 0 | 49/21 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 91 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | VGP Garden Gourmet | dismarzero.vgp.vgpgardengourmet | 0 | 110/70 | [3.红色] VGP Garden Gourmet 与 VGP Garden Drinks 在无依赖关系下重复定义了核心 XML Def（ThingDef:Rawsugarcane），后加载者会整块覆盖先加载者；前置约束：排在 VGP Vegetable Garden 之后（About 强依赖） |
| 92 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | VGP Garden Medicine | dismarzero.vgp.vgpgardenmedicine | 0 | 73/9 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 93 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | VGP Garden Fabrics Simplified | dismarzero.vgp.vgpgardenfabricssimplified | 0 | 25/16 | [3.红色] VGP Vegetable Garden 与 VGP Garden Fabrics Simplified 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突；前置约束：排在 VGP Garden Medicine 之后（XML Patch 目标 Def） |
| 94 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | VGP More Veggies | dismarzero.vgp.vgpmoreveggies | 0 | 26/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 95 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | No Job Authors | doug.nojobauthors | 12 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 96 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | CloseAll | gorymoon.closeall | 1 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 97 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | AFU女士发型_Women's hairstyles | jintuzilamian.hairstyle.afuwomenshairstyles | 0 | 83/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 98 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Melee Weapons: Speed and Damage | ac.mw.sad | 2 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 99 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Apparel BodyType Resolver | gguake.apparelbodyresolver | 0 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 100 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | AFU男士发型_Men's hairstyles | jintuzilamian.hairstyle.afumenshairstyles | 0 | 38/0 | 前置约束：排在 [NL] Facial Animation - WIP 之后（条件兼容目录） |
| 101 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Map Designer | zylle.mapdesigner | 0 | 1/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 102 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ========必备前置======== | humblecoder.prerequisite | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 103 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | =======材质与画质======= | humblecoder.textureandgraphics | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 104 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | =====界面优化+详细信息===== | humblecoder.uiimprovementandoverview | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 105 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ======开局-人物编辑====== | humblecoder.beginning.pawneditor | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 106 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ======开局-人物性格====== | humblecoder.beginning.pawntrait | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 107 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ======开局-人物外形====== | humblecoder.beginning.pawnappearance | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 108 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ========开局-地图======== | humblecoder.beginning.map | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 109 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ========功能-娱乐======== | humblecoder.function.recreation | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 110 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ======功能-逻辑优化====== | humblecoder.function.alogrithmimprovement | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 111 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ========功能-增强======== | humblecoder.function.enhancement | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 112 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ======物品-能源与电力====== | humblecoder.thing.energyandelectricity | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 113 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ========物品-载具======== | humblecoder.thing.vehicle | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 114 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ========物品-动物======== | humblecoder.thing.animal | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 115 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ========物品-植物======== | humblecoder.thing.plant | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 116 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ==========系统========== | humblecoder.system | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 117 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | =======事件与任务======= | humblecoder.eventandtask | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 118 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | =======派系与种族======= | humblecoder.factionsandrace | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 119 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ==========综合========== | humblecoder.composite | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 120 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | =======后置汉化包======= | humblecoder.postchinese | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 121 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ========功能-增加======== | humblecoder.function.addition | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 122 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ==========物品========== | humblecoder.thing | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 123 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ======物品-武器与装备====== | humblecoder.thing.armament | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 124 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ========物品-农牧======== | humblecoder.thing.farming | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 125 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ========功能-其他======== | humblecoder.function.other | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 126 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Rimpedia | yrtwof.rimpedia | 12 | 9/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 127 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | VGP Garden Drinks | dismarzero.vgp.vgpgardendrinks | 0 | 61/1 | [3.红色] VGP Garden Gourmet 与 VGP Garden Drinks 在无依赖关系下重复定义了核心 XML Def（ThingDef:Rawsugarcane），后加载者会整块覆盖先加载者；前置约束：排在 VGP Vegetable Garden 之后（About.loadAfter） |
| 128 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ======物品-食物与饮品====== | humblecoder.thing.foodanddrink | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 129 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Standalone Hot Spring | balistafreak.standalonehotspring | 2 | 5/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 130 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | kemomimihouse Kz | moo.kemomimihouse.kz | 17 | 140/4 | [3.红色] 缺少必需前置模组 kemomimihouse (psyche.kemomimihouse)；[2.黄色] kemomimihouse Kz 与 虫娘 Insect Girls 在 RimWorld.Toils_LayDown.LayDown 上均存在无守卫的返回值直接赋值，后加载者会覆盖先加载者结果 |
| 131 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | =======开局-叙述者======= | humblecoder.beginning.storyteller | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 132 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Pocket Sand | usagirei.pocketsand | 2 | 2/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 133 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [NL] Dynamic Portraits | nals.dynamicportraits | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 134 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Choice Of Psycasts | azuraal.choiceofpsycasts | 4 | 0/2 | 前置约束：排在 Royalty 之后（About 强依赖）；前置约束：排在 Harmony 之后（About 强依赖） |
| 135 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Pawn Education (Continued) | mlie.pawneducation | 1 | 27/2 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 136 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Better Pod Launcher (Continued) | mlie.betterpodlauncher | 0 | 13/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 137 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [Kit] Graze up | kittahkhan.grazeup | 4 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 138 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Stop, Drop, And Roll! [BAL] | balistafreak.stopdropandroll | 3 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 139 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [RH2] Rimdeed® - Pawn Recruitment | cp.rimdeed | 5 | 11/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Visible Raid Points 之后（守卫 Prefix 顺位） |
| 140 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Destroy Item | garwel.destroyitem | 0 | 9/1 | 前置约束：排在 Core 之后（About.loadAfter） |
| 141 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | RPG Style Inventory Revamped | sandy.rpgstyleinventory.avilmask.revamped | 0 | 48/14 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 142 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [NL] Apparel Paper Pattern | nalsnoir.apparelpaperpattern | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 143 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Mines 2.0 | cain.mineshaft | 0 | 5/2 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 144 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | 角色名字汉化补全PawnNameZH | kapa.pawnnamezh | 2 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 145 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Repair Items | moony.repairitems | 0 | 3/4 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 146 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Need Bar Overflow | amch.needbaroverflow | 0 | 1/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 147 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Bed Rest For Food Poisoning | bedrestforfoodpoisoning.1trickponyta | 1 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 148 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Better Explosions | nephlite.advexplosions | 0 | 10/26 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 149 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | =======环境与生态======= | humblecoder.environmentsandbiomes | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 150 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ======功能-操作优化====== | humblecoder.function.operationimprovement | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 151 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | No Random Relations | dvs.norandomrelations | 1 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 152 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ==========医疗========== | humblecoder.medicine | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 153 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | No Version Warning | mlie.noversionwarning | 1 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 154 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Romance On The Rim | telardo.romanceontherim | 14 | 206/3 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Interaction Bubbles 之后（DLL 程序集引用） |
| 155 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | 中式龙娘——冶龍 SMELTED LOONG | ny.smeltedloong | 43 | 898/36 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Biotech 之后（About 强依赖） |
| 156 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Cargo container | aoba.cargocontainer | 0 | 16/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 157 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Geological Landforms | m00nl1ght.geologicallandforms | 0 | 45/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 158 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | What's Today's Menu | kaitorisenkou.whatstodaysmenu | 0 | 134/7 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 159 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Scrollable Gizmos | blockdude.scrollablegizmos | 2 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 160 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Food Poisoning Stack Fix (Continued) | mlie.foodpoisoningstackfix | 3 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 161 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Shella Backgrounds | stm.shellabackgrounds | 7 | 14/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 162 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Food Poisoning Cures | crocodil.foodpoisoningcures | 0 | 4/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 163 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Diagonal Walls 2 | chv.diagonalwalls2 | 0 | 45/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 164 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Trait and Backstory Icons | superniquito.traiticons | 4 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Trait Rarity Colors 之后（About.loadAfter） |
| 165 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [CAT] Show Hair With Hats or Hide All Hats | cat2002.showhair | 8 | 12/2 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Royalty 之后（条件兼容目录） |
| 166 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | DevelopMode fix | observer.developmode.fix | 0 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 167 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Gloomy Hair | gloomylynx.kotobike | 0 | 9/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 168 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Cargo waste container | aoba.cargocontainer.waste | 0 | 3/0 | 前置约束：排在 Biotech 之后（About 强依赖） |
| 169 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | No Disabled Factions In Quests | kathanon.nodisabledfactions | 2 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 170 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Gloomy Hair mk2 FORKED | klr.gloomyhairmk2forked | 0 | 33/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 171 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Better Children Skill Learning | redmattis.betterchildren | 2 | 0/0 | 前置约束：排在 Biotech 之后（About 强依赖）；前置约束：排在 Harmony 之后（About 强依赖） |
| 172 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Show Weapon Tallies | kathanon.showweapontallies | 1 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 173 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Gore Upon Dismemberment | thumb.goremod | 0 | 8/2 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 174 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Smoothed stone is worthless | smoothedstoneisworthless.mod | 1 | 0/1 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 175 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | AB's Head Apparel Tweaker | ab.hatweaker | 0 | 1/1 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Humanoid Alien Races 之后（DLL 程序集引用） |
| 176 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Priority Treatment Ressurected | tk421storm.prioritytreatmentressurected | 3 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 177 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Dynamic Trade Interface | zeracronius.dynamictradeinterface | 2 | 18/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 178 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Extract translation | hailuan.translate | 1 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 179 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Character Editor 中文化 | nb.charactereditor.chinese | 0 | 0/0 | 前置约束：排在 Character Editor 之后（About 强依赖） |
| 180 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Rottenfood | sunsetmoderteam.rottenfood | 0 | 5/6 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 181 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Get Out Of My Chair! | aelanna.getoutofmychair | 0 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 182 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | JustWoodFloor | tiagocc0.justwoodfloor | 0 | 2/1 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 183 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [SYR] Processor Framework | syrchalis.processor.framework | 2 | 10/6 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 184 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Rabbie The Moonrabbit race | runnelatki.rabbieracemod | 0 | 443/8 | 前置约束：排在 Humanoid Alien Races 之后（About 强依赖）；前置约束：排在 [SYR] Processor Framework 之后（About 强依赖） |
| 185 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | XiaoLan's chengyingsword | chengyingjian.lan | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 186 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Metalhorror Scanner | mo.mhscanner | 0 | 3/0 | 前置约束：排在 Core 之后（About.loadAfter）；前置约束：排在 Royalty 之后（About.loadAfter） |
| 187 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Basic Double Doors | gt.sam.basicdoubledoors | 0 | 2/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 188 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | No Forced Slowdown | thegoosebehindtheslaughter.noforcedslowdown | 2 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 189 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | 青的更多特性【More Traits】 | q.m | 4 | 248/678 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Character Editor 之后（动态反射修补） |
| 190 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [TW1.6]烧烤之星2.0 Barbecue~Star2.0 | tw.bbqs.barbecue.star | 0 | 129/2 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 191 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Name Your Entities | ferny.nameyourentities | 2 | 0/0 | 前置约束：排在 Core 之后（About.loadAfter）；前置约束：排在 Anomaly 之后（About.loadAfter） |
| 192 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Mallow's Gloves (Re-Upload) | mallow.gloves | 0 | 6/0 | 前置约束：排在 Biotech 之后（条件兼容目录）；前置约束：排在 Royalty 之后（条件兼容目录） |
| 193 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Better Research Tabs | andery233xj.mod.betterresearchtabs | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 194 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | I can equip/wear this | none1637.icanequip | 3 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 195 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Ponpeco Kids' Clothes | ponpeco.kidsclothes | 0 | 46/1 | 前置约束：排在 Biotech 之后（About 强依赖）；前置约束：排在 Odyssey 之后（条件兼容目录） |
| 196 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | BattIePatch: Ghoul Mood Bar Shows Their Hunger | battiebear.battiepatch.ghoulmoodbarfix | 1 | 0/0 | 前置约束：排在 Anomaly 之后（About 强依赖）；前置约束：排在 Harmony 之后（About 强依赖） |
| 197 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | 拼音搜索 Pinyin Quick Search | cedaro.pinyinquicksearch | 2 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 198 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Simple Texture Tweaks | buyue.simpletexturetweaks | 0 | 1/0 | 前置约束：排在 XML Extensions 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 199 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Surrogate Mechanoid | sov.det.surrogatemech | 4 | 3/6 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Biotech 之后（About 强依赖） |
| 200 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [1.5-1.6] Custom Xenotype Exporter Tool | gege.customxenotypeexportertoolreupload | 1 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Biotech 之后（About 强依赖） |
| 201 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Capture Them (Continued) | mlie.capturethem | 3 | 3/3 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 202 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Ponpeco Hair | ponpeco.pphair | 0 | 91/1 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 203 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Simple FX: Splashes | owlchemist.simplefx.splashes | 8 | 1/2 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 204 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Mid-saver Saver | owlchemist.midsaversaver | 8 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 205 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Simple FX: Smoke | owlchemist.simplefx.smoke2 | 3 | 6/25 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 206 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Scattered Flames | owlchemist.scatteredflames | 7 | 1/1 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Simple FX: Smoke 之后（About.loadAfter） |
| 207 | — | 新增 | 玩法与扩展 | 未启用 | 2.黄色 (无法通过代码分析判断) | Toggleable Readouts | owlchemist.toggleablereadouts | 3 | 0/0 | [2.黄色] Dubs Mint Menus 与 Toggleable Readouts 同时对 RimWorld.ResourceReadout.DoReadoutCategorized 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；前置约束：排在 Harmony 之后（About 强依赖） |
| 208 | — | 新增 | 玩法与扩展 | 未启用 | 2.黄色 (无法通过代码分析判断) | Dubs Mint Menus | dubwise.dubsmintmenus | 20 | 12/0 | [2.黄色] Dubs Mint Menus 与 Toggleable Readouts 同时对 RimWorld.ResourceReadout.DoReadoutCategorized 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；前置约束：排在 Toggleable Readouts 之后（Transpiler 顺序启发） |
| 209 | — | 新增 | 前置框架 | 未启用 | 1.绿色 (确认无冲突) | Nice Health Tab | andromeda.nicehealthtab | 20 | 220/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Dubs Rimatomics 之后（DLL 程序集引用） |
| 210 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Toggleable Shields | owlchemist.toggleableshields | 8 | 0/1 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 中式龙娘——冶龍 SMELTED LOONG 之后（守卫 Prefix 顺位） |
| 211 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Storage Refill Hysteresis | puremj.mjrimmods.storagerefillhysteresis | 11 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 212 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Real Faction Guest (Forked) | rtxyd.realfactionguest | 1 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 213 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Better Distress Call | nalzurin.betterdistresscall | 2 | 50/1 | 前置约束：排在 Anomaly 之后（About 强依赖）；前置约束：排在 Harmony 之后（About 强依赖） |
| 214 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Misstall's Armory Charge Weapons | misstall.chargeweaponzzz | 0 | 61/0 | 前置约束：排在 Muzzle Flash 之后（About.loadAfter） |
| 215 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Geneva Checklist | thathitmann.genevachecklist | 0 | 48/0 | 前置约束：排在 Core 之后（About.loadAfter） |
| 216 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | [SR]Raid Extension (fork) | sr.modrimworld.raidextension | 2 | 17/0 | [3.红色] [1.6] CAI 5000 - Advanced AI + Fog Of War (continued) 在 About.xml 中显式声明与 [SR]Raid Extension (fork) 不兼容；前置约束：排在 Harmony 之后（About 强依赖） |
| 217 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Select Similar | fxz.selectsimilar | 1 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 218 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Ponpeco Furnitures : Kids' Room | ponpeco.pfkidsroom | 0 | 80/4 | 前置约束：排在 Biotech 之后（条件兼容目录） |
| 219 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Amorphous style pack | xmb.amorphousstylepack.mo | 0 | 43/0 | 前置约束：排在 Ideology 之后（About 强依赖） |
| 220 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | I will be back | hailuan.iwbb | 0 | 2/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 221 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Let's Be Friends | alentuon.letsbefirends | 3 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 222 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | World Tech Level | m00nl1ght.worldtechlevel | 0 | 22/0 | 前置约束：排在 Core 之后（About.loadAfter）；前置约束：排在 Royalty 之后（About.loadAfter） |
| 223 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Davai's Sorted Categories | davai.sortedcategories | 0 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 XML Extensions 之后（About 强依赖） |
| 224 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [SR]Factional War (fork) | sr.modrimworld.factionalwarcontinued | 0 | 23/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 225 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Auto Dissector | lingluo.pawncuter | 0 | 4/0 | 前置约束：排在 Biotech 之后（About 强依赖） |
| 226 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | SpeakUp畅所欲言 中文版 | cn.speakup.ttyet | 12 | 186/29 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Interaction Bubbles 之后（About 强依赖） |
| 227 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [NL] Dynamic Portraits_zh | zh.nals.dynamicportraits | 2 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 [NL] Dynamic Portraits 之后（About.loadAfter） |
| 228 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Wolfein Race Gene Patch | ancot.wolfeinracegenepatch | 0 | 13/7 | 前置约束：排在 Biotech 之后（About 强依赖）；前置约束：排在 Wolfein Race 之后（About 强依赖） |
| 229 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | TendYourself | msd.tendyourself | 0 | 1/1 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 230 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Misstall's Foods and Social Drugs | misstall.nisstallsfoodsandsocialdrugsz | 0 | 64/2 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 231 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Giant crop | lingluo.giantcrop | 0 | 83/17 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 232 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Who shot my leg off? | tixiv.whoshotmylegoff | 1 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 233 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | 鼠族财富扩展-叙事者-雪铁芋 | thewwworld.ratkinwealthstoryteller | 0 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 234 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Diner Supplies | dsp.lun | 0 | 15/1 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 235 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Pigeon | fearl.pigeon | 0 | 25/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 236 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Nice Bill Tab | andromeda.nicebilltab | 13 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Nice Health Tab 之后（DLL 程序集引用） |
| 237 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | ReTend | temmie3754.retend.1 | 0 | 1/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 238 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | PrisonerManagementPanel(囚犯管理面板) | radio14.prisonermanagementpanel | 1 | 11/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 239 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Cat Hiss | hailuan.cathiss | 0 | 6/6 | 前置约束：排在 Odyssey 之后（条件兼容目录）；前置约束：排在 Biotech 之后（条件兼容目录） |
| 240 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | RunAndGun | roolo.runandgun.kotobike | 7 | 5/1 | 前置约束：排在 HugsLib 之后（About 强依赖） |
| 241 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [MUS]太空基地家具Space Base Furniture | mingtuwuxiang.spacebasedecorative | 0 | 78/2 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 242 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Performance - Slower Pawn Tick Rate | arkymn.slowerpawntickrate | 9 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 243 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | 百里墨的逆重飞船拓展 | hailuan.gravshipexpanded | 0 | 2/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 244 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Infinite Fishing | bakacandy.infinitefishing | 0 | 0/0 | 前置约束：排在 Odyssey 之后（About 强依赖）；前置约束：排在 Harmony 之后（About 强依赖） |
| 245 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | J-Music (Original Music Pack) | j.music | 0 | 8/2 | 前置约束：排在 Odyssey 之后（条件兼容目录） |
| 246 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Misstall's Shuttle | misstalls.vrshuttle | 0 | 4/0 | 前置约束：排在 Odyssey 之后（About 强依赖） |
| 247 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [TW1.6]幻彩林地 Rainbow forest | tw.tangsbiome.rainbowforest | 0 | 46/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 248 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | No Beards On Teenagers | kuchiki.nobeardsonteenagers | 1 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 249 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Build From Inventory - Continued | memegoddess.buildfrominventory | 3 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 250 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Temperature Control Unit | murmur.tcu.kotobike | 0 | 6/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 251 | — | 新增 | 玩法与扩展 | 未启用 | 2.黄色 (无法通过代码分析判断) | Choose Where To Land | kearril.choosewheretoland | 9 | 1/0 | [2.黄色] RimWorld Together 在 Verse.GameDataSaveLoader.SaveGame 上包含返回 false 的接管型 Prefix，需确保 Choose Where To Land 的前置补丁先执行；前置约束：排在 Harmony 之后（About 强依赖） |
| 252 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Gradient Hair Fixes | cosmosteller.gradienthair.fixes | 2 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Gradient Hair 之后（About 强依赖） |
| 253 | — | 新增 | 玩法与扩展 | 未启用 | 2.黄色 (无法通过代码分析判断) | Customize Weapon | vortex.customizeweapon | 11 | 120/0 | [2.黄色] Customize Weapon 与 ChezhouLib 同时对 Verse.PawnRenderUtility.DrawEquipmentAiming 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；[2.黄色] Customize Weapon 与 Raven Race - Embers in Shadow 同时对 Verse.PawnRenderUtility.DrawEquipmentAiming 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响 |
| 254 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Cat's Boots and Gloves | catlover366.bootsandgloves | 0 | 20/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 255 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Save File Compression | amch.savefilecompression | 0 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 256 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Giant crop Ex | lingluo.giantcropex | 0 | 38/4 | 前置约束：排在 Giant crop 之后（XML Patch 目标 Def） |
| 257 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Oh My Hand! | mjd.ohmyhand | 1 | 1/0 | 前置约束：排在 Core 之后（About 强依赖）；前置约束：排在 Harmony 之后（About.loadAfter） |
| 258 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Oh My Hand!_zh 我的手手！- 简繁中文汉化包 | mjd.ohmyhand.zh | 1 | 0/0 | 前置约束：排在 Core 之后（About 强依赖）；前置约束：排在 Oh My Hand! 之后（About.loadAfter） |
| 259 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Just Help For Me | cn.justhelpforme | 3 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 260 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Rim Plant Species Xeno | redstonepanda.plantspeciesxeno | 1 | 4/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 261 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Steam friend name | hailuan.steamfriend | 1 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 262 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Customize Room Role | lingluo.customizeroomrole | 1 | 1/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 263 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | RimtalkPatch | mjcg.rimtalkpatch | 1 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 RimTalk 之后（About 强依赖） |
| 264 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Nice Inventory Tab | andromeda.niceinventorytab | 15 | 15/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Nice Health Tab 之后（DLL 程序集引用） |
| 265 | — | 新增 | 系统与诊断 | 未启用 | 1.绿色 (确认无冲突) | Vanilla Combat Tweak | assssssqwww.ct | 31 | 0/5 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Nice Inventory Tab 之后（About.loadAfter） |
| 266 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Immortal Regenerant Gene | custom.immortalregenerant | 9 | 4/1 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Biotech 之后（About 强依赖） |
| 267 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Hive Creature Framework | hailuan.hivecreatureframework | 0 | 98/1 | 前置约束：排在 Custom Quest Framework 之后（About.loadAfter） |
| 268 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Hive Creature-Insect | hailuan.hivegroup.insect | 0 | 25/1 | 前置约束：排在 Hive Creature Framework 之后（About 强依赖） |
| 269 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | RimTalk: Persona Director | rp.rimtalk.personadirector | 41 | 1/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 RimTalk 之后（About 强依赖） |
| 270 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Faction Territories and Vassalage | jaeger972.factionterritories | 34 | 6/0 | 前置约束：排在 Map Mode Framework 之后（About 强依赖） |
| 271 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | RimTalk.DisplayOptimization | oceantest5.rimtalk.enhance | 15 | 1/0 | 前置约束：排在 RimTalk 之后（About 强依赖） |
| 272 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | Butter++ | olli.butterplusplus | 4 | 0/0 | [3.红色] Butter++ 在 About.xml 中显式声明与 Dubs Performance Analyzer 不兼容；前置约束：排在 Harmony 之后（About 强依赖） |
| 273 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Universal Apparel Tweaker | zuoyao.universalappareltweaker | 4 | 2/0 | 前置约束：排在 Humanoid Alien Races 之后（DLL 程序集引用）；前置约束：排在 Core 之后（About.loadAfter） |
| 274 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | 边缘经验球拓展Experience Orb Expansion | sunsetmoderteam.experiencedevice | 1 | 3/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 275 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Performance Esmolas | arkymn.performanceesmolas | 11 | 0/2 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 276 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [TW1.7]工业风格 Industrialism Style | tw.is.industrialismstyle | 0 | 22/3 | 前置约束：排在 Ideology 之后（About 强依赖） |
| 277 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Miho Eltex Staff Addon | openai.mihoeltexcdaddon | 0 | 5/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 278 | — | 新增 | 玩法与扩展 | 未启用 | 2.黄色 (无法通过代码分析判断) | RimChat - Word to Actions | yancy.rimchat | 20 | 15/2 | [2.黄色] RimWorld Together 在 Verse.GameDataSaveLoader.SaveGame 上包含返回 false 的接管型 Prefix，需确保 RimChat - Word to Actions 的前置补丁先执行；[2.黄色] RimWorld Together 在 Verse.LoadedObjectDirectory.RegisterLoaded 上包含返回 false 的接管型 Prefix，需确保 RimChat - Word to Actions 的前置补丁先执行 |
| 279 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Wolfein Allegiance | leopoko.wolfeinallegiance | 47 | 181/11 | 前置约束：排在 Wolfein Race 之后（About 强依赖）；前置约束：排在 Royalty 之后（About 强依赖） |
| 280 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | [RH2] Faction: V.O.I.D. | rh2.faction.void | 65 | 635/23 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Muzzle Flash 之后（守卫 Prefix 顺位） |
| 281 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | RimSaves | arandomkiwi.rimsaves | 13 | 0/0 | [3.红色] RimWorld Together 在 About.xml 中显式声明与 RimSaves 不兼容；[2.黄色] RimWorld Together 在 Verse.GameDataSaveLoader.SaveGame 上包含返回 false 的接管型 Prefix，需确保 RimSaves 的前置补丁先执行 |
| 282 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Nice Plants Menu | andromeda.niceplantsmenu | 1 | 9/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Dubs Rimatomics 之后（DLL 程序集引用） |
| 283 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Gear Stat Optimization | secsky2.gearstatoptimization | 6 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 284 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Seamless Doors | bellodhr.seamlessdoors | 6 | 1/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 285 | — | 新增 | 前置框架 | 未启用 | 2.黄色 (无法通过代码分析判断) | Adaptive Storage Framework | adaptive.storage.framework | 31 | 11/2 | [2.黄色] Adaptive Storage Framework 与 Raven Race - Embers in Shadow 同时对 Verse.ThingOverlays.ThingOverlaysOnGUI 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；前置约束：排在 Harmony 之后（About 强依赖） |
| 286 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Adaptive Simple Storage | adaptive.simplestorage | 0 | 42/0 | 前置约束：排在 Adaptive Storage Framework 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 287 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | LWM's Adaptive Deep Storage | asf.deepstorage | 0 | 32/0 | 前置约束：排在 Adaptive Storage Framework 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 288 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Adaptive Primitive Storage | adaptive.primitivestorage | 0 | 31/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Adaptive Storage Framework 之后（About 强依赖） |
| 289 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [sbz] Neat Storage | sbz.neatstorage | 0 | 59/0 | 前置约束：排在 Adaptive Storage Framework 之后（About 强依赖） |
| 290 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [sbz] Workbench Shelf | sbz.neatstorageworkbenchshelf | 1 | 7/47 | 前置约束：排在 Adaptive Storage Framework 之后（About 强依赖）；前置约束：排在 Royalty 之后（条件兼容目录） |
| 291 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [sbz] Fridge | sbz.neatstoragefridge | 8 | 16/1 | 前置约束：排在 Adaptive Storage Framework 之后（About 强依赖）；前置约束：排在 Odyssey 之后（条件兼容目录） |
| 292 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Warehouse Storage | vin.warehouse.storage | 0 | 33/0 | 前置约束：排在 Adaptive Storage Framework 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 293 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Adaptive Storage - Global Settings | nuanki.adaptivestorageglobalsettings | 1 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Adaptive Storage Framework 之后（About 强依赖） |
| 294 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [sbz] Gravship Storage | sbz.gravshipstorage | 0 | 31/2 | 前置约束：排在 Adaptive Storage Framework 之后（About 强依赖）；前置约束：排在 Odyssey 之后（条件兼容目录） |
| 295 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | Architect Menu Optimizer | mrk.architectmenuoptimizer | 25 | 0/0 | [3.红色] Better Architect Menu 在 RimWorld.ArchitectCategoryTab.DesignationTabOnGUI 上无条件返回 false 跳过原方法，会使 Architect Menu Optimizer 针对原方法体的补丁无法生效；[2.黄色] Allow Tool (1.6 Temp) 与 Architect Menu Optimizer 同时对 Verse.Command.GizmoOnGUIInt 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响 |
| 296 | — | 新增 | 玩法与扩展 | 未启用 | 2.黄色 (无法通过代码分析判断) | Allow Tool (1.6 Temp) | dawnsglow.allowtool | 16 | 44/3 | [2.黄色] Allow Tool (1.6 Temp) 与 Architect Menu Optimizer 同时对 Verse.Command.GizmoOnGUIInt 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；[2.黄色] Allow Tool (1.6 Temp) 与 Blueprints Forked - 1.6 同时对 Verse.GizmoGridDrawer.DrawGizmoGridFor 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响 |
| 297 | 24 | ▼273 | 前置框架 | 已启用 | 1.绿色 (确认无冲突) | Milira Race | ancot.milirarace | 35 | 1103/44 | 前置约束：排在 Humanoid Alien Races 之后（About 强依赖）；前置约束：排在 Ancot Library 之后（About 强依赖） |
| 298 | — | 新增 | 前置框架 | 未启用 | 2.黄色 (无法通过代码分析判断) | Blueprints Forked - 1.6 | defi.blueprints.fork | 2 | 1/0 | [2.黄色] Allow Tool (1.6 Temp) 与 Blueprints Forked - 1.6 同时对 Verse.GizmoGridDrawer.DrawGizmoGridFor 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；前置约束：排在 Harmony 之后（About 强依赖） |
| 299 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Milira Race Gene Patch | ancot.miliraracegenepatch | 0 | 8/4 | 前置约束：排在 Milira Race 之后（About 强依赖）；前置约束：排在 Biotech 之后（About 强依赖） |
| 300 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Milira Cultivated SolarCrystal | linruo.miliracultivatedsolarcrystal | 0 | 1/1 | 前置约束：排在 Milira Race 之后（About 强依赖） |
| 301 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | 瓦莱丽的米莉拉扩展包 | valeries.miliraexpansion | 0 | 119/13 | 前置约束：排在 Milira Race 之后（About 强依赖） |
| 302 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Milira Tech: Milian Modification | ancot.milianmodification | 10 | 476/31 | 前置约束：排在 Ancot Library 之后（About 强依赖）；前置约束：排在 Milira Race 之后（About 强依赖） |
| 303 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Universal Blueprints | trigger.universalblueprints | 9 | 6/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Blueprints Forked - 1.6 之后（动态反射修补） |
| 304 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | BetterMiliraFallenAngel | aquin.bettermilirafallenangel | 7 | 7/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Milira Race 之后（About 强依赖） |
| 305 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Milira Addon: Fianchetto Variation | rabiosus.funnelmilian | 9 | 217/9 | 前置约束：排在 Biotech 之后（About 强依赖）；前置约束：排在 Harmony 之后（About 强依赖） |
| 306 | — | 新增 | 玩法与扩展 | 未启用 | 2.黄色 (无法通过代码分析判断) | ManyCats Toolbox | ialways1ose.manycats.toolbox | 194 | 12/1 | [2.黄色] Unlimited Mechanitor Command Range 在 MechanitorUtility.InMechanitorCommandRange 上包含返回 false 的接管型 Prefix，需确保 ManyCats Toolbox 的前置补丁先执行；[2.黄色] Unlimited Mechanitor Command Range 在 RimWorld.Pawn_MechanitorTracker.DrawCommandRadius 上包含返回 false 的接管型 Prefix，需确保 ManyCats Toolbox 的前置补丁先执行 |
| 307 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | RimTalk - Expand Actions | sanguo.rimtalk.expandactions | 6 | 6/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 RimTalk 之后（About 强依赖） |
| 308 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Eternal Pawns | helldan.eternalpawns | 17 | 1/0 | 前置约束：排在 Core 之后（About.loadAfter）；前置约束：排在 Ideology 之后（About.loadAfter） |
| 309 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Family Relations: Adoption | rycon.familyrelationsadoption | 0 | 65/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 310 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | More Useful Grenade | cn.moreusefulgrenade | 12 | 41/3 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Non uno Pinata (don't drop items) 之后（守卫 Prefix 顺位） |
| 311 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Memory album | hailuan.photo | 43 | 3/1 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 312 | 10 | ▼302 | 玩法与扩展 | 已启用 | 1.绿色 (确认无冲突) | The Second Seat | sanguo.thesecondseat | 4 | 93/0 | 前置约束：排在 Core 之后（About.loadAfter）；前置约束：排在 Royalty 之后（About.loadAfter） |
| 313 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | 【CA】更好的垃圾分解仪 | cagier.betterwastepackatomizer | 0 | 0/8 | 前置约束：排在 Biotech 之后（About 强依赖） |
| 314 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Wolfein Chocolate Delicacies-沃芬族巧克力佳肴 | zuoyao.wolfeinchocolatedelicacies | 1 | 37/0 | 前置约束：排在 Wolfein Race 之后（About 强依赖） |
| 315 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | RimIOT - Logistic Matrix | cn.rimiot | 32 | 5/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Adaptive Storage Framework 之后（About.loadAfter） |
| 316 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Cluster Projection | acutus.clusterprojection | 4 | 24/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 317 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Misc. Training | haplo.miscellaneous.training | 0 | 21/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 318 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | P-Music | peppsen.pmusic | 0 | 56/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 319 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Tilled Soil | gt.sam.tilledsoil | 0 | 4/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 320 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Tech Advancing | ghxx.techadvancing | 8 | 1/0 | 前置约束：排在 Dubs Rimatomics 之后（DLL 程序集引用） |
| 321 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Better Workbench Management | falconne.bwm | 23 | 0/1 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 322 | 33 | ▼289 | 种族与阵营 | 已启用 | 2.黄色 (无法通过代码分析判断) | Rim Girls' Frontline | rimgirlsfrontline.main | 51 | 1590/0 | [2.黄色] Unlimited Mechanitor Command Range 在 MechanitorUtility.InMechanitorCommandRange 上包含返回 false 的接管型 Prefix，需确保 Rim Girls' Frontline 的前置补丁先执行；前置约束：排在 Harmony 之后（About 强依赖） |
| 323 | — | 新增 | 玩法与扩展 | 未启用 | 2.黄色 (无法通过代码分析判断) | ChezhouLib | chezhou.chezhoulib.lib | 188 | 155/0 | [2.黄色] NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 ChezhouLib 的前置补丁先执行；[2.黄色] Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 ChezhouLib 的前置补丁先执行 |
| 324 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | LEVIATHANS:SANDWORM | chezhou.creature.sandworm | 2 | 9/0 | [3.红色] 缺少必需前置模组 RimUnity (chezhou.rimunity)；前置约束：排在 ChezhouLib 之后（About 强依赖） |
| 325 | 25 | ▼300 | 渲染与动画 | 已启用 | 1.绿色 (确认无冲突) | Milira Race Facial Animation | ancot.miliraracefacialanimation | 0 | 64/3 | 前置约束：排在 Milira Race 之后（About 强依赖）；前置约束：排在 [NL] Facial Animation - WIP 之后（About 强依赖） |
| 326 | — | 新增 | 渲染与动画 | 未启用 | 3.红色 (明确冲突) | Moyo Facial Animation Patch | aw.moyo.fac | 0 | 20/0 | [3.红色] 缺少必需前置模组 Moyo2.0-Blood stained business (nemonian.my2)；前置约束：排在 [NL] Facial Animation - WIP 之后（About 强依赖） |
| 327 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | MoeLotl Facial Animation | hentailoliteam.axolotl.fa | 0 | 115/3 | 前置约束：排在 [NL] Facial Animation - WIP 之后（About 强依赖）；前置约束：排在 MoeLotl Race 之后（About 强依赖） |
| 328 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Vigil Race Facial Animation | misstall.vr.famain | 0 | 49/0 | 前置约束：排在 Misstall's Vigil Race 之后（About 强依赖）；前置约束：排在 [NL] Facial Animation - WIP 之后（About 强依赖） |
| 329 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Wolfein Race Facial Animation | melondove.wolfeinrace.fa | 0 | 112/4 | 前置约束：排在 [NL] Facial Animation - WIP 之后（About 强依赖）；前置约束：排在 Wolfein Race 之后（About 强依赖） |
| 330 | 26 | ▼304 | 渲染与动画 | 已启用 | 1.绿色 (确认无冲突) | Pawn Universal Adjuster | sanguo.pawnuniversaladjuster | 6 | 1/0 | 前置约束：排在 Humanoid Alien Races 之后（动态反射修补）；前置约束：排在 Muzzle Flash 之后（守卫 Prefix 顺位） |
| 331 | 27 | ▼304 | 渲染与动画 | 已启用 | 2.黄色 (无法通过代码分析判断) | PUA Body Animation | lea.puabodyanimation | 11 | 4/0 | [2.黄色] NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 PUA Body Animation 的前置补丁先执行；[2.黄色] Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 PUA Body Animation 的前置补丁先执行 |
| 332 | — | 新增 | 玩法与扩展 | 未启用 | 2.黄色 (无法通过代码分析判断) | SRALib | dizhuan.sralib | 21 | 11/0 | [2.黄色] Unlimited Mechanitor Command Range 在 MechanitorUtility.InMechanitorCommandRange 上包含返回 false 的接管型 Prefix，需确保 SRALib 的前置补丁先执行；[2.黄色] NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 SRALib 的前置补丁先执行 |
| 333 | — | 新增 | 种族与阵营 | 未启用 | 2.黄色 (无法通过代码分析判断) | 联合重工-UF Heavy Industries | kindseal.lol | 57 | 685/1 | [2.黄色] 联合重工-UF Heavy Industries 与 [1.6] CAI 5000 - Advanced AI + Fog Of War (continued) 同时对 Verse.AI.Pawn_PathFollower.SetupMoveIntoNextCell 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；前置约束：排在 Harmony 之后（About 强依赖） |
| 334 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | [1.6] CAI 5000 - Advanced AI + Fog Of War (continued) | krkr.rule56 | 98 | 24/2 | [3.红色] [1.6] CAI 5000 - Advanced AI + Fog Of War (continued) 在 About.xml 中显式声明与 Pawn Target Fix 不兼容；[3.红色] [1.6] CAI 5000 - Advanced AI + Fog Of War (continued) 在 About.xml 中显式声明与 [SR]Raid Extension (fork) 不兼容 |
| 335 | — | 新增 | 前置框架 | 未启用 | 2.黄色 (无法通过代码分析判断) | Vanilla Expanded Framework | oskarpotocki.vanillafactionsexpanded.core | 403 | 178/33 | [2.黄色] Vanilla Expanded Framework 与 Epona Dynastic Rise 同时对 Verse.AI.JobDriver_Wait.CheckForAutoAttack 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；[2.黄色] Vanilla Expanded Framework 与 tailin race1.2.2 同时对 Verse.AI.JobDriver_Wait.CheckForAutoAttack 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响 |
| 336 | — | 新增 | 前置框架 | 未启用 | 3.红色 (明确冲突) | Vanilla Textures Expanded | vanillaexpanded.vtexe | 2 | 1/26 | [3.红色] Vanilla Textures Expanded 与 [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突；前置约束：排在 Harmony 之后（About 强依赖） |
| 337 | — | 新增 | 前置框架 | 未启用 | 2.黄色 (无法通过代码分析判断) | Best Mix (Continued) | mlie.bestmix | 6 | 0/0 | [2.黄色] Vanilla Expanded Framework 与 Best Mix (Continued) 同时对 RimWorld.WorkGiver_DoBill.TryFindBestBillIngredientsInSet_AllowMix 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；前置约束：排在 Harmony 之后（About 强依赖） |
| 338 | — | 新增 | 系统与诊断 | 未启用 | 1.绿色 (确认无冲突) | Fill backstories | mlie.fillbackstories | 0 | 0/0 | 前置约束：排在 Best Mix (Continued) 之后（DLL 程序集引用） |
| 339 | — | 新增 | 前置框架 | 未启用 | 1.绿色 (确认无冲突) | Food Texture Variety Core | goat.food.texture.variety.core | 0 | 0/0 | 前置约束：排在 Vanilla Textures Expanded 之后（About.loadAfter） |
| 340 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Alpha Animals | sarg.alphaanimals | 5 | 919/15 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Vanilla Expanded Framework 之后（About 强依赖） |
| 341 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Vanilla Events Expanded | vanillaexpanded.vee | 16 | 126/18 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Vanilla Expanded Framework 之后（About 强依赖） |
| 342 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Alpha Biomes | sarg.alphabiomes | 32 | 743/103 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Vanilla Expanded Framework 之后（About 强依赖） |
| 343 | — | 新增 | 玩法与扩展 | 未启用 | 2.黄色 (无法通过代码分析判断) | Dev In Your Language | latta.devl10n | 13 | 0/0 | [2.黄色] Vanilla Expanded Framework 与 Dev In Your Language 同时对 Verse.DebugWindowsOpener.DrawButtons 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；[2.黄色] Dev In Your Language 与 HugsLib 同时对 Verse.DebugWindowsOpener.DrawButtons 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响 |
| 344 | — | 新增 | 玩法与扩展 | 未启用 | 2.黄色 (无法通过代码分析判断) | Vanilla Social Interactions Expanded | vanillaexpanded.vanillasocialinteractionsexpanded | 67 | 171/0 | [2.黄色] Hospitality 在 RimWorld.Pawn_InteractionsTracker.TryInteractRandomly 上包含返回 false 的接管型 Prefix，需确保 Vanilla Social Interactions Expanded 的前置补丁先执行；[2.黄色] Vanilla Social Interactions Expanded 与 EndlessGrowth 同时对 RimWorld.QualityUtility.GenerateQualityCreatedByPawn 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响 |
| 345 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Stuff List (Continued) | mlie.stufflist | 0 | 1/0 | 前置约束：排在 Best Mix (Continued) 之后（DLL 程序集引用） |
| 346 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Yayo's Combat 3 (Continued) | mlie.yayoscombat3 | 33 | 41/3 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Humanoid Alien Races 之后（DLL 程序集引用） |
| 347 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Mugirl Race | har.mugirlrace | 157 | 586/32 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Humanoid Alien Races 之后（About 强依赖） |
| 348 | — | 新增 | 玩法与扩展 | 未启用 | 2.黄色 (无法通过代码分析判断) | EndlessGrowth | slimesenpai.endlessgrowth | 14 | 0/0 | [2.黄色] Vanilla Social Interactions Expanded 与 EndlessGrowth 同时对 RimWorld.QualityUtility.GenerateQualityCreatedByPawn 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；前置约束：排在 Harmony 之后（About 强依赖） |
| 349 | — | 新增 | 种族与阵营 | 未启用 | 2.黄色 (无法通过代码分析判断) | Fortified Features Framework | aoba.framework | 148 | 174/1 | [2.黄色] NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Fortified Features Framework 的前置补丁先执行；[2.黄色] Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Fortified Features Framework 的前置补丁先执行 |
| 350 | — | 新增 | 种族与阵营 | 未启用 | 2.黄色 (无法通过代码分析判断) | Exosuit Framework | aoba.exosuit.framework | 25 | 90/8 | [2.黄色] NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Exosuit Framework 的前置补丁先执行；[2.黄色] Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Exosuit Framework 的前置补丁先执行 |
| 351 | — | 新增 | 种族与阵营 | 未启用 | 2.黄色 (无法通过代码分析判断) | Epona Dynastic Rise | epona.eponadynasticrise | 66 | 979/21 | [2.黄色] NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Epona Dynastic Rise 的前置补丁先执行；[2.黄色] Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Epona Dynastic Rise 的前置补丁先执行 |
| 352 | — | 新增 | 种族与阵营 | 未启用 | 2.黄色 (无法通过代码分析判断) | tailin race1.2.2 | lepechandeusro.tailin | 52 | 645/0 | [2.黄色] Vanilla Expanded Framework 与 tailin race1.2.2 同时对 Verse.AI.JobDriver_Wait.CheckForAutoAttack 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；[2.黄色] Epona Dynastic Rise 与 tailin race1.2.2 同时对 Verse.AI.JobDriver_Wait.CheckForAutoAttack 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响 |
| 353 | — | 新增 | 种族与阵营 | 未启用 | 2.黄色 (无法通过代码分析判断) | Wolfein Black Science Expand | wolfeinexpand.blackscience | 99 | 183/8 | [2.黄色] Unlimited Mechanitor Command Range 在 MechanitorUtility.InMechanitorCommandRange 上包含返回 false 的接管型 Prefix，需确保 Wolfein Black Science Expand 的前置补丁先执行；[2.黄色] NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Wolfein Black Science Expand 的前置补丁先执行 |
| 354 | — | 新增 | 种族与阵营 | 未启用 | 3.红色 (明确冲突) | NewRatkinPlus[1.6] | fxz.solaris.ratkinracemod.odyssey | 10 | 349/31 | [3.红色] NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_HamsterWheelGenerator），后加载者会整块覆盖先加载者；[3.红色] NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_HandTailoringBench），后加载者会整块覆盖先加载者 |
| 355 | — | 新增 | 种族与阵营 | 未启用 | 3.红色 (明确冲突) | Yuran race | rooandgloomy.yuranracemod | 1 | 589/28 | [3.红色] NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上无条件返回 false 跳过原方法，会使 Yuran race 针对原方法体的补丁无法生效；[3.红色] Yuran race 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上无条件返回 false 跳过原方法，会使 Yayo's Animation (Continued) 针对原方法体的补丁无法生效 |
| 356 | — | 新增 | 前置框架 | 未启用 | 3.红色 (明确冲突) | Miho, the celestial fox | miho.fortifiedoutremer | 7 | 904/34 | [3.红色] NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上无条件返回 false 跳过原方法，会使 Miho, the celestial fox 针对原方法体的补丁无法生效；[3.红色] Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上无条件返回 false 跳过原方法，会使 Yayo's Animation (Continued) 针对原方法体的补丁无法生效 |
| 357 | 22 | ▼335 | 前置框架 | 已启用 | 3.红色 (明确冲突) | NewRatkinPlus | solaris.ratkinracemod_steam | 3 | 500/68 | [3.红色] NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_HamsterWheelGenerator），后加载者会整块覆盖先加载者；[3.红色] NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_HandTailoringBench），后加载者会整块覆盖先加载者 |
| 358 | — | 新增 | 前置框架 | 未启用 | 1.绿色 (确认无冲突) | The Dead Man's Switch | aoba.deadmanswitch.core | 2 | 730/17 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Biotech 之后（About 强依赖） |
| 359 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | [OA]Ratkin Faction: Oberonia aurea | oark.ratkinfaction.oberoniaaurea | 26 | 942/36 | 前置约束：排在 Royalty 之后（About 强依赖）；前置约束：排在 NewRatkinPlus 之后（About 强依赖） |
| 360 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Ratkin Anomaly+ | fxz.ratkinanomaly.update | 42 | 333/11 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 NewRatkinPlus 之后（About 强依赖） |
| 361 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | The Dead Man's Switch - AncientCorps | aoba.deadmanswitch.ancientcorps | 2 | 399/12 | 前置约束：排在 The Dead Man's Switch 之后（About 强依赖） |
| 362 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | 联合重工派系拓展-UFPC | kindseal.ufpc | 1 | 224/1 | 前置约束：排在 联合重工-UF Heavy Industries 之后（About 强依赖）；前置约束：排在 NewRatkinPlus 之后（About.loadAfter） |
| 363 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Monolyn Race | asel.monolynrace | 31 | 1778/103 | 前置约束：排在 Humanoid Alien Races 之后（About 强依赖）；前置约束：排在 Yuran race 之后（About.loadAfter） |
| 364 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Down For Me | arandomkiwi.downforme | 7 | 1/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Milira Race 之后（守卫 Prefix 顺位） |
| 365 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Fortifications - Industrial | aoba.fortress.industrial | 0 | 115/6 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Fortified Features Framework 之后（About 强依赖） |
| 366 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Fortification Industrial -Nuclear Dawn | aoba.fortress.industrial.nucleardawn | 0 | 23/0 | 前置约束：排在 Vanilla Expanded Framework 之后（About 强依赖）；前置约束：排在 Fortifications - Industrial 之后（About 强依赖） |
| 367 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Fortification Industrial - Citadel | aoba.fortress.industrial.citadel | 0 | 151/0 | 前置约束：排在 Fortifications - Industrial 之后（About 强依赖）；前置约束：排在 Vanilla Expanded Framework 之后（About.loadAfter） |
| 368 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Show Mod Updates | mlie.showmodupdates | 3 | 2/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Best Mix (Continued) 之后（DLL 程序集引用） |
| 369 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Search and Destroy (Continued) | memegoddess.searchanddestroy | 5 | 1/4 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Vanilla Expanded Framework 之后（About.loadAfter） |
| 370 | 23 | ▼347 | 种族与阵营 | 已启用 | 2.黄色 (无法通过代码分析判断) | Nivarian Race | keeptpa.nivarianrace_steam | 193 | 1051/22 | [2.黄色] NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Nivarian Race 的前置补丁先执行；[2.黄色] Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Nivarian Race 的前置补丁先执行 |
| 371 | — | 新增 | 种族与阵营 | 未启用 | 2.黄色 (无法通过代码分析判断) | Ratkin Weapons+ | bbb.ratkinweapon.morefailure | 8 | 107/8 | [2.黄色] NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Ratkin Weapons+ 的前置补丁先执行；[2.黄色] Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Ratkin Weapons+ 的前置补丁先执行 |
| 372 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Ratkin Faction+ | fxz.ratkinfaction | 2 | 28/10 | 前置约束：排在 NewRatkinPlus 之后（About 强依赖）；前置约束：排在 Ratkin Weapons+ 之后（About.loadBefore） |
| 373 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | [OA]Ratkin Gene Expand | oark.ratkinfaction.geneexpand | 12 | 157/29 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Biotech 之后（About 强依赖） |
| 374 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Ratkin Knights+ | rkk.ratknights.core | 3 | 242/5 | 前置约束：排在 Ratkin Weapons+ 之后（About 强依赖）；前置约束：排在 NewRatkinPlus 之后（DLL 程序集引用） |
| 375 | 37 | ▼338 | 玩法与扩展 | 已启用 | 1.绿色 (确认无冲突) | 涅瓦莲：繁星之路 | sanguo.nivarianstarryroad | 3 | 87/1 | 前置约束：排在 Nivarian Race 之后（About.loadAfter）；前置约束：排在 Humanoid Alien Races 之后（About.loadAfter） |
| 376 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | BetterInfoCard | creeper.betterinfocard | 12 | 2/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Nivarian Race 之后（守卫 Prefix 顺位） |
| 377 | — | 新增 | 前置框架 | 未启用 | 1.绿色 (确认无冲突) | Vehicle Framework | smashphil.vehicleframework | 0 | 221/14 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Royalty 之后（条件兼容目录） |
| 378 | — | 新增 | 前置框架 | 未启用 | 1.绿色 (确认无冲突) | RimThunder - Core | rimthunder.core | 2 | 64/1 | 前置约束：排在 Vehicle Framework 之后（About 强依赖）；前置约束：排在 Engine Industries 之后（About.loadAfter） |
| 379 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | RimThunder - Roaring Tiger | aoba.rimthunder.roca | 0 | 44/0 | 前置约束：排在 Vehicle Framework 之后（About 强依赖）；前置约束：排在 RimThunder - Core 之后（About 强依赖） |
| 380 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | RimThunder - Time Forward | rimthunderteams.rimthunder.timeforward | 0 | 61/0 | 前置约束：排在 Vehicle Framework 之后（About 强依赖）；前置约束：排在 RimThunder - Core 之后（About 强依赖） |
| 381 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Warband Warfare | thumb.warbands | 0 | 37/2 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Humanoid Alien Races 之后（DLL 程序集引用） |
| 382 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Injured Carry | haecriver.injuredcarry | 1 | 1/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Vehicle Framework 之后（About.loadAfter） |
| 383 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Vanilla Vehicles Expanded | oskarpotocki.vanillavehiclesexpanded | 25 | 212/11 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Vehicle Framework 之后（About 强依赖） |
| 384 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | RimThunder - Cold Front | aoba.rimthunder.redarmy.coldfront | 0 | 35/0 | 前置约束：排在 Vehicle Framework 之后（About 强依赖）；前置约束：排在 RimThunder - Core 之后（About 强依赖） |
| 385 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | RimThunder - Western Green | zyszhao.rimthunder.westerngreen | 0 | 55/0 | 前置约束：排在 Vehicle Framework 之后（About 强依赖）；前置约束：排在 Vanilla Expanded Framework 之后（About 强依赖） |
| 386 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | SimpleCameraSetting | ray1203.simplecamerasetting | 8 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Vanilla Expanded Framework 之后（守卫 Prefix 顺位） |
| 387 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | The Dead Man's Switch - Motorized | aoba.deadmanswitch.motorized | 0 | 54/0 | 前置约束：排在 Vehicle Framework 之后（About 强依赖）；前置约束：排在 The Dead Man's Switch 之后（About 强依赖） |
| 388 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Vanilla Vehicles Expanded - Upgrades | oskarpotocki.vanillavehiclesexpandedupgrades | 0 | 16/7 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Vehicle Framework 之后（About 强依赖） |
| 389 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Food Texture Variety - Cannibalism | goat.food.texture.variety.cannibal | 0 | 9/0 | 前置约束：排在 Food Texture Variety Core 之后（About 强依赖） |
| 390 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Food Texture Variety - Children Meals | goat.food.texture.variety.children | 0 | 4/0 | 前置约束：排在 Biotech 之后（About 强依赖）；前置约束：排在 Food Texture Variety Core 之后（About 强依赖） |
| 391 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | The Dead Man's Switch - Synthetic | aoba.deadmanswitch.synthetic | 0 | 19/4 | 前置约束：排在 The Dead Man's Switch 之后（About 强依赖）；前置约束：排在 [NL] Facial Animation - WIP 之后（条件兼容目录） |
| 392 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [OA]Ratkin Scenario: Snowstorm Orphan | oark.ratkinfaction.scenarioexpand.snowstorm | 8 | 162/24 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 [OA]Ratkin Gene Expand 之后（About 强依赖） |
| 393 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Miling Mulberry Shrubs | miho.milingmulberryshrubs | 0 | 1/0 | 前置约束：排在 Miho, the celestial fox 之后（About 强依赖） |
| 394 | — | 新增 | 玩法与扩展 | 未启用 | 2.黄色 (无法通过代码分析判断) | Elite Raid 精英化袭击 | trigger.eliteraid | 14 | 102/0 | [2.黄色] Elite Raid 精英化袭击 与 Mechanoids: Total Warfare 同时对 RimWorld.StorytellerUtility.DefaultThreatPointsNow 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；前置约束：排在 Harmony 之后（About 强依赖） |
| 395 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Almost There! Fork | duz.almosttherefork | 5 | 0/2 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Vehicle Framework 之后（About.loadAfter） |
| 396 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Ratkin Underground+ | rku.ratkinunderground | 30 | 323/16 | 前置约束：排在 NewRatkinPlus 之后（About 强依赖）；前置约束：排在 Ratkin Faction+ 之后（About 强依赖） |
| 397 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | [FSF] Better Exploration Loot | frozensnowfox.betterexplorationloot | 0 | 14/4 | 前置约束：排在 Miho, the celestial fox 之后（About.loadAfter）；前置约束：排在 Ideology 之后（条件兼容目录） |
| 398 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Expanded Prosthetics and Organ Engineering - Forked | vat.epoeforked | 0 | 450/115 | 前置约束：排在 XML Extensions 之后（About.loadAfter）；前置约束：排在 Royalty 之后（条件兼容目录） |
| 399 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Harvest Everything! | ogliss.ykara.harvesteverything | 2 | 1/21 | 前置约束：排在 Expanded Prosthetics and Organ Engineering - Forked 之后（XML Patch 目标 Def） |
| 400 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Death Rattle Continued [1.2+] | troopersmith1.deathrattle | 5 | 10/18 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Expanded Prosthetics and Organ Engineering - Forked 之后（About.loadAfter） |
| 401 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | A Dog Said... Animal Prosthetics 2 | sambucher.adogsaidanimalprosthetics2 | 0 | 83/4 | 前置约束：排在 XML Extensions 之后（About.loadAfter）；前置约束：排在 Expanded Prosthetics and Organ Engineering - Forked 之后（About.loadAfter） |
| 402 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Trains of the Rim | princeofpluto.vehicles.trains | 9 | 71/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Vehicle Framework 之后（About 强依赖） |
| 403 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Log After Def Error (Continued) | mlie.logafterdeferror | 2 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Best Mix (Continued) 之后（DLL 程序集引用） |
| 404 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Auto Ability | mjcg.autoability | 3 | 0/4 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Search and Destroy (Continued) 之后（About.loadAfter） |
| 405 | — | 新增 | 种族与阵营 | 未启用 | 3.红色 (明确冲突) | Raven Race - Embers in Shadow | zuoyao.ravenrace | 399 | 1798/64 | [3.红色] 缺少必需前置模组 RimUnity (chezhou.rimunity)；[2.黄色] Customize Weapon 与 Raven Race - Embers in Shadow 同时对 Verse.PawnRenderUtility.DrawEquipmentAiming 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响 |
| 406 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | Pawn Target Fix | fed1splay.pawntargetfix | 1 | 0/0 | [3.红色] [1.6] CAI 5000 - Advanced AI + Fog Of War (continued) 在 About.xml 中显式声明与 Pawn Target Fix 不兼容；前置约束：排在 Harmony 之后（About 强依赖） |
| 407 | — | 新增 | 玩法与扩展 | 未启用 | 2.黄色 (无法通过代码分析判断) | Safe TechHediffs Patch | kual.safetechhediffs.patch | 3 | 0/0 | [2.黄色] Safe TechHediffs Patch 与 Raven Race - Embers in Shadow 同时对 RimWorld.PawnTechHediffsGenerator.GenerateTechHediffsFor 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；前置约束：排在 Raven Race - Embers in Shadow 之后（Transpiler 顺序启发） |
| 408 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Epona Umamusume Apparel | epona.umamusumeapparel | 0 | 6/0 | 前置约束：排在 Epona Dynastic Rise 之后（About 强依赖） |
| 409 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Miho Hediff Fix | zades.mihohedifffix | 1 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Miho, the celestial fox 之后（About 强依赖） |
| 410 | — | 新增 | 玩法与扩展 | 未启用 | 2.黄色 (无法通过代码分析判断) | Hospitality | orion.hospitality | 76 | 65/2 | [2.黄色] Hospitality 在 RimWorld.Pawn_InteractionsTracker.TryInteractRandomly 上包含返回 false 的接管型 Prefix，需确保 Vanilla Social Interactions Expanded 的前置补丁先执行；[2.黄色] Gloomy Dragonian race 在 RimWorld.ForbidUtility.SetForbidden 上包含返回 false 的接管型 Prefix，需确保 Hospitality 的前置补丁先执行 |
| 411 | — | 新增 | 前置框架 | 未启用 | 3.红色 (明确冲突) | Dubs Bad Hygiene Lite | dubwise.dubsbadhygiene.lite | 18 | 268/13 | [3.红色] Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 在无依赖关系下重复定义了核心 XML Def（ThingDef:PrimitiveWell），后加载者会整块覆盖先加载者；[3.红色] Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 在无依赖关系下重复定义了核心 XML Def（ThingDef:BurnPit），后加载者会整块覆盖先加载者 |
| 412 | — | 新增 | 前置框架 | 未启用 | 3.红色 (明确冲突) | Dubs Bad Hygiene | dubwise.dubsbadhygiene | 18 | 274/13 | [3.红色] Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 在无依赖关系下重复定义了核心 XML Def（ThingDef:PrimitiveWell），后加载者会整块覆盖先加载者；[3.红色] Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 在无依赖关系下重复定义了核心 XML Def（ThingDef:BurnPit），后加载者会整块覆盖先加载者 |
| 413 | — | 新增 | 前置框架 | 未启用 | 2.黄色 (无法通过代码分析判断) | Cinders of the Embergarden | breadmo.cinders | 18 | 621/24 | [2.黄色] NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Cinders of the Embergarden 的前置补丁先执行；[2.黄色] Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Cinders of the Embergarden 的前置补丁先执行 |
| 414 | — | 新增 | 种族与阵营 | 未启用 | 2.黄色 (无法通过代码分析判断) | Glitterworld Destroyer 5 - Mechanoid Addon | fxz.glitterworlddestroyer.mk5 | 91 | 666/164 | [2.黄色] Unlimited Mechanitor Command Range 在 MechanitorUtility.InMechanitorCommandRange 上包含返回 false 的接管型 Prefix，需确保 Glitterworld Destroyer 5 - Mechanoid Addon 的前置补丁先执行；[2.黄色] Unlimited Mechanitor Command Range 在 RimWorld.Pawn_MechanitorTracker.DrawCommandRadius 上包含返回 false 的接管型 Prefix，需确保 Glitterworld Destroyer 5 - Mechanoid Addon 的前置补丁先执行 |
| 415 | — | 新增 | 前置框架 | 未启用 | 1.绿色 (确认无冲突) | Mya's fufu | fxz.myafufu | 0 | 75/2 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Glitterworld Destroyer 5 - Mechanoid Addon 之后（About.loadAfter） |
| 416 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | MoeLotl Faction Expand | hentailoliteam.axolotl.factionexpand | 14 | 111/10 | 前置约束：排在 MoeLotl Race 之后（About 强依赖）；前置约束：排在 RimTalk 之后（守卫 Prefix 顺位） |
| 417 | — | 新增 | 种族与阵营 | 未启用 | 3.红色 (明确冲突) | Mechanoids: Total Warfare | nyar.nclvstw | 18 | 519/56 | [3.红色] Mechanoids: Total Warfare 在 About.xml 中显式声明与 Gunplay 不兼容；[2.黄色] Unlimited Mechanitor Command Range 在 MechanitorUtility.InMechanitorCommandRange 上包含返回 false 的接管型 Prefix，需确保 Mechanoids: Total Warfare 的前置补丁先执行 |
| 418 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Maru Race | vamv.maruracemod | 4 | 737/20 | 前置约束：排在 Humanoid Alien Races 之后（About 强依赖）；前置约束：排在 Harmony 之后（About 强依赖） |
| 419 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Ideology Scavenger Role | amegakull.scvrole | 0 | 35/0 | 前置约束：排在 Ideology 之后（About 强依赖）；前置约束：排在 Mya's fufu 之后（About.loadAfter） |
| 420 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | 美狐扩展派系-帝国狂想曲-美狐第三帝国 | chezhou.kind.mihoadfqs | 23 | 82/13 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Humanoid Alien Races 之后（About 强依赖） |
| 421 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Mincho, The Mint Choco Slime ~ HAR ver. | sutsutman.minchothemintchocoslimeharver | 62 | 413/19 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Humanoid Alien Races 之后（About 强依赖） |
| 422 | 18 | ▼404 | 前置框架 | 已启用 | 1.绿色 (确认无冲突) | Kiiro Race | ancot.kiirorace | 8 | 622/42 | 前置约束：排在 Humanoid Alien Races 之后（About 强依赖）；前置约束：排在 Ancot Library 之后（About 强依赖） |
| 423 | 19 | ▼404 | 前置框架 | 已启用 | 1.绿色 (确认无冲突) | Kiiro Race Gene Patch | ancot.kiiroracegenepatch | 0 | 14/6 | 前置约束：排在 Kiiro Race 之后（About 强依赖） |
| 424 | — | 新增 | 前置框架 | 未启用 | 1.绿色 (确认无冲突) | Kiiro Race - Orange Cat Xenotype (绮罗橘猫异种) | zuoyao.kiiroorangecat | 4 | 3/4 | 前置约束：排在 Biotech 之后（About 强依赖）；前置约束：排在 Kiiro Race 之后（About 强依赖） |
| 425 | 20 | ▼405 | 种族与阵营 | 已启用 | 1.绿色 (确认无冲突) | Kiiro Story: Events Expanded | ancot.kiirostoryeventsexpanded | 8 | 327/6 | 前置约束：排在 Kiiro Race 之后（About 强依赖）；前置约束：排在 Ancot Library 之后（About 强依赖） |
| 426 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Marilyn the Mincho Worshipper Witch ~ HAR ver. | sutsutman.marilyntheminchoworshipperwitchharver | 4 | 95/2 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Royalty 之后（About 强依赖） |
| 427 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Milira Event Story Expand: The Tale of Milira | pakerwot.miliraeventandstortexpandthetaleofmilira | 3 | 79/13 | 前置约束：排在 Milira Race 之后（About 强依赖）；前置约束：排在 瓦莱丽的米莉拉扩展包 之后（About 强依赖） |
| 428 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | 绮罗-背景故事拓展 | mingyue.bao4 | 0 | 55/1 | 前置约束：排在 Humanoid Alien Races 之后（About 强依赖）；前置约束：排在 Kiiro Race 之后（About 强依赖） |
| 429 | — | 新增 | 种族与阵营 | 未启用 | 2.黄色 (无法通过代码分析判断) | Gloomy Dragonian race | rooandgloomy.dragonianracemod | 2 | 118/17 | [2.黄色] Gloomy Dragonian race 在 RimWorld.ForbidUtility.SetForbidden 上包含返回 false 的接管型 Prefix，需确保 Hospitality 的前置补丁先执行；前置约束：排在 Humanoid Alien Races 之后（About 强依赖） |
| 430 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Dragonian Mix | kalospacer.dragonianmix | 11 | 273/58 | 前置约束：排在 Humanoid Alien Races 之后（About 强依赖）；前置约束：排在 Gloomy Dragonian race 之后（About 强依赖） |
| 431 | 21 | ▼410 | 玩法与扩展 | 已启用 | 1.绿色 (确认无冲突) | 绮罗蹭饭扩展 (Kiiro Bum Meals Off Expanded) | ricehaoci.kiirobummealsoffexpanded | 0 | 48/7 | 前置约束：排在 Kiiro Race 之后（About 强依赖）；前置约束：排在 Ancot Library 之后（About 强依赖） |
| 432 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Dubs Central Heating | dubwise.dubscentralheating | 2 | 31/1 | 前置约束：排在 Dubs Bad Hygiene Lite 之后（DLL 程序集引用）；前置约束：排在 Dubs Rimatomics 之后（DLL 程序集引用） |
| 433 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Scar's Basic Farming | scar.basicfarming | 0 | 18/2 | 前置约束：排在 Dubs Bad Hygiene 之后（About.loadAfter） |
| 434 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Dragonian Strange Apparels | rabiosus.dragonianexapparel | 0 | 12/1 | 前置约束：排在 Gloomy Dragonian race 之后（About 强依赖） |
| 435 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Xiaolan'fufu | lan.fufu | 0 | 1/0 | 前置约束：排在 Mya's fufu 之后（About 强依赖） |
| 436 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | The Dead Man's Switch - MobileDragoon | aoba.deadmanswitch.mobiledragoon | 0 | 139/8 | 前置约束：排在 Exosuit Framework 之后（About 强依赖）；前置约束：排在 The Dead Man's Switch 之后（About 强依赖） |
| 437 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Alien Cuisine | linruo.aliencuisine | 0 | 23/0 | 前置约束：排在 Gloomy Dragonian race 之后（About.loadAfter）；前置约束：排在 Yuran race 之后（About.loadAfter） |
| 438 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | The Dead Man's Switch - Control Spine | riko.controlspine | 2 | 11/0 | 前置约束：排在 The Dead Man's Switch 之后（About 强依赖）；前置约束：排在 [NL] Facial Animation - WIP 之后（About.loadAfter） |
| 439 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Cinders_ErodedWeaponsaExtended | asiimoves.cinderserodedweaponsaextended | 0 | 13/0 | 前置约束：排在 Cinders of the Embergarden 之后（About 强依赖）；前置约束：排在 The Dead Man's Switch 之后（About.loadAfter） |
| 440 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Fisher_Cat's Plush Friends - Neuro Universe Vol.1 | fishercat.plushie.neuroverse.vol1 | 0 | 14/0 | 前置约束：排在 Mya's fufu 之后（About 强依赖） |
| 441 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | 鼠族财富扩展-雪铁芋Fufu | thewwworld.ratkinwealthfufu | 0 | 1/0 | 前置约束：排在 Mya's fufu 之后（About 强依赖） |
| 442 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Glitterworld Destroyer 5 Expansion | qiuci.gd5.expansion | 17 | 309/50 | 前置约束：排在 Glitterworld Destroyer 5 - Mechanoid Addon 之后（About 强依赖）；前置约束：排在 Vanilla Expanded Framework 之后（About.loadAfter） |
| 443 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Kiiro Medieval Expansion | chougou.kiiromedievalexpansion | 0 | 13/6 | 前置约束：排在 Ancot Library 之后（About 强依赖）；前置约束：排在 Kiiro Race 之后（About 强依赖） |
| 444 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Loading Screen Repository | ferny.loadingscreenrepository | 0 | 3/0 | 前置约束：排在 Blueprints Forked - 1.6 之后（About.loadAfter）；前置约束：排在 Dubs Bad Hygiene 之后（About.loadAfter） |
| 445 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | 联合重工-龙骑兵拓展 UFHI-MobileDragoon | kindseal.mobiledragoon | 0 | 40/0 | 前置约束：排在 Exosuit Framework 之后（About 强依赖）；前置约束：排在 The Dead Man's Switch - MobileDragoon 之后（About 强依赖） |
| 446 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | 边缘豆腐 | qj.toufucraft | 13 | 274/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 厨间百艺 (Culinary Arts 100) 之后（守卫 Prefix 顺位） |
| 447 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Storyteller:Mia | ialways1ose.mia.storyteller | 87 | 89/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Royalty 之后（About.loadAfter） |
| 448 | — | 新增 | 种族与阵营 | 未启用 | 2.黄色 (无法通过代码分析判断) | MoeLotl: Rigor Mortis | fxz.moelotlzombie.update | 85 | 472/43 | [2.黄色] NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 MoeLotl: Rigor Mortis 的前置补丁先执行；[2.黄色] Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 MoeLotl: Rigor Mortis 的前置补丁先执行 |
| 449 | — | 新增 | 种族与阵营 | 未启用 | 2.黄色 (无法通过代码分析判断) | Draconis Koelime | draconis.koelime | 14 | 88/1 | [2.黄色] MoeLotl: Rigor Mortis 与 Draconis Koelime 同时对 RimWorld.PregnancyUtility.ApplyBirthOutcome 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；[2.黄色] Draconis Koelime 与 Milira Faction: Milira Imperium 同时对 RimWorld.PregnancyUtility.ApplyBirthOutcome 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响 |
| 450 | — | 新增 | 前置框架 | 未启用 | 2.黄色 (无法通过代码分析判断) | Ariandel Library | ariandel.ariandellibrary | 34 | 226/2 | [2.黄色] MoeLotl: Rigor Mortis 与 Ariandel Library 同时对 RimWorld.PregnancyUtility.ApplyBirthOutcome 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；[2.黄色] Draconis Koelime 与 Ariandel Library 同时对 RimWorld.PregnancyUtility.ApplyBirthOutcome 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响 |
| 451 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Dellin's Milira Attire Expaned | dellin.ariandel.miliraattireexpaned | 0 | 62/23 | 前置约束：排在 Ariandel Library 之后（About 强依赖）；前置约束：排在 Milira Race 之后（About 强依赖） |
| 452 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Bill Doors' Frostnova Expanded Unofficial by Haqimi | 3hstltd.whitebunny.unofficial | 18 | 264/6 | 前置约束：排在 Royalty 之后（条件兼容目录）；前置约束：排在 Anomaly 之后（条件兼容目录） |
| 453 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | RimSimManagementFramework边缘模拟经营框架 | chezhou.framework.rimsimmanagementframework | 17 | 156/6 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Kiiro Race 之后（About.loadAfter） |
| 454 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Ancient urban ruins | xmb.ancienturbanruins.mo | 10 | 642/13 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Map Designer 之后（About.loadAfter） |
| 455 | — | 新增 | 玩法与扩展 | 未启用 | 2.黄色 (无法通过代码分析判断) | kemomimihouse HardworkingKz | moo.hardworking.kz | 55 | 39/1 | [2.黄色] Vanilla Expanded Framework 与 kemomimihouse HardworkingKz 同时对 RimWorld.Frame.CompleteConstruction 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；[2.黄色] kemomimihouse HardworkingKz 与 Replace Stuff - Continued 同时对 RimWorld.Frame.CompleteConstruction 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响 |
| 456 | — | 新增 | 种族与阵营 | 未启用 | 2.黄色 (无法通过代码分析判断) | 虫娘 Insect Girls | pupa.insectgirls | 7 | 209/83 | [2.黄色] kemomimihouse Kz 与 虫娘 Insect Girls 在 RimWorld.Toils_LayDown.LayDown 上均存在无守卫的返回值直接赋值，后加载者会覆盖先加载者结果；前置约束：排在 Harmony 之后（About 强依赖） |
| 457 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Melee Animation | co.uk.epicguru.meleeanimation | 21 | 262/2 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Humanoid Alien Races 之后（DLL 程序集引用） |
| 458 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | Ancient amorphous threat | xmb.ancientthreat.mo | 3 | 133/13 | 前置约束：排在 Melee Animation 之后（About.loadAfter）；前置约束：排在 Cinders of the Embergarden 之后（About.loadAfter） |
| 459 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Stack gap | andromeda.stackgap | 22 | 1/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Dubs Rimatomics 之后（DLL 程序集引用） |
| 460 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | CeleTech Arsenal MKIII | tot.celetech.mkiii | 30 | 608/31 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 461 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | CeleTech Faction | ianlee.celetechfaction | 0 | 13/0 | 前置约束：排在 CeleTech Arsenal MKIII 之后（About 强依赖） |
| 462 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | Work Tab | fluffy.worktab | 38 | 3/0 | [3.红色] DragSelect 在 RimWorld.WidgetsWork.DrawWorkBoxFor 上无条件返回 false 跳过原方法，会使 Work Tab 针对原方法体的补丁无法生效；前置约束：排在 Harmony 之后（About 强依赖） |
| 463 | — | 新增 | 种族与阵营 | 未启用 | 1.绿色 (确认无冲突) | [MCW]Miho expand:Blood Moon Temple 美狐神殿拓展 | supermelee.sword | 17 | 223/3 | 前置约束：排在 Miho, the celestial fox 之后（About 强依赖）；前置约束：排在 Humanoid Alien Races 之后（DLL 程序集引用） |
| 464 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | DragSelect | telardo.dragselect | 4 | 0/0 | [3.红色] DragSelect 在 RimWorld.WidgetsWork.DrawWorkBoxFor 上无条件返回 false 跳过原方法，会使 Work Tab 针对原方法体的补丁无法生效；前置约束：排在 Harmony 之后（About 强依赖） |
| 465 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | Replace Stuff - Continued | memegoddess.replacestuff | 81 | 75/11 | [3.红色] Replace Stuff - Continued 在 About.xml 中显式声明与 GloomyFurniture 不兼容；[2.黄色] Vanilla Social Interactions Expanded 与 Replace Stuff - Continued 同时对 RimWorld.Mineable.TrySpawnYield 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响 |
| 466 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Extra Armor For Cinder | cn.amorphousarmor | 3 | 6/6 | 前置约束：排在 Cinders of the Embergarden 之后（About 强依赖）；前置约束：排在 Ancient amorphous threat 之后（About.loadAfter） |
| 467 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | More Useful Injection | cn.moreusefulinjection | 7 | 33/1 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 RimTalk 之后（守卫 Prefix 顺位） |
| 468 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | 绮罗拓展：动力装甲 | es.kiirorace.powerarmor | 0 | 21/9 | 前置约束：排在 Kiiro Race 之后（About 强依赖）；前置约束：排在 Fortified Features Framework 之后（XML Patch 目标 Def） |
| 469 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | MiliraKiiroCuddle | kogasastudio.milirakiirocuddle | 0 | 17/0 | 前置约束：排在 Kiiro Race 之后（About 强依赖）；前置约束：排在 Milira Race 之后（About.loadAfter） |
| 470 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | XiaoLan’s Magic House Fufu | hailuan.lan.fufu | 0 | 10/0 | 前置约束：排在 Mya's fufu 之后（About 强依赖） |
| 471 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Kiiro Catmint Food Expanded | pikajo.kiirocatmintfoodexpend | 0 | 28/0 | 前置约束：排在 Ancot Library 之后（About 强依赖）；前置约束：排在 Kiiro Race 之后（About 强依赖） |
| 472 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | 通用义体兼容 (Universal Implant Compatibility) | zuoyao.universalimplantcompat | 17 | 0/0 | 前置约束：排在 Biotech 之后（About 强依赖）；前置约束：排在 Character Editor 之后（动态反射修补） |
| 473 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Kiiro Race - Maine Coon Xenotype (绮罗缅因异种) | zuoyao.kiiromainecoon | 8 | 3/6 | 前置约束：排在 Biotech 之后（About 强依赖）；前置约束：排在 Kiiro Race 之后（About 强依赖） |
| 474 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Melee Animation Patch: Revenge Blade - Execution Counter Kill | xt489.executioncounterkill | 0 | 2/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Melee Animation 之后（About.loadAfter） |
| 475 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | RimSMF HAR Addon | pikajo.rimsmframework.haraddon | 0 | 44/29 | 前置约束：排在 RimSimManagementFramework边缘模拟经营框架 之后（About 强依赖）；前置约束：排在 Kiiro Race 之后（条件兼容目录） |
| 476 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | 菜篮子工程-外卖扩展 | shenpc.vegetablebasketproject.chefmeowstall | 2 | 3/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 RimSimManagementFramework边缘模拟经营框架 之后（About 强依赖） |
| 477 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Dubs Skylights | dubwise.dubsskylights | 8 | 16/0 | 前置约束：排在 Vanilla Expanded Framework 之后（守卫 Prefix 顺位） |
| 478 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | Better Architect Menu | ferny.betterarchitect | 16 | 104/309 | [3.红色] Better Architect Menu 在 RimWorld.ArchitectCategoryTab.DesignationTabOnGUI 上无条件返回 false 跳过原方法，会使 Architect Menu Optimizer 针对原方法体的补丁无法生效；前置约束：排在 Harmony 之后（About 强依赖） |
| 479 | 34 | ▼445 | 渲染与动画 | 已启用 | 1.绿色 (确认无冲突) | Kiiro Race Facial Animation | ancot.kiirofacialanimation | 0 | 15/3 | 前置约束：排在 [NL] Facial Animation - WIP 之后（About 强依赖）；前置约束：排在 Kiiro Race 之后（About 强依赖） |
| 480 | — | 新增 | 种族与阵营 | 未启用 | 2.黄色 (无法通过代码分析判断) | Milira Faction: Milira Imperium | ariandel.miliraimperium | 61 | 1166/93 | [2.黄色] Draconis Koelime 与 Milira Faction: Milira Imperium 同时对 RimWorld.PregnancyUtility.ApplyBirthOutcome 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响；前置约束：排在 Milira Race 之后（About 强依赖） |
| 481 | — | 新增 | 种族与阵营 | 未启用 | 3.红色 (明确冲突) | RimWorld Together | nova.rimworldtogether | 65 | 14/0 | [3.红色] RimWorld Together 在 About.xml 中显式声明与 RimSaves 不兼容；[2.黄色] RimWorld Together 在 Verse.GameDataSaveLoader.SaveGame 上包含返回 false 的接管型 Prefix，需确保 RimTalk 的前置补丁先执行 |
| 482 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | RimWorld Together_zh | lan.rimworldtogether.zh | 0 | 0/0 | 前置约束：排在 RimWorld Together 之后（About.loadAfter） |
| 483 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Kiiro Race - Siamese Xenotype (绮罗暹罗异种) | zuoyao.kiirosiamese | 4 | 6/4 | 前置约束：排在 Biotech 之后（About 强依赖）；前置约束：排在 Kiiro Race 之后（About 强依赖） |
| 484 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Milira RimSMFramework Expand | milira.rimsmframework.expand | 0 | 7/11 | 前置约束：排在 Milira Race 之后（About 强依赖）；前置约束：排在 RimSimManagementFramework边缘模拟经营框架 之后（About 强依赖） |
| 485 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Kiiro Race - Ragdoll Xenotype (绮罗布偶异种) | zuoyao.kiiroragdoll | 5 | 7/5 | 前置约束：排在 Biotech 之后（About 强依赖）；前置约束：排在 Kiiro Race 之后（About 强依赖） |
| 486 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Hyacinth's Ratkin Facial Animation | fxz.ratkin.facialanimation | 0 | 94/3 | 前置约束：排在 [NL] Facial Animation - WIP 之后（About 强依赖）；前置约束：排在 NewRatkinPlus 之后（About 强依赖） |
| 487 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Mincho FacialAnime | talos.mincho.fa | 0 | 93/7 | 前置约束：排在 [NL] Facial Animation - WIP 之后（About 强依赖）；前置约束：排在 Mincho, The Mint Choco Slime ~ HAR ver. 之后（About 强依赖） |
| 488 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Miho Facial Animation | talos.miho.face.fa | 0 | 120/7 | 前置约束：排在 [NL] Facial Animation - WIP 之后（About 强依赖）；前置约束：排在 Miho, the celestial fox 之后（About 强依赖） |
| 489 | — | 新增 | 种族与阵营 | 未启用 | 3.红色 (明确冲突) | [SRC]Miho,Star Ring Corporation | src.core.markeazzyh | 30 | 966/35 | [3.红色] Unlimited Mechanitor Command Range 在 RimWorld.Pawn_MechanitorTracker.DrawCommandRadius 上无条件返回 false 跳过原方法，会使 [SRC]Miho,Star Ring Corporation 针对原方法体的补丁无法生效；[2.黄色] [SRC]Miho,Star Ring Corporation 在 RimWorld.Pawn_MechanitorTracker.DrawCommandRadius 上包含返回 false 的接管型 Prefix，需确保 Glitterworld Destroyer 5 - Mechanoid Addon 的前置补丁先执行 |
| 490 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | Unlimited Mechanitor Command Range | swwu.mechanitorcommandrange | 4 | 0/0 | [3.红色] Unlimited Mechanitor Command Range 在 RimWorld.Pawn_MechanitorTracker.DrawCommandRadius 上无条件返回 false 跳过原方法，会使 [SRC]Miho,Star Ring Corporation 针对原方法体的补丁无法生效；[2.黄色] Unlimited Mechanitor Command Range 在 MechanitorUtility.InMechanitorCommandRange 上包含返回 false 的接管型 Prefix，需确保 Rim Girls' Frontline 的前置补丁先执行 |
| 491 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Maru Facial Animation Continued | marufa.continued | 0 | 16/3 | 前置约束：排在 [NL] Facial Animation - WIP 之后（About 强依赖）；前置约束：排在 Maru Race 之后（About 强依赖） |
| 492 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Gloomy Dragonian Facial Animation | talos.dragonianfacialanimation | 0 | 84/5 | 前置约束：排在 [NL] Facial Animation - WIP 之后（About 强依赖）；前置约束：排在 Gloomy Dragonian race 之后（About 强依赖） |
| 493 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Epona Facial Animation（Dynastic Rise） | epona.eponadynasticrisefacial | 0 | 238/25 | 前置约束：排在 [NL] Facial Animation - WIP 之后（About 强依赖）；前置约束：排在 Epona Dynastic Rise 之后（About 强依赖） |
| 494 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Tailin_Face_Animation | tailin.face.fa | 0 | 9/2 | 前置约束：排在 [NL] Facial Animation - WIP 之后（About 强依赖）；前置约束：排在 tailin race1.2.2 之后（About.loadAfter） |
| 495 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Draconis Koelime Facial Animation | draconis.koelime.fa | 0 | 101/2 | 前置约束：排在 [NL] Facial Animation - WIP 之后（About 强依赖）；前置约束：排在 Draconis Koelime 之后（About 强依赖） |
| 496 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Show Me Your Hands | mlie.showmeyourhands | 4 | 187/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Best Mix (Continued) 之后（DLL 程序集引用） |
| 497 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Ratkin Ear and Tail Animation | aota.ratkin.ear.tail.animation | 5 | 2/1 | 前置约束：排在 Biotech 之后（About 强依赖）；前置约束：排在 Harmony 之后（About 强依赖） |
| 498 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Alpha Animals_zh | qaq.qaaaq | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 499 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Optional Icons for Architect Icons | proxyer.optionalicons4ai | 0 | 0/0 | 前置约束：排在 Architect Icons 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 500 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Misc._zh_pack | rwzh.chinesepack.miscellaneous | 0 | 0/0 | 前置约束：排在 Misc. Training 之后（About.loadAfter） |
| 501 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Dubs Bad Hygiene - Thirst | dubwise.dubsbadhygiene.thirst | 0 | 0/0 | 前置约束：排在 Dubs Bad Hygiene Lite 之后（About 强依赖） |
| 502 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Dubs Bad Hygiene zh-pack | rwzh.chinesepack.dubsbadhygiene | 0 | 0/0 | 前置约束：排在 Dubs Bad Hygiene 之后（About.loadAfter）；前置约束：排在 Dubs Bad Hygiene Lite 之后（About.loadAfter） |
| 503 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Vanilla Hair Retextured | neronix17.retexture.vanillahair | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 504 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Vanilla Beards Retextured | neronix17.retexture.vanillabeards | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 505 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | [NL] Realistic Body | nals.realisticbody | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 506 | — | 新增 | 玩法与扩展 | 未启用 | 1.绿色 (确认无冲突) | Hooman Cute Reborn | lanyv.hoomancutereborn | 0 | 2/4 | 前置约束：排在 [NL] Realistic Body 之后（About.loadAfter） |
| 507 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Miho, the celestial fox_zh | zh.miho.fortifiedoutremer | 0 | 0/0 | 前置约束：排在 Miho, the celestial fox 之后（About.loadAfter） |
| 508 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Not My Fault | vesper.notmyfault | 0 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 509 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Floordrawings | tials.floordrawings | 0 | 0/0 | 前置约束：排在 Biotech 之后（About 强依赖） |
| 510 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Go Away Eye Strain! | oppey.goawayeyestrain | 0 | 0/0 | 前置约束：排在 Core 之后（About.loadAfter） |
| 511 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Moyo-From the depth_zh | zh.nemonian.my | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 512 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | [TW1.7]堂丸贴图重置~原材料 TangW's~Retexture~Resource | tw.tangs.retexture.resource | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 513 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | [TW1.7]堂丸贴图重置~脸型 TangW's~Retexture~Face | tw.tangs.retexture.face.scar | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 514 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | [TW1.7]堂丸贴图重置~制成品 TangW's~Retexture~Manufactured | tw.tangs.retexture.manufactured | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 515 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Won Hair Men Retextured | neronix17.retexture.wonhairmen | 0 | 0/0 | 前置约束：排在 Won hair_men 之后（About 强依赖） |
| 516 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Won Hair Women Retextured | neronix17.retexture.wonhairwomen | 0 | 0/0 | 前置约束：排在 Won hair_women 之后（About 强依赖） |
| 517 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | [TW1.6]堂丸贴图重置~UI Tang's~Retexture~UI | tw.tangs.retexture.ui | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 518 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | [TW1.6]堂丸贴图重置~结构 Tang's~Retexture~Structure | tw.tangs.retexture.structure | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 519 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | [TW1.7]堂丸贴图重置~体型 TangW's~Retexture~Body | tw.tangs.retexture.body | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 520 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | JustStoneTile | tiagocc0.juststonetile | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 521 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | No Gray Eyes | jijiang.eyes | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 522 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | No Red Eyes | jijiang.red.eyes | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 523 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | [TW1.6]堂丸贴图重置~服饰 Tang's~Retexture~Apparel | tw.tangs.retexture.apparel | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 524 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Moyo2.0-Blood stained business_zh | zh.mzmgow.nemonian.my2 | 0 | 0/0 | 前置约束：排在 Moyo2.0-Blood stained business 之后（About.loadAfter） |
| 525 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Show Me Your Eyes | novsole.showmeyoureyes.ratkin | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 526 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | [TW1.7]雪人贴图重置 Retexture~Snow man | tw.tangs.retexture.snowman | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 527 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Hooman Cute Reborn Extra Mod Support | lanyv.hoomancuterebornextramodsupport | 0 | 0/0 | 前置约束：排在 Hooman Cute Reborn 之后（About 强依赖）；前置约束：排在 Miho, the celestial fox 之后（About.loadAfter） |
| 528 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Moenoid 512x512 | moenoid512x512.mechanoid.retexture | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 529 | — | 新增 | 渲染与动画 | 未启用 | 3.红色 (明确冲突) | Layered Wall Destruction - Additional Patches | seromas.layereddestruction.patches | 0 | 0/0 | [3.红色] 检测到硬性排序循环依赖: Layered Wall Destruction (keshash.layeredwalldestruction) → Layered Wall Destruction - Additional Patches (seromas.layereddestruction.patches)，已断开该边以保证排序可解（Layered Wall Destruction 声明 loadBefore Layered Wall Destruction - Additional Patches；Layered Wall Destruction - Additional Patches 强依赖前置 Layered Wall Destruction；Layered Wall Destruction - Additional Patches 声明 loadAfter Layered Wall Destruction）；前置约束：排在 Core 之后（About.loadAfter） |
| 530 | — | 新增 | 玩法与扩展 | 未启用 | 3.红色 (明确冲突) | Layered Wall Destruction | keshash.layeredwalldestruction | 2 | 16/2 | [3.红色] 检测到硬性排序循环依赖: Layered Wall Destruction (keshash.layeredwalldestruction) → Layered Wall Destruction - Additional Patches (seromas.layereddestruction.patches)，已断开该边以保证排序可解（Layered Wall Destruction 声明 loadBefore Layered Wall Destruction - Additional Patches；Layered Wall Destruction - Additional Patches 强依赖前置 Layered Wall Destruction；Layered Wall Destruction - Additional Patches 声明 loadAfter Layered Wall Destruction）；前置约束：排在 Harmony 之后（About 强依赖） |
| 531 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Better Architect Menu 汉化 | cc.chinese.rimworld.betterarchitectmenu | 0 | 0/0 | 前置约束：排在 Better Architect Menu 之后（About.loadAfter） |
| 532 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Select One | vortex.selectone | 0 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Core 之后（About.loadAfter） |
| 533 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | Beautiful Outdoors | meltup.beautifuloutdoors | 0 | 0/93 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 534 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | Realistic Rooms Rewritten | lucifer.realisticrooms | 0 | 0/3 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 535 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | Rabbie Facial Animation | hourai.rabbiefacialanimation | 0 | 0/1 | 前置约束：排在 [NL] Facial Animation - WIP 之后（About 强依赖）；前置约束：排在 [NL] Facial Animation - Experimentals 之后（About 强依赖） |
| 536 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | Buildable Blood torches | aludisnhg.buildbloodtorch | 0 | 0/2 | 前置约束：排在 Biotech 之后（About.loadAfter） |
| 537 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | Childrens Drawings | zylle.childrensdrawings | 0 | 0/1 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 538 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | Floors Are (Absolutely) Worthless | hawkwood.floorsarealmostworthless | 0 | 0/3 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 539 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | [TW1.7]堂丸贴图重置~武器 TangW's~Retexture~Weapons | tw.tangs.retexture.weapons | 0 | 0/20 | 前置约束：排在 Glitterworld Destroyer 5 - Mechanoid Addon 之后（XML Patch 目标 Def）；前置约束：排在 Mechanoids: Total Warfare 之后（XML Patch 目标 Def） |
| 540 | — | 新增 | 纯 XML 补丁 | 未启用 | 3.红色 (明确冲突) | [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods | tw.tangs.retexture.foods | 0 | 0/19 | [3.红色] Vanilla Textures Expanded 与 [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突 |
| 541 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | [TW1.7]堂丸贴图重置~植物 TangW's~Retexture~Plants | tw.tangs.retexture.plants | 0 | 0/18 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 542 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | [Patch]Chips No HitPoints|芯片无耐久度 | biotech.patch.chip.wwskyon | 0 | 0/3 | 前置约束：排在 Biotech 之后（About 强依赖）；前置约束：排在 The Dead Man's Switch 之后（About.loadAfter） |
| 543 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | Food Texture Variety - Nutrient Paste | goat.food.texture.variety.paste | 0 | 0/3 | 前置约束：排在 Food Texture Variety Core 之后（About 强依赖）；前置约束：排在 Vanilla Textures Expanded 之后（About.loadAfter） |
| 544 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | Food Texture Variety - Beer | goat.food.texture.variety.beer | 0 | 0/3 | 前置约束：排在 Food Texture Variety Core 之后（About 强依赖）；前置约束：排在 Vanilla Textures Expanded 之后（About.loadAfter） |
| 545 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | Food Texture Variety - Chocolate | goat.food.texture.variety.chocolate | 0 | 0/4 | 前置约束：排在 Food Texture Variety Core 之后（About 强依赖）；前置约束：排在 Vanilla Textures Expanded 之后（About.loadAfter） |
| 546 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | Food Texture Variety - Babies | goat.food.texture.variety.babies | 0 | 0/3 | 前置约束：排在 Biotech 之后（About 强依赖）；前置约束：排在 Food Texture Variety Core 之后（About 强依赖） |
| 547 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | Food Texture Variety - Replacement | goat.food.texture.variety.replacement | 0 | 0/29 | 前置约束：排在 Food Texture Variety Core 之后（About 强依赖）；前置约束：排在 Vanilla Textures Expanded 之后（About.loadAfter） |
| 548 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | Ponpeco Furnitures : School Desk | ponpeco.pfkschooldesk | 0 | 0/2 | 前置约束：排在 Biotech 之后（About 强依赖）；前置约束：排在 Vanilla Textures Expanded 之后（About.loadAfter） |
| 549 | — | 新增 | 渲染与动画 | 未启用 | 1.绿色 (确认无冲突) | Ponpeco Furnitures_zh | zh.mzmgow.ponpeco.pf | 0 | 0/0 | 前置约束：排在 Ponpeco Furnitures : Kids' Room 之后（About.loadAfter）；前置约束：排在 Ponpeco Furnitures : School Desk 之后（About.loadAfter） |
| 550 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | sunflowers Can Be Grown In Hydroponics | sunflowers.can.be.grown.in.hydroponics | 0 | 0/1 | 前置约束：排在 Miho, the celestial fox 之后（About.loadAfter） |
| 551 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | [ZY]去除仿生眼贴图 | zzty.mod4 | 0 | 0/2 | 前置约束：排在 Core 之后（About.loadAfter） |
| 552 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | Milians Can't Resist EMP | milirarace.emppatch | 0 | 0/2 | 前置约束：排在 Milira Race 之后（About 强依赖） |
| 553 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | Mountains Are Beautiful | fearl.mountainsarebeautiful | 0 | 0/12 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 554 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | Epona Kemonomimi+ | epona.kemonomimi | 0 | 0/18 | 前置约束：排在 Epona Dynastic Rise 之后（About 强依赖） |
| 555 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | Lighter Chunks | aspctt.lighterchunks | 0 | 0/5 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 556 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | Fleshbeast Colony 血饲殖群 简体中文汉化 | fb.cx.zh | 0 | 0/1 | 前置约束：排在 Fleshbeast Colony 之后（About 强依赖） |
| 557 | — | 新增 | 纯 XML 补丁 | 未启用 | 1.绿色 (确认无冲突) | [FSF] No Default Shelf Storage | frozensnowfox.nodefaultshelfstorage | 0 | 0/20 | 前置约束：排在 Adaptive Storage Framework 之后（About.loadAfter） |
| 558 | 16 | ▼542 | 语言汉化包 | 已启用 | 1.绿色 (确认无冲突) | [NL] Facial Animation - WIP-zh | day29zh.facialanimation | 0 | 0/0 | 前置约束：排在 [NL] Facial Animation - WIP 之后（About.loadAfter） |
| 559 | 29 | ▼530 | 语言汉化包 | 已启用 | 1.绿色 (确认无冲突) | Dubs Performance Analyzer_zh | zh.dubwise.dubsperformanceanalyzer.steam | 0 | 0/0 | 前置约束：排在 Dubs Performance Analyzer 之后（About 强依赖） |
| 560 | 30 | ▼530 | 语言汉化包 | 已启用 | 1.绿色 (确认无冲突) | Interaction Bubbles_zh | jaxe.bubbles.zh.hc | 0 | 0/0 | 前置约束：排在 Interaction Bubbles 之后（About.loadAfter） |
| 561 | 32 | ▼529 | 语言汉化包 | 已启用 | 1.绿色 (确认无冲突) | Yayo's Animation_zh | zh.com.yayo.yayoani | 0 | 0/0 | 前置约束：排在 Yayo's Animation (Continued) 之后（About.loadAfter） |
| 562 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Range Finder zh-pack | rwzh.chinesepack.rangefinder | 0 | 0/0 | 前置约束：排在 Range Finder 之后（About.loadAfter） |
| 563 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Humanoid Alien Races zh-pack | rwzh.chinesepack.humanoidalienraces | 0 | 0/0 | 前置约束：排在 Humanoid Alien Races 之后（About.loadAfter） |
| 564 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | HugsLib zh-pack | rwzh.chinesepack.hugslib | 0 | 0/0 | 前置约束：排在 HugsLib 之后（About.loadAfter） |
| 565 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Go Explore! zh-pack | rwzh.chinesepack.goexplore | 0 | 0/0 | 前置约束：排在 Go Explore! 之后（About.loadAfter） |
| 566 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Alpha Biomes_zh | qa.qqijq | 0 | 0/0 | 前置约束：排在 Alpha Biomes 之后（About.loadAfter）；前置约束：排在 Odyssey 之后（条件兼容目录） |
| 567 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | 详细菜单 汉化 Dubs Mint Menus | xun.dubsmintmenus | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 568 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | RimHUD_zh | shouer.chinesepack.rimhud | 0 | 0/0 | 前置约束：排在 RimHUD 之后（About.loadAfter） |
| 569 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Vanilla Events Expanded zh-pack | rwzh.chinesepack.vanillaexpandedvee | 0 | 0/0 | 前置约束：排在 Vanilla Events Expanded 之后（About.loadAfter） |
| 570 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Damage Indicators [1.7] | caesarv6.damageindicators | 0 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 571 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Allow Tool zh-pack | rwzh.chinesepack.allowtool | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 572 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Expanded Prosthetics and Organ Engineering - Forked zh-pack | rwzh.leafzxg.epoeforked | 0 | 0/0 | 前置约束：排在 Expanded Prosthetics and Organ Engineering - Forked 之后（About.loadAfter） |
| 573 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Vanilla Expanded Framework zh-pack | rwzh.chinesepack.vanillafactionsexpandedcore | 0 | 0/0 | 前置约束：排在 Vanilla Expanded Framework 之后（About.loadAfter） |
| 574 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Hospitality zh-pack | rwzh.chinesepack.hospitality | 0 | 0/0 | 前置约束：排在 Hospitality 之后（About.loadAfter） |
| 575 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | OgreStack zh-pack | rwzh.chinesepack.ogrestack | 0 | 0/0 | 前置约束：排在 OgreStack 之后（About.loadAfter） |
| 576 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Vanilla Textures Expanded zh-pack | rwzh.chinesepack.vtexe | 0 | 0/0 | 前置约束：排在 Vanilla Textures Expanded 之后（About.loadAfter） |
| 577 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | GloomyFurniture zh-pack | rwzh.chinesepack.furniturebase | 0 | 0/0 | 前置约束：排在 GloomyFurniture 之后（About.loadAfter） |
| 578 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Map Designer zh-pack | rwzh.chinesepack.mapdesigner | 0 | 0/0 | 前置约束：排在 Map Designer 之后（About.loadAfter） |
| 579 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Yayo's Combat 3 zh pack | shavius.yayocombat3.zh | 0 | 0/0 | 前置约束：排在 Yayo's Combat 3 (Continued) 之后（About.loadAfter）；前置约束：排在 Core 之后（About.loadAfter） |
| 580 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Sparkling Worlds - Full Mod zh-pack | rwzh.chinesepack.sparklingworldsfull | 0 | 0/0 | 前置约束：排在 Sparkling Worlds - Full Mod 之后（About.loadAfter） |
| 581 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Rimpedia zh pack | shavius.rimpedia.zh | 0 | 0/0 | 前置约束：排在 Rimpedia 之后（About.loadAfter）；前置约束：排在 Core 之后（About.loadAfter） |
| 582 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | RimSaves zh-pack | rwzh.chinesepack.rimsaves | 0 | 0/0 | 前置约束：排在 RimSaves 之后（About.loadAfter） |
| 583 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Standalone Hot Spring zh-pack | rwzh.chinesepack.standalonehotspring | 0 | 0/0 | 前置约束：排在 Standalone Hot Spring 之后（About.loadAfter） |
| 584 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Snap Out! zh-pack | rwzh.chinesepack.snapout | 0 | 0/0 | 前置约束：排在 Snap Out! 之后（About.loadAfter） |
| 585 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Best Mix中文化 | chinese.bestmix.20200831 | 0 | 0/0 | 前置约束：排在 Best Mix (Continued) 之后（About 强依赖） |
| 586 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Dubs Mint Minimap zh-pack | rwzh.chinesepack.dubsmintminimap | 0 | 0/0 | 前置约束：排在 Dubs Mint Minimap 之后（About.loadAfter） |
| 587 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Dubs Rimatomics zh-pack | rwzh.chinesepack.rimatomics | 0 | 0/0 | 前置约束：排在 Dubs Rimatomics 之后（About.loadAfter） |
| 588 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Dub's Paint Shop zh-pack | rwzh.chinesepack.dubspaintshop | 0 | 0/0 | 前置约束：排在 Dub's Paint Shop 之后（About.loadAfter） |
| 589 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Dubs Mint Menus zh-pack | rwzh.chinesepack.dubsmintmenus | 0 | 0/0 | 前置约束：排在 Dubs Mint Menus 之后（About.loadAfter） |
| 590 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Melee Weapons: Speed and Damage zh-pack | rwzh.chinesepack.mwsad | 0 | 0/0 | 前置约束：排在 Melee Weapons: Speed and Damage 之后（About.loadAfter） |
| 591 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Pawn Education zh-pack | rwzh.chinesepack.pawneducation | 0 | 0/0 | 前置约束：排在 Pawn Education (Continued) 之后（About.loadAfter） |
| 592 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Vanilla Social Interactions Expanded zh-pack | rwzh.chinesepack.vanillasocialinteractionsexpanded | 0 | 0/0 | 前置约束：排在 Vanilla Social Interactions Expanded 之后（About.loadAfter） |
| 593 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Better Pod Launcher (Continued)繁簡漢化 | oninotamasi.mlie.betterpodlauncher.zh | 0 | 0/0 | 前置约束：排在 Better Pod Launcher (Continued) 之后（About.loadAfter） |
| 594 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Mincho, The Mint Choco Slime ~ HAR ver.中文化語言包 | mincho.har.cht.chs | 0 | 0/0 | 前置约束：排在 Mincho, The Mint Choco Slime ~ HAR ver. 之后（About 强依赖）；前置约束：排在 Royalty 之后（条件兼容目录） |
| 595 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Dev In Your Language_zh | latta.devl10n.zh | 0 | 0/0 | 前置约束：排在 Dev In Your Language 之后（About 强依赖） |
| 596 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Marilyn the Mincho Worshipper Witch ~ HAR ver.中文化語言包 | marilyn.the.mincho.worshipper.witch.har.ver.cht.chs | 0 | 0/0 | 前置约束：排在 Marilyn the Mincho Worshipper Witch ~ HAR ver. 之后（About 强依赖） |
| 597 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Down For Me_zh | arandomkiwi.downforme.zh.hc | 0 | 0/0 | 前置约束：排在 Down For Me 之后（About.loadAfter） |
| 598 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | What's That Mod_zh | co.uk.epicguru.whatsthatmod.zh.hc | 0 | 0/0 | 前置约束：排在 What's That Mod 之后（About.loadAfter） |
| 599 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Damage Indicators [1.6]_zh | caesarv6.damageindicators.zh.hc | 0 | 0/0 | 前置约束：排在 Damage Indicators [1.7] 之后（About.loadAfter） |
| 600 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | [CAT] Show Hair With Hats or Hide All Hats_zh | cat2002.showhair.zh.hc | 0 | 0/0 | 前置约束：排在 [CAT] Show Hair With Hats or Hide All Hats 之后（About.loadAfter） |
| 601 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Geological Landforms_zh | zh.m00nl1ght.geologicallandforms | 0 | 0/0 | 前置约束：排在 Geological Landforms 之后（About.loadAfter） |
| 602 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Trait Rarity Colors简体中文汉化包 | zh.traitraritycolors | 0 | 0/0 | 前置约束：排在 Trait Rarity Colors 之后（About.loadAfter） |
| 603 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | [RH2] Rimdeed® 简繁中文汉化 | asavikle.rimdeed.moujia.cn | 0 | 0/0 | 前置约束：排在 [RH2] Rimdeed® - Pawn Recruitment 之后（About.loadAfter） |
| 604 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Stuff List繁簡漢化 | oninotamasi.mlie.stufflist.zh | 0 | 0/0 | 前置约束：排在 Stuff List (Continued) 之后（About.loadAfter） |
| 605 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Map Preview_zh | zh.m00nl1ght.mappreview | 0 | 0/0 | 前置约束：排在 Map Preview 之后（About.loadAfter） |
| 606 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Moyo-The cartel arrives_zh | zh.nemonian.mycartel | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 607 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Choice Of Psycasts_zh | ggboy.choiceofpsycasts.zh | 0 | 0/0 | 前置约束：排在 Choice Of Psycasts 之后（About.loadAfter） |
| 608 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Yuran race_zh | zh.rooandgloomy.yuranracemod | 0 | 0/0 | 前置约束：排在 Yuran race 之后（About.loadAfter） |
| 609 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Food Poisoning Cures_zh | zh.crocodil.foodpoisoningcures | 0 | 0/0 | 前置约束：排在 Food Poisoning Cures 之后（About.loadAfter） |
| 610 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | What's Today's Menu 汉化 | whatstodaysmenu.zh | 0 | 0/0 | 前置约束：排在 What's Today's Menu 之后（About.loadAfter） |
| 611 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Mallow's Gloves 自由个人汉化 | ziyou.chinesetranslation.mallowgloves | 0 | 0/0 | 前置约束：排在 Mallow's Gloves (Re-Upload) 之后（About 强依赖） |
| 612 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Diagonal Walls 2_zh | chv.diagonalwalls2.zh.hc | 0 | 0/0 | 前置约束：排在 Diagonal Walls 2 之后（About.loadAfter） |
| 613 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Harvest Everything CHS | tinygrox.chs.harvesteverything | 0 | 0/0 | 前置约束：排在 Harvest Everything! 之后（About 强依赖） |
| 614 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Graphics Settings CHS | tinygrox.chs.graphicssettings | 0 | 0/0 | 前置约束：排在 Graphics Settings+ 之后（About.loadAfter） |
| 615 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Dubs Performance Analyzer ru zh pl | dubwise.dubsperformanceanalyzer.steam.ru | 0 | 0/0 | 前置约束：排在 Dubs Performance Analyzer 之后（About 强依赖） |
| 616 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | EndlessGrowth_zh | endlessgrowth.zh | 0 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 617 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | NewRatkinPlus_zh | zh.new.solaris.ratkinracemod | 0 | 0/0 | 前置约束：排在 NewRatkinPlus 之后（About.loadAfter）；前置约束：排在 NewRatkinPlus[1.6] 之后（About.loadAfter） |
| 618 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | [Kit] Graze up_zh | grazeup.zh | 0 | 0/0 | 前置约束：排在 [Kit] Graze up 之后（About.loadAfter） |
| 619 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Work_Tab_zh | rwzh.leafzxg.worktab | 0 | 0/0 | 前置约束：排在 Work Tab 之后（About.loadAfter） |
| 620 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Music_Manager_zh | rwzh.leafzxg.musicmanager | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 621 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Tilled Soil_zh | zh.gt.sam.tilledsoil | 0 | 0/0 | 前置约束：排在 Tilled Soil 之后（About 强依赖） |
| 622 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Mid-saver Saver_zh | chinesepackage.owlchemist.midsaversaver | 0 | 0/0 | 前置约束：排在 Mid-saver Saver 之后（About.loadAfter） |
| 623 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Rabbie The Moonrabbit - 简繁汉化包(1.6) | dr.rabbiechinesepatch | 0 | 0/0 | 前置约束：排在 Rabbie The Moonrabbit race 之后（About 强依赖） |
| 624 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | [RH2] Faction: V.O.I.D. - 简繁汉化 | dr.voidchinesepatch | 0 | 0/0 | 前置约束：排在 [RH2] Faction: V.O.I.D. 之后（About.loadAfter） |
| 625 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Melee Animation_zh | zh.co.uk.epicguru.meleeanimation | 0 | 0/0 | 前置约束：排在 Melee Animation 之后（About.loadAfter） |
| 626 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Gloomy Dragonian race中文語言包 | rooandgloomy.dragonianracemod.cht.chs | 0 | 0/0 | 前置约束：排在 Gloomy Dragonian race 之后（About 强依赖） |
| 627 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Door Mat [zht+zh] | spirit.doormat.zht | 0 | 0/0 | 前置约束：排在 Door Mat 之后（About 强依赖） |
| 628 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Vehicle Framework zh pack | shavius.vehicleframework.zh | 0 | 0/0 | 前置约束：排在 Vehicle Framework 之后（About.loadAfter） |
| 629 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Layered Wall Destruction_zh | zh.keshash.layeredwalldestruction | 0 | 0/0 | 前置约束：排在 Layered Wall Destruction 之后（About.loadAfter）；前置约束：排在 Layered Wall Destruction - Additional Patches 之后（About.loadAfter） |
| 630 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Adaptive Storage_zh | zh.adaptive.storage.neolithic | 0 | 0/0 | 前置约束：排在 Adaptive Storage Framework 之后（About.loadAfter）；前置约束：排在 Adaptive Simple Storage 之后（About.loadAfter） |
| 631 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | [QW]房间统计数据染色 | qw.coloredroomstats | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 632 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | 鼠族名称扩展RatkinNamePlus | thewwworld.ratkinnameplus | 0 | 0/0 | 前置约束：排在 NewRatkinPlus 之后（About.loadAfter） |
| 633 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Get Out Of My Chair!_zh | getoutofmychair.zh | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 634 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Scar's Basic Farming_zh | basicfarming.zh.lan | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 635 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Dynamic Trade Interface_zh | dynamictradeinterface.zh | 0 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 636 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Anty the war ant race蚁族新简体中文汉化包 | zh.antyracemod | 0 | 0/0 | 前置约束：排在 Anty the war ant race 之后（About.loadAfter） |
| 637 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Stack gap-堆叠间隙&简中文补丁 | chinesepackage.andromeda.stackgap | 0 | 0/0 | 前置约束：排在 Stack gap 之后（About.loadAfter） |
| 638 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Basic Double Doors基础大门汉化 | gt.sam.basicdoubledoors.cn | 0 | 0/0 | 前置约束：排在 Basic Double Doors 之后（About.loadAfter） |
| 639 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Temperature Control Unit_zh | lan.tcu.zh | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 640 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Smart Speed_zh | soulwolf.ch.smartspeed | 0 | 0/0 | 前置约束：排在 Smart Speed 之后（About.loadAfter） |
| 641 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Sometimes Raids Go Wrong_zh | zh.sometimesraidsgowrong | 0 | 0/0 | 前置约束：排在 Sometimes Raids Go Wrong 之后（About.loadAfter） |
| 642 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Non uno Pinata (don't drop items)简体中文汉化 | hecatessg.avilmask.nonunopinata.cn | 0 | 0/0 | 前置约束：排在 Non uno Pinata (don't drop items) 之后（About 强依赖） |
| 643 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | SimpleCameraSetting-汉化 | zh.ray1203.simplecamerasetting | 0 | 0/0 | 前置约束：排在 SimpleCameraSetting 之后（About 强依赖） |
| 644 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Death Rattle Continued ZH | deathrattle.zh | 0 | 0/0 | 前置约束：排在 Death Rattle Continued [1.2+] 之后（About.loadAfter） |
| 645 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Ponpeco Kids' Clothes_zh | lan.ppclothes.zh | 0 | 0/0 | 前置约束：排在 Biotech 之后（About 强依赖） |
| 646 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | A Dog Said... Animal Prosthetics 2 zh-pack | aki.adogsaidanimalprosthetics2 | 0 | 0/0 | 前置约束：排在 A Dog Said... Animal Prosthetics 2 之后（About 强依赖）；前置约束：排在 XML Extensions 之后（About.loadAfter） |
| 647 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Surrogate Mechanoid_zh | zh.mzmgow.sov.det.surrogatemech | 0 | 0/0 | 前置约束：排在 Surrogate Mechanoid 之后（About.loadAfter） |
| 648 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Pocket Sand_zh | soulwolf.ch.pocketsand | 0 | 0/0 | 前置约束：排在 Pocket Sand 之后（About.loadAfter） |
| 649 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Maru Race_zh | lan.vmarurace.zh | 0 | 0/0 | 前置约束：排在 Maru Race 之后（About 强依赖） |
| 650 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Map Mode Framework_zh | senilia.mapmodeframework.zh | 0 | 0/0 | 前置约束：排在 Map Mode Framework 之后（About.loadAfter） |
| 651 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | CloseAll -- 一键闭嘴 简繁汉化包 | zh.gorymoon.closeall.sklm | 0 | 0/0 | 前置约束：排在 CloseAll 之后（About.loadAfter） |
| 652 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | VGP Vegetable Garden 菜园子简中补充汉化包 | loyalpup.vgpvegtablegarden.zh | 0 | 0/0 | 前置约束：排在 VGP Vegetable Garden 之后（About.loadAfter） |
| 653 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Geneva Checklist - 日内瓦备忘录 - 简繁汉化包 | thathitmann.genevachecklist.zh | 0 | 0/0 | 前置约束：排在 Geneva Checklist 之后（About.loadAfter）；前置约束：排在 Core 之后（About.loadAfter） |
| 654 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Misstall's Armor and Uniforms个人汉化 | zh.misstall.nisstallsoutfitszzz | 0 | 0/0 | 前置约束：排在 Misstall's Armor and Uniforms 之后（About 强依赖） |
| 655 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Misstall's Armory_ZH[Misstall的军械库_简繁中文汉化] | dx12.misstall.nisstallsarmoryz.zh | 0 | 0/0 | 前置约束：排在 Misstall's Armory 之后（About 强依赖） |
| 656 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Misstall's Armory Charge Weapons_ZH[Misstall的军械库电荷武器_简繁中文汉化] | dx12.misstall.chargeweaponzzz.zh | 0 | 0/0 | 前置约束：排在 Misstall's Armory Charge Weapons 之后（About 强依赖） |
| 657 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Misstall's Armor and Uniforms_ZH[Misstall的护甲和制服_简繁中文汉化] | dx12.misstall.nisstallsoutfitszzz.zh | 0 | 0/0 | 前置约束：排在 Misstall's Armor and Uniforms 之后（About 强依赖） |
| 658 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Food Texture Variety Core的简中汉化补丁 | lovetianyilove.foodtvcore.zhcn | 0 | 0/0 | 前置约束：排在 Food Texture Variety Core 之后（About 强依赖） |
| 659 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | World Tech Level_zh | zh.world.tech.level | 0 | 0/0 | 前置约束：排在 World Tech Level 之后（About.loadAfter） |
| 660 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Davai's Sorted Categories zh-pack | davai.sortedcategories.zh | 0 | 0/0 | 前置约束：排在 Davai's Sorted Categories 之后（About 强依赖）；前置约束：排在 XML Extensions 之后（About.loadAfter） |
| 661 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | [sbz] Neat Storage_zh | zh.mzmgow.sbz.neatstorage | 0 | 0/0 | 前置约束：排在 [sbz] Neat Storage 之后（About.loadAfter） |
| 662 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Real Ruins 汉化 | cc.chinese.rimworld.realruins | 0 | 0/0 | 前置约束：排在 Real Ruins 之后（About.loadAfter） |
| 663 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Korossa: scorched brass（科罗莎:焦铜）中文翻译 | schmidtkaffee.translation.korossa | 0 | 0/0 | 前置约束：排在 Korossa: scorched brass 之后（About 强依赖） |
| 664 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Nice Health Tab-zh | day29zh.nicehealthtab | 0 | 0/0 | 前置约束：排在 Nice Health Tab 之后（About.loadAfter） |
| 665 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Misstall's Vigil Race_ZH[Misstall的种族-卫戒_简繁中文汉化] | dx12.misstalls.vigilracemodmain.zh | 0 | 0/0 | 前置约束：排在 Misstall's Vigil Race 之后（About 强依赖） |
| 666 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Misstall's Foods and Social Drugs_ZH[Misstall的食品与社交成瘾品_简繁中文汉化] | dx12.misstall.nisstallsfoodsandsocialdrugsz.zh | 0 | 0/0 | 前置约束：排在 Misstall's Foods and Social Drugs 之后（About 强依赖） |
| 667 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | [sbz] Fridge_zh | zh.mzmgow.sbz.neatstoragefridge | 0 | 0/0 | 前置约束：排在 [sbz] Fridge 之后（About.loadAfter） |
| 668 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | [sbz] Workbench Shelf_zh | zh.mzmgow.sbz.neatstorageworkbenchshelf | 0 | 0/0 | 前置约束：排在 [sbz] Workbench Shelf 之后（About.loadAfter） |
| 669 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | TAWM's MoeLotl | tawm.ml | 0 | 0/0 | 前置约束：排在 MoeLotl Race 之后（About 强依赖）；前置约束：排在 Mya's fufu 之后（About.loadAfter） |
| 670 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Nice Bill Tab zh-pack | andromeda.nicebilltab.zh | 0 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Nice Bill Tab 之后（About 强依赖） |
| 671 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | VGP Garden Medicine 菜园子医药简中汉化补充包 | loyalpup.vgpmedicine.ch | 0 | 0/0 | 前置约束：排在 VGP Garden Medicine 之后（About.loadAfter） |
| 672 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | pigeon（鸽子）中文翻译 | schmidtkaffee.translation.pigeon | 0 | 0/0 | 前置约束：排在 Pigeon 之后（About 强依赖） |
| 673 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Pick Up And Haul-zh | day29zh.pickupandhaul | 0 | 0/0 | 前置约束：排在 Pick Up And Haul 之后（About.loadAfter） |
| 674 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Warehouse Storage_zh | zh.mzmgow.vin.warehouse.storage | 0 | 0/0 | 前置约束：排在 Warehouse Storage 之后（About.loadAfter） |
| 675 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Mines 2.0-汉化 | noflag.mines2.0 | 0 | 0/0 | 前置约束：排在 Mines 2.0 之后（About 强依赖） |
| 676 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Performance - Slower Pawn Tick Rate 中文化 | arkymn.slowerpawntickrate.zh | 0 | 0/0 | 前置约束：排在 Performance - Slower Pawn Tick Rate 之后（About 强依赖） |
| 677 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Food Texture Variety_zh | zh.foodtexturevariety | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |
| 678 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Adaptive Storage - Global Settings.zh 自适应存储——全局设置 | nuanki.adaptivestorageglobalsettings.zh | 0 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Adaptive Storage Framework 之后（About 强依赖） |
| 679 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Misstall's Shuttle_ZH[Misstall的穿梭机_简繁中文汉化] | dx12.misstalls.vrshuttle.zh | 0 | 0/0 | 前置约束：排在 Misstall's Shuttle 之后（About 强依赖）；前置约束：排在 Odyssey 之后（About 强依赖） |
| 680 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Dubs Bad Hygiene 1.6简&繁汉化补充包 | dubwise.dubsbadhygiene.youchenzh | 0 | 0/0 | 前置约束：排在 Dubs Bad Hygiene 之后（About 强依赖）；前置约束：排在 Dubs Bad Hygiene zh-pack 之后（About 强依赖） |
| 681 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Diner Supplies_zh | zh.mzmgow.dsp.lun | 0 | 0/0 | 前置约束：排在 Diner Supplies 之后（About.loadAfter） |
| 682 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | [sbz] Gravship Storage_zh | zh.mzmgow.sbz.gravshipstorage | 0 | 0/0 | 前置约束：排在 [sbz] Gravship Storage 之后（About.loadAfter） |
| 683 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Trains of the Rim_zh 边缘火车 | princeofpluto.vehicles.trains.zh | 0 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Vehicle Framework 之后（About 强依赖） |
| 684 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Loading Progress（简中汉化） | ilyvion.loadingprogress.zh | 0 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Loading Progress 之后（About 强依赖） |
| 685 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Better Distress Signal_zh 更好的求救信号 简繁汉化(1.6) | xyzf.chinesepack.betterdistresscall | 0 | 0/0 | 前置约束：排在 Better Distress Call 之后（About 强依赖） |
| 686 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Cat's Boots and Gloves_zh 手套和靴子 | catlover366.bootsandgloves.zh | 0 | 0/0 | 前置约束：排在 Cat's Boots and Gloves 之后（About.loadAfter） |
| 687 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | RimTalk（边缘世谭）简繁中文汉化 | maiya.rimtalk.zh | 0 | 0/0 | 前置约束：排在 RimTalk 之后（About.loadAfter） |
| 688 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Do Something for Idle 简繁中文汉化包 | zh.ai.dsfi | 0 | 0/0 | 前置约束：排在 Do Something for Idle 之后（About.loadAfter） |
| 689 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | 米莉拉扩展：侧卫 简体中文/繁體中文翻译 Milira Addon: Fianchetto Variation zh | ariandel.rabiosus.funnelmilian | 0 | 0/0 | 前置约束：排在 Milira Addon: Fianchetto Variation 之后（About 强依赖） |
| 690 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Faction Weapons and Apparel Set | hailuan.factionwa | 0 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖） |
| 691 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Nice Plants Menu zh-pack | andromeda.niceplantsmenu.zh | 0 | 0/0 | 前置约束：排在 Harmony 之后（About 强依赖）；前置约束：排在 Nice Plants Menu 之后（About 强依赖） |
| 692 | — | 新增 | 语言汉化包 | 未启用 | 1.绿色 (确认无冲突) | Nice Inventory Tab 简/繁体中文翻译 | baldman47.nit.zh | 0 | 0/0 | 未发现代码或 XML 冲突，加载顺序约束完备 |

## 二、3.红色（明确冲突）明细（共 88 项）

### [CyclicLoadOrderConstraint] Layered Wall Destruction ↔ Layered Wall Destruction - Additional Patches
- 冲突目标: keshash.layeredwalldestruction <-> seromas.layereddestruction.patches
- 结论说明: 检测到硬性排序循环依赖: Layered Wall Destruction (keshash.layeredwalldestruction) → Layered Wall Destruction - Additional Patches (seromas.layereddestruction.patches)，已断开该边以保证排序可解（Layered Wall Destruction 声明 loadBefore Layered Wall Destruction - Additional Patches；Layered Wall Destruction - Additional Patches 强依赖前置 Layered Wall Destruction；Layered Wall Destruction - Additional Patches 声明 loadAfter Layered Wall Destruction）
- 处置建议: 存在双向循环依赖，已自动断开后置边以保证拓扑可解；建议联系模组作者修正 About.xml 声明
  - 证据: Layered Wall Destruction 声明 loadBefore Layered Wall Destruction - Additional Patches；Layered Wall Destruction - Additional Patches 强依赖前置 Layered Wall Destruction；Layered Wall Destruction - Additional Patches 声明 loadAfter Layered Wall Destruction

### [DestructiveXmlPatchCollision] Humanoid Alien Races ↔ Humanoid Alien Races ~ Dev
- 冲突目标: Defs/PawnRenderTreeDef//li[texPath="Things/Pawn/Humanlike/Apparel/SwaddledBaby/Swaddled_Child"]/nodeClass
- 结论说明: Humanoid Alien Races 与 Humanoid Alien Races ~ Dev 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突
- 处置建议: 两个无依赖模组同时替换或删除同一节点，后执行者可能报错或吞掉前置改动
  - 证据: Humanoid Alien Races: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\839005762\1.7\Patches\HumansAreAliensToo.xml)
  - 证据: Humanoid Alien Races ~ Dev: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\2640710953\1.7\Patches\HumansAreAliensToo.xml)

### [DestructiveXmlPatchCollision] VGP Vegetable Garden ↔ VGP Garden Fabrics Simplified
- 冲突目标: Defs/ThingDef[defName = "Plant_Cotton"]/graphicData/texPath
- 结论说明: VGP Vegetable Garden 与 VGP Garden Fabrics Simplified 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突
- 处置建议: 两个无依赖模组同时替换或删除同一节点，后执行者可能报错或吞掉前置改动
  - 证据: VGP Vegetable Garden: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\2007061826\1.7\Patches\Vanilla_Plants_Art.xml)
  - 证据: VGP Garden Fabrics Simplified: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\2007062335\1.6\Patches\Vanilla_Plants.xml)

### [DestructiveXmlPatchCollision] VGP Vegetable Garden ↔ VGP Garden Fabrics Simplified
- 冲突目标: Defs/ThingDef[defName = "Plant_Cotton"]/plant/immatureGraphicPath
- 结论说明: VGP Vegetable Garden 与 VGP Garden Fabrics Simplified 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突
- 处置建议: 两个无依赖模组同时替换或删除同一节点，后执行者可能报错或吞掉前置改动
  - 证据: VGP Vegetable Garden: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\2007061826\1.7\Patches\Vanilla_Plants_Art.xml)
  - 证据: VGP Garden Fabrics Simplified: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\2007062335\1.6\Patches\Vanilla_Plants.xml)

### [DestructiveXmlPatchCollision] VGP Vegetable Garden ↔ VGP Garden Fabrics Simplified
- 冲突目标: Defs/ThingDef[defName = "Plant_Devilstrand"]/graphicData/texPath
- 结论说明: VGP Vegetable Garden 与 VGP Garden Fabrics Simplified 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突
- 处置建议: 两个无依赖模组同时替换或删除同一节点，后执行者可能报错或吞掉前置改动
  - 证据: VGP Vegetable Garden: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\2007061826\1.7\Patches\Vanilla_Plants_Art.xml)
  - 证据: VGP Garden Fabrics Simplified: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\2007062335\1.6\Patches\Vanilla_Plants.xml)

### [DestructiveXmlPatchCollision] Humanoid Alien Races ↔ Humanoid Alien Races ~ Dev
- 冲突目标: Defs/ThingDef[defName="Human" or defName="CreepJoiner"]
- 结论说明: Humanoid Alien Races 与 Humanoid Alien Races ~ Dev 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突
- 处置建议: 两个无依赖模组同时替换或删除同一节点，后执行者可能报错或吞掉前置改动
  - 证据: Humanoid Alien Races: PatchOperationAttributeSet (D:\Steam\steamapps\workshop\content\294100\839005762\1.7\Patches\HumansAreAliensToo.xml)
  - 证据: Humanoid Alien Races ~ Dev: PatchOperationAttributeSet (D:\Steam\steamapps\workshop\content\294100\2640710953\1.7\Patches\HumansAreAliensToo.xml)

### [DestructiveXmlPatchCollision] Vanilla Textures Expanded ↔ [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods
- 冲突目标: Defs/ThingDef[defName="MealFine"]/graphicData/texPath
- 结论说明: Vanilla Textures Expanded 与 [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突
- 处置建议: 两个无依赖模组同时替换或删除同一节点，后执行者可能报错或吞掉前置改动
  - 证据: Vanilla Textures Expanded: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\2016436324\1.6\Patches\ModSettings\Special-Patches.xml)
  - 证据: [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\3050762215\Patches\Food_Patch.xml)

### [DestructiveXmlPatchCollision] Vanilla Textures Expanded ↔ [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods
- 冲突目标: Defs/ThingDef[defName="MealFine_Meat"]/graphicData/texPath
- 结论说明: Vanilla Textures Expanded 与 [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突
- 处置建议: 两个无依赖模组同时替换或删除同一节点，后执行者可能报错或吞掉前置改动
  - 证据: Vanilla Textures Expanded: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\2016436324\1.6\Patches\ModSettings\Special-Patches.xml)
  - 证据: [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\3050762215\Patches\Food_Patch.xml)

### [DestructiveXmlPatchCollision] Vanilla Textures Expanded ↔ [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods
- 冲突目标: Defs/ThingDef[defName="MealFine_Veg"]/graphicData/texPath
- 结论说明: Vanilla Textures Expanded 与 [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突
- 处置建议: 两个无依赖模组同时替换或删除同一节点，后执行者可能报错或吞掉前置改动
  - 证据: Vanilla Textures Expanded: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\2016436324\1.6\Patches\ModSettings\Special-Patches.xml)
  - 证据: [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\3050762215\Patches\Food_Patch.xml)

### [DestructiveXmlPatchCollision] Vanilla Textures Expanded ↔ [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods
- 冲突目标: Defs/ThingDef[defName="MealLavish"]/graphicData/texPath
- 结论说明: Vanilla Textures Expanded 与 [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突
- 处置建议: 两个无依赖模组同时替换或删除同一节点，后执行者可能报错或吞掉前置改动
  - 证据: Vanilla Textures Expanded: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\2016436324\1.6\Patches\ModSettings\Special-Patches.xml)
  - 证据: [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\3050762215\Patches\Food_Patch.xml)

### [DestructiveXmlPatchCollision] Vanilla Textures Expanded ↔ [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods
- 冲突目标: Defs/ThingDef[defName="MealLavish_Meat"]/graphicData/texPath
- 结论说明: Vanilla Textures Expanded 与 [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突
- 处置建议: 两个无依赖模组同时替换或删除同一节点，后执行者可能报错或吞掉前置改动
  - 证据: Vanilla Textures Expanded: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\2016436324\1.6\Patches\ModSettings\Special-Patches.xml)
  - 证据: [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\3050762215\Patches\Food_Patch.xml)

### [DestructiveXmlPatchCollision] Vanilla Textures Expanded ↔ [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods
- 冲突目标: Defs/ThingDef[defName="MealLavish_Veg"]/graphicData/texPath
- 结论说明: Vanilla Textures Expanded 与 [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突
- 处置建议: 两个无依赖模组同时替换或删除同一节点，后执行者可能报错或吞掉前置改动
  - 证据: Vanilla Textures Expanded: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\2016436324\1.6\Patches\ModSettings\Special-Patches.xml)
  - 证据: [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\3050762215\Patches\Food_Patch.xml)

### [DestructiveXmlPatchCollision] Vanilla Textures Expanded ↔ [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods
- 冲突目标: Defs/ThingDef[defName="MealNutrientPaste"]/graphicData/texPath
- 结论说明: Vanilla Textures Expanded 与 [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突
- 处置建议: 两个无依赖模组同时替换或删除同一节点，后执行者可能报错或吞掉前置改动
  - 证据: Vanilla Textures Expanded: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\2016436324\1.6\Patches\ModSettings\Special-Patches.xml)
  - 证据: [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\3050762215\Patches\Food_Patch.xml)

### [DestructiveXmlPatchCollision] Vanilla Textures Expanded ↔ [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods
- 冲突目标: Defs/ThingDef[defName="MealSimple"]/graphicData/texPath
- 结论说明: Vanilla Textures Expanded 与 [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突
- 处置建议: 两个无依赖模组同时替换或删除同一节点，后执行者可能报错或吞掉前置改动
  - 证据: Vanilla Textures Expanded: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\2016436324\1.6\Patches\ModSettings\Special-Patches.xml)
  - 证据: [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\3050762215\Patches\Food_Patch.xml)

### [DestructiveXmlPatchCollision] Vanilla Textures Expanded ↔ [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods
- 冲突目标: Defs/ThingDef[defName="MealSurvivalPack"]/graphicData/texPath
- 结论说明: Vanilla Textures Expanded 与 [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods 在无依赖关系下对同一 XPath 节点执行了无条件替换或删除，存在直接覆盖冲突
- 处置建议: 两个无依赖模组同时替换或删除同一节点，后执行者可能报错或吞掉前置改动
  - 证据: Vanilla Textures Expanded: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\2016436324\1.6\Patches\ModSettings\Special-Patches.xml)
  - 证据: [TW1.7]堂丸贴图重置~食物 TangW's~Retexture~Foods: PatchOperationReplace (D:\Steam\steamapps\workshop\content\294100\3050762215\Patches\Food_Patch.xml)

### [DuplicateXmlDefOverwrite] Dubs Bad Hygiene Lite ↔ Dubs Bad Hygiene
- 冲突目标: ThingDef:AirConOutdoorUnit
- 结论说明: Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 在无依赖关系下重复定义了核心 XML Def（ThingDef:AirConOutdoorUnit），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: Dubs Bad Hygiene Lite: D:\Steam\steamapps\workshop\content\294100\2570319432\1.7\Defs\ThingDefs_Buildings\BuildingsE_Heating.xml
  - 证据: Dubs Bad Hygiene: D:\Steam\steamapps\workshop\content\294100\836308268\1.7\Defs\ThingDefs_Buildings\BuildingsE_Heating.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:Bullet_BFR_AP
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:Bullet_BFR_AP），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_Range.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Weapon.xml

### [DuplicateXmlDefOverwrite] Dubs Bad Hygiene Lite ↔ Dubs Bad Hygiene
- 冲突目标: ThingDef:BurnPit
- 结论说明: Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 在无依赖关系下重复定义了核心 XML Def（ThingDef:BurnPit），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: Dubs Bad Hygiene Lite: D:\Steam\steamapps\workshop\content\294100\2570319432\1.7\Defs\ThingDefs_Buildings\BuildingsB_Hygiene.xml
  - 证据: Dubs Bad Hygiene: D:\Steam\steamapps\workshop\content\294100\836308268\1.7\Defs\ThingDefs_Buildings\BuildingsB_Hygiene.xml

### [DuplicateXmlDefOverwrite] Dubs Bad Hygiene Lite ↔ Dubs Bad Hygiene
- 冲突目标: ThingDef:DBHSwimmingPool
- 结论说明: Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 在无依赖关系下重复定义了核心 XML Def（ThingDef:DBHSwimmingPool），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: Dubs Bad Hygiene Lite: D:\Steam\steamapps\workshop\content\294100\2570319432\1.7\Defs\ThingDefs_Buildings\BuildingsC_Joy.xml
  - 证据: Dubs Bad Hygiene: D:\Steam\steamapps\workshop\content\294100\836308268\1.7\Defs\ThingDefs_Buildings\BuildingsC_Joy.xml

### [DuplicateXmlDefOverwrite] Dubs Bad Hygiene Lite ↔ Dubs Bad Hygiene
- 冲突目标: ThingDef:DBH_WaterBottle
- 结论说明: Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 在无依赖关系下重复定义了核心 XML Def（ThingDef:DBH_WaterBottle），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: Dubs Bad Hygiene Lite: D:\Steam\steamapps\workshop\content\294100\2570319432\1.7\Defs\ThingDefs_Items\Items_Resource_Stuff.xml
  - 证据: Dubs Bad Hygiene: D:\Steam\steamapps\workshop\content\294100\836308268\1.7\Defs\ThingDefs_Items\Items_Resource_Stuff.xml

### [DuplicateXmlDefOverwrite] Dubs Bad Hygiene Lite ↔ Dubs Bad Hygiene
- 冲突目标: ThingDef:DeepWaterWellInlet
- 结论说明: Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 在无依赖关系下重复定义了核心 XML Def（ThingDef:DeepWaterWellInlet），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: Dubs Bad Hygiene Lite: D:\Steam\steamapps\workshop\content\294100\2570319432\1.7\Defs\ThingDefs_Buildings\BuildingsD_WaterManagement.xml
  - 证据: Dubs Bad Hygiene: D:\Steam\steamapps\workshop\content\294100\836308268\1.7\Defs\ThingDefs_Buildings\BuildingsD_WaterManagement.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:GunlanceAfterIgnition
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:GunlanceAfterIgnition），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_HighTech.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Gunlance.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:GunlancePreIgnition
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:GunlancePreIgnition），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_HighTech.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Gunlance.xml

### [DuplicateXmlDefOverwrite] Dubs Bad Hygiene Lite ↔ Dubs Bad Hygiene
- 冲突目标: ThingDef:PrimitiveWell
- 结论说明: Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 在无依赖关系下重复定义了核心 XML Def（ThingDef:PrimitiveWell），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: Dubs Bad Hygiene Lite: D:\Steam\steamapps\workshop\content\294100\2570319432\1.7\Defs\ThingDefs_Buildings\BuildingsB_Hygiene.xml
  - 证据: Dubs Bad Hygiene: D:\Steam\steamapps\workshop\content\294100\836308268\1.7\Defs\ThingDefs_Buildings\BuildingsB_Hygiene.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_AutoCrossBow
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_AutoCrossBow），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_Range.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Weapon.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_Axe
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_Axe），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_Util.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Weapon.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_Cleaver
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_Cleaver），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_Util.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Weapon.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_Crossbow
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_Crossbow），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_Range.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Weapon.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_Dagger
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_Dagger），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_Melee.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Lance.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_ElectricSmithy
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_ElectricSmithy），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingDefs_Building\Buildings_Production.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingDefs_Building\Buildings_Production.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_ElectricTailoringBench
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_ElectricTailoringBench），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingDefs_Building\Buildings_Production.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingDefs_Building\Buildings_Production.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_EmpBomb
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_EmpBomb），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingDefs_Building\Buildings_RatkinTunnel.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingDefs_Building\Buildings_RatkinTunnel.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_Fork
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_Fork），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_Util.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Weapon.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_FueledSmithy
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_FueledSmithy），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingDefs_Building\Buildings_Production.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingDefs_Building\Buildings_Production.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_Halberd
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_Halberd），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_Melee.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Lance.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_HamsterWheelGenerator
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_HamsterWheelGenerator），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingDefs_Building\Buildings_Power.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingDefs_Building\Buildings_Power.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_HandTailoringBench
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_HandTailoringBench），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingDefs_Building\Buildings_Production.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingDefs_Building\Buildings_Production.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_HeavyLance
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_HeavyLance），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_Melee.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Lance.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_HeavyShield
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_HeavyShield），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Apparel_Shield.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Apparel.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_Hockey
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_Hockey），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_Util.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Weapon.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_KingHamster
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_KingHamster），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingDefs_Races\RK_Animal.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingDefs_Races\RK_Animal.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_LightLance
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_LightLance），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_Melee.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Lance.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_LongSword
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_LongSword），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_Melee.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Lance.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_Mace
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_Mace），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_Melee.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Lance.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_MagicWand
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_MagicWand），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_DropOnly.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_EnemyWeapon.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_OneHanded
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_OneHanded），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_Melee.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Lance.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_PrototypePulseRifle
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_PrototypePulseRifle），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_Range.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Weapon.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_Rifle
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_Rifle），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_Range.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Weapon.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_SniperRifle
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_SniperRifle），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_Range.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Weapon.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_Spear
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_Spear），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_Melee.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Lance.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_StrawberryBeer
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_StrawberryBeer），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\Things_ItemDefs\Thing_Drink.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Beer.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_TwoHanded
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_TwoHanded），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Weapon_Melee.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Lance.xml

### [DuplicateXmlDefOverwrite] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: ThingDef:RK_WoodenShield
- 结论说明: NewRatkinPlus 与 NewRatkinPlus[1.6] 在无依赖关系下重复定义了核心 XML Def（ThingDef:RK_WoodenShield），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: NewRatkinPlus: D:\Steam\steamapps\workshop\content\294100\1578693166\1.6\Defs\ThingsDefs\Apparel_Shield.xml
  - 证据: NewRatkinPlus[1.6]: D:\Steam\steamapps\workshop\content\294100\3497673755\1.6\Defs\ThingsDefs\RK_Apparel.xml

### [DuplicateXmlDefOverwrite] VGP Garden Gourmet ↔ VGP Garden Drinks
- 冲突目标: ThingDef:Rawsugarcane
- 结论说明: VGP Garden Gourmet 与 VGP Garden Drinks 在无依赖关系下重复定义了核心 XML Def（ThingDef:Rawsugarcane），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: VGP Garden Gourmet: D:\Steam\steamapps\workshop\content\294100\2007062982\1.6\Defs\ThingDefs\Items_Resources.xml
  - 证据: VGP Garden Drinks: D:\Steam\steamapps\workshop\content\294100\2167582651\1.6\Defs\ThingDefs\Items_Resources.xml

### [DuplicateXmlDefOverwrite] Dubs Bad Hygiene Lite ↔ Dubs Bad Hygiene
- 冲突目标: ThingDef:ToiletStuff
- 结论说明: Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 在无依赖关系下重复定义了核心 XML Def（ThingDef:ToiletStuff），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: Dubs Bad Hygiene Lite: D:\Steam\steamapps\workshop\content\294100\2570319432\1.7\Defs\ThingDefs_Buildings\BuildingsB_Hygiene.xml
  - 证据: Dubs Bad Hygiene: D:\Steam\steamapps\workshop\content\294100\836308268\1.7\Defs\ThingDefs_Buildings\BuildingsB_Hygiene.xml

### [DuplicateXmlDefOverwrite] Dubs Bad Hygiene Lite ↔ Dubs Bad Hygiene
- 冲突目标: ThingDef:WaterButt
- 结论说明: Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 在无依赖关系下重复定义了核心 XML Def（ThingDef:WaterButt），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: Dubs Bad Hygiene Lite: D:\Steam\steamapps\workshop\content\294100\2570319432\1.7\Defs\ThingDefs_Buildings\BuildingsD_WaterManagement.xml
  - 证据: Dubs Bad Hygiene: D:\Steam\steamapps\workshop\content\294100\836308268\1.7\Defs\ThingDefs_Buildings\BuildingsD_WaterManagement.xml

### [DuplicateXmlDefOverwrite] Dubs Bad Hygiene Lite ↔ Dubs Bad Hygiene
- 冲突目标: ThingDef:WaterTowerL
- 结论说明: Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 在无依赖关系下重复定义了核心 XML Def（ThingDef:WaterTowerL），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: Dubs Bad Hygiene Lite: D:\Steam\steamapps\workshop\content\294100\2570319432\1.7\Defs\ThingDefs_Buildings\BuildingsD_WaterManagement.xml
  - 证据: Dubs Bad Hygiene: D:\Steam\steamapps\workshop\content\294100\836308268\1.7\Defs\ThingDefs_Buildings\BuildingsD_WaterManagement.xml

### [DuplicateXmlDefOverwrite] Dubs Bad Hygiene Lite ↔ Dubs Bad Hygiene
- 冲突目标: ThingDef:WaterTowerS
- 结论说明: Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 在无依赖关系下重复定义了核心 XML Def（ThingDef:WaterTowerS），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: Dubs Bad Hygiene Lite: D:\Steam\steamapps\workshop\content\294100\2570319432\1.7\Defs\ThingDefs_Buildings\BuildingsD_WaterManagement.xml
  - 证据: Dubs Bad Hygiene: D:\Steam\steamapps\workshop\content\294100\836308268\1.7\Defs\ThingDefs_Buildings\BuildingsD_WaterManagement.xml

### [DuplicateXmlDefOverwrite] Dubs Bad Hygiene Lite ↔ Dubs Bad Hygiene
- 冲突目标: ThingDef:WaterWellInlet
- 结论说明: Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 在无依赖关系下重复定义了核心 XML Def（ThingDef:WaterWellInlet），后加载者会整块覆盖先加载者
- 处置建议: 两个无依赖关系的模组全量重定义了同一 Def，属于明确覆盖冲突
  - 证据: Dubs Bad Hygiene Lite: D:\Steam\steamapps\workshop\content\294100\2570319432\1.7\Defs\ThingDefs_Buildings\BuildingsD_WaterManagement.xml
  - 证据: Dubs Bad Hygiene: D:\Steam\steamapps\workshop\content\294100\836308268\1.7\Defs\ThingDefs_Buildings\BuildingsD_WaterManagement.xml

### [ExplicitIncompatibility] Dubs Bad Hygiene Lite ↔ Dubs Bad Hygiene
- 冲突目标: dubwise.dubsbadhygiene.lite <-> dubwise.dubsbadhygiene
- 结论说明: Dubs Bad Hygiene Lite 在 About.xml 中显式声明与 Dubs Bad Hygiene 不兼容
- 处置建议: 两个模组显式互斥，不能同时启用
  - 证据: About.xml 的 incompatibleWith 包含 dubwise.dubsbadhygiene

### [ExplicitIncompatibility] [1.6] CAI 5000 - Advanced AI + Fog Of War (continued) ↔ Pawn Target Fix
- 冲突目标: krkr.rule56 <-> fed1splay.pawntargetfix
- 结论说明: [1.6] CAI 5000 - Advanced AI + Fog Of War (continued) 在 About.xml 中显式声明与 Pawn Target Fix 不兼容
- 处置建议: 两个模组显式互斥，不能同时启用
  - 证据: About.xml 的 incompatibleWith 包含 fed1splay.pawntargetfix

### [ExplicitIncompatibility] [1.6] CAI 5000 - Advanced AI + Fog Of War (continued) ↔ [SR]Raid Extension (fork)
- 冲突目标: krkr.rule56 <-> sr.modrimworld.raidextension
- 结论说明: [1.6] CAI 5000 - Advanced AI + Fog Of War (continued) 在 About.xml 中显式声明与 [SR]Raid Extension (fork) 不兼容
- 处置建议: 两个模组显式互斥，不能同时启用
  - 证据: About.xml 的 incompatibleWith 包含 sr.modrimworld.raidextension

### [ExplicitIncompatibility] Replace Stuff - Continued ↔ GloomyFurniture
- 冲突目标: memegoddess.replacestuff <-> solaris.furniturebase
- 结论说明: Replace Stuff - Continued 在 About.xml 中显式声明与 GloomyFurniture 不兼容
- 处置建议: 两个模组显式互斥，不能同时启用
  - 证据: About.xml 的 incompatibleWith 包含 solaris.furniturebase

### [ExplicitIncompatibility] RimWorld Together ↔ RimSaves
- 冲突目标: nova.rimworldtogether <-> arandomkiwi.rimsaves
- 结论说明: RimWorld Together 在 About.xml 中显式声明与 RimSaves 不兼容
- 处置建议: 两个模组显式互斥，不能同时启用
  - 证据: About.xml 的 incompatibleWith 包含 arandomkiwi.rimsaves

### [ExplicitIncompatibility] Mechanoids: Total Warfare ↔ Gunplay
- 冲突目标: nyar.nclvstw <-> automatic.gunplay
- 结论说明: Mechanoids: Total Warfare 在 About.xml 中显式声明与 Gunplay 不兼容
- 处置建议: 两个模组显式互斥，不能同时启用
  - 证据: About.xml 的 incompatibleWith 包含 automatic.gunplay

### [ExplicitIncompatibility] Butter++ ↔ Dubs Performance Analyzer
- 冲突目标: olli.butterplusplus <-> dubwise.dubsperformanceanalyzer.steam
- 结论说明: Butter++ 在 About.xml 中显式声明与 Dubs Performance Analyzer 不兼容
- 处置建议: 两个模组显式互斥，不能同时启用
  - 证据: About.xml 的 incompatibleWith 包含 dubwise.dubsperformanceanalyzer.steam

### [ExplicitIncompatibility] Pick Up at Home (Fork) ↔ Pick Up And Haul
- 冲突目标: rw.mod.pickupathome <-> mehni.pickupandhaul
- 结论说明: Pick Up at Home (Fork) 在 About.xml 中显式声明与 Pick Up And Haul 不兼容
- 处置建议: 两个模组显式互斥，不能同时启用
  - 证据: About.xml 的 incompatibleWith 包含 mehni.pickupandhaul

### [HarmonyBlockingPrefixConflict] Better Architect Menu ↔ Architect Menu Optimizer
- 冲突目标: RimWorld.ArchitectCategoryTab.DesignationTabOnGUI
- 结论说明: Better Architect Menu 在 RimWorld.ArchitectCategoryTab.DesignationTabOnGUI 上无条件返回 false 跳过原方法，会使 Architect Menu Optimizer 针对原方法体的补丁无法生效
- 处置建议: 无条件截断 Prefix 会跳过原方法体，导致针对原方法 IL 的 Transpiler 失效
  - 证据: Better Architect Menu: [Prefix] BetterArchitect.ArchitectCategoryTab_DesignationTabOnGUI_Patch.Prefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Architect Menu Optimizer: [Transpiler] ArchitectMenuOptimizer.Patches.Patch_BuildingPagination.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [HarmonyBlockingPrefixConflict] Mod Options Sort ↔ HugsLib
- 冲突目标: RimWorld.Dialog_Options.DoModOptions
- 结论说明: Mod Options Sort 在 RimWorld.Dialog_Options.DoModOptions 上无条件返回 false 跳过原方法，会使 HugsLib 针对原方法体的补丁无法生效
- 处置建议: 无条件截断 Prefix 会跳过原方法体，导致针对原方法 IL 的 Transpiler 失效
  - 证据: Mod Options Sort: [Prefix] ModOptionsSort.Dialog_Options_DoModOptions_Patch.Prefix (Priority=800, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: HugsLib: [Transpiler] HugsLib.Patches.DialogOptions_DoModOptions_Patch.InterceptWindowOpening (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [HarmonyBlockingPrefixConflict] Unlimited Mechanitor Command Range ↔ [SRC]Miho,Star Ring Corporation
- 冲突目标: RimWorld.Pawn_MechanitorTracker.DrawCommandRadius
- 结论说明: Unlimited Mechanitor Command Range 在 RimWorld.Pawn_MechanitorTracker.DrawCommandRadius 上无条件返回 false 跳过原方法，会使 [SRC]Miho,Star Ring Corporation 针对原方法体的补丁无法生效
- 处置建议: 无条件截断 Prefix 会跳过原方法体，导致针对原方法 IL 的 Transpiler 失效
  - 证据: Unlimited Mechanitor Command Range: [Prefix] MechanitorCommandRange.Patches.PatchMechanitorTracker_DrawCommandRadius.Prefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: [SRC]Miho,Star Ring Corporation: [Prefix] SRM.Patch_Pawn_MechanitorTracker.DrawCommandRadiusPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: [SRC]Miho,Star Ring Corporation: [Postfix] SRM.Patch_Pawn_MechanitorTracker.DrawCommandRadiusPostfix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [HarmonyBlockingPrefixConflict] DragSelect ↔ Work Tab
- 冲突目标: RimWorld.WidgetsWork.DrawWorkBoxFor
- 结论说明: DragSelect 在 RimWorld.WidgetsWork.DrawWorkBoxFor 上无条件返回 false 跳过原方法，会使 Work Tab 针对原方法体的补丁无法生效
- 处置建议: 无条件截断 Prefix 会跳过原方法体，导致针对原方法 IL 的 Transpiler 失效
  - 证据: DragSelect: [Prefix] DragSelect.HarmonyPatch_WidgetsWork.DraggableButtonInvisible (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: DragSelect: [Transpiler] DragSelect.HarmonyPatch_WidgetsWork.AddDraggableButtonForNumber (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Work Tab: [Transpiler] WorkTab.WidgetsWork_DrawWorkBoxFor.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [HarmonyBlockingPrefixConflict] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: Verse.Hediff_Injury.PostAdd
- 结论说明: NewRatkinPlus 在 Verse.Hediff_Injury.PostAdd 上无条件返回 false 跳过原方法，会使 NewRatkinPlus[1.6] 针对原方法体的补丁无法生效
- 处置建议: 无条件截断 Prefix 会跳过原方法体，导致针对原方法 IL 的 Transpiler 失效
  - 证据: NewRatkinPlus: [Prefix] NewRatkin.Hediff_InjuryPatch.Prefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: NewRatkinPlus[1.6]: [Prefix] NewRatkin.Hediff_InjuryPatch.Prefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [HarmonyBlockingPrefixConflict] Graphics Settings+ ↔ Image Opt
- 冲突目标: Verse.ModContentLoader.LoadTexture
- 结论说明: Graphics Settings+ 在 Verse.ModContentLoader.LoadTexture 上无条件返回 false 跳过原方法，会使 Image Opt 针对原方法体的补丁无法生效
- 处置建议: 无条件截断 Prefix 会跳过原方法体，导致针对原方法 IL 的 Transpiler 失效
  - 证据: Graphics Settings+: [Prefix] GraphicSetter.Patches.TextureLoadingPatch.LoadTexture_Patch.Prefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=False)
  - 证据: Image Opt: [Prefix] ImageOpt.TextureLoadPatch.Prefix (Priority=1000, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [HarmonyBlockingPrefixConflict] NewRatkinPlus ↔ Yayo's Animation (Continued)
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上无条件返回 false 跳过原方法，会使 Yayo's Animation (Continued) 针对原方法体的补丁无法生效
- 处置建议: 无条件截断 Prefix 会跳过原方法体，导致针对原方法 IL 的 Transpiler 失效
  - 证据: NewRatkinPlus: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Yayo's Animation (Continued): [Prefix] YayoAnimation.HarmonyPatches.PawnPatches.AimingPatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Yayo's Animation (Continued): [Finalizer] YayoAnimation.HarmonyPatches.PawnPatches.AimingPatch.Finalizer (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Yayo's Animation (Continued): [Transpiler] YayoAnimation.HarmonyPatches.PawnPatches.AimingPatch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [HarmonyBlockingPrefixConflict] NewRatkinPlus ↔ Miho, the celestial fox
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上无条件返回 false 跳过原方法，会使 Miho, the celestial fox 针对原方法体的补丁无法生效
- 处置建议: 无条件截断 Prefix 会跳过原方法体，导致针对原方法 IL 的 Transpiler 失效
  - 证据: NewRatkinPlus: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Miho, the celestial fox: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [HarmonyBlockingPrefixConflict] NewRatkinPlus ↔ Yuran race
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上无条件返回 false 跳过原方法，会使 Yuran race 针对原方法体的补丁无法生效
- 处置建议: 无条件截断 Prefix 会跳过原方法体，导致针对原方法 IL 的 Transpiler 失效
  - 证据: NewRatkinPlus: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Yuran race: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [HarmonyBlockingPrefixConflict] NewRatkinPlus ↔ NewRatkinPlus[1.6]
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上无条件返回 false 跳过原方法，会使 NewRatkinPlus[1.6] 针对原方法体的补丁无法生效
- 处置建议: 无条件截断 Prefix 会跳过原方法体，导致针对原方法 IL 的 Transpiler 失效
  - 证据: NewRatkinPlus: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: NewRatkinPlus[1.6]: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [HarmonyBlockingPrefixConflict] Yayo's Animation (Continued) ↔ Miho, the celestial fox
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上无条件返回 false 跳过原方法，会使 Yayo's Animation (Continued) 针对原方法体的补丁无法生效
- 处置建议: 无条件截断 Prefix 会跳过原方法体，导致针对原方法 IL 的 Transpiler 失效
  - 证据: Yayo's Animation (Continued): [Prefix] YayoAnimation.HarmonyPatches.PawnPatches.AimingPatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Yayo's Animation (Continued): [Finalizer] YayoAnimation.HarmonyPatches.PawnPatches.AimingPatch.Finalizer (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Yayo's Animation (Continued): [Transpiler] YayoAnimation.HarmonyPatches.PawnPatches.AimingPatch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Miho, the celestial fox: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [HarmonyBlockingPrefixConflict] Yayo's Animation (Continued) ↔ Yuran race
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Yuran race 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上无条件返回 false 跳过原方法，会使 Yayo's Animation (Continued) 针对原方法体的补丁无法生效
- 处置建议: 无条件截断 Prefix 会跳过原方法体，导致针对原方法 IL 的 Transpiler 失效
  - 证据: Yayo's Animation (Continued): [Prefix] YayoAnimation.HarmonyPatches.PawnPatches.AimingPatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Yayo's Animation (Continued): [Finalizer] YayoAnimation.HarmonyPatches.PawnPatches.AimingPatch.Finalizer (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Yayo's Animation (Continued): [Transpiler] YayoAnimation.HarmonyPatches.PawnPatches.AimingPatch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Yuran race: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [HarmonyBlockingPrefixConflict] Yayo's Animation (Continued) ↔ NewRatkinPlus[1.6]
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus[1.6] 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上无条件返回 false 跳过原方法，会使 Yayo's Animation (Continued) 针对原方法体的补丁无法生效
- 处置建议: 无条件截断 Prefix 会跳过原方法体，导致针对原方法 IL 的 Transpiler 失效
  - 证据: Yayo's Animation (Continued): [Prefix] YayoAnimation.HarmonyPatches.PawnPatches.AimingPatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Yayo's Animation (Continued): [Finalizer] YayoAnimation.HarmonyPatches.PawnPatches.AimingPatch.Finalizer (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Yayo's Animation (Continued): [Transpiler] YayoAnimation.HarmonyPatches.PawnPatches.AimingPatch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: NewRatkinPlus[1.6]: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [HarmonyBlockingPrefixConflict] Miho, the celestial fox ↔ Yuran race
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上无条件返回 false 跳过原方法，会使 Yuran race 针对原方法体的补丁无法生效
- 处置建议: 无条件截断 Prefix 会跳过原方法体，导致针对原方法 IL 的 Transpiler 失效
  - 证据: Miho, the celestial fox: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Yuran race: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [HarmonyBlockingPrefixConflict] Miho, the celestial fox ↔ NewRatkinPlus[1.6]
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上无条件返回 false 跳过原方法，会使 NewRatkinPlus[1.6] 针对原方法体的补丁无法生效
- 处置建议: 无条件截断 Prefix 会跳过原方法体，导致针对原方法 IL 的 Transpiler 失效
  - 证据: Miho, the celestial fox: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: NewRatkinPlus[1.6]: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [HarmonyBlockingPrefixConflict] Yuran race ↔ NewRatkinPlus[1.6]
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Yuran race 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上无条件返回 false 跳过原方法，会使 NewRatkinPlus[1.6] 针对原方法体的补丁无法生效
- 处置建议: 无条件截断 Prefix 会跳过原方法体，导致针对原方法 IL 的 Transpiler 失效
  - 证据: Yuran race: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: NewRatkinPlus[1.6]: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [MissingDependency] LEVIATHANS:SANDWORM ↔ RimUnity
- 冲突目标: chezhou.rimunity
- 结论说明: 缺少必需前置模组 RimUnity (chezhou.rimunity)
- 处置建议: 必须补充启用该前置模组，并置于当前模组之前
  - 证据: About.xml 的 modDependencies 声明依赖 chezhou.rimunity，但当前启用列表中未包含该模组

### [MissingDependency] Raven Race - Embers in Shadow ↔ RimUnity
- 冲突目标: chezhou.rimunity
- 结论说明: 缺少必需前置模组 RimUnity (chezhou.rimunity)
- 处置建议: 必须补充启用该前置模组，并置于当前模组之前
  - 证据: About.xml 的 modDependencies 声明依赖 chezhou.rimunity，但当前启用列表中未包含该模组

### [MissingDependency] Moyo Facial Animation Patch ↔ Moyo2.0-Blood stained business
- 冲突目标: nemonian.my2
- 结论说明: 缺少必需前置模组 Moyo2.0-Blood stained business (nemonian.my2)
- 处置建议: 必须补充启用该前置模组，并置于当前模组之前
  - 证据: About.xml 的 modDependencies 声明依赖 nemonian.my2，但当前启用列表中未包含该模组

### [MissingDependency] kemomimihouse Kz ↔ kemomimihouse
- 冲突目标: psyche.kemomimihouse
- 结论说明: 缺少必需前置模组 kemomimihouse (psyche.kemomimihouse)
- 处置建议: 必须补充启用该前置模组，并置于当前模组之前
  - 证据: About.xml 的 modDependencies 声明依赖 psyche.kemomimihouse，但当前启用列表中未包含该模组

### [MissingDependency] [1.7]边缘仙路-神机天纵 RimImmortal-Forge ↔ 边缘仙路-羽化登仙 RimImmortal-Core[1.6]
- 冲突目标: ri.rimimmortal.core
- 结论说明: 缺少必需前置模组 边缘仙路-羽化登仙 RimImmortal-Core[1.6] (ri.rimimmortal.core)
- 处置建议: 必须补充启用该前置模组，并置于当前模组之前
  - 证据: About.xml 的 modDependencies 声明依赖 ri.rimimmortal.core，但当前启用列表中未包含该模组

## 三、2.黄色（无法通过代码分析完全判断）明细（共 102 项）

### [ConditionalPrefixOrResultMutationOverlap] Rim Girls' Frontline ↔ Unlimited Mechanitor Command Range
- 冲突目标: MechanitorUtility.InMechanitorCommandRange
- 结论说明: Unlimited Mechanitor Command Range 在 MechanitorUtility.InMechanitorCommandRange 上包含返回 false 的接管型 Prefix，需确保 Rim Girls' Frontline 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Rim Girls' Frontline 排在截断型 Prefix 模组 Unlimited Mechanitor Command Range 之前，确保双方前置逻辑依次执行
  - 证据: Rim Girls' Frontline: [Prefix] RimGirlsFrontline.Patch_TDollControl.Patch_MechanitorUtility_InMechanitorCommandRange.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: Unlimited Mechanitor Command Range: [Prefix] MechanitorCommandRange.Patches.PatchMechanitorUtility_InMechanitorCommandRange.Prefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Unlimited Mechanitor Command Range ↔ Glitterworld Destroyer 5 - Mechanoid Addon
- 冲突目标: MechanitorUtility.InMechanitorCommandRange
- 结论说明: Unlimited Mechanitor Command Range 在 MechanitorUtility.InMechanitorCommandRange 上包含返回 false 的接管型 Prefix，需确保 Glitterworld Destroyer 5 - Mechanoid Addon 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Glitterworld Destroyer 5 - Mechanoid Addon 排在截断型 Prefix 模组 Unlimited Mechanitor Command Range 之前，确保双方前置逻辑依次执行
  - 证据: Unlimited Mechanitor Command Range: [Prefix] MechanitorCommandRange.Patches.PatchMechanitorUtility_InMechanitorCommandRange.Prefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Glitterworld Destroyer 5 - Mechanoid Addon: [Prefix] GD3.InMechanitorCommandRangePatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Unlimited Mechanitor Command Range ↔ Mechanoids: Total Warfare
- 冲突目标: MechanitorUtility.InMechanitorCommandRange
- 结论说明: Unlimited Mechanitor Command Range 在 MechanitorUtility.InMechanitorCommandRange 上包含返回 false 的接管型 Prefix，需确保 Mechanoids: Total Warfare 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Mechanoids: Total Warfare 排在截断型 Prefix 模组 Unlimited Mechanitor Command Range 之前，确保双方前置逻辑依次执行
  - 证据: Unlimited Mechanitor Command Range: [Prefix] MechanitorCommandRange.Patches.PatchMechanitorUtility_InMechanitorCommandRange.Prefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Mechanoids: Total Warfare: [Prefix] MechanitorCommandRange.PatchMechanitorUtility_InMechanitorCommandRange.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: Mechanoids: Total Warfare: [Postfix] NCL.Patch_CommanderControlRange.Postfix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Unlimited Mechanitor Command Range ↔ SRALib
- 冲突目标: MechanitorUtility.InMechanitorCommandRange
- 结论说明: Unlimited Mechanitor Command Range 在 MechanitorUtility.InMechanitorCommandRange 上包含返回 false 的接管型 Prefix，需确保 SRALib 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 SRALib 排在截断型 Prefix 模组 Unlimited Mechanitor Command Range 之前，确保双方前置逻辑依次执行
  - 证据: Unlimited Mechanitor Command Range: [Prefix] MechanitorCommandRange.Patches.PatchMechanitorUtility_InMechanitorCommandRange.Prefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: SRALib: [Prefix] SRA.MechanitorUtility_InMechanitorCommandRange_Patch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Unlimited Mechanitor Command Range ↔ ManyCats Toolbox
- 冲突目标: MechanitorUtility.InMechanitorCommandRange
- 结论说明: Unlimited Mechanitor Command Range 在 MechanitorUtility.InMechanitorCommandRange 上包含返回 false 的接管型 Prefix，需确保 ManyCats Toolbox 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 ManyCats Toolbox 排在截断型 Prefix 模组 Unlimited Mechanitor Command Range 之前，确保双方前置逻辑依次执行
  - 证据: Unlimited Mechanitor Command Range: [Prefix] MechanitorCommandRange.Patches.PatchMechanitorUtility_InMechanitorCommandRange.Prefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: ManyCats Toolbox: [Prefix] ManyCatsToolbox.Features.MechanitorControlRangeUnlimitedFeature_InRangePatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Unlimited Mechanitor Command Range ↔ Wolfein Black Science Expand
- 冲突目标: MechanitorUtility.InMechanitorCommandRange
- 结论说明: Unlimited Mechanitor Command Range 在 MechanitorUtility.InMechanitorCommandRange 上包含返回 false 的接管型 Prefix，需确保 Wolfein Black Science Expand 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Wolfein Black Science Expand 排在截断型 Prefix 模组 Unlimited Mechanitor Command Range 之前，确保双方前置逻辑依次执行
  - 证据: Unlimited Mechanitor Command Range: [Prefix] MechanitorCommandRange.Patches.PatchMechanitorUtility_InMechanitorCommandRange.Prefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Wolfein Black Science Expand: [Prefix] BlackScience.MechanitorUtility_InMechanitorCommandRange_UnlimitedRangePatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: Wolfein Black Science Expand: [Postfix] BlackScience.MechanitorUtility_InMechanitorCommandRange_UnlimitedRangePatch.Postfix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Mod Options Sort ↔ RimWorld Together
- 冲突目标: RimWorld.Dialog_Options.DoModOptions
- 结论说明: Mod Options Sort 在 RimWorld.Dialog_Options.DoModOptions 上包含返回 false 的接管型 Prefix，需确保 RimWorld Together 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 RimWorld Together 排在截断型 Prefix 模组 Mod Options Sort 之前，确保双方前置逻辑依次执行
  - 证据: Mod Options Sort: [Prefix] ModOptionsSort.Dialog_Options_DoModOptions_Patch.Prefix (Priority=800, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: RimWorld Together: [Prefix] RTClient.Patches.Pages.Patch_Dialog_Options_DoModOptions.DoPre (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Gloomy Dragonian race ↔ Hospitality
- 冲突目标: RimWorld.ForbidUtility.SetForbidden
- 结论说明: Gloomy Dragonian race 在 RimWorld.ForbidUtility.SetForbidden 上包含返回 false 的接管型 Prefix，需确保 Hospitality 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Hospitality 排在截断型 Prefix 模组 Gloomy Dragonian race 之前，确保双方前置逻辑依次执行
  - 证据: Gloomy Dragonian race: [Prefix] Dragonian.FobidPatch.Prefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Hospitality: [Prefix] Hospitality.Patches.ForbidUtility_Patch.SetForbidden.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Vanilla Social Interactions Expanded ↔ Hospitality
- 冲突目标: RimWorld.Pawn_InteractionsTracker.TryInteractRandomly
- 结论说明: Hospitality 在 RimWorld.Pawn_InteractionsTracker.TryInteractRandomly 上包含返回 false 的接管型 Prefix，需确保 Vanilla Social Interactions Expanded 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Vanilla Social Interactions Expanded 排在截断型 Prefix 模组 Hospitality 之前，确保双方前置逻辑依次执行
  - 证据: Vanilla Social Interactions Expanded: [Prefix] VanillaSocialInteractionsExpanded.TryInteractRandomly_Patch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: Hospitality: [Prefix] Hospitality.Patches.Pawn_InteractionsTracker_Patch.TryInteractRandomly.Replacement (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Unlimited Mechanitor Command Range ↔ Mechanoids: Total Warfare
- 冲突目标: RimWorld.Pawn_MechanitorTracker.CanCommandTo
- 结论说明: Unlimited Mechanitor Command Range 在 RimWorld.Pawn_MechanitorTracker.CanCommandTo 上包含返回 false 的接管型 Prefix，需确保 Mechanoids: Total Warfare 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Mechanoids: Total Warfare 排在截断型 Prefix 模组 Unlimited Mechanitor Command Range 之前，确保双方前置逻辑依次执行
  - 证据: Unlimited Mechanitor Command Range: [Prefix] MechanitorCommandRange.Patches.PatchMechanitorTracker_CanCommandTo.Prefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Mechanoids: Total Warfare: [Prefix] MechanitorCommandRange.PatchMechanitorTracker_CanCommandTo.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Unlimited Mechanitor Command Range ↔ Glitterworld Destroyer 5 - Mechanoid Addon
- 冲突目标: RimWorld.Pawn_MechanitorTracker.DrawCommandRadius
- 结论说明: Unlimited Mechanitor Command Range 在 RimWorld.Pawn_MechanitorTracker.DrawCommandRadius 上包含返回 false 的接管型 Prefix，需确保 Glitterworld Destroyer 5 - Mechanoid Addon 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Glitterworld Destroyer 5 - Mechanoid Addon 排在截断型 Prefix 模组 Unlimited Mechanitor Command Range 之前，确保双方前置逻辑依次执行
  - 证据: Unlimited Mechanitor Command Range: [Prefix] MechanitorCommandRange.Patches.PatchMechanitorTracker_DrawCommandRadius.Prefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Glitterworld Destroyer 5 - Mechanoid Addon: [Prefix] GD3.DrawRadiusPatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Unlimited Mechanitor Command Range ↔ Mechanoids: Total Warfare
- 冲突目标: RimWorld.Pawn_MechanitorTracker.DrawCommandRadius
- 结论说明: Unlimited Mechanitor Command Range 在 RimWorld.Pawn_MechanitorTracker.DrawCommandRadius 上包含返回 false 的接管型 Prefix，需确保 Mechanoids: Total Warfare 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Mechanoids: Total Warfare 排在截断型 Prefix 模组 Unlimited Mechanitor Command Range 之前，确保双方前置逻辑依次执行
  - 证据: Unlimited Mechanitor Command Range: [Prefix] MechanitorCommandRange.Patches.PatchMechanitorTracker_DrawCommandRadius.Prefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Mechanoids: Total Warfare: [Prefix] MechanitorCommandRange.PatchMechanitorTracker_DrawCommandRadius.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Unlimited Mechanitor Command Range ↔ ManyCats Toolbox
- 冲突目标: RimWorld.Pawn_MechanitorTracker.DrawCommandRadius
- 结论说明: Unlimited Mechanitor Command Range 在 RimWorld.Pawn_MechanitorTracker.DrawCommandRadius 上包含返回 false 的接管型 Prefix，需确保 ManyCats Toolbox 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 ManyCats Toolbox 排在截断型 Prefix 模组 Unlimited Mechanitor Command Range 之前，确保双方前置逻辑依次执行
  - 证据: Unlimited Mechanitor Command Range: [Prefix] MechanitorCommandRange.Patches.PatchMechanitorTracker_DrawCommandRadius.Prefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: ManyCats Toolbox: [Prefix] ManyCatsToolbox.Features.MechanitorControlRangeUnlimitedFeature_DrawRadiusPatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=False)

### [ConditionalPrefixOrResultMutationOverlap] [SRC]Miho,Star Ring Corporation ↔ Glitterworld Destroyer 5 - Mechanoid Addon
- 冲突目标: RimWorld.Pawn_MechanitorTracker.DrawCommandRadius
- 结论说明: [SRC]Miho,Star Ring Corporation 在 RimWorld.Pawn_MechanitorTracker.DrawCommandRadius 上包含返回 false 的接管型 Prefix，需确保 Glitterworld Destroyer 5 - Mechanoid Addon 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Glitterworld Destroyer 5 - Mechanoid Addon 排在截断型 Prefix 模组 [SRC]Miho,Star Ring Corporation 之前，确保双方前置逻辑依次执行
  - 证据: [SRC]Miho,Star Ring Corporation: [Prefix] SRM.Patch_Pawn_MechanitorTracker.DrawCommandRadiusPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: [SRC]Miho,Star Ring Corporation: [Postfix] SRM.Patch_Pawn_MechanitorTracker.DrawCommandRadiusPostfix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Glitterworld Destroyer 5 - Mechanoid Addon: [Prefix] GD3.DrawRadiusPatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] [SRC]Miho,Star Ring Corporation ↔ Mechanoids: Total Warfare
- 冲突目标: RimWorld.Pawn_MechanitorTracker.DrawCommandRadius
- 结论说明: [SRC]Miho,Star Ring Corporation 在 RimWorld.Pawn_MechanitorTracker.DrawCommandRadius 上包含返回 false 的接管型 Prefix，需确保 Mechanoids: Total Warfare 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Mechanoids: Total Warfare 排在截断型 Prefix 模组 [SRC]Miho,Star Ring Corporation 之前，确保双方前置逻辑依次执行
  - 证据: [SRC]Miho,Star Ring Corporation: [Prefix] SRM.Patch_Pawn_MechanitorTracker.DrawCommandRadiusPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: [SRC]Miho,Star Ring Corporation: [Postfix] SRM.Patch_Pawn_MechanitorTracker.DrawCommandRadiusPostfix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Mechanoids: Total Warfare: [Prefix] MechanitorCommandRange.PatchMechanitorTracker_DrawCommandRadius.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] [SRC]Miho,Star Ring Corporation ↔ ManyCats Toolbox
- 冲突目标: RimWorld.Pawn_MechanitorTracker.DrawCommandRadius
- 结论说明: [SRC]Miho,Star Ring Corporation 在 RimWorld.Pawn_MechanitorTracker.DrawCommandRadius 上包含返回 false 的接管型 Prefix，需确保 ManyCats Toolbox 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 ManyCats Toolbox 排在截断型 Prefix 模组 [SRC]Miho,Star Ring Corporation 之前，确保双方前置逻辑依次执行
  - 证据: [SRC]Miho,Star Ring Corporation: [Prefix] SRM.Patch_Pawn_MechanitorTracker.DrawCommandRadiusPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: [SRC]Miho,Star Ring Corporation: [Postfix] SRM.Patch_Pawn_MechanitorTracker.DrawCommandRadiusPostfix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: ManyCats Toolbox: [Prefix] ManyCatsToolbox.Features.MechanitorControlRangeUnlimitedFeature_DrawRadiusPatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=False)

### [ConditionalPrefixOrResultMutationOverlap] kemomimihouse Kz ↔ 虫娘 Insect Girls
- 冲突目标: RimWorld.Toils_LayDown.LayDown
- 结论说明: kemomimihouse Kz 与 虫娘 Insect Girls 在 RimWorld.Toils_LayDown.LayDown 上均存在无守卫的返回值直接赋值，后加载者会覆盖先加载者结果
- 处置建议: 保持 kemomimihouse Kz 在 虫娘 Insect Girls 之前的稳定加载顺序
  - 证据: kemomimihouse Kz: [Postfix] Kz.KemhouseHarmony.Toils_LayDown_LayDown_Patch.Postfix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: 虫娘 Insect Girls: [Postfix] Kzi.InsectGirlHarmony.Toils_LayDown_LayDown_Patch.Postfix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)

### [ConditionalPrefixOrResultMutationOverlap] RimTalk ↔ RimWorld Together
- 冲突目标: Verse.GameDataSaveLoader.SaveGame
- 结论说明: RimWorld Together 在 Verse.GameDataSaveLoader.SaveGame 上包含返回 false 的接管型 Prefix，需确保 RimTalk 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 RimTalk 排在截断型 Prefix 模组 RimWorld Together 之前，确保双方前置逻辑依次执行
  - 证据: RimTalk: [Prefix] RimTalk.Patches.SaveGamePatch.PreSaveGame (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: RimWorld Together: [Prefix] RTClient.Patches.SaveOnlineGame.DoPre (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] RimSaves ↔ RimWorld Together
- 冲突目标: Verse.GameDataSaveLoader.SaveGame
- 结论说明: RimWorld Together 在 Verse.GameDataSaveLoader.SaveGame 上包含返回 false 的接管型 Prefix，需确保 RimSaves 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 RimSaves 排在截断型 Prefix 模组 RimWorld Together 之前，确保双方前置逻辑依次执行
  - 证据: RimSaves: [Prefix] aRandomKiwi.ARS.GameDataSaveLoader_Patch.SaveGame.Listener (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: RimWorld Together: [Prefix] RTClient.Patches.SaveOnlineGame.DoPre (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] RimWorld Together ↔ Choose Where To Land
- 冲突目标: Verse.GameDataSaveLoader.SaveGame
- 结论说明: RimWorld Together 在 Verse.GameDataSaveLoader.SaveGame 上包含返回 false 的接管型 Prefix，需确保 Choose Where To Land 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Choose Where To Land 排在截断型 Prefix 模组 RimWorld Together 之前，确保双方前置逻辑依次执行
  - 证据: RimWorld Together: [Prefix] RTClient.Patches.SaveOnlineGame.DoPre (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Choose Where To Land: [Prefix] ChooseWhereToLand.Patch_GameDataSaveLoader_SaveGame.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] RimWorld Together ↔ RimChat - Word to Actions
- 冲突目标: Verse.GameDataSaveLoader.SaveGame
- 结论说明: RimWorld Together 在 Verse.GameDataSaveLoader.SaveGame 上包含返回 false 的接管型 Prefix，需确保 RimChat - Word to Actions 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 RimChat - Word to Actions 排在截断型 Prefix 模组 RimWorld Together 之前，确保双方前置逻辑依次执行
  - 证据: RimWorld Together: [Prefix] RTClient.Patches.SaveOnlineGame.DoPre (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: RimChat - Word to Actions: [Prefix] RimChat.Patches.SaveContextCapturePatch_SaveGame.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)

### [ConditionalPrefixOrResultMutationOverlap] RimWorld Together ↔ RimChat - Word to Actions
- 冲突目标: Verse.LoadedObjectDirectory.RegisterLoaded
- 结论说明: RimWorld Together 在 Verse.LoadedObjectDirectory.RegisterLoaded 上包含返回 false 的接管型 Prefix，需确保 RimChat - Word to Actions 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 RimChat - Word to Actions 排在截断型 Prefix 模组 RimWorld Together 之前，确保双方前置逻辑依次执行
  - 证据: RimWorld Together: [Prefix] RTClient.Patches.PatchRegisterLoaded.DoPre (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: RimChat - Word to Actions: [Prefix] RimChat.Patches.LoadedObjectDirectoryPatch_FixLegacyLetterLoadID.FixLegacyLetterLoadID (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

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

### [ConditionalPrefixOrResultMutationOverlap] NewRatkinPlus ↔ Ratkin Weapons+
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Ratkin Weapons+ 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Ratkin Weapons+ 排在截断型 Prefix 模组 NewRatkinPlus 之前，确保双方前置逻辑依次执行
  - 证据: NewRatkinPlus: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Ratkin Weapons+: [Prefix] RatkinWeapons.Bayonet_Patch.Prefix (Priority=40000, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] NewRatkinPlus ↔ Exosuit Framework
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Exosuit Framework 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Exosuit Framework 排在截断型 Prefix 模组 NewRatkinPlus 之前，确保双方前置逻辑依次执行
  - 证据: NewRatkinPlus: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Exosuit Framework: [Prefix] Exosuit.WeaponDrawPosPatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] NewRatkinPlus ↔ Cinders of the Embergarden
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Cinders of the Embergarden 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Cinders of the Embergarden 排在截断型 Prefix 模组 NewRatkinPlus 之前，确保双方前置逻辑依次执行
  - 证据: NewRatkinPlus: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Cinders of the Embergarden: [Prefix] Embergarden.Patch_DrawVehicleTurret.Prefix (Priority=600, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] NewRatkinPlus ↔ MoeLotl: Rigor Mortis
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 MoeLotl: Rigor Mortis 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 MoeLotl: Rigor Mortis 排在截断型 Prefix 模组 NewRatkinPlus 之前，确保双方前置逻辑依次执行
  - 证据: NewRatkinPlus: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: MoeLotl: Rigor Mortis: [Prefix] RigorMortis.Render_Patch.Prefix (Priority=40000, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] NewRatkinPlus ↔ Fortified Features Framework
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Fortified Features Framework 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Fortified Features Framework 排在截断型 Prefix 模组 NewRatkinPlus 之前，确保双方前置逻辑依次执行
  - 证据: NewRatkinPlus: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Fortified Features Framework: [Prefix] Fortified.Patch_DrawVehicleTurret.Prefix (Priority=600, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: Fortified Features Framework: [Prefix] Fortified.Patch_PawnRenderUtility_DrawAimingApparel.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] NewRatkinPlus ↔ Epona Dynastic Rise
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Epona Dynastic Rise 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Epona Dynastic Rise 排在截断型 Prefix 模组 NewRatkinPlus 之前，确保双方前置逻辑依次执行
  - 证据: NewRatkinPlus: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Epona Dynastic Rise: [Prefix] Epona.HeavyWeapon.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] NewRatkinPlus ↔ SRALib
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 SRALib 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 SRALib 排在截断型 Prefix 模组 NewRatkinPlus 之前，确保双方前置逻辑依次执行
  - 证据: NewRatkinPlus: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: SRALib: [Prefix] SRA.Patch_DrawVehicleTurret.Prefix (Priority=600, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: SRALib: [Postfix] SRA.Patch_DrawPawnRenderExtra.Postfix (Priority=200, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] NewRatkinPlus ↔ ChezhouLib
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 ChezhouLib 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 ChezhouLib 排在截断型 Prefix 模组 NewRatkinPlus 之前，确保双方前置逻辑依次执行
  - 证据: NewRatkinPlus: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: ChezhouLib: [Prefix] ChezhouLib.Patch.Patch_ClipAnimation.Patch_DrawEquipmentContext.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: ChezhouLib: [Finalizer] ChezhouLib.Patch.Patch_ClipAnimation.Patch_DrawEquipmentContext.Finalizer (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)

### [ConditionalPrefixOrResultMutationOverlap] NewRatkinPlus ↔ Wolfein Black Science Expand
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Wolfein Black Science Expand 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Wolfein Black Science Expand 排在截断型 Prefix 模组 NewRatkinPlus 之前，确保双方前置逻辑依次执行
  - 证据: NewRatkinPlus: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Wolfein Black Science Expand: [Prefix] BlackScience.PawnRenderUtility_DrawEquipmentAndApparelExtras_SheathPatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Wolfein Black Science Expand: [Postfix] BlackScience.PawnRenderUtility_DrawEquipmentAndApparelExtras_SheathPatch.Postfix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Nivarian Race ↔ Miho, the celestial fox
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Nivarian Race 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Nivarian Race 排在截断型 Prefix 模组 Miho, the celestial fox 之前，确保双方前置逻辑依次执行
  - 证据: Nivarian Race: [Prefix] Nivarian_Race.Code.Patches.Patch_BathHideApparelExtras.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: Miho, the celestial fox: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Nivarian Race ↔ Yuran race
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Yuran race 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Nivarian Race 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Nivarian Race 排在截断型 Prefix 模组 Yuran race 之前，确保双方前置逻辑依次执行
  - 证据: Nivarian Race: [Prefix] Nivarian_Race.Code.Patches.Patch_BathHideApparelExtras.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: Yuran race: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Nivarian Race ↔ NewRatkinPlus[1.6]
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus[1.6] 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Nivarian Race 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Nivarian Race 排在截断型 Prefix 模组 NewRatkinPlus[1.6] 之前，确保双方前置逻辑依次执行
  - 证据: Nivarian Race: [Prefix] Nivarian_Race.Code.Patches.Patch_BathHideApparelExtras.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: NewRatkinPlus[1.6]: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] PUA Body Animation ↔ Miho, the celestial fox
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 PUA Body Animation 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 PUA Body Animation 排在截断型 Prefix 模组 Miho, the celestial fox 之前，确保双方前置逻辑依次执行
  - 证据: PUA Body Animation: [Prefix] PUABodyAnimation.PUAAnimRenderPatch.DrawEquipmentAndApparelExtrasPrefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: PUA Body Animation: [Finalizer] PUABodyAnimation.PUAAnimRenderPatch.DrawEquipmentAndApparelExtrasFinalizer (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Miho, the celestial fox: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] PUA Body Animation ↔ Yuran race
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Yuran race 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 PUA Body Animation 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 PUA Body Animation 排在截断型 Prefix 模组 Yuran race 之前，确保双方前置逻辑依次执行
  - 证据: PUA Body Animation: [Prefix] PUABodyAnimation.PUAAnimRenderPatch.DrawEquipmentAndApparelExtrasPrefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: PUA Body Animation: [Finalizer] PUABodyAnimation.PUAAnimRenderPatch.DrawEquipmentAndApparelExtrasFinalizer (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Yuran race: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] PUA Body Animation ↔ NewRatkinPlus[1.6]
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus[1.6] 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 PUA Body Animation 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 PUA Body Animation 排在截断型 Prefix 模组 NewRatkinPlus[1.6] 之前，确保双方前置逻辑依次执行
  - 证据: PUA Body Animation: [Prefix] PUABodyAnimation.PUAAnimRenderPatch.DrawEquipmentAndApparelExtrasPrefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: PUA Body Animation: [Finalizer] PUABodyAnimation.PUAAnimRenderPatch.DrawEquipmentAndApparelExtrasFinalizer (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: NewRatkinPlus[1.6]: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Ratkin Weapons+ ↔ Miho, the celestial fox
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Ratkin Weapons+ 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Ratkin Weapons+ 排在截断型 Prefix 模组 Miho, the celestial fox 之前，确保双方前置逻辑依次执行
  - 证据: Ratkin Weapons+: [Prefix] RatkinWeapons.Bayonet_Patch.Prefix (Priority=40000, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: Miho, the celestial fox: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Ratkin Weapons+ ↔ Yuran race
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Yuran race 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Ratkin Weapons+ 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Ratkin Weapons+ 排在截断型 Prefix 模组 Yuran race 之前，确保双方前置逻辑依次执行
  - 证据: Ratkin Weapons+: [Prefix] RatkinWeapons.Bayonet_Patch.Prefix (Priority=40000, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: Yuran race: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Ratkin Weapons+ ↔ NewRatkinPlus[1.6]
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus[1.6] 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Ratkin Weapons+ 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Ratkin Weapons+ 排在截断型 Prefix 模组 NewRatkinPlus[1.6] 之前，确保双方前置逻辑依次执行
  - 证据: Ratkin Weapons+: [Prefix] RatkinWeapons.Bayonet_Patch.Prefix (Priority=40000, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: NewRatkinPlus[1.6]: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Miho, the celestial fox ↔ Exosuit Framework
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Exosuit Framework 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Exosuit Framework 排在截断型 Prefix 模组 Miho, the celestial fox 之前，确保双方前置逻辑依次执行
  - 证据: Miho, the celestial fox: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Exosuit Framework: [Prefix] Exosuit.WeaponDrawPosPatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Miho, the celestial fox ↔ Cinders of the Embergarden
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Cinders of the Embergarden 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Cinders of the Embergarden 排在截断型 Prefix 模组 Miho, the celestial fox 之前，确保双方前置逻辑依次执行
  - 证据: Miho, the celestial fox: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Cinders of the Embergarden: [Prefix] Embergarden.Patch_DrawVehicleTurret.Prefix (Priority=600, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Miho, the celestial fox ↔ MoeLotl: Rigor Mortis
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 MoeLotl: Rigor Mortis 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 MoeLotl: Rigor Mortis 排在截断型 Prefix 模组 Miho, the celestial fox 之前，确保双方前置逻辑依次执行
  - 证据: Miho, the celestial fox: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: MoeLotl: Rigor Mortis: [Prefix] RigorMortis.Render_Patch.Prefix (Priority=40000, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Miho, the celestial fox ↔ Fortified Features Framework
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Fortified Features Framework 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Fortified Features Framework 排在截断型 Prefix 模组 Miho, the celestial fox 之前，确保双方前置逻辑依次执行
  - 证据: Miho, the celestial fox: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Fortified Features Framework: [Prefix] Fortified.Patch_DrawVehicleTurret.Prefix (Priority=600, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: Fortified Features Framework: [Prefix] Fortified.Patch_PawnRenderUtility_DrawAimingApparel.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Miho, the celestial fox ↔ Epona Dynastic Rise
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Epona Dynastic Rise 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Epona Dynastic Rise 排在截断型 Prefix 模组 Miho, the celestial fox 之前，确保双方前置逻辑依次执行
  - 证据: Miho, the celestial fox: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Epona Dynastic Rise: [Prefix] Epona.HeavyWeapon.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Miho, the celestial fox ↔ SRALib
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 SRALib 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 SRALib 排在截断型 Prefix 模组 Miho, the celestial fox 之前，确保双方前置逻辑依次执行
  - 证据: Miho, the celestial fox: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: SRALib: [Prefix] SRA.Patch_DrawVehicleTurret.Prefix (Priority=600, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: SRALib: [Postfix] SRA.Patch_DrawPawnRenderExtra.Postfix (Priority=200, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Miho, the celestial fox ↔ ChezhouLib
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 ChezhouLib 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 ChezhouLib 排在截断型 Prefix 模组 Miho, the celestial fox 之前，确保双方前置逻辑依次执行
  - 证据: Miho, the celestial fox: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: ChezhouLib: [Prefix] ChezhouLib.Patch.Patch_ClipAnimation.Patch_DrawEquipmentContext.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: ChezhouLib: [Finalizer] ChezhouLib.Patch.Patch_ClipAnimation.Patch_DrawEquipmentContext.Finalizer (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)

### [ConditionalPrefixOrResultMutationOverlap] Miho, the celestial fox ↔ Wolfein Black Science Expand
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Miho, the celestial fox 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Wolfein Black Science Expand 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Wolfein Black Science Expand 排在截断型 Prefix 模组 Miho, the celestial fox 之前，确保双方前置逻辑依次执行
  - 证据: Miho, the celestial fox: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Wolfein Black Science Expand: [Prefix] BlackScience.PawnRenderUtility_DrawEquipmentAndApparelExtras_SheathPatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Wolfein Black Science Expand: [Postfix] BlackScience.PawnRenderUtility_DrawEquipmentAndApparelExtras_SheathPatch.Postfix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Yuran race ↔ Exosuit Framework
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Yuran race 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Exosuit Framework 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Exosuit Framework 排在截断型 Prefix 模组 Yuran race 之前，确保双方前置逻辑依次执行
  - 证据: Yuran race: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Exosuit Framework: [Prefix] Exosuit.WeaponDrawPosPatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Yuran race ↔ Cinders of the Embergarden
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Yuran race 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Cinders of the Embergarden 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Cinders of the Embergarden 排在截断型 Prefix 模组 Yuran race 之前，确保双方前置逻辑依次执行
  - 证据: Yuran race: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Cinders of the Embergarden: [Prefix] Embergarden.Patch_DrawVehicleTurret.Prefix (Priority=600, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Yuran race ↔ MoeLotl: Rigor Mortis
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Yuran race 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 MoeLotl: Rigor Mortis 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 MoeLotl: Rigor Mortis 排在截断型 Prefix 模组 Yuran race 之前，确保双方前置逻辑依次执行
  - 证据: Yuran race: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: MoeLotl: Rigor Mortis: [Prefix] RigorMortis.Render_Patch.Prefix (Priority=40000, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Yuran race ↔ Fortified Features Framework
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Yuran race 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Fortified Features Framework 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Fortified Features Framework 排在截断型 Prefix 模组 Yuran race 之前，确保双方前置逻辑依次执行
  - 证据: Yuran race: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Fortified Features Framework: [Prefix] Fortified.Patch_DrawVehicleTurret.Prefix (Priority=600, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: Fortified Features Framework: [Prefix] Fortified.Patch_PawnRenderUtility_DrawAimingApparel.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Yuran race ↔ Epona Dynastic Rise
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Yuran race 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Epona Dynastic Rise 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Epona Dynastic Rise 排在截断型 Prefix 模组 Yuran race 之前，确保双方前置逻辑依次执行
  - 证据: Yuran race: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Epona Dynastic Rise: [Prefix] Epona.HeavyWeapon.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Yuran race ↔ SRALib
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Yuran race 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 SRALib 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 SRALib 排在截断型 Prefix 模组 Yuran race 之前，确保双方前置逻辑依次执行
  - 证据: Yuran race: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: SRALib: [Prefix] SRA.Patch_DrawVehicleTurret.Prefix (Priority=600, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: SRALib: [Postfix] SRA.Patch_DrawPawnRenderExtra.Postfix (Priority=200, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Yuran race ↔ ChezhouLib
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Yuran race 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 ChezhouLib 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 ChezhouLib 排在截断型 Prefix 模组 Yuran race 之前，确保双方前置逻辑依次执行
  - 证据: Yuran race: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: ChezhouLib: [Prefix] ChezhouLib.Patch.Patch_ClipAnimation.Patch_DrawEquipmentContext.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: ChezhouLib: [Finalizer] ChezhouLib.Patch.Patch_ClipAnimation.Patch_DrawEquipmentContext.Finalizer (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)

### [ConditionalPrefixOrResultMutationOverlap] Yuran race ↔ Wolfein Black Science Expand
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: Yuran race 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Wolfein Black Science Expand 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Wolfein Black Science Expand 排在截断型 Prefix 模组 Yuran race 之前，确保双方前置逻辑依次执行
  - 证据: Yuran race: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Wolfein Black Science Expand: [Prefix] BlackScience.PawnRenderUtility_DrawEquipmentAndApparelExtras_SheathPatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Wolfein Black Science Expand: [Postfix] BlackScience.PawnRenderUtility_DrawEquipmentAndApparelExtras_SheathPatch.Postfix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Exosuit Framework ↔ NewRatkinPlus[1.6]
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus[1.6] 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Exosuit Framework 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Exosuit Framework 排在截断型 Prefix 模组 NewRatkinPlus[1.6] 之前，确保双方前置逻辑依次执行
  - 证据: Exosuit Framework: [Prefix] Exosuit.WeaponDrawPosPatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: NewRatkinPlus[1.6]: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] Cinders of the Embergarden ↔ NewRatkinPlus[1.6]
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus[1.6] 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Cinders of the Embergarden 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Cinders of the Embergarden 排在截断型 Prefix 模组 NewRatkinPlus[1.6] 之前，确保双方前置逻辑依次执行
  - 证据: Cinders of the Embergarden: [Prefix] Embergarden.Patch_DrawVehicleTurret.Prefix (Priority=600, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: NewRatkinPlus[1.6]: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] MoeLotl: Rigor Mortis ↔ NewRatkinPlus[1.6]
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus[1.6] 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 MoeLotl: Rigor Mortis 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 MoeLotl: Rigor Mortis 排在截断型 Prefix 模组 NewRatkinPlus[1.6] 之前，确保双方前置逻辑依次执行
  - 证据: MoeLotl: Rigor Mortis: [Prefix] RigorMortis.Render_Patch.Prefix (Priority=40000, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: NewRatkinPlus[1.6]: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] NewRatkinPlus[1.6] ↔ Fortified Features Framework
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus[1.6] 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Fortified Features Framework 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Fortified Features Framework 排在截断型 Prefix 模组 NewRatkinPlus[1.6] 之前，确保双方前置逻辑依次执行
  - 证据: NewRatkinPlus[1.6]: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Fortified Features Framework: [Prefix] Fortified.Patch_DrawVehicleTurret.Prefix (Priority=600, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: Fortified Features Framework: [Prefix] Fortified.Patch_PawnRenderUtility_DrawAimingApparel.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] NewRatkinPlus[1.6] ↔ Epona Dynastic Rise
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus[1.6] 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Epona Dynastic Rise 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Epona Dynastic Rise 排在截断型 Prefix 模组 NewRatkinPlus[1.6] 之前，确保双方前置逻辑依次执行
  - 证据: NewRatkinPlus[1.6]: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Epona Dynastic Rise: [Prefix] Epona.HeavyWeapon.DrawEquipment_WeaponBackPatch.DrawEquipmentPrefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] NewRatkinPlus[1.6] ↔ SRALib
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus[1.6] 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 SRALib 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 SRALib 排在截断型 Prefix 模组 NewRatkinPlus[1.6] 之前，确保双方前置逻辑依次执行
  - 证据: NewRatkinPlus[1.6]: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: SRALib: [Prefix] SRA.Patch_DrawVehicleTurret.Prefix (Priority=600, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: SRALib: [Postfix] SRA.Patch_DrawPawnRenderExtra.Postfix (Priority=200, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [ConditionalPrefixOrResultMutationOverlap] NewRatkinPlus[1.6] ↔ ChezhouLib
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus[1.6] 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 ChezhouLib 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 ChezhouLib 排在截断型 Prefix 模组 NewRatkinPlus[1.6] 之前，确保双方前置逻辑依次执行
  - 证据: NewRatkinPlus[1.6]: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: ChezhouLib: [Prefix] ChezhouLib.Patch.Patch_ClipAnimation.Patch_DrawEquipmentContext.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: ChezhouLib: [Finalizer] ChezhouLib.Patch.Patch_ClipAnimation.Patch_DrawEquipmentContext.Finalizer (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)

### [ConditionalPrefixOrResultMutationOverlap] NewRatkinPlus[1.6] ↔ Wolfein Black Science Expand
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras
- 结论说明: NewRatkinPlus[1.6] 在 Verse.PawnRenderUtility.DrawEquipmentAndApparelExtras 上包含返回 false 的接管型 Prefix，需确保 Wolfein Black Science Expand 的前置补丁先执行
- 处置建议: 已将非截断 Prefix 模组 Wolfein Black Science Expand 排在截断型 Prefix 模组 NewRatkinPlus[1.6] 之前，确保双方前置逻辑依次执行
  - 证据: NewRatkinPlus[1.6]: [Prefix] SYS.DrawEquipment_WeaponBackPatch.DrawEquipmentAndApparelExtrasPrifix (Priority=400, AlwaysSkip=True, CondSkip=False, Guarded=True)
  - 证据: Wolfein Black Science Expand: [Prefix] BlackScience.PawnRenderUtility_DrawEquipmentAndApparelExtras_SheathPatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Wolfein Black Science Expand: [Postfix] BlackScience.PawnRenderUtility_DrawEquipmentAndApparelExtras_SheathPatch.Postfix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Vanilla Expanded Framework ↔ ChezhouLib
- 冲突目标: RimWorld.ApparelGraphicRecordGetter.TryGetGraphicApparel
- 结论说明: Vanilla Expanded Framework 与 ChezhouLib 同时对 RimWorld.ApparelGraphicRecordGetter.TryGetGraphicApparel 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: ChezhouLib 声明了更高的 HarmonyPriority (800 > 400)
  - 证据: Vanilla Expanded Framework: [Transpiler] VEF.Apparels.VanillaExpandedFramework_ApparelGraphicRecordGetter_TryGetGraphicApparel_Transpiler.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: ChezhouLib: [Transpiler] ChezhouLib.ClThingComp.Patch_TryGetGraphicApparel.Transpiler (Priority=800, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] RimTalk ↔ MoeLotl: Rigor Mortis
- 冲突目标: RimWorld.CharacterCardUtility.DoTopStack
- 结论说明: RimTalk 与 MoeLotl: Rigor Mortis 同时对 RimWorld.CharacterCardUtility.DoTopStack 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 RimTalk 在 MoeLotl: Rigor Mortis 之前的稳定加载顺序
  - 证据: RimTalk: [Transpiler] RimTalk.Patches.BioTabPersonalityPatch.DoTopStack_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: MoeLotl: Rigor Mortis: [Prefix] RigorMortis.CharacterCard_Patch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: MoeLotl: Rigor Mortis: [Transpiler] RigorMortis.CharacterCard_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Vanilla Expanded Framework ↔ kemomimihouse HardworkingKz
- 冲突目标: RimWorld.Frame.CompleteConstruction
- 结论说明: Vanilla Expanded Framework 与 kemomimihouse HardworkingKz 同时对 RimWorld.Frame.CompleteConstruction 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Vanilla Expanded Framework 在 kemomimihouse HardworkingKz 之前的稳定加载顺序
  - 证据: Vanilla Expanded Framework: [Transpiler] VEF.Pawns.VanillaExpandedFramework_Frame_CompleteConstruction_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: kemomimihouse HardworkingKz: [Transpiler] Kz.HarmonyPatch_Stable.Frame_CompleteConstruction_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Vanilla Expanded Framework ↔ Replace Stuff - Continued
- 冲突目标: RimWorld.Frame.CompleteConstruction
- 结论说明: Vanilla Expanded Framework 与 Replace Stuff - Continued 同时对 RimWorld.Frame.CompleteConstruction 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 建议将非截断补丁模组 Vanilla Expanded Framework 置于含条件截断 Prefix 的 Replace Stuff - Continued 之前
  - 证据: Vanilla Expanded Framework: [Transpiler] VEF.Pawns.VanillaExpandedFramework_Frame_CompleteConstruction_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Replace Stuff - Continued: [Prefix] Replace_Stuff.Virtualize_CompleteConstruction.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: Replace Stuff - Continued: [Prefix] Replace_Stuff.NewThing.RememberWasNewThing.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Replace Stuff - Continued: [Transpiler] Replace_Stuff.DestroyedRestore.ReviveBuilding.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] kemomimihouse HardworkingKz ↔ Replace Stuff - Continued
- 冲突目标: RimWorld.Frame.CompleteConstruction
- 结论说明: kemomimihouse HardworkingKz 与 Replace Stuff - Continued 同时对 RimWorld.Frame.CompleteConstruction 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 建议将非截断补丁模组 kemomimihouse HardworkingKz 置于含条件截断 Prefix 的 Replace Stuff - Continued 之前
  - 证据: kemomimihouse HardworkingKz: [Transpiler] Kz.HarmonyPatch_Stable.Frame_CompleteConstruction_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Replace Stuff - Continued: [Prefix] Replace_Stuff.Virtualize_CompleteConstruction.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: Replace Stuff - Continued: [Prefix] Replace_Stuff.NewThing.RememberWasNewThing.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Replace Stuff - Continued: [Transpiler] Replace_Stuff.DestroyedRestore.ReviveBuilding.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Graphics Settings+ ↔ HugsLib
- 冲突目标: RimWorld.MainMenuDrawer.DoMainMenuControls
- 结论说明: Graphics Settings+ 与 HugsLib 同时对 RimWorld.MainMenuDrawer.DoMainMenuControls 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Graphics Settings+ 在 HugsLib 之前的稳定加载顺序
  - 证据: Graphics Settings+: [Prefix] GraphicSetter.Patches.UIPatches.DoMainMenuControlsPatch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Graphics Settings+: [Transpiler] GraphicSetter.Patches.UIPatches.DoMainMenuControlsPatch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: HugsLib: [Transpiler] HugsLib.Patches.MainMenuDrawer_Quickstart_Patch.QuicktestButtonUsesQuickstarter (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Vanilla Social Interactions Expanded ↔ Replace Stuff - Continued
- 冲突目标: RimWorld.Mineable.TrySpawnYield
- 结论说明: Vanilla Social Interactions Expanded 与 Replace Stuff - Continued 同时对 RimWorld.Mineable.TrySpawnYield 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Vanilla Social Interactions Expanded 在 Replace Stuff - Continued 之前的稳定加载顺序
  - 证据: Vanilla Social Interactions Expanded: [Transpiler] VanillaSocialInteractionsExpanded.MineableYield_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Replace Stuff - Continued: [Transpiler] Replace_Stuff.OverMineable.DropOnPawn.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Vanilla Expanded Framework ↔ Epona Dynastic Rise
- 冲突目标: RimWorld.PawnFlyer.MakeFlyer
- 结论说明: Vanilla Expanded Framework 与 Epona Dynastic Rise 同时对 RimWorld.PawnFlyer.MakeFlyer 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Vanilla Expanded Framework 在 Epona Dynastic Rise 之前的稳定加载顺序
  - 证据: Vanilla Expanded Framework: [Transpiler] VEF.Abilities.VanillaExpandedFramework_PawnFlyer_MakeFlyer_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Epona Dynastic Rise: [Transpiler] Epona.Riding.Patch_PawnFlyer.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Safe TechHediffs Patch ↔ Raven Race - Embers in Shadow
- 冲突目标: RimWorld.PawnTechHediffsGenerator.GenerateTechHediffsFor
- 结论说明: Safe TechHediffs Patch 与 Raven Race - Embers in Shadow 同时对 RimWorld.PawnTechHediffsGenerator.GenerateTechHediffsFor 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 建议将非截断补丁模组 Raven Race - Embers in Shadow 置于含条件截断 Prefix 的 Safe TechHediffs Patch 之前
  - 证据: Safe TechHediffs Patch: [Prefix] SafeTechHediffs.Patch_GenerateTechHediffsSafe.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: Safe TechHediffs Patch: [Finalizer] SafeTechHediffs.Patch_GenerateTechHediffsSafe.Finalizer (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Safe TechHediffs Patch: [Transpiler] SafeTechHediffs.Patch_GenerateTechHediffsSafe.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Raven Race - Embers in Shadow: [Transpiler] RavenRace.Features.Bionics.Patch_FusangFluidImplantPreference.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] MoeLotl: Rigor Mortis ↔ Draconis Koelime
- 冲突目标: RimWorld.PregnancyUtility.ApplyBirthOutcome
- 结论说明: MoeLotl: Rigor Mortis 与 Draconis Koelime 同时对 RimWorld.PregnancyUtility.ApplyBirthOutcome 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 MoeLotl: Rigor Mortis 在 Draconis Koelime 之前的稳定加载顺序
  - 证据: MoeLotl: Rigor Mortis: [Postfix] RigorMortis.Child_Patch.Postfix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: MoeLotl: Rigor Mortis: [Transpiler] RigorMortis.Child_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Draconis Koelime: [Transpiler] Koelime.Patch.Patch_PregnancyUtility.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] MoeLotl: Rigor Mortis ↔ Ariandel Library
- 冲突目标: RimWorld.PregnancyUtility.ApplyBirthOutcome
- 结论说明: MoeLotl: Rigor Mortis 与 Ariandel Library 同时对 RimWorld.PregnancyUtility.ApplyBirthOutcome 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 MoeLotl: Rigor Mortis 在 Ariandel Library 之前的稳定加载顺序
  - 证据: MoeLotl: Rigor Mortis: [Postfix] RigorMortis.Child_Patch.Postfix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: MoeLotl: Rigor Mortis: [Transpiler] RigorMortis.Child_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Ariandel Library: [Transpiler] AriandelLibrary.AriandelLibrary_PregnancyUtility_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Draconis Koelime ↔ Milira Faction: Milira Imperium
- 冲突目标: RimWorld.PregnancyUtility.ApplyBirthOutcome
- 结论说明: Draconis Koelime 与 Milira Faction: Milira Imperium 同时对 RimWorld.PregnancyUtility.ApplyBirthOutcome 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Draconis Koelime 在 Milira Faction: Milira Imperium 之前的稳定加载顺序
  - 证据: Draconis Koelime: [Transpiler] Koelime.Patch.Patch_PregnancyUtility.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Milira Faction: Milira Imperium: [Transpiler] MiliraImperium.MiliraImperium_PregnancyUtility_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Draconis Koelime ↔ Ariandel Library
- 冲突目标: RimWorld.PregnancyUtility.ApplyBirthOutcome
- 结论说明: Draconis Koelime 与 Ariandel Library 同时对 RimWorld.PregnancyUtility.ApplyBirthOutcome 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Draconis Koelime 在 Ariandel Library 之前的稳定加载顺序
  - 证据: Draconis Koelime: [Transpiler] Koelime.Patch.Patch_PregnancyUtility.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Ariandel Library: [Transpiler] AriandelLibrary.AriandelLibrary_PregnancyUtility_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Vanilla Social Interactions Expanded ↔ EndlessGrowth
- 冲突目标: RimWorld.QualityUtility.GenerateQualityCreatedByPawn
- 结论说明: Vanilla Social Interactions Expanded 与 EndlessGrowth 同时对 RimWorld.QualityUtility.GenerateQualityCreatedByPawn 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Vanilla Social Interactions Expanded 在 EndlessGrowth 之前的稳定加载顺序
  - 证据: Vanilla Social Interactions Expanded: [Transpiler] VanillaSocialInteractionsExpanded.GenerateQualityCreatedByPawn_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: EndlessGrowth: [Transpiler] SlimeSenpai.EndlessGrowth.QualityUtility_GenerateQualityCreatedByPawn_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Dubs Mint Menus ↔ Toggleable Readouts
- 冲突目标: RimWorld.ResourceReadout.DoReadoutCategorized
- 结论说明: Dubs Mint Menus 与 Toggleable Readouts 同时对 RimWorld.ResourceReadout.DoReadoutCategorized 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 建议将非截断补丁模组 Toggleable Readouts 置于含条件截断 Prefix 的 Dubs Mint Menus 之前
  - 证据: Dubs Mint Menus: [Prefix] DubsMintMenus.PinningSys.Patch_DoReadoutCategorized.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: Dubs Mint Menus: [Transpiler] DubsMintMenus.PinningSys.Patch_DoReadoutCategorized.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Toggleable Readouts: [Transpiler] ToggleableReadouts.Replace_DoReadoutCategorized.Transpiler (Priority=0, AlwaysSkip=False, CondSkip=False, Guarded=False)

### [MultipleTranspilersSameTarget] Visible Raid Points ↔ Mechanoids: Total Warfare
- 冲突目标: RimWorld.StorytellerUtility.DefaultThreatPointsNow
- 结论说明: Visible Raid Points 与 Mechanoids: Total Warfare 同时对 RimWorld.StorytellerUtility.DefaultThreatPointsNow 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Visible Raid Points 在 Mechanoids: Total Warfare 之前的稳定加载顺序
  - 证据: Visible Raid Points: [Transpiler] VisibleRaidPoints.Patch_StorytellerUtility_DefaultThreatPointsNow.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Mechanoids: Total Warfare: [Transpiler] NCL_Storyteller.Patch_StorytellerUtility.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Elite Raid 精英化袭击 ↔ Mechanoids: Total Warfare
- 冲突目标: RimWorld.StorytellerUtility.DefaultThreatPointsNow
- 结论说明: Elite Raid 精英化袭击 与 Mechanoids: Total Warfare 同时对 RimWorld.StorytellerUtility.DefaultThreatPointsNow 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Elite Raid 精英化袭击 在 Mechanoids: Total Warfare 之前的稳定加载顺序
  - 证据: Elite Raid 精英化袭击: [Transpiler] EliteRaid.StorytellerUtility_DefaultThreatPointsNow_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Elite Raid 精英化袭击: [Postfix] EliteRaid.Patch_DefaultThreatPointsNow.Postfix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Mechanoids: Total Warfare: [Transpiler] NCL_Storyteller.Patch_StorytellerUtility.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Vanilla Expanded Framework ↔ Best Mix (Continued)
- 冲突目标: RimWorld.WorkGiver_DoBill.TryFindBestBillIngredientsInSet_AllowMix
- 结论说明: Vanilla Expanded Framework 与 Best Mix (Continued) 同时对 RimWorld.WorkGiver_DoBill.TryFindBestBillIngredientsInSet_AllowMix 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Vanilla Expanded Framework 在 Best Mix (Continued) 之前的稳定加载顺序
  - 证据: Vanilla Expanded Framework: [Prefix] VEF.Cooking.VanillaExpandedFramework_WorkGiver_DoBill_TryFindBestBillIngredientsInSet_AllowMix_Patch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Vanilla Expanded Framework: [Transpiler] VEF.Cooking.VanillaExpandedFramework_WorkGiver_DoBill_TryFindBestBillIngredientsInSet_AllowMix_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Best Mix (Continued): [Transpiler] BestMix.Patches.WorkGiver_DoBill_TryFindBestBillIngredientsInSet_AllowMix.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Vanilla Expanded Framework ↔ Epona Dynastic Rise
- 冲突目标: Toils_Combat.<FollowAndMeleeAttack>
- 结论说明: Vanilla Expanded Framework 与 Epona Dynastic Rise 同时对 Toils_Combat.<FollowAndMeleeAttack> 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Vanilla Expanded Framework 在 Epona Dynastic Rise 之前的稳定加载顺序
  - 证据: Vanilla Expanded Framework: [Prefix] VEF.Weapons.VanillaExpandedFramework_Toils_Combat_FollowAndMeleeAttack_Patch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Vanilla Expanded Framework: [Finalizer] VEF.Weapons.VanillaExpandedFramework_Toils_Combat_FollowAndMeleeAttack_Patch.Finalizer (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Vanilla Expanded Framework: [Transpiler] VEF.Weapons.VanillaExpandedFramework_Toils_Combat_FollowAndMeleeAttack_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Epona Dynastic Rise: [Prefix] MstMeleeWeaponRangePatch.MstLittleMeleeWeaponRangePatch_FollowAndMeleeAttack_Patch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)

### [MultipleTranspilersSameTarget] Vanilla Expanded Framework ↔ tailin race1.2.2
- 冲突目标: Toils_Combat.<FollowAndMeleeAttack>
- 结论说明: Vanilla Expanded Framework 与 tailin race1.2.2 同时对 Toils_Combat.<FollowAndMeleeAttack> 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Vanilla Expanded Framework 在 tailin race1.2.2 之前的稳定加载顺序
  - 证据: Vanilla Expanded Framework: [Prefix] VEF.Weapons.VanillaExpandedFramework_Toils_Combat_FollowAndMeleeAttack_Patch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Vanilla Expanded Framework: [Finalizer] VEF.Weapons.VanillaExpandedFramework_Toils_Combat_FollowAndMeleeAttack_Patch.Finalizer (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Vanilla Expanded Framework: [Transpiler] VEF.Weapons.VanillaExpandedFramework_Toils_Combat_FollowAndMeleeAttack_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: tailin race1.2.2: [Prefix] CXCore.MeleeRange.Patch_FollowAndMeleeAttack.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)

### [MultipleTranspilersSameTarget] Epona Dynastic Rise ↔ tailin race1.2.2
- 冲突目标: Toils_Combat.<FollowAndMeleeAttack>
- 结论说明: Epona Dynastic Rise 与 tailin race1.2.2 同时对 Toils_Combat.<FollowAndMeleeAttack> 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Epona Dynastic Rise 在 tailin race1.2.2 之前的稳定加载顺序
  - 证据: Epona Dynastic Rise: [Prefix] MstMeleeWeaponRangePatch.MstLittleMeleeWeaponRangePatch_FollowAndMeleeAttack_Patch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Epona Dynastic Rise: [Finalizer] MstMeleeWeaponRangePatch.MstLittleMeleeWeaponRangePatch_FollowAndMeleeAttack_Patch.Finalizer (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Epona Dynastic Rise: [Transpiler] MstMeleeWeaponRangePatch.MstLittleMeleeWeaponRangePatch_FollowAndMeleeAttack_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: tailin race1.2.2: [Prefix] CXCore.MeleeRange.Patch_FollowAndMeleeAttack.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)

### [MultipleTranspilersSameTarget] Vanilla Expanded Framework ↔ Epona Dynastic Rise
- 冲突目标: Toils_Combat.<GotoCastPosition>
- 结论说明: Vanilla Expanded Framework 与 Epona Dynastic Rise 同时对 Toils_Combat.<GotoCastPosition> 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Vanilla Expanded Framework 在 Epona Dynastic Rise 之前的稳定加载顺序
  - 证据: Vanilla Expanded Framework: [Transpiler] VEF.Weapons.VanillaExpandedFramework_Toils_Combat_GotoCastPosition_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Epona Dynastic Rise: [Transpiler] MstMeleeWeaponRangePatch.MstLittleMeleeWeaponRangePatch_GotoCastPosition_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Vanilla Expanded Framework ↔ Epona Dynastic Rise
- 冲突目标: Verse.AI.JobDriver_Wait.CheckForAutoAttack
- 结论说明: Vanilla Expanded Framework 与 Epona Dynastic Rise 同时对 Verse.AI.JobDriver_Wait.CheckForAutoAttack 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Vanilla Expanded Framework 在 Epona Dynastic Rise 之前的稳定加载顺序
  - 证据: Vanilla Expanded Framework: [Transpiler] VEF.Weapons.VanillaExpandedFramework_JobDriver_Wait_CheckForAutoAttack_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Epona Dynastic Rise: [Prefix] MstMeleeWeaponRangePatch.MstLittleMeleeWeaponRangePatch_JobDriver_Wait_CheckForAutoAttack_Patch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Epona Dynastic Rise: [Finalizer] MstMeleeWeaponRangePatch.MstLittleMeleeWeaponRangePatch_JobDriver_Wait_CheckForAutoAttack_Patch.Finalizer (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Epona Dynastic Rise: [Transpiler] MstMeleeWeaponRangePatch.MstLittleMeleeWeaponRangePatch_JobDriver_Wait_CheckForAutoAttack_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Vanilla Expanded Framework ↔ tailin race1.2.2
- 冲突目标: Verse.AI.JobDriver_Wait.CheckForAutoAttack
- 结论说明: Vanilla Expanded Framework 与 tailin race1.2.2 同时对 Verse.AI.JobDriver_Wait.CheckForAutoAttack 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Vanilla Expanded Framework 在 tailin race1.2.2 之前的稳定加载顺序
  - 证据: Vanilla Expanded Framework: [Transpiler] VEF.Weapons.VanillaExpandedFramework_JobDriver_Wait_CheckForAutoAttack_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: tailin race1.2.2: [Transpiler] CXCore.MeleeRange.Patch_JobDriver_Wait_CheckForAutoAttack.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)

### [MultipleTranspilersSameTarget] Epona Dynastic Rise ↔ tailin race1.2.2
- 冲突目标: Verse.AI.JobDriver_Wait.CheckForAutoAttack
- 结论说明: Epona Dynastic Rise 与 tailin race1.2.2 同时对 Verse.AI.JobDriver_Wait.CheckForAutoAttack 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Epona Dynastic Rise 在 tailin race1.2.2 之前的稳定加载顺序
  - 证据: Epona Dynastic Rise: [Prefix] MstMeleeWeaponRangePatch.MstLittleMeleeWeaponRangePatch_JobDriver_Wait_CheckForAutoAttack_Patch.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Epona Dynastic Rise: [Finalizer] MstMeleeWeaponRangePatch.MstLittleMeleeWeaponRangePatch_JobDriver_Wait_CheckForAutoAttack_Patch.Finalizer (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)
  - 证据: Epona Dynastic Rise: [Transpiler] MstMeleeWeaponRangePatch.MstLittleMeleeWeaponRangePatch_JobDriver_Wait_CheckForAutoAttack_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: tailin race1.2.2: [Transpiler] CXCore.MeleeRange.Patch_JobDriver_Wait_CheckForAutoAttack.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)

### [MultipleTranspilersSameTarget] 联合重工-UF Heavy Industries ↔ [1.6] CAI 5000 - Advanced AI + Fog Of War (continued)
- 冲突目标: Verse.AI.Pawn_PathFollower.SetupMoveIntoNextCell
- 结论说明: 联合重工-UF Heavy Industries 与 [1.6] CAI 5000 - Advanced AI + Fog Of War (continued) 同时对 Verse.AI.Pawn_PathFollower.SetupMoveIntoNextCell 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 联合重工-UF Heavy Industries 在 [1.6] CAI 5000 - Advanced AI + Fog Of War (continued) 之前的稳定加载顺序
  - 证据: 联合重工-UF Heavy Industries: [Transpiler] TurbojetBackpack.Patch_Pawn_PathFollower.Transpiler_Setup (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: [1.6] CAI 5000 - Advanced AI + Fog Of War (continued): [Transpiler] CombatAI.Patches.Pawn_PathFollower_Patch.Pawn_PathFollower__Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Allow Tool (1.6 Temp) ↔ Architect Menu Optimizer
- 冲突目标: Verse.Command.GizmoOnGUIInt
- 结论说明: Allow Tool (1.6 Temp) 与 Architect Menu Optimizer 同时对 Verse.Command.GizmoOnGUIInt 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: Architect Menu Optimizer 声明了更高的 HarmonyPriority (800 > 400)
  - 证据: Allow Tool (1.6 Temp): [Transpiler] AllowTool.Patches.Command_GizmoOnGUI_Patch.DrawRightClickIcon (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Allow Tool (1.6 Temp): [Postfix] AllowTool.Patches.Command_GizmoOnGUI_Patch.InterceptInteraction (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Architect Menu Optimizer: [Transpiler] ArchitectMenuOptimizer.Patches.Patch_TextMeasure.Transpiler (Priority=800, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Vanilla Expanded Framework ↔ Dev In Your Language
- 冲突目标: Verse.DebugWindowsOpener.DrawButtons
- 结论说明: Vanilla Expanded Framework 与 Dev In Your Language 同时对 Verse.DebugWindowsOpener.DrawButtons 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Vanilla Expanded Framework 在 Dev In Your Language 之前的稳定加载顺序
  - 证据: Vanilla Expanded Framework: [Transpiler] VEF.Sounds.VanillaExpandedFramework_DebugWindowsOpener_DrawButtons_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Dev In Your Language: [Transpiler] DevInYourLanguage.H_DebugWindowsOpener.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Vanilla Expanded Framework ↔ HugsLib
- 冲突目标: Verse.DebugWindowsOpener.DrawButtons
- 结论说明: Vanilla Expanded Framework 与 HugsLib 同时对 Verse.DebugWindowsOpener.DrawButtons 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Vanilla Expanded Framework 在 HugsLib 之前的稳定加载顺序
  - 证据: Vanilla Expanded Framework: [Transpiler] VEF.Sounds.VanillaExpandedFramework_DebugWindowsOpener_DrawButtons_Patch.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: HugsLib: [Transpiler] HugsLib.Patches.DebugWindowsOpener_Patch.DrawAdditionalButtons (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Dev In Your Language ↔ HugsLib
- 冲突目标: Verse.DebugWindowsOpener.DrawButtons
- 结论说明: Dev In Your Language 与 HugsLib 同时对 Verse.DebugWindowsOpener.DrawButtons 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Dev In Your Language 在 HugsLib 之前的稳定加载顺序
  - 证据: Dev In Your Language: [Transpiler] DevInYourLanguage.H_DebugWindowsOpener.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: HugsLib: [Transpiler] HugsLib.Patches.DebugWindowsOpener_Patch.DrawAdditionalButtons (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Anty the war ant race ↔ Miho, the celestial fox
- 冲突目标: Verse.Explosion.TrySpawnExplosionThing
- 结论说明: Anty the war ant race 与 Miho, the celestial fox 同时对 Verse.Explosion.TrySpawnExplosionThing 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Anty the war ant race 在 Miho, the celestial fox 之前的稳定加载顺序
  - 证据: Anty the war ant race: [Transpiler] ExplosionFaction.ExplodeFaction.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Miho, the celestial fox: [Transpiler] Monotail.ExplodeFaction.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Vanilla Expanded Framework ↔ Replace Stuff - Continued
- 冲突目标: Verse.GhostUtility.GhostGraphicFor
- 结论说明: Vanilla Expanded Framework 与 Replace Stuff - Continued 同时对 Verse.GhostUtility.GhostGraphicFor 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 建议将非截断补丁模组 Replace Stuff - Continued 置于含条件截断 Prefix 的 Vanilla Expanded Framework 之前
  - 证据: Vanilla Expanded Framework: [Prefix] VEF.Buildings.VanillaExpandedFramework_GhostUtility_GhostGraphicFor_Patch.DisplayBlueprintGraphic (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: Vanilla Expanded Framework: [Transpiler] VEF.Buildings.CustomizableGraphicsPatches.UseUiIconForCustomizableGraphicGhosts.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Replace Stuff - Continued: [Transpiler] Replace_Stuff.OverMineable.ShowGhostOverFog.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=False)

### [MultipleTranspilersSameTarget] Allow Tool (1.6 Temp) ↔ Blueprints Forked - 1.6
- 冲突目标: Verse.GizmoGridDrawer.DrawGizmoGridFor
- 结论说明: Allow Tool (1.6 Temp) 与 Blueprints Forked - 1.6 同时对 Verse.GizmoGridDrawer.DrawGizmoGridFor 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Allow Tool (1.6 Temp) 在 Blueprints Forked - 1.6 之前的稳定加载顺序
  - 证据: Allow Tool (1.6 Temp): [Transpiler] AllowTool.Patches.GizmoGridDrawer_DrawGizmoGridFor_Patch.ClearReverseDesignators (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Blueprints Forked - 1.6: [Transpiler] Blueprints.Patch_GizmoGridDrawer_DrawGizmoGridFor.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Customize Weapon ↔ ChezhouLib
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAiming
- 结论说明: Customize Weapon 与 ChezhouLib 同时对 Verse.PawnRenderUtility.DrawEquipmentAiming 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 建议将非截断补丁模组 Customize Weapon 置于含条件截断 Prefix 的 ChezhouLib 之前
  - 证据: Customize Weapon: [Transpiler] CWF.HarmonyPatches.Transpiler_PawnRenderUtility_DrawEquipmentAiming.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: ChezhouLib: [Prefix] ChezhouLib.Patch.Patch_DrawEquipmentAiming_Prefix.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: ChezhouLib: [Prefix] ChezhouLib.Extension.Patch_HideWeaponOnPawn.Prefix (Priority=400, AlwaysSkip=False, CondSkip=True, Guarded=True)
  - 证据: ChezhouLib: [Transpiler] ChezhouLib.Patch.Patch_ClipAnimation.Patch_DrawEquipmentAiming.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Customize Weapon ↔ Raven Race - Embers in Shadow
- 冲突目标: Verse.PawnRenderUtility.DrawEquipmentAiming
- 结论说明: Customize Weapon 与 Raven Race - Embers in Shadow 同时对 Verse.PawnRenderUtility.DrawEquipmentAiming 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: Raven Race - Embers in Shadow 声明了更高的 HarmonyPriority (800 > 400)
  - 证据: Customize Weapon: [Transpiler] CWF.HarmonyPatches.Transpiler_PawnRenderUtility_DrawEquipmentAiming.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Raven Race - Embers in Shadow: [Prefix] RavenRace.Features.UniqueWeapons.ModiM01.Patch_ModiEquipmentRendering.Prefix (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Raven Race - Embers in Shadow: [Transpiler] RavenRace.Features.UniqueWeapons.ModiM01.Patch_ModiEquipmentRendering.Transpiler (Priority=800, AlwaysSkip=False, CondSkip=False, Guarded=True)

### [MultipleTranspilersSameTarget] Adaptive Storage Framework ↔ Raven Race - Embers in Shadow
- 冲突目标: Verse.ThingOverlays.ThingOverlaysOnGUI
- 结论说明: Adaptive Storage Framework 与 Raven Race - Embers in Shadow 同时对 Verse.ThingOverlays.ThingOverlaysOnGUI 挂载了 Transpiler，静态分析无法排除 IL 锚点相互影响
- 处置建议: 保持 Adaptive Storage Framework 在 Raven Race - Embers in Shadow 之前的稳定加载顺序
  - 证据: Adaptive Storage Framework: [Transpiler] AdaptiveStorage.HarmonyPatches.CacheZoomAndMousePosition.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)
  - 证据: Raven Race - Embers in Shadow: [Transpiler] RavenRace.Core.Harmony.Patch_ThingOverlaysOnGUI_HideStoredItems.Transpiler (Priority=400, AlwaysSkip=False, CondSkip=False, Guarded=True)

## 四、1.绿色（共同作用于同一目标但确认无冲突）明细（共 8037 项）

- [AdditiveXmlPatchOverlap] NewRatkinPlus + Mincho, The Mint Choco Slime ~ HAR ver. 于 */BiomeDef: NewRatkinPlus 与 Mincho, The Mint Choco Slime ~ HAR ver. 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + NewRatkinPlus[1.6] 于 */BiomeDef: NewRatkinPlus 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + Epona Dynastic Rise 于 */BiomeDef: NewRatkinPlus 与 Epona Dynastic Rise 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + 中式龙娘——冶龍 SMELTED LOONG 于 */BiomeDef: NewRatkinPlus 与 中式龙娘——冶龍 SMELTED LOONG 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Mincho, The Mint Choco Slime ~ HAR ver. + NewRatkinPlus[1.6] 于 */BiomeDef: Mincho, The Mint Choco Slime ~ HAR ver. 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Mincho, The Mint Choco Slime ~ HAR ver. + Epona Dynastic Rise 于 */BiomeDef: Mincho, The Mint Choco Slime ~ HAR ver. 与 Epona Dynastic Rise 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Mincho, The Mint Choco Slime ~ HAR ver. + 中式龙娘——冶龍 SMELTED LOONG 于 */BiomeDef: Mincho, The Mint Choco Slime ~ HAR ver. 与 中式龙娘——冶龍 SMELTED LOONG 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus[1.6] + Epona Dynastic Rise 于 */BiomeDef: NewRatkinPlus[1.6] 与 Epona Dynastic Rise 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus[1.6] + 中式龙娘——冶龍 SMELTED LOONG 于 */BiomeDef: NewRatkinPlus[1.6] 与 中式龙娘——冶龍 SMELTED LOONG 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Epona Dynastic Rise + 中式龙娘——冶龍 SMELTED LOONG 于 */BiomeDef: Epona Dynastic Rise 与 中式龙娘——冶龍 SMELTED LOONG 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Dubs Bad Hygiene Lite + Dubs Central Heating 于 */DesignationCategoryDef[defName = "Temperature"]/specialDesignatorClasses: Dubs Bad Hygiene Lite 与 Dubs Central Heating 存在明确上下游依赖，按顺序对同一 XPath 节点执行扩展更新
- [AdditiveXmlPatchOverlap] Dubs Bad Hygiene Lite + Dubs Bad Hygiene 于 */DesignationCategoryDef[defName = "Temperature"]/specialDesignatorClasses: Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Dubs Central Heating + Dubs Bad Hygiene 于 */DesignationCategoryDef[defName = "Temperature"]/specialDesignatorClasses: Dubs Central Heating 与 Dubs Bad Hygiene 存在明确上下游依赖，按顺序对同一 XPath 节点执行扩展更新
- [AdditiveXmlPatchOverlap] Dubs Bad Hygiene Lite + Dubs Bad Hygiene 于 */DutyDef[defName = "Relax"]: Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Dubs Bad Hygiene Lite + Dubs Bad Hygiene 于 */DutyDef[defName = "Relax"]/thinkNode[@Class="ThinkNode_Priority"]/subNodes/li[@Class="Hospitality.ThinkNode_OnlyAllowed"]/subNodes/li[@Class="ThinkNode_Tagger"]/subNodes/li[@Class="ThinkNode_PrioritySorter"]/subNodes//li[1]: Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Mincho, The Mint Choco Slime ~ HAR ver. + Marilyn the Mincho Worshipper Witch ~ HAR ver. 于 */MeditationFocusDef[defName="Natural"]/requiredBackstoriesAny: Mincho, The Mint Choco Slime ~ HAR ver. 与 Marilyn the Mincho Worshipper Witch ~ HAR ver. 存在明确上下游依赖，按顺序对同一 XPath 节点执行扩展更新
- [AdditiveXmlPatchOverlap] NewRatkinPlus + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName = "ComplexClothing"]/tags: NewRatkinPlus 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName = "ComplexFurniture"]/tags: NewRatkinPlus 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName = "PassiveCooler"]/tags: NewRatkinPlus 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName = "Pemmican"]/tags: NewRatkinPlus 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + MoeLotl Race 于 */ResearchProjectDef[defName = "Smithing"]/tags: NewRatkinPlus 与 MoeLotl Race 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName = "Smithing"]/tags: NewRatkinPlus 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] MoeLotl Race + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName = "Smithing"]/tags: MoeLotl Race 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName = "Stonecutting"]/tags: NewRatkinPlus 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + MoeLotl Race 于 */ResearchProjectDef[defName="ComplexClothing"]/tags: NewRatkinPlus 与 MoeLotl Race 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName="ComplexClothing"]/tags: NewRatkinPlus 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] MoeLotl Race + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName="ComplexClothing"]/tags: MoeLotl Race 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + MoeLotl Race 于 */ResearchProjectDef[defName="ComplexFurniture"]/tags: NewRatkinPlus 与 MoeLotl Race 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName="ComplexFurniture"]/tags: NewRatkinPlus 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] MoeLotl Race + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName="ComplexFurniture"]/tags: MoeLotl Race 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + MoeLotl Race 于 */ResearchProjectDef[defName="PassiveCooler"]/tags: NewRatkinPlus 与 MoeLotl Race 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName="PassiveCooler"]/tags: NewRatkinPlus 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] MoeLotl Race + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName="PassiveCooler"]/tags: MoeLotl Race 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + MoeLotl Race 于 */ResearchProjectDef[defName="Pemmican"]/tags: NewRatkinPlus 与 MoeLotl Race 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName="Pemmican"]/tags: NewRatkinPlus 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] MoeLotl Race + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName="Pemmican"]/tags: MoeLotl Race 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + MoeLotl Race 于 */ResearchProjectDef[defName="Smithing"]: NewRatkinPlus 与 MoeLotl Race 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName="Smithing"]: NewRatkinPlus 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] MoeLotl Race + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName="Smithing"]: MoeLotl Race 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + MoeLotl Race 于 */ResearchProjectDef[defName="Smithing"]/tags: NewRatkinPlus 与 MoeLotl Race 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName="Smithing"]/tags: NewRatkinPlus 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] MoeLotl Race + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName="Smithing"]/tags: MoeLotl Race 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + MoeLotl Race 于 */ResearchProjectDef[defName="Stonecutting"]/tags: NewRatkinPlus 与 MoeLotl Race 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName="Stonecutting"]/tags: NewRatkinPlus 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] MoeLotl Race + NewRatkinPlus[1.6] 于 */ResearchProjectDef[defName="Stonecutting"]/tags: MoeLotl Race 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + NewRatkinPlus[1.6] 于 */ThingDef[defName = "Hay"]: NewRatkinPlus 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Race + Sparkling Worlds - Full Mod 于 */ThingDef[defName = "HiTechResearchBench"]/comps/li[@Class = "CompProperties_AffectedByFacilities"]/linkableFacilities: Milira Race 与 Sparkling Worlds - Full Mod 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Race + Fortifications - Industrial 于 */ThingDef[defName = "HiTechResearchBench"]/comps/li[@Class = "CompProperties_AffectedByFacilities"]/linkableFacilities: Milira Race 与 Fortifications - Industrial 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Race + CeleTech Arsenal MKIII 于 */ThingDef[defName = "HiTechResearchBench"]/comps/li[@Class = "CompProperties_AffectedByFacilities"]/linkableFacilities: Milira Race 与 CeleTech Arsenal MKIII 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Sparkling Worlds - Full Mod + Fortifications - Industrial 于 */ThingDef[defName = "HiTechResearchBench"]/comps/li[@Class = "CompProperties_AffectedByFacilities"]/linkableFacilities: Sparkling Worlds - Full Mod 与 Fortifications - Industrial 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Sparkling Worlds - Full Mod + CeleTech Arsenal MKIII 于 */ThingDef[defName = "HiTechResearchBench"]/comps/li[@Class = "CompProperties_AffectedByFacilities"]/linkableFacilities: Sparkling Worlds - Full Mod 与 CeleTech Arsenal MKIII 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Fortifications - Industrial + CeleTech Arsenal MKIII 于 */ThingDef[defName = "HiTechResearchBench"]/comps/li[@Class = "CompProperties_AffectedByFacilities"]/linkableFacilities: Fortifications - Industrial 与 CeleTech Arsenal MKIII 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + NewRatkinPlus[1.6] 于 */ThingDef[defName="Hay"]: NewRatkinPlus 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] NewRatkinPlus + NewRatkinPlus[1.6] 于 */ThingDef[defName="Hay"]/comps: NewRatkinPlus 与 NewRatkinPlus[1.6] 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Dubs Bad Hygiene Lite + Dubs Bad Hygiene 于 */ThinkTreeDef[defName = "MainColonistBehaviorCore"]/thinkRoot[@Class="ThinkNode_Tagger"]/subNodes/li[@Class="ThinkNode_PrioritySorter"]/subNodes//li[2]: Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Dubs Bad Hygiene Lite + Dubs Bad Hygiene 于 */ThinkTreeDef[defName = "SatisfyBasicNeeds"]/thinkRoot[@Class="ThinkNode_Tagger"]/subNodes/li[@Class="ThinkNode_PrioritySorter"]/subNodes//li[2]: Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Dubs Bad Hygiene Lite + Dubs Bad Hygiene 于 */ThinkTreeDef[defName = "SatisfyVeryUrgentNeeds"]/thinkRoot[@Class="ThinkNode_Tagger"]/subNodes/li[@Class="ThinkNode_PrioritySorter"]/subNodes//li[2]: Dubs Bad Hygiene Lite 与 Dubs Bad Hygiene 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Vanilla Expanded Framework + Auto Ability 于 .//li[@Class="JobGiver_Orders"]: Vanilla Expanded Framework 与 Auto Ability 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] VGP Vegetable Garden + Mincho, The Mint Choco Slime ~ HAR ver. 于 /Defs: VGP Vegetable Garden 与 Mincho, The Mint Choco Slime ~ HAR ver. 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] 绮罗蹭饭扩展 (Kiiro Bum Meals Off Expanded) + Milira Faction: Milira Imperium 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Kiiro_Race"]/alienRace/generalSettings/alienPartGenerator/bodyAddons: 绮罗蹭饭扩展 (Kiiro Bum Meals Off Expanded) 与 Milira Faction: Milira Imperium 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Event Story Expand: The Tale of Milira + Milira Faction: Milira Imperium 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/generalSettings/alienPartGenerator/bodyAddons: Milira Event Story Expand: The Tale of Milira 与 Milira Faction: Milira Imperium 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Event Story Expand: The Tale of Milira + Dellin's Milira Attire Expaned 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/generalSettings/alienPartGenerator/bodyAddons: Milira Event Story Expand: The Tale of Milira 与 Dellin's Milira Attire Expaned 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Faction: Milira Imperium + Dellin's Milira Attire Expaned 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/generalSettings/alienPartGenerator/bodyAddons: Milira Faction: Milira Imperium 与 Dellin's Milira Attire Expaned 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Event Story Expand: The Tale of Milira + Milira Faction: Milira Imperium 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/generalSettings/alienPartGenerator/bodyAddons/li[name="milira ahoge"]/conditions: Milira Event Story Expand: The Tale of Milira 与 Milira Faction: Milira Imperium 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Event Story Expand: The Tale of Milira + Dellin's Milira Attire Expaned 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/generalSettings/alienPartGenerator/bodyAddons/li[name="milira ahoge"]/conditions: Milira Event Story Expand: The Tale of Milira 与 Dellin's Milira Attire Expaned 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Faction: Milira Imperium + Dellin's Milira Attire Expaned 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/generalSettings/alienPartGenerator/bodyAddons/li[name="milira ahoge"]/conditions: Milira Faction: Milira Imperium 与 Dellin's Milira Attire Expaned 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Event Story Expand: The Tale of Milira + Milira Faction: Milira Imperium 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/generalSettings/alienPartGenerator/bodyAddons/li[name="milira hair BG"]/conditions: Milira Event Story Expand: The Tale of Milira 与 Milira Faction: Milira Imperium 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Event Story Expand: The Tale of Milira + Dellin's Milira Attire Expaned 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/generalSettings/alienPartGenerator/bodyAddons/li[name="milira hair BG"]/conditions: Milira Event Story Expand: The Tale of Milira 与 Dellin's Milira Attire Expaned 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Faction: Milira Imperium + Dellin's Milira Attire Expaned 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/generalSettings/alienPartGenerator/bodyAddons/li[name="milira hair BG"]/conditions: Milira Faction: Milira Imperium 与 Dellin's Milira Attire Expaned 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Event Story Expand: The Tale of Milira + Milira Faction: Milira Imperium 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/generalSettings/alienPartGenerator/bodyAddons/li[name="milira hair"]/conditions: Milira Event Story Expand: The Tale of Milira 与 Milira Faction: Milira Imperium 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Event Story Expand: The Tale of Milira + Dellin's Milira Attire Expaned 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/generalSettings/alienPartGenerator/bodyAddons/li[name="milira hair"]/conditions: Milira Event Story Expand: The Tale of Milira 与 Dellin's Milira Attire Expaned 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Faction: Milira Imperium + Dellin's Milira Attire Expaned 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/generalSettings/alienPartGenerator/bodyAddons/li[name="milira hair"]/conditions: Milira Faction: Milira Imperium 与 Dellin's Milira Attire Expaned 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Event Story Expand: The Tale of Milira + Milira Faction: Milira Imperium 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/generalSettings/alienPartGenerator/bodyAddons/li[name="milira halo"]/conditions: Milira Event Story Expand: The Tale of Milira 与 Milira Faction: Milira Imperium 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Event Story Expand: The Tale of Milira + Dellin's Milira Attire Expaned 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/generalSettings/alienPartGenerator/bodyAddons/li[name="milira halo"]/conditions: Milira Event Story Expand: The Tale of Milira 与 Dellin's Milira Attire Expaned 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Faction: Milira Imperium + Dellin's Milira Attire Expaned 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/generalSettings/alienPartGenerator/bodyAddons/li[name="milira halo"]/conditions: Milira Faction: Milira Imperium 与 Dellin's Milira Attire Expaned 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Event Story Expand: The Tale of Milira + Milira Faction: Milira Imperium 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/generalSettings/alienPartGenerator/colorChannels: Milira Event Story Expand: The Tale of Milira 与 Milira Faction: Milira Imperium 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Event Story Expand: The Tale of Milira + Dellin's Milira Attire Expaned 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/generalSettings/alienPartGenerator/colorChannels: Milira Event Story Expand: The Tale of Milira 与 Dellin's Milira Attire Expaned 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] Milira Faction: Milira Imperium + Dellin's Milira Attire Expaned 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/generalSettings/alienPartGenerator/colorChannels: Milira Faction: Milira Imperium 与 Dellin's Milira Attire Expaned 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] 瓦莱丽的米莉拉扩展包 + Milira Faction: Milira Imperium 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/raceRestriction/apparelList: 瓦莱丽的米莉拉扩展包 与 Milira Faction: Milira Imperium 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- [AdditiveXmlPatchOverlap] 瓦莱丽的米莉拉扩展包 + Dellin's Milira Attire Expaned 于 /Defs/AlienRace.ThingDef_AlienRace[defName="Milira_Race"]/alienRace/raceRestriction/apparelList: 瓦莱丽的米莉拉扩展包 与 Dellin's Milira Attire Expaned 对同一 XPath 节点执行追加或带条件兼容门的 Patch，可安全共存
- …（另有 7957 项绿色重叠记录）


# 维护记录：2026-10-24

后续进度：用户已验收最初修复与本地化；光披风统一数据与战斗接入的最新说明见 `SCARF_INTEGRATION.md`。下文保留各阶段的历史状态，新的接入内容以该文档为准。

## 本轮范围

修复游戏更新后的胜利动作触发，补充英语和日语，整理重复代码，并核对 Skin Changer 兼容。视觉还原与演出增强留待单独讨论。

本轮没有修改 Godot 场景、Shader、动画帧或音频。中文原文保持原样；英日文由现有中文翻译，并非声称已与《死亡细胞》官方译文逐条校对。

## 胜利动作

旧补丁指定 `CombatManager.EndCombatInternal()`。本机游戏的正常胜利流程通过 `CheckWinCondition(CombatTurnState)` 调用 `EndCombatInternal(CombatTurnState)`，因此旧补丁不能触发。

新补丁使用 `TargetMethod()` 精确定位带内部类型 `MegaCrit.Sts2.Core.Combat.CombatTurnState` 的重载。不直接引用不可访问的内部类型，也不笼统匹配所有同名方法。原有三种胜利动画保留；已经播放死亡动画或不在场景树中的精灵不播放庆祝动作。

## 文本与代码结构

- `Scripts/KingLocalization.cs`：读取游戏 `LocManager.Instance.Language`；支持 `zhs`、`eng`、`jpn`，未支持语言回退英语，单项缺失继续回退中文。
- `Localization/*.json`：每种语言包含 6 条界面文本和 30 条夜歌台词，编译为 DLL 内嵌资源。更新译文只需重编 DLL，无需重导 PCK。
- `Scripts/SerenadeSpeech.cs`：弹幕显示逻辑从战斗补丁中独立出来。长文本自动换行，按实际尺寸居中。
- `Scripts/KingSelectBackground.cs`：复用多人读档主机与客机的背景挂载逻辑；两者各自的角色提取逻辑保留。
- `KingGlobals.ApplyCurrentSkin`：战斗、商店与营地统一复用原有调色板设置，保留实例材质副本。
- 删除了没有调用者的旧换肤方法、空语音池和音频缓存方法；纠正了头像替换“已注释休眠”的过时注释。

## Skin Changer

参考资料为未锈铠项目中的 `03_研究记录/兼容性/SkinChanger/苏芙比_皮肤切换器兼容交接.md`，其他项目与第三方文件均未修改。

本项目本来就有显式字段 `public const string TargetCharacterId = "REGENT"`，符合实际安装的 Skin Changer 1.0.19.0 的扫描规则，因此不需要照搬苏芙比的补丁。该字段应保留，不能在重构时只留下方法内的字符串比较。

使用切换器实际 DLL 的扫描器与资源目录，对现有工坊工作区旧包及本轮新包进行离线检查：

- 两者均识别为 `targets=[regent]`、`direct_visual_patch=true`。
- 选择国王 → 切回原皮 → 重选国王，对应提供者为 `Hcxmmx_King_Skin` → 空 → `Hcxmmx_King_Skin`。
- 没有运行 Mod 初始化器、没有安装 Harmony 补丁、没有修改切换器或用户设置。

## 构建与检查

项目 SDK 为 Godot.NET.Sdk 4.5.1，目标框架 net9.0。游戏路径由 `Sts2Dir` 指定，可通过 MSBuild 参数覆盖。

仅构建，不自动复制到游戏目录：

```powershell
dotnet build Hcxmmx_King_Skin.csproj -c Release -p:DeployModOnBuild=false
```

产物：`.godot/mono/temp/bin/Release/Hcxmmx_King_Skin.dll`。原工程的默认构建部署行为保留；检查时务必传上述参数。

本轮本地预览包位于 `artifacts/preview/Hcxmmx_King_Skin/`，包含新 DLL、原清单和逐字节复用的上传工作区 PCK。没有覆盖工坊上传目录，也没有部署或发布。版本号暂时保持原值，待实机验收后决定发布版本。

注意：`03-godot-project/Hcxmmx_King_Skin.pck` 与上传工作区 PCK 的哈希不同，不能当作同一份。本轮预览包沿用 `04_ModUper/ModUploader-win-x64/NewModWorkspace/content/Hcxmmx_King_Skin.pck`，SHA-256 为 `a1f82cbd99e0bda91c593380808e4b46e7ab108ef7e1c788779ba4cf9eabb0f8`，两处原文件都没有改动。

离线验证工具位于 `02_Scripts_Tools/Validation/`，需要 .NET 10 SDK。完整运行命令：

```powershell
dotnet run --project 02_Scripts_Tools/Validation/KingValidation.csproj -c Release -- 'D:\youxi\steamapps\common\Slay the Spire 2' 'D:\youxi\steamapps\workshop\content\2868840\3787302680' 'D:\King_Workspace\04_ModUper\ModUploader-win-x64\NewModWorkspace\content' 'D:\King_Workspace\artifacts\preview\Hcxmmx_King_Skin'
```

检查结果：Release 构建 0 警告、0 错误；21 个 Harmony 补丁目标及参数绑定能在本机游戏程序集解析；胜利补丁定位到带参重载；三种语言文本与台词完整，未支持语言的英文回退通过；旧包、新包的实际切换器识别与选择路由通过。

这些检查不等于游戏内验收。待实机确认：普通战斗胜利动作；中英日选人介绍、换肤按钮与夜歌弹幕的字体和换行；切回原皮再重选后的实际场景表现。用户此前已反馈旧包在切换器下兼容，本轮新包尚未实机验收。

## 本地验收部署

2026-10-24，按用户要求，将预览包的 DLL、JSON、PCK 部署到 `D:\youxi\steamapps\common\Slay the Spire 2\mods\Hcxmmx_King_Skin`。部署前未检测到游戏进程，目标目录此前不存在；三个文件均与预览包完成 SHA-256 一致性核对。没有启动游戏、覆盖工坊上传文件或发布更新，实机验收待用户反馈。

用户随后反馈本轮实机验收完成，均无问题。

## 光披风预览

按用户要求建立独立 `05_Preview/` Godot 工程，左右对照现有场景与光披风重建。使用上传工作区原 PCK、原版 `scarfGlow` 贴图、`KingDefault` / `KingWhite` 各五条围巾配置和逐帧躯干挂点。53 个动作共 1443 帧均能映射到原版挂点。当前摆动算法与柔光为重建小样，待用户观察调整；没有把预览代码接入或部署到正式 Mod。

用户指出初版挂点、重力和待机方向错误后，已从本机游戏字节码核对 `ScarfSegment`：改用 `headBone` 与原背侧偏移，废弃初版人为水平力，移植适用分支的重力、阻尼、长度和分段缩放公式。新增无风待机专项检查与“仅看光带”。完整依据和剩余差异记录于 `05_Preview/ORIGINAL_SCARF.md`；初版躯干挂点说明仅代表已废弃的方案。

用户认可修正后要求继续复刻。本轮在独立预览接入原版环境风、武器出招反向推力、0.4 秒活跃期、141 帧前后遮挡及原初始化随机贴图选择；统一动作与物理时间步进，补充可恢复随机/物理状态的逐帧回看。正反风、转身、遮挡边界、推力衰减和回看一致性检查通过；静止、逆风、前层攻击、转身四种原生渲染图已保存。正式 Mod 及游戏 mods 均未部署新改动。

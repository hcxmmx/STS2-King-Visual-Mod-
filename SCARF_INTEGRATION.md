# 光披风接入与验收

2026-10-24，第一阶段。本地测试版沿用发布清单版本号 1.0；正式更新版本号待验收后统一确定。

## 本次实现

- `03-godot-project/Hcxmmx_King/Presentation/weapons.json` 为武器唯一维护源，保留原有 10 组、31 段动作与 30 个刀光映射、等权抽取。DLL 内嵌此文件；预览生成器不再解析 C# 字符串。
- `presentation.json` 从原版 `data.cdb`、`king_tracks.json` 和现有身体序列生成，包含 1424 个唯一头部挂点及 141 个前层帧，共覆盖 53 个动作、1443 个动画帧。缺失挂点会停止生成。
- `ScarfRig.gd` 是预览和 Mod 共用的披风物理维护源。`KingScarf.gd` 只适配战斗身体节点、当前帧、朝向和皮肤；保留用户验收过的原版配色、头部挂点、重力、加法混合及出招推力。
- `KingPresentation.cs` 挂载每个国王的披风，按战斗实例缓存三选一的风：+1 / -1 / -2。使用 SHA-256 从只读的局种子、幕、地图位置、房间栈深度、遭遇 ID 派生视觉随机值；不调用游戏 RNG 流。房间栈在 `CombatRoom.Enter` 前已压入，载入时按同样顺序恢复。同场节点重建沿用缓存，等价读档键得到同样风向；不同战斗允许随机抽到相同风。
- 连段和死亡状态按国王节点独立保存，随节点退出清理。`NCreature._Ready` 不再让敌人或召唤物重置国王状态。重复初始化不会叠加披风。
- 本阶段接入战斗；商店/营地沿用此前表现。攻击音效和新武器待下一阶段。

## 包与回退

- 候选包：`artifacts/scarf-integration/package/`，部署时只复制 DLL、JSON、PCK 三个同名文件。
- 已验收旧包备份：`artifacts/scarf-integration/approved-baseline/`。
- PCK 以该旧包为底；`build_scarf_pack.py` 验证全部文件 MD5，保留原 5054 项及其完整字节，仅追加本阶段的 6 个资源文件。不使用工程里哈希不同的旧导出 PCK，不覆盖上传工作区。
- 包差异报告和验证日志位于 `artifacts/scarf-integration/`。无需修改游戏存档或皮肤配置。

已部署到 `D:\youxi\steamapps\common\Slay the Spire 2\mods\Hcxmmx_King_Skin`：部署前确认游戏未运行，且旧文件与已保存备份一致；部署后三个文件与候选包 SHA-256 一致。新 DLL 为 `6D8DD0B84F740D1D77A5E1C70AF4FEAD60D507F81F00E40EED8CF67011E67AEB`，新 PCK 为 `D4A237BE51EEB17076D27BC754AE263DE66B36891994267E26E0A051749480DC`。未启动游戏，待用户实机验收；工坊上传目录保持原样。

回退时退出游戏，将备份目录里的 DLL、JSON、PCK 覆盖回 `D:\youxi\steamapps\common\Slay the Spire 2\mods\Hcxmmx_King_Skin`。

## 生成与验证

在工作区根目录运行；安装包始终显式复制，验证构建禁用自动部署。

```powershell
python 05_Preview/prepare_preview.py
dotnet build Hcxmmx_King_Skin.csproj -c Release -p:DeployModOnBuild=false
python 02_Scripts_Tools/build_scarf_pack.py --baseline artifacts/scarf-integration/approved-baseline/Hcxmmx_King_Skin.pck
Copy-Item .godot/mono/temp/bin/Release/Hcxmmx_King_Skin.dll,Hcxmmx_King_Skin.json artifacts/scarf-integration/package/
```

原生 Godot 检查：

```powershell
$godotConsole = 'D:\STS2\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe'
& $godotConsole --headless --path D:/King_Workspace/05_Preview --quit-after 120 -- --validate
& $godotConsole --headless --path D:/King_Workspace/05_Preview --quit-after 600 --script D:/King_Workspace/02_Scripts_Tools/Validation/ValidateScarf.gd -- D:/King_Workspace/artifacts/scarf-integration/package/Hcxmmx_King_Skin.pck
dotnet build 02_Scripts_Tools/Validation/GodotBridge/GodotBridge.csproj
& $godotConsole --headless --path D:/King_Workspace/02_Scripts_Tools/Validation/GodotBridge --quit-after 300 -- D:/King_Workspace/artifacts/scarf-integration/package 'D:/youxi/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64'
```

不能只看 Godot 退出码：需出现对应 `*_PASS`，且无 `SCRIPT ERROR`、`ERROR:` 或断言失败。

已通过：Release 构建零警告/错误；预览物理、回看、刀光同步；候选 PCK 的双皮肤×四种风共 11544 帧挂点/层级检查；真实编译 DLL 到 GDScript 的挂载、攻击推力、换肤、重复挂载保护、角色状态隔离、风向缓存/等价读档/新战斗、节点释放；实际 SkinChanger 1.0.19.0 的 REGENT 提供者识别与选择路由；21 个 Harmony 目标；中英日文本回归。

上述是资源与原生引擎/桥接验证，尚不能代替完整游戏内验收。

## 实机观察

1. 蓝王、白王各打一场：待机、连续攻击、受击、胜利；重点看头后挂点、前后遮挡与身体光晕。
2. 连续进入几场战斗：同一场保持同一风；跨场允许重复，不保证三种风依次出现。
3. 同场存档重进，或切回原皮再重选国王：应无披风叠加、遗留或重新抽风。
4. 有机会时看死亡/转身，以及召唤新怪物后国王连段不被重置。原有攻击速度与伤害等待保持不变。

## 验收后调整：死亡隐藏披风

用户已验收光披风。按后续要求，进入 `letha` 死亡动画立即隐藏披风，恢复其他动作时恢复显示；独立预览同步此显示规则。通过双皮肤/四风的原生验证，额外检查死亡时即时隐藏、后续处理不重新显示、返回待机恢复。更新包与已验收包仅 `KingScarf.gd` 内容不同，已更新本地 mods 的 PCK，DLL 保持原样；调整前 PCK 保存在 `artifacts/scarf-death-adjustment/previous-approved/`。

# 武士刀 / 平底锅攻击音效样例

2026-10-24。按计划先完成两种不同质感的样例，现有其他八组武器仍使用原声。攻击速度、连段池和游戏伤害等待保持原值；样例听感验收后再铺开。

## 素材与定义

`02_Scripts_Tools/prepare_weapon_audio.py` 从原版 `data.cdb/weapon` 的 `sfxRelease`、`sfxHit`、`hitFrame` 提取对应关系，并更新 `Presentation/weapons.json` 的音频字段。原文件扩展名是 WAV，但文件头实际为 OggS；导入只复制字节并改为 `.ogg`，没有重采样或重新压缩。

- 武士刀 B2/B3 使用原表 `release1`，B4 使用 `release3`；三段命中均用 `hit1`，不按文件编号猜测。
- 平底锅 A/B/C/D 分别对应 `release1..4` 与 `hit1..4`。
- 共 7 段、11 份独立音频。源文件哈希记录在 `artifacts/audio-integration/source-audio.json`。
- 每个 cue 的 `volume_db` 初值 -6 dB、`offset_seconds` 初值 0，可在唯一武器定义中调节。再次导入保留已调过的增益与起点。
- 原始命中帧作为后续校准参考保留；实机音效跟随实际动作/扣血，不能拿原游戏帧号延迟塔的伤害。

## 实机播放与替换

`KingWeaponAudio.cs` 缓存 OGG 流，每个角色最多保留 8 个发声节点，音频结束释放，角色销毁时随实例清理。使用游戏的 `SFX` 总线，因此总音量、音效音量和后台静音沿用现有游戏控制；不调用游戏随机音高 RNG。

`Patch_WeaponAudio.cs` 只改三个异步状态机中的具体调用点，没有全局静音：

1. `CreatureCmd.TriggerAnim` 的 Attack 分支原版挥击声：检查真实 REGENT/KingMecha 节点和下一段音频是否可用，再决定替换；下一段由预取与实际出招共用，不多推进连段。
2. `AttackCommand.Execute`：通过包装原动画和伤害调用，将武器和实际攻击命令绑定。只抑制该命令的通用斩击/钝击/重击声；特定卡牌魔法声、夜歌/君王之剑、其他角色/武器保留原有路径。原调用的参数、目标、等待及结果透传。
3. 主 `CreatureCmd.Damage` 的扣血调用：执行原 `LoseHpInternal` 后，按该次真实伤害调用的目标集合身份，在首次正数扣血时播放命中声。避免等待伤害方法末尾固定 0.1—0.2 秒以及死亡/能力触发流程后才响。群攻同一组只响一次，多段每组分别响；全格挡、零伤害、无目标不加撞击声。嵌套的状态/反伤伤害不会借用外层武器声音。

攻击关联使用逐命令弱表和有界异步作用域，不读取全局“最后一次武器”。资源缺失/解码失败时保留原声。当前安装版本的调用点数量已由实际 Harmony 补丁生成验证；后续游戏版本若改变这些调用点，检查会明确报告目标变化。

## 预览与验收

`05_Preview/start-preview.ps1 -Prepare` 更新预览。选择武士刀或平底锅，打开“随动作播放音效”；也可单独试听挥击、命中。“普通命中 / 全格挡 / 落空”供声音对照，暂停和逐帧回看不重复触发声音。预览命中参考时刻为普通储君 0.15 秒，实机使用实际扣血时刻。

实机重点听：

1. 武士刀前三段的区别，平底锅四段的挥击/撞击配对，声音与现有出牌速度是否自然、音量是否合适。
2. 单击、多段、群攻，以及连续快速出牌，确认无原版通用武器声叠播、错武器或偏晚的撞击。
3. 全格挡保留格挡反馈，非攻击伤害和夜歌保留独立声音；其他八组武器仍有原声。
4. 战斗结束、退出/换皮后无声音节点残留；顺便验收上轮死亡动画隐藏披风。

## 包、检查与回退

候选包：`artifacts/audio-integration/package/`。更新前的本地包备份：`artifacts/audio-integration/previous-installed/`，包含上轮死亡隐藏披风调整。退回时退出游戏，将备份中 DLL、JSON、PCK 三个文件覆盖回 mods 的国王目录。工坊上传工作区不改动，发布版本号待统一验收。

样例已部署到 `D:\youxi\steamapps\common\Slay the Spire 2\mods\Hcxmmx_King_Skin`。部署前确认游戏未运行且旧文件与备份一致，部署后三个文件均与候选包 SHA-256 一致，详见 `artifacts/audio-integration/deployment.json`；未启动完整游戏，听感待用户验收。

PCK 仍以最初已验收的 5054 个资源为基线，保留全部原字节，追加 17 个表现资源（含 11 份音频），并保留死亡隐藏披风。

生成：

```powershell
python 02_Scripts_Tools/prepare_weapon_audio.py
python 05_Preview/prepare_preview.py
dotnet build Hcxmmx_King_Skin.csproj -c Release -p:DeployModOnBuild=false
python 02_Scripts_Tools/build_scarf_pack.py --baseline artifacts/scarf-integration/approved-baseline/Hcxmmx_King_Skin.pck --output artifacts/audio-integration/package/Hcxmmx_King_Skin.pck
Copy-Item .godot/mono/temp/bin/Release/Hcxmmx_King_Skin.dll,Hcxmmx_King_Skin.json artifacts/audio-integration/package/
```

已通过：Release 构建零警告/错误；实际 .NET 9.0.7 + Godot Mono 中三个音效补丁的生成与移除、7 段音频原生解码、即时扣血且原扣血结果不变、群攻/多段/全格挡/无伤害/嵌套伤害隔离、按命令抑制通用音效、其他武器回退、SFX 总线、8 发声节点上限及释放；预览播放/暂停/回看与已结束音频清理；披风 11544 帧回归；24 个 Harmony 目标、中英日和 SkinChanger 1.0.19.0 提供者路由回归。日志位于 `artifacts/audio-integration/`。

独立测试窗口默认可能选用系统 .NET 10，该版本与游戏所带 Harmony 不兼容。为实际补丁验证，在 `artifacts/audio-integration/dotnet9/` 放置了微软官方 .NET 9.0.7 便携运行时，并核对官方 SHA-512；未安装到系统或修改游戏。运行原生桥接验证前，仅对该终端设置 `DOTNET_ROOT` 和 `DOTNET_ROOT_X64` 为这个目录。完整游戏内听感仍待用户验收。

## 2026-10-25：预览循环与节奏讨论

用户反馈连段和循环停住，已定位并修复预览时间轴范围收缩触发暂停的问题，原生按钮完整多轮循环验证通过。此前音效检查验证归属/触发和音频解码，不代表各动作的可见命中帧已经对齐塔的伤害时点。当前预览固定 0.15 秒的撞击参考无法反映逐段差异；节奏方案待讨论后，再统一校准动作、刀光与声音。游戏已安装的音效样例及动画速度在此轮保持原样。

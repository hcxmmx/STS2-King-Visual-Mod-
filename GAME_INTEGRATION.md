# 双节奏与 13 套武器游戏接入

v1.1.0 已恢复正常随机池，加入迅捷弓后合计 13 套、44 段。弓箭目标关联、发行环境纹理解码与全弓测试已通过用户验收，相关接入见 `RANGED_PLAN.md`。以下保留最初 12 套武器的接入记录。

2026-10-25。已将验收过的预览方案接入正式 C#／Godot 运行时，并部署到本地游戏 mods；安装后三件套 SHA-256 与测试包完全一致。

音量试听调整：按用户要求将全部武器音效由 -6 dB 恢复到 0 dB，包括 43 段挥击、43 段击中和 20 个蓄力音效定义。预览与生成器默认值同步更新，原始音频字节、音高、动作及声音触发时序未改。该次更新与之前安装包的 PCK 差异仅为四份 JSON 的音量参数，67 个音频文件完全不变；原生加载检查通过。旧版本备份在 `artifacts/audio-volume/previous-installed/`，本次安装报告在 `artifacts/audio-volume/deployment.json`。

## 游戏行为

- 12 套武器、43 段进入实际随机池：原有十套，加巨型剪刀两段、双截锅十段。各武器权重均为 1，完成一套连段后重新抽取；纯视觉随机不消耗战斗 RNG。
- 默认加速节奏；选中储君后，右上角齿轮展开设置面板，可切换蓝王／白王与加速／原作节奏，点击后自动保存。简中、英语、日语均已补齐文字。头像上的两个旧按钮已迁入面板，切换反馈使用面板选中状态。
- 设置沿用 `user://king_mod_skin.cfg`，新字段为 `[Presentation] OriginalTiming`，皮肤设置一并保留。
- 伤害按游戏原有时点结算，角色动作使用独立表现时钟。当前挥击受保护，最多保留一组后继，极端输入合并表现。
- 击中声按本次真实伤害结果触发；挥击声和蓄力声按当前可见动作的时序播放。每条攻击命令保存自己的武器段，不借用全局最后一次武器。
- 剪刀继续使用已验收的身体／刀光共用进度；新武器的原始帧、头部挂点和披风前后层随正式 PCK 安装。
- 受击、施法、死亡、胜利、切换节奏、皮肤隐藏时清理相关队列和声音，结束动作不会被迟到攻击覆盖。死亡继续隐藏披风。

弓箭、盾牌外观精修及全挡／穿盾动作分流仍是后续阶段，本轮沿用现有处理。

## 构建与安装位置

```powershell
python 02_Scripts_Tools/prepare_game_presentation.py
dotnet build Hcxmmx_King_Skin.csproj -c Release -p:DeployModOnBuild=false --no-restore
python 02_Scripts_Tools/build_scarf_pack.py --baseline artifacts/scarf-integration/approved-baseline/Hcxmmx_King_Skin.pck --output artifacts/game-integration/package/Hcxmmx_King_Skin.pck
```

将构建 DLL 与根目录 JSON 一并放入上述 package。生成器将新武器加入共享定义，导入 67 个原版音频文件、370 张新增帧图，复制与预览逐字节相同的节奏脚本，重写运行时资源路径并补齐披风数据。PCK 打包保留原包全部 5054 项负载不变，新增 450 项资源，无需重导整个 Godot 工程。

安装目标：`D:\youxi\steamapps\common\Slay the Spire 2\mods\Hcxmmx_King_Skin`。

旧版本三件套备份：`artifacts/game-integration/previous-installed/`。新包：`artifacts/game-integration/package/`。安装哈希报告：`artifacts/game-integration/deployment.json`。上传工作区和工坊未更新。

## 验证

- 生产构建零警告零错误。
- 实际 PCK 在 Godot Mono/.NET 9.0.7 中加载；12 套双模式各 50 连击、全部 43 段完整播放、C# 合并与音频选段、隐藏／受击／死亡／胜利中断、全部帧披风挂点、剪刀源帧对应均通过。
- 实际原生模式按钮、重复注入保护、设置保存恢复，以及切换节奏不改皮肤选择均通过。
- 实际异步音效补丁、AOE、多段、全格挡、嵌套伤害隔离、音量总线、声音数量上限及节点清理检查通过。
- SkinChanger 1.0.19.0 扫描、切回原皮再选择、24 个 Harmony 目标及三语 9 项界面文字／30 条台词检查通过。
- 预览现有 12 套回归与剪刀全段 60/120-Hz 检查通过；212 项音频来源字节核对通过。

这些是正式资源与运行时桥接验证，尚需用户在实际对局中确认最终观感。重启游戏后，可先看选人界面的齿轮设置面板，再检查普通攻击、星尘连续攻击和死亡／胜利收尾。

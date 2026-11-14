# 国王与夜歌更新整理

整理日期为 2026-11-14。用户已验收本轮游戏表现及新设置面板，可以进入发布准备。

本轮版本为 `1.1.0`。用户已验收全弓测试，授权恢复随机池并依次更新创意工坊、GitHub 源码及 Release。公开更新说明见 `CHANGELOG.md`。

## 中文更新说明草稿

这次把之前陆续做的外观、动作和声音调整一起整理更新，也适配了当前的 beta 0.113。

### 新增与调整

- **光效披风**。为蓝王和白王加入随动作摆动的光带，配色跟随皮肤。每次进入战斗会抽取一种风向表现，死亡动作隐藏披风。
- **两种演出节奏**。可以选择「加速」或「原作」。加速模式缩短前摇与收招，原作模式保留原作动作节奏。快速连击时优先完成当前挥击，并合并后续演出，改善频繁重置动作的问题。伤害与出牌继续沿用游戏结算。
- **新武器**。加入巨型剪刀、双截锅和迅捷弓，当前随机武器池共有 13 套、44 段攻击，并补齐已有武器的动作、刀光和声音适配。
- **原版声音**。攻击挥击声、击中声、盾挡声和角色受伤声采用《死亡细胞》原版资源。挥击声跟随动作，击中声跟随实际伤害，重新调整了音量。
- **盾牌与受击**。完全挡住攻击时使用盾挡，掉血时使用受伤动作。还原突击盾、寒冰盾、雷盾的举盾、收盾、粒子与格挡音效。
- **敌人受击反馈**。加入原版血液喷溅与受击闪色，调整方向和大小，碎块效果在致死命中时出现。群攻会在各个实际受击目标上显示效果。
- **设置面板**。选中储君后，点击右上角齿轮即可调整蓝王／白王及演出节奏，选择自动保存。旧版设置会继续沿用。
- **本地化**。补充英语、日语，覆盖角色介绍、设置和夜歌台词。

### 修复

- 修复游戏更新后胜利动作失效的问题。
- 适配 beta 0.113 的选人、商店退出及攻击伤害相关接口。
- 修复与未锈铠同装时可能导致 DLL 初始化失败的补丁冲突，并处理苏芙比相关音效补丁的加载顺序兼容。
- 校正吸血鬼杀手的特效起播、巨镰后两段刀光配对，以及剪刀加速模式下刀光与动作的进度对应。
- 完善受伤、死亡、胜利及切换状态时的演出与声音清理。

## English changelog draft

This update brings together the recent visual, animation, and audio improvements, with support for beta 0.113.

### Additions and improvements

- Added an animated light-trail cape for both King outfits, with outfit-specific colors and three wind presets selected per battle. The cape is hidden during the death animation.
- Added Fast and Original animation pacing. Fast mode shortens wind-up and recovery, while Original retains the original animation pacing. Rapid attacks finish the current swing and merge follow-up animations to reduce repeated restarts. Damage and card resolution follow the game's timing.
- Added Giant Scissors, Panchaku, and the Quick Bow. The random weapon pool now includes 13 weapon sets and 44 attack stages, with animation, trail, and audio adjustments across the existing weapons.
- Added original Dead Cells swing, impact, shield-block, and player-hurt sounds. Swing sounds follow animations, impact sounds follow actual damage, and audio levels have been adjusted.
- Added shield animations for fully blocked attacks and hurt animations for HP loss. Updated the Assault, Ice, and Thunder Shields with original raise/lower frames, particles, and block sounds.
- Added enemy blood splashes and damage flashes, with adjusted direction and scale. Fragment particles appear on lethal hits, and area attacks display feedback on each damaged target.
- Added a gear-button settings panel at the upper right of the character selection screen when the Regent is selected. Outfit and animation pace are saved automatically, with existing preferences retained.
- Added English and Japanese localization for character text, settings, and Serenade dialogue.

### Fixes

- Fixed the victory animation after a game update.
- Updated character selection, merchant-exit, and damage hooks for beta 0.113.
- Fixed a patch conflict that could cause DLL initialization to fail when installed with AKnightSkin, and addressed audio-patch load-order compatibility with SothebySkin.
- Corrected Vampire Killer effect timing, the final two Scythe Claws effect mappings, and Giant Scissors animation/trail alignment in Fast mode.
- Improved animation and audio cleanup during hurt, death, victory, and state changes.

## 工坊主页需要同步的内容

保留现有标题、夜歌介绍及致谢内容。主页中关于换肤的旧说明需要同步替换，并补入本轮新增功能。

### 中文操作说明

选中储君后，点击选人界面右上角的齿轮，可切换蓝王／白王皮肤及加速／原作演出节奏，设置自动保存。

### English controls

Select the Regent and click the gear icon at the upper right of the character selection screen to choose your outfit and animation pace. Preferences are saved automatically.

主页功能列表可补入光效披风、双节奏、13 套武器与原版音效、盾挡／受伤分流、敌人受击效果，以及中英日文本支持。兼容说明写明本轮验证目标为 beta 0.113。

## 发布范围与待办

正式构建已恢复 13 套武器随机池，1000 次抽取覆盖全部武器。箭矢加载已通过游戏发行运行环境检查。工坊已上传，Steam 公开元数据确认说明已更新。GitHub 源码与 Release 按用户授权继续同步。

- 游戏采用开启血液的敌人受击表现。血液开关位于独立预览中，游戏设置面板提供皮肤和演出节奏两项。
- 本轮包含此前已完成的胜利修复、本地化和披风，及这次最终验收的迅捷弓、受击效果与设置面板。
- GitHub Release 包使用正式随机池构建，并提供校验文件。

## 已验收包与检查记录

正式包目录为 `artifacts/release-1.1.0/ranged-game-integration/selection-settings/enemy-impact/shield-integration/package/`。

| 文件 | SHA-256 |
| --- | --- |
| Hcxmmx_King_Skin.dll | 5723650AB30F7D0F80145DE0762CAB53E6687BA37AE8540A9C28597DABD253BA |
| Hcxmmx_King_Skin.pck | 511352FACC622E51C7AB48E1DD75AC5C68A470821383DC7A1FB6DC2A3365862B |
| Hcxmmx_King_Skin.json | 13D329CC62AE7C098EEE3D9B3BF3BE94C24158E7BDD19EBB3557AE7433117FCE |

上述 JSON 对应 `1.1.0` 清单。

本轮已通过构建、补丁安装与 JIT、13 套武器双节奏压力检查、音效和盾牌分流、敌人受击结果路由、设置保存恢复及界面生命周期检查。国王、未锈铠、苏芙比的四种组合加载顺序通过。此前游戏表现与设置面板已获用户验收。迅捷弓此次补齐游戏接入，原生检查通过，实际对局效果已获用户验收。

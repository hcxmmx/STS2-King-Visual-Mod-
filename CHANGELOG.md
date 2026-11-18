# v1.1.2

- 修复正式版 v0.111.1 上模组初始化失败，恢复盾挡和攻击反馈，并适配正式版选人接口。
- 修复 beta v0.113.1 中打击可能卡在界面上、敌人受击特效未播放的问题。

## English

- Fixed mod initialization on stable v0.111.1, restoring shield and attack effects and adapting to its character selection API.
- Fixed stalled Strike plays and missing enemy hit effects on beta v0.113.1.

# v1.1.1

- 修复与其他皮肤模组同装时，部分攻击牌播放音效导致出牌流程卡住的问题。此次日志中的触发牌为 LUNAR_BLAST。
- 保持 v1.1.0 的 13 套武器随机池及全部外观功能。

## English

- Fixed an audio-patch argument mismatch that could stall attack cards when installed alongside other skin mods. The reported failure occurred with Lunar Blast.
- Includes the 13-weapon random pool and visual features from v1.1.0.

# v1.1.0

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
- 修复箭矢图片加载在游戏发行环境中被跳过，造成空纹理错误的问题。

## English

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
- Fixed arrow image loading in release builds, which could produce an empty texture.

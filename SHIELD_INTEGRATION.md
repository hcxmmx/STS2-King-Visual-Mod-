# 盾牌表现与格挡分流

2026-11-14。以 beta v0.113.0 为目标，完成本地接入和部署，实际对局验收交用户进行。

## 触发规则

- 来自其他单位的攻击，实际结果为完全格挡、格挡量大于零且 HP 损失为零时，播放盾挡。
- 攻击穿盾或直接造成 HP 损失时，游戏原生 Hit 信号播放 `stun`，移除旧随机受击池中的盾挡动作。
- 零伤害、自伤、无攻击者、非攻击伤害、不可格挡伤害及 SkipHurtAnim 路径跳过盾挡。
- 在伤害结算协程读取完整 DamageResult 的全挡分支观察结果，返回原布尔值。游戏伤害计算继续使用原参数、目标记录和返回 Task。
- 已播放国王盾挡的结果用弱引用表关联，替换该结果稍后的原生 `block_hit` 声音。其他结果继续进入游戏原声路径。

## 盾牌与资源依据

使用原有三面盾，突击盾、寒冰盾、雷盾。一次盾挡随机选择，动作尚在进行时的连续格挡沿用该盾型并刷新反馈。使用独立视觉随机源。

- 每面盾从死亡细胞原图集导入一帧举盾和九帧收盾，合计 30 帧。原项目的身体素材与原图集一致，因此拆开原来的重复帧拼接，分别控制停留和收盾。
- 披风挂点及前后遮挡取自 `king_tracks.json` 与 `backFrames`。
- 读取原 `Fx.shieldParryStart` 与 `Fx.frontShieldHit` 的粒子实现，使用 `fxCommon` 图集的 `fxLineWhite`、`fxParryLine` 原贴图，移植 11 条内收线与两组各 16 条命中线的弧度、摩擦、速度和寿命参数。
- 原程序以实体坐标生成粒子。本工程的完整画布以脚底为原点，采用原始 Spine2 高度换算特效中心，避免光弧落在脚下。粒子采用 Godot 加法混合与原作 30-Hz 摩擦的解析位移。
- 声音使用原 `weapon_shield_block`、`weapon_iceshield_hit` OGG 字节，播放增益为 0 dB。

针对尖塔离散受击做有限时长适配。原作模式举盾停留 0.2 秒，再以 30 FPS 播放九帧收盾。加速模式停留 0.12 秒，收盾为 1.4 倍速。新攻击、受伤、施法、死亡、胜利与皮肤隐藏均清理盾挡表现。两种节奏的时长与粒子呈现仍需用户观察确认。

## 预览与生成

双击根目录 `启动战斗预览.cmd`，新增「全格挡」一行，可分别播放三面盾。上方「受击」可观察穿盾后的动作。节奏、皮肤、速度、暂停和循环控件可继续使用。

`prepare_shield_presentation.py` 复用 Atlas 提取器，生成原始身体帧、声音、粒子贴图、挂点和盾牌定义。`prepare_battle_preview.py` 与 `prepare_game_presentation.py` 共用该数据；预览的 `ShieldPresentation.gd` 由生产脚本复制。

生产包由已验收的原始 PCK 基线追加呈现资源。新增资源与游戏适配后的 DLL 一起部署。

## 基础检查与安装

- 生产构建零警告、零错误。
- 原生 Godot/.NET 9.0.7 中全部 23 个补丁类安装成功，覆盖 21 个原方法，270 个方法 JIT 通过。
- 新增针对性检查覆盖完全格挡／穿盾／零伤害／自伤／状态伤害分类，三盾双节奏的停留与收盾、原版 OGG 解码、攻击／受伤／隐藏打断。
- 原有攻击音效基础检查通过。原生渲染捕获三面盾并核对光弧挂点，截图位于 `artifacts/shield-integration/shield-0.png` 至 `shield-2.png`。

测试包为 `artifacts/shield-integration/package/`，部署前备份为 `artifacts/shield-integration/previous-installed/`。已安装到 `D:/youxi/steamapps/common/Slay the Spire 2/mods/Hcxmmx_King_Skin`，文件哈希与包一致。

- DLL SHA-256 为 `218D319B82B597D5848F7F457015CCADE3C23A41EFEDB8E013C17BDFBFC6469B`。
- PCK SHA-256 为 `08BB1088409783F2D05E9DDB1ABEF2637B99D92690FC05696CE6EEA6DF9B23EA`。
- 安装报告为 `artifacts/shield-integration/deployment.json`。
- 基础检查日志为 `artifacts/shield-integration/patch-check.log`。

实测优先覆盖全挡、部分格挡掉血、连续多段全挡、最后一击破盾掉血、死亡与结束后清理，再观察三面盾的形态、光弧和音量。

# 敌人受击特效资源与接入评估

2026-11-14。依据本地死亡细胞原始资源、HashLink 字节码和尖塔 beta v0.113.0 程序集完成清点。切击、钝击及箭矢命中首版已实现，等待预览与实机验收。

## 原版受击调用复查与双分支预览

用户反馈首版火花过小、与原作印象不符后，继续从原版字节码追到实际怪物承伤入口。确认链路为 `AttackUtils.hit → AttackTargetImpl.applyHit → Mob.applyAttackResult → Entity.applyAttackResult → Mob.onDamage`。其中 Entity 通过虚方法进入 Mob.onDamage，Mob.onDamage 首先调用 Entity.onDamage。

### 普通物理命中

- `Entity.pickColorBlink` 对成功的 Physical 伤害调用红色 `0xff0000` 闪色，强度参数 0.9、时长 0.15 秒。
- `Mob.onDamage` 对非 Critical 的 Physical 伤害调用 `Fx.bloodHit`。
- `Fx.bloodHit` 在 `fx_bloodA/bloodA` 与 `fxBloodB/bloodB` 间随机选择，使用普通 Alpha 混合，缩放 0.75～1.0，朝向按 AttackData 处理。实际资源分别为 10、19 帧，完整画布 99×99。HParticle 按原始 30 Hz 时间基准推进动画。
- 同一函数生成 15 个 fxGib 粒子，包含两类出射速度、0.25／0.32 的逐 tick 重力、0.96～0.99 的摩擦及 0.4～2.0 秒寿命。原代码还包含地图碰撞和血迹沉积。
- Mob.onDamage 还进入 Splatter.singleLight／singleHeavy。原作按四舍五入后的 rawFinalDmg 是否达到 30 选择轻重场景喷溅，disableSplatter 会影响这条分支。
- 怪物可以启用自身的受击叠加动画和 hitDistort，是否播放取决于其配置及资源。

### 关闭血液

`Fx.bloodHit` 检查 options.noBlood，开启无血选项时跳过血液动画与飞散粒子。Physical 的本体闪色改用 `0xf78505` 橙黄色，保留 0.9 强度与 0.15 秒时长。

场景 Splatter 构造函数另有 `_noblood` 贴图替换检查，涉及 gameElements 图集及其运行时分组。本地 fxCommon 中能找到黄绿色血迹候选，但其与该分支的图集对应仍需核对。双分支预览目前展示已确认的血液开关与本体闪色，场景残留喷溅属于后续恢复内容。

### 暴击和箭矢

`HitResult` 原版枚举明确区分 Normal、Partial、Critical、Miss、Block。Critical 由 Entity.onDamage 调用 Fx.critical，包含更大的双侧血液动画、15 条长线和其他粒子，并受血液开关影响。尖塔暴击触发条件仍需在接入时单独确定。

已定位的 `Fx.bulletHit` 调用之一来自 `Bullet.blockOnCollision`，包含动态光、圆盘、圆环和粒子。箭矢对怪物造成 Physical 伤害时仍会进入上述怪物承伤链。把 bulletHit 一概作为箭矢掉血的反馈需要进一步核对具体碰撞分支。

`Fx.hitLines` 当前定位到的调用包括盾牌反馈、盾反和若干特殊武器／技能。第一轮将其抽象为所有近战的主要火花，并按 slash／blunt 分类，偏离了已确认的普通物理受击主体。

### 预览入口与取舍

运行根目录 `启动战斗预览.cmd`，在新增「敌人受击」下拉框切换「开启血液」和「关闭血液」，点击「单独观察命中」，也可直接播放武器连段及迅捷弓。慢速、暂停和逐帧按钮适用于这两种效果。

本轮恢复普通物理命中的 29 张原始动画帧、两张 fxGib 贴图、15 个粒子的基础运动，以及两种闪色。用户验收后进一步指定喷溅翻转至目标身后、尺寸放大两倍，并把碎块限制到致死命中。当前特效比例为每原始像素 8 预览像素，普通命中保留动画和闪色，致死命中才生成 15 个 fxGib。原版普通 Fx.bloodHit 也会生成这组碎粒，致死限定是本项目按用户偏好作出的适配。

新增「致死命中」按钮将靶标 A 的生命设为 6，再以 6 点伤害将其归零，闪色结束后隐藏木桩本体。连续攻击与箭矢同样检查实际生命值从正数变为零，已归零目标的重复命中不触发碎块。后续实战接入应结合 DamageResult 的实际承伤目标与致死结果关联，避免借用其他目标的死亡事件。

木桩本体闪色使用颜色插值模拟，地形碰撞使用预览平地近似，粒子末尾 0.2 秒淡出是展示适配。地图血迹、暴击与敌人材质分支仍需继续恢复。

`OriginalEnemyHit.gd` 与 OriginalHitAssets 现已接入游戏。生成器将预览脚本逐字节复制到 Presentation，运行时与预览共用普通命中、放大比例、翻转及致死碎块逻辑。游戏默认采用开启血液的表现。

### 实战接入

KingEnemyImpact 通过已结算 DamageResult 的 Receiver 取得真实承伤节点，在原先命中观察点读取其 VfxSpawnPosition 与 Hitbox 底边。源像素比例按国王的实际全局变换换算至战斗特效容器，脚本再应用已验收的两倍比例。致死碎块要求 WasTargetKilled、Receiver 当前死亡及实际 HP 损失同时满足。普通命中、全格挡和零伤害分别走对应分支，同一结果去重，群攻逐目标显示。

敌人本体闪色采用 Body 节点 Modulate 近似，原有材质与透明度保留。0.15 秒后恢复基色，连续命中刷新时间，节点释放与房间退出会清理。若其他演出期间改写颜色，该颜色继续由后续演出控制。与预览木桩的 RGB 插值相比，实机乘色的观感需要进游戏验收。

已完成 beta 0.113 的补丁安装／JIT、编译 DLL 到真实 Godot 的结果路由检查、43 段资源加载、变换后的两倍比例、普通／致死／存活目标／格挡／零伤害分流、逐结果去重、染色恢复与外部颜色接管、致死后节点释放及房间清理检查。国王／未锈铠／苏芙比四种加载顺序继续通过。实机验收重点为不同敌人的挂点、大小、染色、群攻及致死碎块。

原始调用记录保存在 `artifacts/enemy-impact-research/actual-hit-chain/`，包含普通／暴击／血液开关的已命名字节码、枚举映射、帧抽样和预览截图。研究脚本只读取原游戏二进制并生成调查产物。

## 上一轮火花接入记录

运行根目录 `启动战斗预览.cmd`。武士刀、剪刀等显示切击线；平底锅、双截锅、权杖和墓碑显示放射状钝击火花。迅捷弓按钮显示较小的箭矢命中火花，新增「敌方全格挡反馈」按钮显示蓝白色护甲火花。原作与加速两种节奏都按模拟伤害时点触发，支持暂停和慢速观察。

实战接入当前 12 套近战武器、43 段攻击。迅捷弓仍是独立预览项目，其敌人侧样式随预览一起实现。现有武器击中音频继续使用已接入的原版文件。

本轮使用 fxCommon 中原始 fxLineWhite、fxLineWhiteSharp、fxPixelWhite、fxGlowWhite 四张贴图及解析式粒子衰减。切击、钝击、箭矢、全格挡的最长持续时间分别为 0.28、0.32、0.22、0.18 秒。数量、速度、颜色、尺度和这组时长是本项目的首轮适配值。原版无血命中序列、血液、暴击和材质分支留待进一步核对其原始混合及调用。

实际伤害结果按 Receiver 定位，每个结果独立去重。群攻逐敌人显示，声音仍按现有伤害组规则触发。效果位置在命中时保存，挂在战斗特效容器，致死后可播放余帧，退出房间随容器清理。全房间最多保留 32 组短时效果。

通用 slash、blunt、heavyBlunt 卡牌效果，以及成功生成自定义反馈后的 NHitSparkVfx，通过现有国王攻击作用域替换。群攻的侧面通用 VFX 也检查自定义资源可用后再替换。专属卡牌表现继续沿用游戏调用。盾牌分流仍由同一个已结算结果观察点驱动。

已完成编译、原生 Godot 中的 DLL/PCK 加载、43 段及两种节奏的预览、50 连击、20 连射、数量上限和清理检查，并通过国王／未锈铠／苏芙比四种顺序的完整 PatchAll 检查。实际敌人的挂点、尺度、伤害转移和致死表现还需本轮进游戏验收。

## 已有命中声音

当前游戏中 12 套武器、43 段攻击均已配置死亡细胞原版命中声音，合计 16 个不同音频文件。它们在本次攻击实际造成 HP 损失时触发，群攻按伤害组播放一次，多段分别播放。挥击声与角色受伤声另有各自的触发位置。

| 当前武器 | 已接入的击中音频 |
| --- | --- |
| 武士刀 | weapon_katana_hit1 |
| 国王权杖 | weapon_kingscepter_hit |
| 吸血鬼杀手 | purpleDLC_vampireKiller_hit |
| 重剑、巨镰、完美战戟 | hit_broadsword |
| 平底锅 | weapon_pan_hit1～4 |
| 刺剑、战戟 | hit_blade |
| 墓碑 | weapon_tombstone_hit、weapon_tombstone_hit3 |
| 巨型剪刀 | weapon_scissors_hit2～3 |
| 双截锅 | weapon_nunchukpan_hit1～3、weapon_pan_hit3 |

迅捷弓预览使用 arrow_hit。预览中的「只听击中声」可试听上述命中反馈。

额外资源包含 hit_flesh、hit_hammer、hit_blood、hit_crit、hit_fire、hit_poison、magic_arrow_hit 等。需按原调用来源选用。hero、env、weapon 目录中的同名文件存在字节差异，新增导入应保留来源标识，防止仅按文件名保存时发生覆盖。

尖塔目前还会调用怪物自身 HurtSfx 和 TakeDamageSfx。后续混音应明确武器撞击、材质反馈与怪物叫声各自的播放位置，群攻也需维持现有声音数量约定。

## 视觉资源清单

原始图集位于 `D:/Dead Cells/ModTools/Unpacked_Assets/atlas`。已解析 fxCommon、fxWeapon、gameElements 三个完整 BATL 图集，记录图页、尺寸、透明留白与分组。下表帧数表示资源条目数量，实际时长需结合原代码中的播放、缩放和粒子寿命。

| 类别 | 原始资源或函数 | 已确认内容 | 接入工作量 |
| --- | --- | --- | --- |
| 切击线、冲击火花 | Fx.hitLines、Fx.longHitLine；fxLineWhite、fxLineWhiteSharp、fxPixelWhite、fxDotWhite | 原贴图与代码均有，包含方向、数量、颜色、摩擦和寿命控制 | 较低，可复用盾牌粒子移植方式 |
| 无血命中闪光 | fxCommon 的 fxHit_noblood/Fxhit_noblood、Fxhit_nobloodB/Fxhit_nobloodb | 两组分别 17、12 帧，完整画布 120×120 | 中等，需核对原始混合、染色与时序 |
| 另一组无血闪光 | fxWeapon 的 Fxhit_noblood/Fxhit_noblood、Fxhit_noblood2/Fxhit_nobloodb | 分别 13、12 帧，完整画布 120×120 | 中等，需保留图集来源并选择具体版本 |
| 冲击光圈 | fxCommon 的 fx_impact/Impact；Fx.impact | 10 帧、55×55，代码控制颜色和表现参数 | 较低至中等，适合钝击候选 |
| 血液动画与粒子 | fx_bloodA/bloodA、fxBloodB/bloodB、bloodDirectional、bloodDots；Fx.bloodHit/Fx.bloodHitFree | 两组动画分别 10、19 帧，99×99；方向性血液和小滴贴图齐全 | 中等偏高，涉及目标材质、血色和残留表现 |
| 箭矢命中 | Fx.bulletHit、相关白线／光点粒子；arrow_hit、magic_arrow_hit | 原代码与声音齐全，可配合箭矢朝向生成 | 中等，与远程真实目标接入共用数据 |
| 平底锅相关候选 | fxAtkPanA～D/fxPanHitA～D；fxHitPanA～D/fxPanAtkA～D | 前者每组 4 帧，后者每组 6 帧，画布 300×300 | 中等，需要继续确认攻击者刀光与目标命中的调用归属 |
| 暴击反馈 | Fx.critical、hit_crit | 原函数包含闪光、线条及血液相关处理，声音也有 | 额外触发设计，当前 DamageResult 没有统一暴击字段 |

原图抽样见 `artifacts/enemy-impact-research/impact-resource-sheet.png`。抽样按透明边界裁切以便看清，移植时使用原始完整画布与原始偏移。抽样展示原图颜色，最终效果还需应用原代码对应的混合与颜色处理。

## 接入现状与技术依赖

### 可复用的命中归属

`KingWeaponAudio` 已保存当前实际攻击的武器段、角色与伤害调用作用域。其 ApplyHit 包装了实际 LoseHpInternal 返回值，可以得到真正承伤的 Creature 与 HP 损失。伤害转移后应采用 Receiver，OriginalTarget 表示原先计划攻击的目标。

群攻视觉需要逐目标生成。现有 group.Played 专门限制群攻声音次数，新增视觉通知应放在该声音去重判断之外，并区分多段及伤害转移。两种节奏均在真实伤害时点触发敌人反馈。

### 挂点和尺度

当前 beta 提供 NCreature.VfxSpawnPosition，其位置由怪物 Bounds 中心建立；Creature.GetVfxContainer 返回战斗特效容器。可使用怪物中心与 Bounds 计算像素效果的尺度及方向。

本轮直接反编译当前程序集，确认 VfxCmd.PlayOnCreatureCenter 的第三个可选参数为 forceIfDead，可供致死一击显示反馈。死亡、节点移除及房间退出需要统一清理；先读取命中位置可防止目标释放后无法定位。

### 原生特效与同装

尖塔每个实际伤害结果还会生成 NHitSparkVfx，并可能播放 DamageTargetData 自带的卡牌 HitVfx。新增表现需要限定国王来源，处理对应的通用火花和通用命中特效，保留其他角色及专属卡牌的表现。

实施优先扩展现有命中观察与呈现代码，复用刚完成兼容修复的攻击链。完成后继续检查国王、未锈铠、苏芙比的组合安装与加载顺序。

## 建议实施顺序

1. 先做切击、钝击、箭矢命中三类预览，原始贴图与程序化粒子一起使用。已有武器击中声作为首轮声音基线。
2. 按武器段绑定效果风格，校准尺寸、方向、颜色和持续时间。优先对照武士刀／剪刀、平底锅／权杖、迅捷弓。
3. 接实际敌人，覆盖单体、多段、群攻、全格挡、穿盾、伤害转移、致死一击与战斗结束。全格挡进入独立的护甲／火花反馈，掉血进入命中反馈。
4. 再按效果需求加入血液、材质音或暴击样式。血色与材质映射、暴击的触发条件需要在该阶段明确。

基础效果具备现成资源和可复用调用链，整体可行性高。主要适配成本在目标挂点与尺度、多目标去重、原生效果的限定替换，以及密集连击中的视觉数量控制。

## 调查产物

- `artifacts/enemy-impact-research/atlas-inventory.json`，三图集的完整分组、图页与尺寸。
- `artifacts/enemy-impact-research/summary.json`，抽样视觉资源和当前 12 套命中音频映射。
- `artifacts/enemy-impact-research/original/`，死亡细胞命中相关函数的定向字节码反汇编。
- `artifacts/enemy-impact-research/VfxCmd.cs` 与 `DamageResult.cs`，尖塔当前 beta 的相关接口。

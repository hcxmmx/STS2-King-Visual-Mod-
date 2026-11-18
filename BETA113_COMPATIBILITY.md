# beta 0.113 兼容调查

## 2026-11-18 beta v0.113.1 出牌中断

正式版兼容改动曾共用全挡分支缓存的伤害结果。beta 的原生卡牌特效在该分支之前执行，导致打击调用命中特效时传入空结果，出牌协程抛出异常。beta 恢复从游戏结算闭包读取结果，正式版保留其独立路径。空结果直接回退到原生特效。

用用户当前 v0.113.1 程序集完成补丁安装、攻击音效、敌人受击、盾挡、迅捷弓和设置桥接检查。已替换本地订阅 DLL，记录见 `artifacts/beta-1131-card-stall/deployment.json`，待重进对局复验。

## 2026-11-18 正式版 v0.111.1

工坊 v1.1.1 初始化时在 Harmony 的攻击音效补丁中引用了正式版不存在的 `DamageTargetData`。已挂上的外观补丁仍能运行，后续盾挡与伤害反馈补丁未能安装。现按实际程序集选择伤害重载、参数字段和扣血调用点，并兼容两个版本的选人传参。正式版保留原生卡牌特效调用，自定义受击反馈仍使用实际伤害结果。

本地 v0.111.1 已通过全部补丁安装、盾挡、攻击音效、敌人受击、迅捷弓及设置桥接检查。新版伤害接口的分支保留，尚未在含 `DamageTargetData` 的 beta 程序集上复验。只替换本地订阅目录中的 DLL，备份及哈希见 `artifacts/stable-111/deployment.json`。

## 2026-11-14 同装兼容修复

用户报告未锈铠初始化失败。最新游戏日志显示 `Hcxmmx.AKnightSkin.AttackAudioPatch` 在 `AttackCommand.Execute.MoveNext` 报 `Expected one native damage call, found 0`。国王先将该原生调用替换为 `KingWeaponAudio.DealDamage`，导致未锈铠的新版伤害观察器无法找到挂接位置。

修复集中于国王工程。攻击执行 Transpiler 使用 Last 优先级，让其他皮肤先观察原生伤害调用；音效改为保留当前调用的条件守卫，识别未锈铠／苏芙比已有的音效包装，按真实签名保存和恢复参数。组合检查另外发现苏芙比先加载时，国王选人静音补丁需要识别 PlaySelectionSound 包装，该处也已处理。

在原生 Godot/.NET 9.0.7 中加载当前已安装的未锈铠 0.5.19、苏芙比 0.6.13 与修复后的国王，四组完整 PatchAll 检查通过，包括国王／未锈铠双向顺序、三者正向与反向顺序。检查确认未锈铠命中观察一次、国王伤害作用域一次、国王音效守卫四处。国王独立补丁安装、攻击音效、盾牌及受伤声音基础检查通过。

已部署至本地国王 mods，DLL SHA-256 为 `9B308BB0EF02FC8EDD421CD5E208F6DD6DDB2200D54BED523B4E24626BA32AC3`。备份、安装报告和组合检查日志位于 `artifacts/mod-combination/`。游戏重启后的实际初始化与对局待用户确认。

日志另记录空洞骑士的 `NMerchantRoom.HideScreen` 目标缺失，此项属于该模组的 beta 接口适配问题。

## 首轮调查与接入记录

2026-11-14。参考斯奈德项目的《0.113兼容修复交接》，完成国王模组调查、修复与本地部署。用户负责实际游戏验收，修复交付记录见文末。

## 核对基准

- 当前游戏 `release_info.json` 为 `v0.113.0`，提交 `16ac0fe3`，Steam 分支 `public-beta`。
- 当前 `sts2.dll` SHA-256 为 `A6CC282C52B96AD2E01D5692B1144B33AA8E3BA4E969582F648ECB13B2F751D7`。
- 已安装国王 DLL SHA-256 为 `57D2FDF5BB46F24AD92744874B21F9AC8174D78E7FA2A1901281B21766D8EDE4`，对应上次恢复到 0 dB 的版本。
- 从当前游戏程序集定向反编译本轮相关类型，输出到 `artifacts/beta113-investigation/`。项目原有旧版反编译目录保留。
- 检查时构建显式使用 `-p:DeployModOnBuild=false`。当前源码编译零警告、零错误。

## 修复前确认的问题

### 普通商店与假商人退出

`Patch_Room.cs` 的两个补丁分别定位 `NMerchantRoom.HideScreen` 与 `NFakeMerchant.HideScreen`，当前版本均已移除。实际游戏日志也记录国王初始化在前一个补丁处失败。

两个补丁原本只负责清除 `KingGlobals.IsInShop`。该标志同时控制国王的战斗动作与武器声音，需要随实际房间离开清理。可结合当前 `OnProceedButtonReleased` 与 `_ExitTree` 生命周期处理，覆盖按钮离开和节点退出。普通商店的继续按钮存在新手引导分支，点击按钮后可能仍留在商店，需避免提前清除房间状态。

新版 `NMerchantRoom` 为抽象基类，实际存在 `NStandardMerchantRoom` 与 `NBlitzMerchantRoom`。两者 `_Ready` 都调用基类实现，因此现有基类 `_Ready` 挂点仍能进入。`_players`、`_playerVisuals` 与 `%CharacterContainer` 仍存在，玩家商店节点 `NMerchantCharacter` 仍继承 `Node2D`，现有替换方式具备继续复用的条件。需分别验收两种商店。

假商人的 `%CharacterContainer` 与 `_ExitTree` 仍存在，可复用已有模型清理。当前代码按节点名寻找 Regent，还需实际房间确认替换结果及触发战斗后的过渡。

### 选人参数与原生语音

新版 `NCharacterSelectScreen.SelectCharacter` 只有 `NCharacterSelectButton charSelectButton` 一个参数，通过 `charSelectButton.Character` 读取角色。国王 Prefix 和 Postfix 都依赖已删除的 `characterModel` 实参，Harmony 参数绑定失败。

需要调整角色读取，同时保留国王背景、介绍文字、选人语音和其他角色选择流程。交接文档中的按钮取角色方式可以复用。

另发现原来的静音实现尝试修改 `CharacterSelectSfx` 属性、同名字段及自动属性后备字段。当前属性只有 getter，两个字段均不存在，getter 按角色 ID 动态生成音效路径。修复参数后还需在实际原生声音调用处进行限定角色的处理，才能可靠避免储君声音与国王声音叠加。这属于本次检查发现的现有实现问题，尚未证明由 0.113 引入。

### 国王专属攻击与实际扣血音效

涉及 `Patch_WeaponAudio.cs` 与 `KingWeaponAudio.cs`，需要一起适配。

1. 旧补丁精确定位的七参数 `CreatureCmd.Damage` 已不存在，`KingAudioPatchTargets.Damage` 为 null。
2. 新版 `AttackCommand.Execute.MoveNext` 实际调用八参数重载，第二参数为 `IEnumerable<DamageTargetData>`，末尾增加 `ModifyDamageHookType`。
3. 新的 Creature 列表八参数重载只是普通转发方法。真正包含伤害结算的 async 状态机位于 DamageTargetData 重载。
4. 状态机保存目标序列的字段为 `targetData`，旧补丁读取的 `targets` 字段已不存在。
5. 新状态机直接调用 `LoseHpInternal` 共三处，旧补丁强制要求两处。需要覆盖前置转移、未格挡伤害与转移后原目标剩余伤害路径。
6. 已安装旧 DLL 的 `DealDamage` 状态机 JIT 还会因缺少旧七参数方法而失败。单纯重新编译会使直接调用绑定到八参数方法，但反射定位及两个相关 Transpiler 仍然失效。

国王现有语义是实际产生正数 HP 损失才播放命中声，同一群攻伤害组只播放一次，每段攻击各自保存武器与声音，并隔离嵌套反伤等伤害。斯奈德文档提供的新伤害调用识别方式可以参考，其调用前通知无法独立完成国王当前的扣血判断。

实施时需要保留新的目标记录及附带特效信息，原样传递全部参数和返回 Task，继续在真实扣血结果处通知国王音效。目标序列身份校验、群攻去重、多段独立计数、全格挡静音和死亡／结束清理都需保留。新版增加的扣血分支应进入针对性检查。

## 当前检查通过或未发现接口变化的部分

- 24 个 Harmony 补丁类中，21 个目标可以解析，3 个目标失败。另有选人 Prefix／Postfix 两个参数绑定错误，以及攻击执行 Transpiler 一个改写失败。
- 储君原生攻击音效的 `CreatureCmd.TriggerAnim` 调用处改写仍能完成。
- `NCharacterSelectButton.Init`、顶部头像、多人大厅读档主机／客机的目标与参数绑定仍成立，换肤和节奏按钮挂点保留。
- `NCreature._Ready`、`SetAnimationTrigger`、`AnimDie` 及其直接引用能解析，战斗视觉的 `%Visuals` 入口仍存在。
- 胜利补丁仍精确匹配 `EndCombatInternal(CombatTurnState)`。
- 夜歌使用的 `CardModel.OnPlayWrapper`、`NSovereignBladeVfx._Ready` 与 `Forge` 仍存在。新版代码仍引用 `SpineSword/SwordBone/ScaleContainer` 层级。
- 在原生 Godot Mono 与 .NET 9.0.7 中加载已安装 DLL／PCK，通过披风挂接、防重复、攻击推力、调色、角色状态隔离、风场生命周期、RNG 隔离及节点清理检查。
- 国王没有调用交接文档所列的 `AscensionHelper.GetTitle/GetDescription`，本轮没有相应迁移点。

上述检查覆盖方法定位、绑定、原始 IL 改写及独立资源加载。完整 Harmony 安装、实际对局动作、房间替换与声音需要修复后继续验收。

## 实施顺序

1. 修复两种商店退出挂点与选人参数，处理限定储君的选人声音调用。
2. 适配新版攻击伤害调用、实际伤害协程及三处扣血分支，恢复国王音效关联。
3. 更新兼容验证入口，先运行独立模组检查，再按本地可用情况验证 SkinChanger。既有离线验证工具当前强制要求旧 SkinChanger 安装路径，该路径已不存在，需调整验证入口。
4. 回归两种节奏、普通攻击、星尘连续攻击、群攻、全格挡与伤害转移，再检查死亡／胜利、夜歌、普通／Blitz 商店、假商人和营地。
5. 完成修复与验证后生成本地测试包，交用户进行游戏内验收。当前资源检查通过，预计主体修改集中在 C# 桥接与验证工具。

## 本轮证据

- `artifacts/beta113-investigation/probe-installed.log`，已安装 DLL 的全部补丁目标、参数绑定、三个音效 Transpiler、方法 JIT、新版实际 Damage 调用和扣血点数量。
- `artifacts/beta113-investigation/probe.log`，当前源码重新编译后的接口检查。
- `artifacts/beta113-investigation/native-bridge.log`，原生 Godot 资源及披风桥接检查。
- 游戏日志 `C:/Users/xmmx/AppData/Roaming/SlayTheSpire2/logs/godot.log`，国王初始化的商店挂点错误。

## 修复交付

按用户要求，以本地 beta v0.113.0 为本轮目标完成修复并部署，主要游戏测试交用户进行。

- 普通商店使用 `_ExitTree` 清理商店状态；假商人将状态清理并入已有 `_ExitTree` 补丁。
- 选人从 `charSelectButton.Character` 读取角色；在原生音效调用处增加储君判断，跳过储君选人声，国王场景负责播放自己的选人语音。
- 攻击包装方法接收新版 DamageTargetData 序列和 ModifyDamageHookType，原样传入游戏结算方法。命中音效作用域关联原目标序列对象，实际伤害协程的三处扣血结果均接入原有命中处理。
- 生产构建零警告、零错误。原生 Godot/.NET 9.0.7 中实际安装全部 23 个 Harmony 补丁类，覆盖 21 个原方法，263 个模组方法 JIT 通过。
- 既有音效基础检查通过，覆盖 43 段音频加载、实际扣血结果、群攻去重、多段声音、零伤害、嵌套伤害隔离与声音节点清理。此检查为部署前基础验证，实际对局由用户验收。

本地安装目录为 `D:/youxi/steamapps/common/Slay the Spire 2/mods/Hcxmmx_King_Skin`。已核对安装文件与测试包 SHA-256 一致。新 DLL SHA-256 为 `2B47DC4AB2B38F9F152D12C526523277CB03597408BC4008FB1996A5FEEEB195`。

- 测试包位于 `artifacts/beta113-fix/package/`。
- 部署前备份位于 `artifacts/beta113-fix/previous-installed/`。
- 安装报告位于 `artifacts/beta113-fix/deployment.json`。
- 补丁安装与音效基础检查日志位于 `artifacts/beta113-fix/patch-check.log`。

用户实测优先检查选人背景／语音／换肤和节奏按钮，进入战斗检查两种节奏的攻击与命中声音，尤其是星尘连续攻击及群攻，再检查死亡／胜利、夜歌与商店／假商人离开后的战斗恢复。

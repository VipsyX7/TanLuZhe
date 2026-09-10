# TanLuZhe — 横板 2D 平台跳跃 + 钩锁系统

Unity **6000.3.9f1**（URP 2D / Input System 新版）项目。角色物理为**力驱动 + 速度驱动混合**的完整平台跳跃手感实现，钩锁系统按"保持物理惯性"的原则用**绳索距离约束（径向速度约束 + 绞盘）**求解，而不是直接把玩家"传送"到落点。

---

## 一、如何运行

1. 用 Unity **6000.3.9f1** 打开本项目（`E:\Unity\TanLuZhe`）。
2. 打开场景 `Assets/Scenes/PlatformerDemo.unity`（已在 Build Settings 里排在第 0 位）。
3. 点击 **Play**。

如果场景丢失或想重新生成，菜单栏：

```
TanLuZhe ▸ Build Everything (art + project + scene)
```

这一条命令会：重新生成全部美术 → 配置物理层/物理材质 → 重建整张演示场景。

其他菜单：

| 菜单 | 作用 |
|---|---|
| `TanLuZhe ▸ 0. Setup Project (layers + physics)` | 只配置层、物理材质、2D 物理设置 |
| `TanLuZhe ▸ 1. Generate Art Assets` | 只重新生成 17 张程序化 PNG |
| `TanLuZhe ▸ 2. Build Demo Scene` | 只重建演示场景 |
| `TanLuZhe ▸ 3. Validate Demo Scene` | 静态校验场景接线（47 项检查） |

---

## 二、操作

| 按键 | 功能 |
|---|---|
| **A / D**（或 ← →） | 左右移动 |
| **空格 / W / ↑** | 跳跃（**按住越久跳得越高**；空中松开会被"跳跃截断"重力拉下来） |
| **E** | 朝**鼠标指针方向**发射钩锁 |
| **左 Ctrl** | 松开钩锁（**保留惯性**，钩锁不再拉扯玩家） |
| **鼠标左键** | **主手武器攻击** |
| **鼠标右键** | **副手武器攻击** |
| **F** | 拾取脚下的武器放进背包 |
| **B** | 打开/关闭背包（装备武器） |
| **S + 空格** | 从单向平台向下穿落 |
| **R** | 重开本关 |

屏幕左上角会实时显示钩锁状态（`HOOK HOOKED - WALL  rope 3.21 m  pull 12.4 m/s  |  ACTION LOCKED`）和双手武器（`MAIN [LMB] Sword   OFF [RMB] Bow   BAG 2/8 [B]`）。

---

## 三、钩锁系统（核心需求实现）

代码：`Assets/Scripts/Grapple/GrappleHook2D.cs`（求解）、`GrappleRopeRenderer.cs`（绳索渲染）

状态机：`Idle → Extending → Attached → Retracting`。
发射时沿指针方向用 `Physics2D.CircleCast` 逐帧推进钩头（半径 0.08，避免从墙角漏过），命中后按命中物类型分流。

### 1. 命中墙壁 —— 持续加速度拉向落点，到达后自动脱钩

绳索被解成**单向距离约束 + 持续加速度**，在 `FixedUpdate` 里只改径向分量、完全不碰切向分量：

```csharp
Vector2 toward = (anchor - origin).normalized;   // 玩家 -> 落点

// 1) 持续加速度：每一步都朝落点加速，速度上限 _maxPullSpeed
float radial = Vector2.Dot(velocity, toward);
if (radial < maxPullSpeed)
    velocity += toward * Mathf.Min(pullAcceleration * dt, maxPullSpeed - radial);

// 2) 不可伸长的绳子：只清除"远离落点"的径向分量
if (dist >= ropeLength) {
    float outward = Vector2.Dot(velocity, toward);
    if (outward < 0f) velocity -= toward * outward;
}

// 3) 到达落点 -> 自动脱钩，速度原样保留
if (dist <= releaseDistance) Release();
```

* **切向速度被完整保留** → 玩家是在钩点周围**荡秋千**（真实钟摆），不是被瞬移过去。这就是"保持物理惯性"。
* `_pullAcceleration` 默认 **140 m/s²**（必须明显大于重力，否则站在地上的玩家拉不动 —— 重力 74 m/s² + 抓地力 18 m/s²）；`_maxPullSpeed` 默认 30 m/s 封顶。
* **钩锁拉扯期间、且玩家没有按方向键时，角色控制器不做任何自身减速**（`ApplyHorizontalMovement` 提前返回）。否则地面减速 95 m/s² 会直接吃掉 140 m/s² 的拉扯。
* 绳子长度在挂钩瞬间固定，不再收缩：拉力是"持续加速度"，不是绞盘恒速。
* 玩家中心与落点距离 ≤ `_releaseDistance`（默认 1.0 m）时**自动解除钩锁**，玩家带着当前速度继续飞出去。
* 撞墙时 `collisionDetectionMode = Continuous`，高速拉扯不会穿模。

### 2. 命中敌方单位 —— 双向拉扯（等大反向冲量，按质量分配）

命中 `IGrappleTarget`（敌人 / 移动平台）时，同一条持续加速度对**两个刚体**求解，冲量等大反向：

```csharp
float invP = 1/playerMass, invE = 1/enemyMass, invSum = invP + invE;
float closing = Dot(playerVel - enemyVel, toward);       // 正 = 正在靠近
if (closing < maxPullSpeed) {
    float impulse = Mathf.Min(pullAcceleration * dt, maxPullSpeed - closing) / invSum;
    playerVel += toward * (impulse * invP);              // 玩家被拉向敌人
    enemyVel  -= toward * (impulse * invE);              // 敌人被拉向玩家
}
```

* **总动量守恒**（等大反向冲量），轻的一方被拉得多、重的一方被拉得少 —— 演示关卡里那个 3.6 质量的重装敌人会明显把你拽过去。
* **锁头始终与敌人重合**：挂钩后每帧（`Update` 里，不只是物理帧）把钩头位置钉在敌人身上的挂点 `CurrentAnchorWorld`，钩头朝向也沿绳索方向，所以敌人被拖动时钩子会牢牢"挂"在它身上。
* **玩家身体一碰到被勾住的敌人就自动脱钩**（`Collider2D.Distance` 检测重叠/接触，`_contactReleaseDistance` 默认 0.08 m），不需要手动松钩。
* 敌人被钩中时会掉血、进入硬直，并且**马达出力降到 30%**，形成可读的"拔河"。
* 敌人死亡 / 被销毁时自动脱钩。

### 3. 左 Ctrl 松开 —— 停止拉扯，保留惯性

```csharp
public void Release() {
    DetachInternal(true);   // 只清引用、把状态切成 Retracting
}
```

`Release()` **一个字节都不动刚体速度**：绳索消失，当帧速度原样保留 → 惯性完整保留，拉扯立刻消失。
同时 `PlayerController2D.SetBeingPulled(false)` 立刻恢复玩家行动与重力。

### 4. 勾中期间完全接管玩家（本次新增）

按"钩锁出手 ≠ 钩锁勾中"把控制权严格分开：

| 钩锁状态 | 玩家能否行动 | 重力 |
|---|---|---|
| `Idle` / `Retracting`（待机、收回） | ✅ 完全正常 | ✅ 正常 |
| **`Extending`（钩头飞行中）** | ✅ **完全正常**（可跑、可跳、可转向） | ✅ 正常 |
| **`Attached`（已勾中，正在拉扯）** | ❌ **所有操作键无效** | ❌ **重力被取消** |

实现方式（`PlayerController2D`）：

```csharp
private void FixedUpdate()
{
    ...
    if (IsBeingPulled)          // 只有 Attached 时由 GrappleHook2D 置为 true
    {
        _appliedGravity = 0f;   // 取消重力
        _isWallSliding = false;
        return;                 // 本帧不施加任何玩家驱动：无移动、无跳跃、无穿落、无落地粘附、无下落限速
    }
    ...
}
```

* `Update()` 在 `IsBeingPulled` 时**直接返回**：跳跃缓冲被清空、跳跃截断被取消、朝向不再改变。所以勾中期间按跳跃/方向/下蹲**完全没有任何效果**，也不会"排队"到脱钩瞬间才触发。
* 进入拉扯的瞬间（`SetBeingPulled(true)`）会清掉已有的跳跃缓冲与移动平台引用，避免残留状态。
* 于是勾中期间**唯一能改变玩家速度的就是钩锁求解器**，这才是"保持物理惯性"最纯粹的形式：钩锁只施加径向加速度，玩家原有的切向动量原封不动。
* 玩家死亡时钩锁自动脱钩，不会拖着尸体跑。
* HUD 在勾中时显示 `ACTION LOCKED  (CTRL = cut)`，玩家能立刻知道为什么按不动。

### 其它细节

* 空中无输入时横向加速度为 **0**（不擦除动量），所以松钩后能靠惯性飞出去。
* 绳索渲染：绷紧时直线 + 青色，松弛时按抛物线垂弧，玩家一眼能看出是否真的在被拉。

---

## 三·B、武器与攻击系统

代码：`Assets/Scripts/Weapons/` —— `WeaponDefinition`（数据）、`PlayerWeapons`（装备与攻击）、`WeaponHandVisual`（手持显示）、`Projectile2D`（远程弹道）、`WeaponPickup2D`（地面拾取）、`WeaponInventoryUI`（背包界面）。

### 装备与操作

| 操作 | 行为 |
|---|---|
| **F** | 把脚下高亮的武器放进背包（容量 8），世界里的武器消失 |
| **B** | 打开/关闭背包界面；界面打开时左键不再攻击，而是操作 UI |
| **左键** | 用**主手**武器攻击（`PlayerWeapons.ExecuteAttack(main, false)`） |
| **右键** | 用**副手**武器攻击（`ExecuteAttack(off, true)`） |

背包界面（B）里：**点手部槽位选中 → 点背包里的武器装备进去 → 再点已选中的槽位就卸下**。主副手各自独立冷却，可以一边挥剑一边射箭。

### 攻击实现

* **近战**：`Physics2D.OverlapBox(手部 + 朝向 × reach, meleeSize, 角度, 过滤器, 结果)`，对命中的所有 `IDamageable` 结算伤害 + 沿攻击方向的击退，并生成 `slash.png` 弧光与火花、轻微震屏。
* **远程**：生成 `Projectile2D`，每个物理步用 `Physics2D.Raycast` 扫掠推进（**不用碰撞体**，高速也不会穿墙），命中敌人结算伤害，命中地形只留火花。
* 攻击方向始终跟随鼠标指针；主副手武器都以手部枢轴为圆心指向光标，攻击时播放挥砍弧线动画。
* **勾中期间（绳索拉扯）所有攻击被屏蔽**，与"操作键无效"规则一致；脱钩后立即恢复。

### 打击音效

怪物每次受到伤害都会播放 **`Assets/Audio/660770__madpancake__hit-impact.ogg`**：

* 触发点统一在 `EnemyController2D.TakeDamage()` 里的 `PlayHurtSound()`，所以**任何伤害来源都会响**：武器近战/远程、钩锁勾中瞬间的撞击伤害、踩踏。
* 每只怪自带一个 `AudioSource`（`Awake` 里自动创建，`playOnAwake = false`）；用 `PlayOneShot` 播放，多只怪同时挨打不会互相打断。
* 音高带 ±12% 随机抖动（`_hurtPitchJitter`），连打不会听成机关枪；音量 0.85，`spatialBlend = 0.25` 带一点方位感。
* 死亡后 `TakeDamage` 提前返回，所以尸体不会继续出声。
* 导入设置为 **DecompressOnLoad + Vorbis + 预加载**（`ProjectSetup.ConfigureAudioImport()`），第一次挨打不会卡顿。

换音效：把 ogg 丢进 `Assets/Audio/`，改 `ProjectSetup.HitImpactSoundPath`；或者直接在场景里选中敌人，把 `Enemy Controller 2D` 的 **Hurt Sound** 槽换掉（不需要改代码、也不需要重建场景）。

### 音效一览

| 音效 | 文件 | 触发点 |
|---|---|---|
| 怪物/玩家受击 | `660770__madpancake__hit-impact.ogg` | `EnemyController2D.TakeDamage()`、`PlayerHealth.TakeDamage()` / `Kill()` |
| 金币拾取 | `336936__the-sacha-rush__coin9.wav` | `Collectible2D.OnTriggerEnter2D()` |
| 钩锁拉扯 | `810141__...tissue-pull-out...wav` | `GrappleHook2D.AttachTo()`：钩子咬住的瞬间响一次 |

三个文件都在 `Assets/Audio/`，导入设置由 `ProjectSetup.ConfigureAudioImport()` 统一设为 `DecompressOnLoad + Vorbis + 预加载`。

音效播放分两条路：

* **持久对象（怪物）** 自带 `AudioSource`，直接 `PlayOneShot`，天然带方位感；
* **会被销毁的对象（金币）** 走共享的 `SfxPlayer`（12 路复用的声部池）—— 因为 `Destroy(gameObject)` 会把挂在它身上的音源一起干掉，声音会被掐断。`SfxPlayer` 会在需要时自动创建，也可以像演示场景那样预先摆在 `Services` 下。

所有音效都带**随机音高抖动**（金币 ±12%、受击 ±10%、钩锁 ±8%），连续触发不会听成一个死板的长音。玩家受击音还有一个 0.1 秒的去抖：一次致命伤害会同时走 `TakeDamage` 和 `Kill`，去抖保证只响一次。

### 武器数据（`Assets/Settings/Weapons/*.asset`）

| 武器 | 类型 | 伤害 | 冷却 | 特点 |
|---|---|---|---|---|
| Sword | 近战 | 28 | 0.42 s | 均衡，起始主手 |
| Spear | 近战 | 20 | 0.30 s | 攻击距离 1.3 m，出手快 |
| Hammer | 近战 | 55 | 0.95 s | 击退 22，命中震屏最强 |
| Bow | 远程 | 22 | 0.55 s | 弹速 34 m/s，起始副手 |
| Wand | 远程 | 13 | 0.20 s | 高频低伤 |

全部是 ScriptableObject，可以直接在 Inspector 里改数值，改完立刻生效（不用重编译）。想加新武器：`Assets ▸ Create ▸ TanLuZhe ▸ Weapon`。

---

## 四、玩家物理系统（`PlayerController2D.cs`）

**设计原则：所有移动都走"有限加速度的力/速度驱动"，而不是每帧硬写 `velocity`**，这样钩锁、敌人撞击、移动平台注入的动量不会被控制器瞬间抹平。

| 类别 | 已实现 |
|---|---|
| 跳跃 | 跳跃缓冲（0.12 s）、土狼时间（0.10 s）、可变跳跃高度（松键截断重力 ×2.8） |
| 重力 | 上升 / 下落 / 顶点悬停三段重力（42 / 74 / 26 m/s²）、最大下落速度 24 m/s |
| 水平 | 地面加减速（75 / 95）、空中加速（48）、**空中无输入不减速**、转身加速 ×1.8、勾中期间完全接管 |
| 墙体 | 墙滑（限速 4.5 m/s）、墙跳（11, 15.5）、墙跳操控锁定 0.18 s、墙土狼 0.10 s |
| 地面检测 | 直接读真实接触流形 `Rigidbody2D.GetContacts()`，另加 `BoxCast` 兜底；法线过滤支持 ≤50° 斜坡 |
| 斜坡 | 把驱动力投影到切向；无输入时施加**抓地力**抵消重力切向分量（角色摩擦为 0，不影响墙滑） |
| 平台 | 单向平台（`PlatformEffector2D`）+ S 键穿落；移动平台按帧位移带人 |
| 其他 | 死亡后仅保留重力、受击硬直、`Teleport` 复活、连续碰撞检测、`NeverSleep` |

角色与敌人的物理材质摩擦/弹性均为 **0**（`Assets/Settings/Physics/`），墙滑与绳索摆动不会被摩擦吃掉。

---

## 五、目录结构

```
Assets/
├── Art/Generated/          17 张程序化生成的 PNG（可直接重新生成）
├── Editor/
│   ├── ProceduralArtGenerator.cs   像素美术生成器（纯代码画图 + 导入设置）
│   ├── ProjectSetup.cs             层 / 物理材质 / 2D 物理设置
│   ├── DemoSceneBuilder.cs         整张演示场景的程序化搭建
│   ├── SceneValidator.cs           场景静态校验（47 项，CI 可用）
│   └── BuildPipelineEntry.cs       一键 / 批处理入口
├── Scripts/                （TanLuZhe.Runtime 程序集）
│   ├── Core/      输入抽象、伤害契约、层定义、物理引导
│   ├── Player/    角色控制器、生命/复活
│   ├── Grapple/   钩锁求解、绳索渲染
│   ├── Enemies/   巡逻/追击敌人（可被钩）
│   ├── World/     移动平台、尖刺、金币、检查点、终点、视差
│   ├── Camera/    跟随相机（死区 + 前瞻 + 边界 + 震屏）
│   ├── FX/        池化精灵粒子
│   └── UI/        游戏状态管理、HUD
├── Scenes/PlatformerDemo.unity     已生成并可直接 Play
└── Tests/PlayMode/                 15 个 PlayMode 自动化测试
```

---

## 六、自动化验证（本仓库实际跑通的结果）

### PlayMode 测试：31/31 通过

```
Unity.exe -batchmode -projectPath <项目> -runTests -testPlatform PlayMode -testResults results.xml
```

覆盖内容（全部为真实物理仿真，非 mock）：

| 测试 | 验证的东西 |
|---|---|
| `Player_FallsAndRestsOnGround` | 落到地面并稳定静止 |
| `Player_JumpReachesConfiguredHeight` | 跳跃顶点高度 ≈ 配置值 3.1（±0.6） |
| `Player_KeepsAirMomentum_WithoutInput` | 空中无输入**不擦除**水平动量 |
| `Player_WallSlideLimitsFallSpeed` | 贴墙下落被限速（现在精确钳到 4.5 m/s） |
| `Grapple_Wall_PullsPlayerTowardImpactPoint` | **命中墙壁后玩家被拉向落点**、绳索绷紧、落点在墙面内侧 |
| `Grapple_Wall_AppliesContinuousPullAcceleration` | **拉扯是"持续加速度"**（收拢速度不断攀升）且受上限约束 |
| `Grapple_Wall_AutoReleasesOnArrival` | **玩家到达落点后自动脱钩**，脱钩时确实已经到达（最近距离 ≤1.2 m） |
| `Grapple_Attached_IgnoresPlayerInput` | **勾中期间狂按方向/跳跃/下蹲全部无效**（跳跃速度上限 < 8 m/s 而非 16），钩锁不受影响 |
| `Grapple_Attached_SuspendsGravity` | **勾中期间重力被取消**（水平绳上玩家不下沉、竖直速度 ≥ -0.5） |
| `Grapple_Extending_LeavesPlayerInControl` | **钩头飞行期间玩家操作完全正常**（能跑、能跳） |
| `Grapple_Wall_PreservesTangentialMomentum` | **切向（惯性）速度在拉扯中保留 ≥55%**，径向才被改变 |
| `Grapple_Enemy_PullsBothBodiesTogether` | **敌人被拉向玩家、玩家被拉向敌人**，最终真正接触 |
| `Grapple_Enemy_PullIsMassWeighted` | 4 倍质量的敌人位移只有玩家的 ~1/4（等大反向冲量） |
| `Grapple_Enemy_HeadStaysPinnedToTarget` | **锁头始终与敌人重合**（把敌人手动拖动，钩头误差 < 0.05 m） |
| `Grapple_Enemy_ContactReleasesHook` | **玩家碰到被勾中的敌人时自动脱钩** |
| `Grapple_LeftCtrl_ReleasesAndKeepsInertia` | **左 Ctrl 脱钩后水平惯性原样保留**，且不再被加速 |
| `Grapple_Miss_RetractsToIdle` | 空放后自动收回 |
| `Grapple_EnemyDeath_DetachesHook` | 目标销毁自动脱钩 |
| `Enemy_MassHeavier_MeansLessAcceleration` | 质量对同尺寸冲量的影响生效 |
| `DemoScene_RunsEndToEnd` | **加载真实演示场景**跑完整流程：落地→跑→跳→钩墙（Ctrl 脱钩）→再钩墙（到达自动脱钩）→钩敌人（接触自动脱钩）→相机跟随，期间任何异常/错误日志都会导致失败 |
| `DemoScene_EnemiesPatrolAndPickupsExist` | 场景内容齐备且敌人真的在巡逻 |
| `PickUp_F_AddsWeaponToBackpack` | **按 F 把武器收进背包**，世界里的拾取物消失 |
| `PickUp_BackpackRespectsCapacity` | 背包满时拒绝新武器 |
| `Equip_SwapsBetweenHandsAndBackpack` | 装备/换手/卸下时武器在手与背包之间正确流动 |
| `MeleeAttack_DamagesEnemyInFront` | 主手近战命中前方敌人并结算伤害 |
| `MeleeAttack_RespectsCooldown` | 冷却期间重复按键不生效，冷却结束后恢复 |
| `RangedAttack_ProjectileDamagesEnemy` | 副手远程发射弹道并命中敌人 |
| `LeftMouseUsesMainHand_RightMouseUsesOffHand` | **左键 = 主手（28），右键 = 副手（7）**，伤害值可区分 |
| `Attack_BlockedWhileRopeIsAttached` | 勾中期间攻击无效，脱钩后恢复 |
| `Attack_BlockedWhileBackpackIsOpen` | 背包打开时左键不再攻击 |
| `Inventory_BKeyTogglesThePanel` | **B 键开关背包** |
| `InventoryUI_ClickEquipsIntoTheSelectedHand` | **点背包武器 → 装到选中的手**（主手/副手各测一次） |

### 场景静态校验：48 项检查全过

```
Unity.exe -batchmode -quit -projectPath <项目> -executeMethod TanLuZhe.EditorTools.BuildPipelineEntry.BuildAndValidate
```

包含：相机正交/位置/跟随目标、全局 2D 光照、玩家各组件、钩锁精灵/枪口/掩码、绳索材质、敌人/金币/检查点/终点/尖刺/单向平台/视差层数量、所有 `SpriteRenderer` 都有精灵与材质、**没有 Missing Script**、HUD 各字段接线。

> 顺带说明：这套校验在开发过程中真的抓到了两个 bug —— 钩锁用 `transform.root` 判断"是不是自己"导致钩子穿过所有目标；以及两个 MonoBehaviour 的类名与文件名不一致导致场景里出现 Missing Script。两者都已修复并被测试锁住。

---

## 七、常用调参位置

| 想要的效果 | 改哪里 |
|---|---|
| 钩子飞得更远 / 更快 | `GrappleHook2D._maxRange` / `_hookSpeed` |
| 拉得更猛 / 更柔和 | `_pullAcceleration`（默认 140，需 > 重力 74 + 抓地 18 才能把站立的玩家拉起） |
| 拉扯速度上限 | `_maxPullSpeed`（默认 30 m/s） |
| 更早 / 更晚自动脱钩 | `_releaseDistance`（默认 1.0 m） |
| 碰敌人后更早脱钩 | `_contactReleaseDistance` |
| 绳子更"弹" | `_ropeGrip`（1 = 完全不可伸长） |
| 钩敌人时更拉扯 | `EnemyController2D._grappleMotorFactor`（越小越拉得动） |
| 跳得更高 / 更飘 | `PlayerController2D._jumpHeight`、`_riseGravity`、`_fallGravity` |
| 空中操控更强 | `_airAcceleration` |
| 勾中时是否锁操作 / 关重力 | `PlayerController2D` 里的 `IsBeingPulled` 分支（勾中锁定、出手不锁） |
| 武器伤害/冷却/击退 | `Assets/Settings/Weapons/*.asset`（Inspector 直接改） |
| 背包容量、起始武器 | `PlayerWeapons._backpackCapacity` / `_startingMainHand` / `_startingOffHand` |
| 挥砍动画幅度 | `WeaponHandVisual._swingArc` / `_swingTime` |
| 墙跳手感 | `_wallJumpVelocity`、`_wallJumpControlLock` |

所有参数都在 Inspector 上暴露（`[SerializeField]`），可以在运行时实时拖动观察。

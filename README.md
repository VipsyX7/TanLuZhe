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
| **S + 空格** | 从单向平台向下穿落 |
| **R** | 重开本关 |

屏幕左上角会实时显示钩锁状态：`HOOK HOOKED - WALL   rope 3.21 m   winch 7.4 m/s`，方便观察绳索长度与绞盘速度。

---

## 三、钩锁系统（核心需求实现）

代码：`Assets/Scripts/Grapple/GrappleHook2D.cs`（求解）、`GrappleRopeRenderer.cs`（绳索渲染）

状态机：`Idle → Extending → Attached → Retracting`。
发射时沿指针方向用 `Physics2D.CircleCast` 逐帧推进钩头（半径 0.08，避免从墙角漏过），命中后按命中物类型分流。

### 1. 命中墙壁 —— 保持惯性，把玩家拉向落点

绳索被解成**单向距离约束**，在 `FixedUpdate` 里只改径向分量、完全不碰切向分量：

```csharp
Vector2 toward = (anchor - origin).normalized;   // 玩家 -> 落点
float radial  = Vector2.Dot(velocity, toward);   // 向外（远离落点）为正
if (dist >= ropeLength) {
    // 绷紧：径向速度不得再把绳子拉长，同时绞盘以 reelSpeed 收绳
    if (radial < desiredClosing) velocity += toward * (desiredClosing - radial);
}
if (dist > ropeLength)                            // Baumgarte 位置误差修正
    velocity += toward * (dist - ropeLength) * positionCorrection;
```

* **切向速度被完整保留** → 玩家是在钩点周围**荡秋千**（真实钟摆），不是被瞬移过去。这就是"保持物理惯性"。
* 绞盘速度 `_reelSpeed` 由 `_reelAcceleration` 从 0 平滑加速到 `_maxReelSpeed`（默认 8.5 m/s），所以是**甩**过去而不是瞬间弹射。
* 绳长收到 `_minRopeLength`（0.8 m）后绞盘停止出力，绳子退化为不可伸长的刚绳：**只禁止拉开，不再继续往里推**，玩家就吊在落点附近。
* 撞墙时 `collisionDetectionMode = Continuous`，高速拉扯不会穿模。

### 2. 命中敌方单位 —— 双向拉扯（等大反向冲量，按质量分配）

命中 `IGrappleTarget`（敌人 / 移动平台）时，同一约束对**两个刚体**求解，冲量等大反向：

```csharp
float invP = 1/playerMass, invE = 1/enemyMass, invSum = invP + invE;
float relative = Dot(enemyVel - playerVel, toward);   // 正 = 互相远离
if (relative > -reelSpeed) {
    float impulse = (relative + reelSpeed) / invSum;
    playerVel += toward * (impulse * invP);           // 玩家被拉向敌人
    enemyVel  -= toward * (impulse * invE);           // 敌人被拉向玩家
}
```

* **总动量守恒**（等大反向冲量），轻的一方被拉得多、重的一方被拉得少 —— 演示关卡里那个 3.6 质量的重装敌人会明显把你拽过去。
* 敌人被钩中时会掉血、进入硬直，并且**马达出力降到 30%**，形成可读的"拔河"。
* 敌人死亡 / 被销毁时自动脱钩。

### 3. 左 Ctrl 松开 —— 停止拉扯，保留惯性

```csharp
public void Release() {
    DetachInternal(true);   // 只清引用、把状态切成 Retracting
}
```

`Release()` **一个字节都不动刚体速度**：绳索消失，当帧速度原样保留 → 惯性完整保留，拉扯立刻消失。
同时 `PlayerController2D.SetBeingPulled(false)` 恢复空中操控系数。

### 其它细节

* 空中无输入时横向加速度为 **0**（不擦除动量），所以松钩后能靠惯性飞出去。
* 被钩住时空中操控系数降到 `_grappleAirControl = 0.45`，强调"钩锁主导、惯性主导"。
* 绳索渲染：绷紧时直线 + 青色，松弛时按抛物线垂弧，玩家一眼能看出是否真的在被拉。

---

## 四、玩家物理系统（`PlayerController2D.cs`）

**设计原则：所有移动都走"有限加速度的力/速度驱动"，而不是每帧硬写 `velocity`**，这样钩锁、敌人撞击、移动平台注入的动量不会被控制器瞬间抹平。

| 类别 | 已实现 |
|---|---|
| 跳跃 | 跳跃缓冲（0.12 s）、土狼时间（0.10 s）、可变跳跃高度（松键截断重力 ×2.8） |
| 重力 | 上升 / 下落 / 顶点悬停三段重力（42 / 74 / 26 m/s²）、最大下落速度 24 m/s |
| 水平 | 地面加减速（75 / 95）、空中加速（48）、**空中无输入不减速**、转身加速 ×1.8、被钩时操控 ×0.45 |
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

### PlayMode 测试：15/15 通过

```
Unity.exe -batchmode -projectPath <项目> -runTests -testPlatform PlayMode -testResults results.xml
```

覆盖内容（全部为真实物理仿真，非 mock）：

| 测试 | 验证的东西 |
|---|---|
| `Player_FallsAndRestsOnGround` | 落到地面并稳定静止 |
| `Player_JumpReachesConfiguredHeight` | 跳跃顶点高度 ≈ 配置值 3.1（±0.6） |
| `Player_KeepsAirMomentum_WithoutInput` | 空中无输入**不擦除**水平动量 |
| `Player_WallSlideLimitsFallSpeed` | 贴墙下落被限速 |
| `Grapple_Wall_PullsPlayerTowardImpactPoint` | **命中墙壁后玩家被拉向落点**、绳索绷紧、落点在墙面内侧 |
| `Grapple_Wall_PreservesTangentialMomentum` | **切向（惯性）速度在拉扯中保留 ≥55%**，径向才被改变 |
| `Grapple_Enemy_PullsBothBodiesTogether` | **敌人被拉向玩家、玩家被拉向敌人**，间距缩小 |
| `Grapple_Enemy_PullIsMassWeighted` | 4 倍质量的敌人位移只有玩家的 ~1/4（等大反向冲量） |
| `Grapple_LeftCtrl_ReleasesAndKeepsInertia` | **左 Ctrl 脱钩后水平惯性原样保留**，且不再被加速 |
| `Grapple_Miss_RetractsToIdle` | 空放后自动收回 |
| `Grapple_EnemyDeath_DetachesHook` | 目标销毁自动脱钩 |
| `Grapple_ReelSpeedRampsUp` | 绞盘是加速而不是瞬发 |
| `Enemy_MassHeavier_MeansLessAcceleration` | 质量对同尺寸冲量的影响生效 |
| `DemoScene_RunsEndToEnd` | **加载真实演示场景**跑完整流程：落地→跑→跳→钩墙上爬→Ctrl 脱钩→钩敌人→相机跟随，期间任何异常/错误日志都会导致失败 |
| `DemoScene_EnemiesPatrolAndPickupsExist` | 场景内容齐备且敌人真的在巡逻 |

### 场景静态校验：47 项检查全过

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
| 拉得更猛 / 更柔和 | `_maxReelSpeed`、`_reelAcceleration` |
| 吊得更靠近落点 | `_minRopeLength`（越小越贴脸） |
| 绳子更"弹" | `_ropeGrip`（1 = 完全不可伸长） |
| 钩敌人时更拉扯 | `EnemyController2D._grappleMotorFactor`（越小越拉得动） |
| 跳得更高 / 更飘 | `PlayerController2D._jumpHeight`、`_riseGravity`、`_fallGravity` |
| 空中操控更强 | `_airAcceleration`、`_grappleAirControl` |
| 墙跳手感 | `_wallJumpVelocity`、`_wallJumpControlLock` |

所有参数都在 Inspector 上暴露（`[SerializeField]`），可以在运行时实时拖动观察。

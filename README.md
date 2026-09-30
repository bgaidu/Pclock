# 电脑锁 (PCLock) — Windows 7 儿童电脑使用时长控制
思路来源于https://github.com/wplleo/kidslock

专为 Windows 7 电脑/一体机设计的家长控制程序。后台静默运行，按设定时长倒计时，到时自动锁屏；
孩子必须**连续答对 3 道小学三年级口算题**才能解锁，家长也可用 **PIN 码**快速解锁。

## 功能对照

| 需求 | 实现方式 |
|---|---|
| 开机自启、开机即倒计时 | 首次运行自动创建"登录时运行、最高权限"的计划任务（无 UAC 弹窗）；倒计时从登录那一刻开始 |
| 后台默认运行 | 无窗口，仅托盘小图标；设置/退出均需家长 PIN |
| 定时锁屏 15/30/45/60/90/120 分钟 | 托盘设置里六档可选，默认 60 分钟；剩余 5 分钟和 1 分钟有气泡提醒 |
| 重启仍锁 | 锁屏标记和剩余时间写入注册表 `HKLM\SOFTWARE\PCLock`；锁屏状态下关机重启，开机后**立刻恢复锁屏**；正常使用中重启不清零剩余时间，且**断电/关机期间的时长会折算扣除**（详见防作弊设计） |
| 认字解锁 | 全屏锁屏界面随机出千以内加减法、两位数乘一位数、整除除法（三年级水平），**一次共 10 道互不重复的题，全部答对才能解锁**；答错任意一题则全部清零、等 5 秒从第 1 题重新开始 |
| 家长 PIN | 锁屏界面右下角"家长入口"，输入 PIN 直接解锁；默认 **1234**，可在设置中修改 |
| 卸载需要密码 | 系统中没有任何"添加/删除程序"卸载入口；只能通过"设置 → 卸载并删除程序"，需要单独的**卸载密码**（默认 1234，可改） |
| 任务管理器不能杀进程 | 双重防护：① 进程 DACL 拒绝 `TERMINATE` 权限，任务管理器/taskkill 结束进程时提示**拒绝访问**；② 主程序与看门狗两个进程互相守护，任何一个被杀，另一个 3 秒内把它重新拉起并恢复锁屏。锁屏期间还写入系统策略**禁用任务管理器** |

## 防作弊设计

- **改系统时间没用**：正常倒计时直接递减剩余秒数，与系统时间无关；断电折算用的 UTC 时间戳只减不增，时钟回拨无法获利（回拨系统时间本身就需要管理员权限）。
- **直接断电（拔电源/遥控关机）也没用**：剩余时间每 15 秒连同 UTC 时间戳持久化，下次开机按时间戳扣除离线期间的时长，扣到 0 则开机即锁屏。因此「每次可用时长」实际按**日历时间**消耗——隔夜关机会扣掉隔夜时长；家长通过设置里「退出程序」正常退出时，剩余时间重置为完整时长。
- **关机重启没用**：剩余时间和锁屏状态每 15 秒持久化，重启后接着算。
- **Alt+Tab / Win 键 / Ctrl+Esc 被屏蔽**：锁屏期间通过低级键盘钩子拦截；锁屏窗口全屏置顶、覆盖所有显示器、无法关闭（Alt+F4 无效）。
- **答错清零**：10 题中答错任何一题，进度全部清零并从第 1 题重新开始，题目用完自动去重，防止碰运气刷题。
- **结束进程没用**：DACL 拒绝结束 + 看门狗互相拉活 + 锁屏期间任务管理器被策略禁用。

## 编译

把整个 `pclock` 文件夹拷到目标 Win7 机器（或任意有 .NET 的机器），双击 `build.bat`。
优先使用 .NET 3.5 编译器（Win7 自带），产物 `PCLock.exe` 在 Win7 上**无需安装任何运行库**。

## 安装（只需一次）

1. 在 C 盘建目录，如 `C:\Program Files\PCLock\`，把 `PCLock.exe` 放进去（路径建议固定，以后不要挪动）。
2. 右键 `PCLock.exe` → **以管理员身份运行**。程序会自动：
   - 创建开机自启计划任务（`schtasks` 名为 `PCLock`，最高权限、登录即启动、无 UAC 弹窗）；
   - 初始化默认设置（60 分钟、家长 PIN 1234、卸载密码 1234）；
   - 开始倒计时。
3. 之后每次开机登录都自动开始倒计时，无需任何操作。

## 日常使用（家长）

- 托盘小盾牌图标双击 → 输入家长 PIN → 设置窗口：
  - 选六档时长并保存（本次剩余时间重置）；
   - 修改家长 PIN、修改卸载密码（建议第一时间改掉默认 1234）；
  - 立即锁定 / 退出程序 / 卸载并删除程序（需卸载密码）。

## 卸载

托盘 → 设置（家长 PIN）→ **卸载并删除程序** → 输入卸载密码。
程序会自动删除计划任务、注册表配置和全部文件。

## 忘记密码的应急通道（家长专用）

只要你是 Windows 管理员，用管理员命令提示符执行：

```bat
schtasks /delete /f /tn PCLock
reg delete "HKLM\SOFTWARE\PCLock" /f
```

然后删除程序文件夹即可彻底移除（或删掉注册表键后重跑程序恢复默认 PIN 1234）。

## 安全边界（请务必阅读）

没有绝对不可破解的防护，以下是已知边界和推荐用法：

1. **强烈建议：孩子使用"标准用户"账户，家长管理员账户设置 Windows 密码。**
   这样孩子无法提权，任务管理器/注册表/删除文件都动不了本程序，防护最牢固。
2. **Ctrl+Alt+Del 无法屏蔽**（Windows 安全序列）。锁屏界面下任务管理器已被禁用；
   若孩子注销，重新登录后程序随开机自启立刻恢复锁屏。请勿让孩子知道任何 Windows 账户密码。
3. **安全模式**：开机狂按 F8 进入安全模式时计划任务不启动，孩子可用电脑但依然无法删除程序文件或注册表（标准用户无权限）；退出安全模式正常开机后锁屏状态恢复。
4. 若孩子使用的是**管理员账户**且知道密码，可手动结束进程/删注册表——任何软件都无法完全防住知道管理员密码的人。请管好管理员密码。
5. 程序编译后建议在真机 Win7 上完整验证一遍：倒计时到点锁屏、答完 10 题解锁、PIN 解锁、锁屏中重启恢复、任务管理器结束进程被拒绝。

## 项目结构

```
pclock/
├── build.bat          一键编译
├── app.manifest       要求管理员权限运行
└── src/
    ├── Program.cs     入口：单实例控制、--watchdog 模式分发
    ├── App.cs         主逻辑：托盘、倒计时、锁屏/解锁、自启、退出/卸载
    ├── Store.cs       注册表持久化（时长/PIN/锁屏标记/剩余秒数）
    ├── MathUtil.cs    三年级口算题生成（一批 10 道互不重复）
    ├── Protection.cs  进程 DACL 自保护、DisableTaskMgr 策略
    ├── Watchdog.cs    看门狗：主程序被杀则拉起并保持锁屏
    ├── LockForm.cs    全屏锁屏界面 + 键盘钩子 + 答题/PIN 解锁
    └── SettingsForm.cs 家长设置 + PIN 验证对话框
```

## 本副本修复记录（相对上游）

### Bug 修复

1. **(2026-09-30) 气泡提醒因 Timer 精度漂移可能跳过**（`App.cs`）
   原代码用 `remaining == 300` / `remaining == 60` 精确匹配，Timer 精度可跳过中间值。
   改为 `remaining <= 300` / `remaining <= 60` 范围判断 + `warned5` / `warned1` 标志位防重复，
   在 `Unlock()` 和 `ResetRemaining()` 中重置标志。

2. **(2026-09-30) 计划任务 `/TR` 参数多前导反斜杠**（`App.cs`）
   原代码 `schtasks /TR "\" + exe + "\\"` 生成了 `\\C:\...\PCLock.exe\\`，
   `schtasks` 不接受多前导 `\`。去掉多余转义，现在正确生成 `/TR "C:\Program Files\PCLock\PCLock.exe"`。

3. **(2026-09-30) UnprotectSelf 安全描述符结构错误**（`Protection.cs`）
   原代码构造 20 字节 SD 并设 `SE_DACL_PRESENT`，但 ACL 数据不存在，`Dacl` 偏移无效。
   改为 28 字节（20 header + 8 字节空 ACL），正确设置 Dacl 偏移=20、ACL_REVISION=2、ACE 数=0。

4. **(2026-09-30) 口算题可能凑不齐指定数量**（`MathUtil.cs`）
   `NextBatch(count)` 中 2000 次循环用尽后直接返回，可能少于 count 道。
   增加兜底 while 循环：不重复抽完后再允许重复，保证返回数量恒等于 count。

5. **(2026-09-30) 看门狗日志用相对路径**（`Watchdog.cs`）
   原代码 `File.WriteAllText("watchdog_log.txt", ...)` 写到当前工作目录。
   改为 `AppDomain.CurrentDomain.BaseDirectory + "watchdog_log.txt"`，确保写入 EXE 同目录。

6. **(2026-09-30) 看门狗 while 循环内 GetLockFlag 无异常保护**（`Watchdog.cs`）
   `Store.GetLockFlag()` 可能因注册表异常抛出，直接放在 `if` 条件里会导致 while 异常退出。
   先 try-catch 到局部变量 `flag`，再用 `flag == 1` 判断。

7. **(2026-09-30) PIN 对话框无全局冷却机制**（`SettingsForm.cs`）
   反复打开→失败 5 次→关闭→重开，可无限尝试。增加 `static cooldownUntil`，
   5 连败后全局 30 秒冷却，跨对话框实例生效。

8. **(2026-09-30) PIN 密码无长度上限**（`SettingsForm.cs`、`LockForm.cs`）
   仅校验 `>=4` 位，无上限。三个 PIN 输入框均增加 `MaxLength = 20`。

9. **(2026-09-30) 锁屏界面未拦截 Shift+Tab**（`LockForm.cs`）
   原代码只检查 `alt` 键拦截 Alt+Tab，Shift+Tab 可绕过。
   改为分别处理 `WM_SYSKEYDOWN`（Alt+Tab）和 `WM_KEYDOWN + shift`（Shift+Tab）。

10. **(2026-09-30) 钩子回调 P/Invoke 函数未声明**（`LockForm.cs`）
    原代码调用 `GetKeyState` 但无 `[DllImport]` 声明（编译必失败）。
    改用已声明的 `GetAsyncKeyState`（low-level hook 中更可靠，不依赖线程输入队列），
    删除多余的 `GetKeyState` 声明。

11. **(2026-09-30) 锁屏 PIN 框允许输入非数字字符**（`LockForm.cs`）
    答案框只接受数字，PIN 若包含字母则永远无法匹配。增加数字字符过滤。

### 未改项

- **锁屏期间 Timer 空转**（`App.cs`）：`OnTick` 首行 `lockForm != null` 即 `return`，开销可忽略。
  加 `Stop()/Start()` 反而有进程崩溃后 Timer 永不自启的风险，保留当前设计。

## 更新日志

### 2026-09-30 — v1.1.0 (重构与关键修复)

#### 🐛 Bug 修复（本次提交新增）

1. **(2026-09-30) StopEvent 未创建导致看门狗无法优雅退出** (`App.cs`, `Watchdog.cs`)
   - 主程序启动时创建命名事件 `Local\PCLock_Stop`，`Shutdown()` 时 `Set()` 通知看门狗退出
   - 看门狗 `OpenExisting` 监听，收到信号后清理资源并退出，避免卸载残留进程

2. **(2026-09-30) HKCU 回退降低防护等级** (`Store.cs`)
   - 移除注册表写入失败时回退 HKCU 的逻辑
   - 程序清单已声明 `requireAdministrator`，初始化失败直接抛异常，强制管理员权限运行

3. **(2026-09-30) DeductOffline 整数除法向下取整导致少扣离线时间** (`App.cs`)
   - 离线时长计算改用 `Math.Ceiling` 浮点除法，避免断电/关机期间时间折算偏少
   - 例如离线 59.9 秒原按 59 秒扣除，现向上取整为 60 秒

4. **(2026-09-30) 看门狗 Mutex 异常吞噬误判进程存活** (`Watchdog.cs`)
   - 仅捕获 `WaitHandleCannotBeOpenedException` 判定进程死亡
   - 其他异常（如 `UnauthorizedAccessException`）不再被视为进程存活

5. **(2026-09-30) 看门狗固定 Sleep 8s 改为轮询检测** (`Watchdog.cs`)
   - 主程序重启后每 500ms 检测 Mutex 是否出现，最多等待 8 秒
   - 响应更灵敏，避免慢机器启动超时或快机器空等

6. **(2026-09-30) ProtectSelf 硬编码 Everyone SID** (`Protection.cs`)
   - 改用 `SecurityIdentifier(WellKnownSidType.WorldSid, null)` 动态获取 S-1-1-0
   - 代码可读性更强，避免手写二进制 SID 出错

7. **(2026-09-30) LockForm 低级钩子资源泄漏风险** (`LockForm.cs`)
   - `hook = new LowLevelHook()` 移至构造函数末尾
   - 前面初始化失败时也能正确清理，避免全局钩子泄漏

8. **(2026-09-30) Store.cs 命名空间冲突** (`Store.cs`)
   - `using System.Security.Cryptography` 内置 `Constants` 类导致歧义
   - 添加别名 `using PCLockConstants = PCLock.Constants` 消除冲突

#### 🔧 重构与代码质量

9. **(2026-09-30) 新增 Constants.cs 集中管理所有常量** (`Constants.cs` 新增)
   - 39 个常量统一定义：注册表路径、Mutex 名称、时间阈值、PIN 策略、看门狗参数等
   - 所有源文件引用更新为 `Constants.xxx`，便于维护和测试

10. **(2026-09-30) build.bat / release.yml 同步新增 Constants.cs 编译引用**
    - 本地构建脚本与 GitHub Actions 工作流均已更新

#### 📝 文档与 CI

11. **(2026-09-30) 修复 GitHub Actions 编译缺失 Constants.cs** (`.github/workflows/release.yml`)
    - workflow 编译命令补全 `src\Constants.cs`，CI 现可通过

---

### 2026-09-30 — v1.0.x (原有修复记录)

#### Bug 修复

1. **(2026-09-30) 气泡提醒因 Timer 精度漂移可能跳过** (`App.cs`)
   原代码用 `remaining == 300` / `remaining == 60` 精确匹配，Timer 精度可跳过中间值。
   改为 `remaining <= 300` / `remaining <= 60` 范围判断 + `warned5` / `warned1` 标志位防重复，
   在 `Unlock()` 和 `ResetRemaining()` 中重置标志。

2. **(2026-09-30) 计划任务 `/TR` 参数多前导反斜杠** (`App.cs`)
   原代码 `schtasks /TR "\" + exe + "\\"` 生成了 `\\C:\...\PCLock.exe\\`，
   `schtasks` 不接受多前导 `\`。去掉多余转义，现在正确生成 `/TR "C:\Program Files\PCLock\PCLock.exe"`。

3. **(2026-09-30) UnprotectSelf 安全描述符结构错误** (`Protection.cs`)
   原代码构造 20 字节 SD 并设 `SE_DACL_PRESENT`，但 ACL 数据不存在，`Dacl` 偏移无效。
   改为 28 字节（20 header + 8 字节空 ACL），正确设置 Dacl 偏移=20、ACL_REVISION=2、ACE 数=0。

4. **(2026-09-30) 口算题可能凑不齐指定数量** (`MathUtil.cs`)
   `NextBatch(count)` 中 2000 次循环用尽后直接返回，可能少于 count 道。
   增加兜底 while 循环：不重复抽完后再允许重复，保证返回数量恒等于 count。

5. **(2026-09-30) 看门狗日志用相对路径** (`Watchdog.cs`)
   原代码 `File.WriteAllText("watchdog_log.txt", ...)` 写到当前工作目录。
   改为 `AppDomain.CurrentDomain.BaseDirectory + "watchdog_log.txt"`，确保写入 EXE 同目录。

6. **(2026-09-30) 看门狗 while 循环内 GetLockFlag 无异常保护** (`Watchdog.cs`)
   `Store.GetLockFlag()` 可能因注册表异常抛出，直接放在 `if` 条件里会导致 while 异常退出。
   先 try-catch 到局部变量 `flag`，再用 `flag == 1` 判断。

7. **(2026-09-30) PIN 对话框无全局冷却机制** (`SettingsForm.cs`)
   反复打开→失败 5 次→关闭→重开，可无限尝试。增加 `static cooldownUntil`，
   5 连败后全局 30 秒冷却，跨对话框实例生效。

8. **(2026-09-30) PIN 密码无长度上限** (`SettingsForm.cs`、`LockForm.cs`)
   仅校验 `>=4` 位，无上限。三个 PIN 输入框均增加 `MaxLength = 20`。

9. **(2026-09-30) 锁屏界面未拦截 Shift+Tab** (`LockForm.cs`)
   原代码只检查 `alt` 键拦截 Alt+Tab，Shift+Tab 可绕过。
   改为分别处理 `WM_SYSKEYDOWN`（Alt+Tab）和 `WM_KEYDOWN + shift`（Shift+Tab）。

10. **(2026-09-30) 钩子回调 P/Invoke 函数未声明** (`LockForm.cs`)
    原代码调用 `GetKeyState` 但无 `[DllImport]` 声明（编译必失败）。
    改用已声明的 `GetAsyncKeyState`（low-level hook 中更可靠，不依赖线程输入队列），
    删除多余的 `GetKeyState` 声明。

11. **(2026-09-30) 锁屏 PIN 框允许输入非数字字符** (`LockForm.cs`)
    答案框只接受数字，PIN 若包含字母则永远无法匹配。增加数字字符过滤。

### 未改项

- **锁屏期间 Timer 空转**（`App.cs`）：`OnTick` 首行 `lockForm != null` 即 `return`，开销可忽略。
  加 `Stop()/Start()` 反而有进程崩溃后 Timer 永不自启的风险，保留当前设计。

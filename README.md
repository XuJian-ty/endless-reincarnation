# 无尽轮回（网络版）

无尽轮回是一款使用 Unity 开发的第三人称 3D 动作肉鸽游戏，后端基于 ASP.NET Core 构建。项目包含本地动作战斗、成长存档、账号云存档、好友社交、聊天，以及好友协助的联机地牢玩法。

## 项目钩子

项目以“单局构筑、长期成长、好友入局”为核心体验。玩家在高节奏的第三人称战斗中组合近战、远程、技能和增益效果，通过随机关卡、装备掉落、商店选择和技能树形成每局不同的成长路线；需要协助时，可从好友系统发起援助并进入同一地牢会话，让单机肉鸽循环自然过渡到在线协作。

## 演示构建

- [观看游戏演示](https://github.com/XuJian-ty/endless-reincarnation/releases/latest/download/EndlessReincarnation-Demo.mp4)
- [下载 Android APK](https://github.com/XuJian-ty/endless-reincarnation/releases/latest/download/EndlessReincarnation-Android.apk)

APK 面向 Android 64 位设备，联机、账号和云存档功能需要网络连接。

## 核心特色

- 第三人称近战、远程战斗，包含技能、增益、装备、敌人 AI、首领流程、掉落、商店和检查点恢复。
- 支持任意数量的存档栏位，使用本地 JSON 保存数据，并可按账号同步远程存档。
- 社交平台后端提供账号注册、登录、好友申请、好友列表、聊天消息、在线状态摘要和当前存档上下文。
- 联机地牢后端负责好友助战、地牢实例创建、参与者校验、奖励裁定和实时状态同步。
- 公网客户端通过 WebSocket 隧道同步玩家状态、敌人状态、界面事件、场景加载、伤害事件和奖励快照，服务端保留 UDP/KCP 实时处理通道。
- Android 端提供横屏触控、移动端 HUD、按键重绑定、头像选择和大地图手势操作。

## 技术栈

- 客户端：Unity 2023.2.20f1c1、C#、URP、Input System、Newtonsoft.Json、kcp2k
- 后端：ASP.NET Core 8、EF Core
- 数据库：MySQL 8.4、Pomelo EF Core 数据库提供程序、EF Core 迁移
- 实时通信：HTTP API 负责会话生命周期，WebSocket 隧道承载公网消息，UDP/KCP 负责服务端实时通道

## 仓库结构

```text
Assets/                       Unity 客户端源码和资源
Backend/SocialBackend/        账号、社交、聊天和远程存档后端
Backend/OnlineDungeonServer/  联机地牢会话和实时同步后端
Docs/                         项目架构和联机功能文档
Packages/                     Unity 包清单
ProjectSettings/              Unity 项目设置
```

## 具体工作

- 核心战斗：实现角色状态机、近战与远程输入、技能时间轴、增益效果、敌人 AI、首领阶段、伤害反馈和掉落交互。
- 肉鸽循环：搭建局部关卡生成、检查点恢复、背包、商店、技能树、初始道具配置和多栏位存档流程。
- 场景表现：制作高地关卡环境生成工具，编写地表、草地、花朵、云层、河流和水面交互 Shader，完成导航烘焙和小地图显示。
- 跨端体验：统一键鼠与触控输入事件，完成 Android 横屏布局、虚拟摇杆、触控技能区、大地图缩放拖动、头像原生选择和一键 APK 构建工具。
- 全栈联机：开发账号、好友、聊天、云存档和援助流程，拆分平台后端与地牢服务器，接入参与者令牌、实时快照、可靠事件和服务端奖励裁定。
- 工程交付：配置 MySQL、EF Core、Docker Compose、后端自动启动、社交服务测试和 Git LFS 大资源管理。

## 技术挑战

- 公网实时链路：部署环境只开放 HTTPS 入口，客户端通过 `wss` 连接 `/api/realtime`，地牢服务器将 WebSocket 消息转入本机 UDP 处理管线，复用同一套实时协议和校验逻辑。
- 状态一致性：高频姿态快照与场景、伤害、奖励等可靠事件使用不同语义；协议通过序列号、事件 ID、确认快照和参与者令牌处理乱序、重复提交和越权请求。
- 多端输入冲突：键鼠、手柄式触控、UI 点击、地图拖动和双指缩放共享输入生命周期，通过输入事件层和 UI 桥接层隔离玩法输入，避免面板操作触发角色行为。
- 大场景性能：高密度草地、远景、河流和动态交互水面会同时增加渲染与资源压力；项目使用生成工具固化场景数据，以专用 Shader、移动端帧率策略和 Android Frame Pacing 控制运行开销。
- 存档边界：本地多栏位存档、账号云存档和联机临时奖励采用不同生命周期，联机掉落不写入单机关卡快照，结算结果由地牢服务裁定后再进入玩家持久数据。

## 后端服务

### 社交后端

默认开发地址：

```text
http://127.0.0.1:5076
```

主要接口：

- `/api/auth/register`
- `/api/auth/login`
- `/api/friends`
- `/api/messages/thread`
- `/api/saves`
- `/api/active-save`
- `/api/aid/request`

### 联机地牢服务器

默认开发地址：

```text
http://127.0.0.1:5086
```

主要职责：

- 创建、关闭地牢实例。
- 校验玩家身份和加入令牌。
- 保存活跃地牢会话的内存运行状态。
- 通过 UDP/KCP 同步实时游戏消息。
- 裁定通关奖励、宝箱开启、击杀奖励和掉落拾取结果。

## 本地开发

在仓库根目录双击或执行以下脚本，即可一次启动全部后端：

```powershell
.\start-all-backends.bat
```

使用 Unity 编辑器开发时，点击运行按钮也会自动检查并启动两个后端服务。随后可以直接进入登录界面进行测试。

使用 MySQL 启动完整容器环境：

```powershell
dotnet publish Backend\SocialBackend\SocialBackend.csproj -c Release -o Backend\SocialBackend\publish --no-restore
dotnet publish Backend\OnlineDungeonServer\OnlineDungeonServer.csproj -c Release -o Backend\OnlineDungeonServer\publish --no-restore
docker compose up --build
```

容器启动方式需要安装 Docker。Dockerfile 使用本地 `publish` 输出和 ASP.NET Core 运行时镜像，因此无需在容器内下载 .NET SDK。未使用 Docker 时，需要在 `Backend/SocialBackend/appsettings.Development.json` 中配置 `ConnectionStrings:SocialDatabase`，使其指向本地 MySQL 服务。

执行后端检查：

```powershell
dotnet build Backend\SocialBackend\SocialBackend.csproj --no-restore
dotnet build Backend\OnlineDungeonServer\OnlineDungeonServer.csproj --no-restore
dotnet test Backend\SocialBackend.Tests\SocialBackend.Tests.csproj --no-restore
dotnet tool run dotnet-ef migrations script --project Backend\SocialBackend\SocialBackend.csproj --startup-project Backend\SocialBackend\SocialBackend.csproj --idempotent --no-build
```

## 开发计划

- 扩充好友助战、地牢实例生命周期和实时消息协议的自动化测试。
- 为后端编译、数据库迁移和测试执行增加持续集成检查。
- 补充反向代理、HTTPS 和密钥管理等正式环境部署说明。

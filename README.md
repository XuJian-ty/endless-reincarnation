# 无尽轮回（网络版）

无尽轮回是一款使用 Unity 开发的第三人称 3D 动作肉鸽游戏，后端基于 ASP.NET Core 构建。项目包含本地动作战斗、成长存档、账号云存档、好友社交、聊天，以及好友协助的联机地牢玩法。

## 核心特色

- 第三人称近战、远程战斗，包含技能、增益、装备、敌人 AI、首领流程、掉落、商店和检查点恢复。
- 支持任意数量的存档栏位，使用本地 JSON 保存数据，并可按账号同步远程存档。
- 社交平台后端提供账号注册、登录、好友申请、好友列表、聊天消息、在线状态摘要和当前存档上下文。
- 联机地牢后端负责好友助战、地牢实例创建、参与者校验、奖励裁定和实时状态同步。
- 使用 UDP/KCP 实时同步玩家状态、敌人权威状态、界面事件、场景加载、伤害事件和奖励快照。

## 技术栈

- 客户端：Unity、C#、Newtonsoft.Json、kcp2k
- 后端：ASP.NET Core 8、EF Core
- 数据库：MySQL 8.4、Pomelo EF Core 数据库提供程序、EF Core 迁移
- 实时通信：HTTP API 负责会话生命周期，UDP/KCP 负责游戏状态同步

## 仓库结构

```text
Assets/                       Unity 客户端源码和资源
Backend/SocialBackend/        账号、社交、聊天和远程存档后端
Backend/OnlineDungeonServer/  联机地牢会话和实时同步后端
Docs/                         项目架构和联机功能文档
Packages/                     Unity 包清单
ProjectSettings/              Unity 项目设置
```

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

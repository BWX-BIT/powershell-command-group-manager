# ProcessMonitor

一个基于 WPF (.NET 9) 开发的进程管理工具，用于管理和监控多个命令行应用程序的启动、停止和重启操作。

## 功能特性

- 🚀 **进程管理**: 支持启动、停止、重启命令行应用程序
- 📋 **多命令组**: 支持管理多个独立的命令组
- 📝 **实时日志**: 实时显示进程输出日志
- 🔄 **优雅停止**: 采用分层优雅停止策略，优先保证数据完整性
- 💾 **配置持久化**: 命令组配置自动保存到 JSON 文件
- 🛡️ **线程安全**: 多进程并发日志输出安全
- 🖥️ **自动清理**: 软件退出时自动终止所有进程

## 技术栈

| 类别 | 技术 | 版本 |
|------|------|------|
| 框架 | WPF | .NET 9 |
| 语言 | C# | 12.0 |
| 序列化 | System.Text.Json | - |
| 进程管理 | System.Diagnostics.Process | - |
| 进程树查询 | System.Management | - |

## 使用方法

### 启动应用

运行 `ProcessMonitor.exe` 启动应用程序。

### 创建命令组

1. 点击「新建」按钮
2. 输入命令组名称
3. 配置启动命令（支持 `cd` 前缀设置工作目录）
4. 点击「保存」

### 启动进程

1. 在左侧列表中选择一个命令组
2. 点击「启动」按钮
3. 查看右侧输出窗口中的进程状态和日志

### 停止进程

1. 选择正在运行的命令组
2. 点击「停止」按钮

### 重启进程

1. 选择正在运行的命令组
2. 点击「重启」按钮

## 命令格式

启动命令支持以下格式：

```bash
# 直接执行命令
node app.js

# 设置工作目录后执行命令
cd C:\path\to\project
npm start

# 单行格式（用分号分隔）
cd C:\path\to\project; npm start
```

## 配置文件

命令组配置保存在 `command_groups.json` 文件中，格式如下：

```json
[
  {
    "Name": "后端服务",
    "StartCommand": "cd C:\\Users\\user\\backend\nuvicorn app.main:app --reload",
    "StopCommand": "",
    "RestartCommand": ""
  }
]
```

## 进程停止策略

采用四层优雅停止策略：

1. **第一层**: `CloseMainWindow()` - 模拟 Ctrl+C（最优雅）
2. **第二层**: `taskkill /T` - 发送控制台关闭信号
3. **第三层**: 自定义停止命令（如配置）
4. **第四层**: `taskkill /F /T` - 强制终止

## 项目结构

```
ProcessMonitor/
├── ProcessMonitor.csproj    # 项目配置文件
├── App.xaml                 # 应用程序入口
├── App.xaml.cs              # 应用程序逻辑
├── MainWindow.xaml          # 主窗口界面
├── MainWindow.xaml.cs       # 主窗口逻辑
├── CommandGroup.cs          # 命令组数据模型
├── ProcessManager.cs        # 进程管理核心
├── ConfigManager.cs         # 配置管理
└── command_groups.json      # 命令组配置文件
```

## 编译运行

```bash
# 编译
dotnet build

# 运行
dotnet run
```

## 许可证

MIT License
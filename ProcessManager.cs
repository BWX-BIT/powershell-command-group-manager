using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading.Tasks;

namespace ProcessMonitor
{
    public static class ProcessManager
    {
        public static event Action<CommandGroup, string> OutputReceived;
        public static event Action<CommandGroup> ProcessExited;

        public static void StartProcess(CommandGroup group, Action updateStatus)
        {
            if (group.Process != null && !group.Process.HasExited)
            {
                RaiseOutput(group, "已有进程在运行中\n");
                return;
            }

            // 清空上次的输出日志
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                group.Output = "";
            });

            try
            {
                string workingDir = ExtractWorkingDirectory(group.StartCommand);
                string actualCommand = ExtractCommand(group.StartCommand);

                if (string.IsNullOrWhiteSpace(actualCommand))
                {
                    RaiseOutput(group, "命令为空，无法启动进程\n");
                    return;
                }

                RaiseOutput(group, $"工作目录: {workingDir ?? "默认"}\n");
                RaiseOutput(group, $"执行命令: {actualCommand}\n");

                if (!string.IsNullOrEmpty(workingDir) && !Directory.Exists(workingDir))
                {
                    RaiseOutput(group, $"错误: 工作目录不存在: {workingDir}\n");
                    return;
                }

                // 启动 PowerShell 执行命令（不等待退出）
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoExit -Command \"{actualCommand}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                if (!string.IsNullOrEmpty(workingDir))
                {
                    psi.WorkingDirectory = workingDir;
                }

                var parentProcess = new Process { StartInfo = psi };
                parentProcess.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) RaiseOutput(group, $"输出: {e.Data}\n"); };
                parentProcess.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) RaiseOutput(group, $"错误: {e.Data}\n"); };

                parentProcess.Start();
                parentProcess.BeginOutputReadLine();
                parentProcess.BeginErrorReadLine();

                RaiseOutput(group, $"PowerShell 已启动，PID: {parentProcess.Id}\n");

                // 保存父进程引用
                group.ParentProcess = parentProcess;
                
                // 立即设置为运行状态，以便UI显示输出窗口
                group.IsRunning = true;
                updateStatus?.Invoke();

                // 启动定时器查找子进程
                System.Threading.Tasks.Task.Run(async () =>
                {
                    int childPid = 0;
                    for (int i = 0; i < 10; i++)  // 最多等待 5 秒
                    {
                        await System.Threading.Tasks.Task.Delay(500);
                        try
                        {
                            // 查找 PowerShell 的子进程
                            foreach (Process proc in Process.GetProcesses())
                            {
                                try
                                {
                                    if (GetParentProcessId(proc.Id) == parentProcess.Id)
                                    {
                                        childPid = proc.Id;
                                        break;
                                    }
                                }
                                catch { }
                            }
                            if (childPid > 0) break;
                        }
                        catch { }
                    }

                    // 更新 UI
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (childPid > 0)
                        {
                            try
                            {
                                group.Process = Process.GetProcessById(childPid);
                                RaiseOutput(group, $"检测到子进程，PID: {group.Process.Id}\n");
                                RaiseOutput(group, $"进程已启动\n");
                            }
                            catch
                            {
                                // 如果获取子进程失败，使用父进程
                                group.Process = parentProcess;
                                RaiseOutput(group, $"进程已启动，PID: {group.Process.Id}\n");
                            }
                        }
                        else
                        {
                            // 没有检测到子进程，使用父进程
                            group.Process = parentProcess;
                            RaiseOutput(group, $"进程已启动，PID: {group.Process.Id}\n");
                        }
                        group.IsRunning = true;
                        updateStatus?.Invoke();
                    });
                });

                // 监控父进程退出
                parentProcess.EnableRaisingEvents = true;
                parentProcess.Exited += (s, e) =>
                {
                    try
                    {
                        RaiseOutput(group, $"PowerShell 已退出，退出码: {parentProcess.ExitCode}\n");
                    }
                    catch { }
                    group.Process = null;
                    group.ParentProcess = null;
                    group.IsRunning = false;
                    ProcessExited?.Invoke(group);
                    updateStatus?.Invoke();
                };
            }
            catch (Exception ex)
            {
                RaiseOutput(group, $"启动进程失败: {ex.Message}\n");
                RaiseOutput(group, $"错误详情: {ex.StackTrace}\n");
            }
        }

        private static int GetParentProcessId(int processId)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {processId}"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return Convert.ToInt32(obj["ParentProcessId"]);
                    }
                }
            }
            catch { }
            return 0;
        }

        public static void ExecuteCommand(CommandGroup group, string command, string workingDirectory = null)
        {
            try
            {
                string combinedCommand = command.Replace("\r\n", ";").Replace("\n", ";");

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"{combinedCommand}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    psi.WorkingDirectory = workingDirectory;
                }

                Process process = new Process { StartInfo = psi };
                process.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) RaiseOutput(group, $"输出: {e.Data}\n"); };
                process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) RaiseOutput(group, $"错误: {e.Data}\n"); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                RaiseOutput(group, $"命令执行完成，退出码: {process.ExitCode}\n");
            }
            catch (Exception ex)
            {
                RaiseOutput(group, $"执行命令失败: {ex.Message}\n");
            }
        }

        public static async Task StopProcessAsync(CommandGroup group, Action updateStatus)
        {
            try
            {
                // 检查进程是否存在或已退出
                bool hasExited = true;
                try
                {
                    if (group.Process != null)
                    {
                        hasExited = group.Process.HasExited;
                    }
                }
                catch (InvalidOperationException)
                {
                    // 进程已被外部终止，进程对象无效
                    hasExited = true;
                }

                if (hasExited)
                {
                    RaiseOutput(group, "没有运行中的进程\n");
                    group.Process = null;
                    group.ParentProcess = null;
                    group.IsRunning = false;
                    updateStatus?.Invoke();
                    return;
                }

                bool gracefulStopped = await TryGracefulStopAsync(group);
                
                if (gracefulStopped)
                {
                    try
                    {
                        RaiseOutput(group, $"进程已优雅退出，退出码: {group.Process?.ExitCode ?? 0}\n");
                    }
                    catch
                    {
                        RaiseOutput(group, "进程已优雅退出\n");
                    }
                    group.Process = null;
                    group.ParentProcess = null;
                    group.IsRunning = false;
                    updateStatus?.Invoke();
                    return;
                }

                RaiseOutput(group, "进程未退出，强制终止...\n");
                KillProcess(group, updateStatus);
            }
            catch (Exception ex)
            {
                RaiseOutput(group, $"停止进程失败: {ex.Message}\n");
                // 确保清理状态
                group.Process = null;
                group.ParentProcess = null;
                group.IsRunning = false;
                updateStatus?.Invoke();
            }
        }

        public static async Task<bool> TryGracefulStopAsync(CommandGroup group)
        {
            // 检查进程是否存在或已退出
            bool hasExited = true;
            try
            {
                if (group.Process != null)
                {
                    hasExited = group.Process.HasExited;
                }
            }
            catch (InvalidOperationException)
            {
                // 进程已被外部终止
                hasExited = true;
            }

            if (hasExited)
            {
                return true;
            }

            // ========== 第一层：使用 CloseMainWindow() 模拟 Ctrl+C ==========
            RaiseOutput(group, "【第一层】尝试使用 CloseMainWindow() 模拟 Ctrl+C...\n");
            if (TryCloseMainWindow(group))
            {
                // 等待 5 秒让服务处理完请求
                if (await WaitForProcessExitAsync(group.Process, 5000))
                {
                    RaiseOutput(group, "进程已优雅退出（CloseMainWindow）\n");
                    return true;
                }
            }

            // ========== 第二层：使用 taskkill /T 发送控制台关闭信号 ==========
            RaiseOutput(group, "【第二层】尝试使用 taskkill /T 发送控制台关闭信号...\n");
            
            int processId = 0;
            try
            {
                processId = group.Process?.Id ?? 0;
            }
            catch (InvalidOperationException)
            {
                // 进程已被外部终止
                return true;
            }

            if (processId > 0 && await TryTaskKillWithSignalAsync(processId))
            {
                // 等待 3 秒让进程优雅退出
                if (await WaitForProcessExitAsync(group.Process, 3000))
                {
                    RaiseOutput(group, "进程已优雅退出（taskkill /T）\n");
                    return true;
                }
            }

            // ========== 第三层：执行自定义停止命令（如果有） ==========
            if (!string.IsNullOrEmpty(group.StopCommand))
            {
                string stopCommand = group.StopCommand.Trim();
                if (!stopCommand.Equals("Ctrl+C", StringComparison.OrdinalIgnoreCase))
                {
                    RaiseOutput(group, "执行自定义停止命令...\n");
                    ExecuteCommand(group, group.StopCommand);

                    // 等待 5 秒
                    if (await WaitForProcessExitAsync(group.Process, 5000))
                    {
                        RaiseOutput(group, "进程已优雅退出（自定义命令）\n");
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 尝试使用 CloseMainWindow() 模拟 Ctrl+C（最优雅）
        /// CloseMainWindow() 向控制台程序发送正常关闭通知，等价于 Ctrl+C
        /// </summary>
        private static bool TryCloseMainWindow(CommandGroup group)
        {
            try
            {
                // 检查进程是否存在或已退出
                bool hasExited = true;
                try
                {
                    if (group.Process != null)
                    {
                        hasExited = group.Process.HasExited;
                    }
                }
                catch (InvalidOperationException)
                {
                    // 进程已被外部终止
                    return true;
                }

                if (hasExited)
                    return true;

                // CloseMainWindow() 对控制台程序有效
                return group.Process.CloseMainWindow();
            }
            catch (Exception ex)
            {
                RaiseOutput(group, $"CloseMainWindow 失败: {ex.Message}\n");
                return false;
            }
        }

        /// <summary>
        /// 等待进程退出（异步）
        /// </summary>
        private static async Task<bool> WaitForProcessExitAsync(Process process, int timeoutMs)
        {
            try
            {
                for (int i = 0; i < timeoutMs / 100; i++)
                {
                    if (process.HasExited)
                        return true;

                    await Task.Delay(100);
                }
                return process.HasExited;
            }
            catch
            {
                return true; // 进程已不存在
            }
        }

        /// <summary>
        /// 使用 taskkill /T 发送控制台关闭信号（比 /PID 更优雅）
        /// /T 会向进程及其子进程发送控制台关闭信号，等同于 Ctrl+C
        /// </summary>
        private static async Task<bool> TryTaskKillWithSignalAsync(int processId)
        {
            try
            {
                using (var taskkill = Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = $"/T /PID {processId}",  // 注意：使用 /T 而不是不带参数
                    CreateNoWindow = true,
                    UseShellExecute = false
                }))
                {
                    taskkill?.WaitForExit();
                }
                return true;
            }
            catch (Exception ex)
            {
                RaiseOutput(null!, $"taskkill /T 失败: {ex.Message}\n");
                return false;
            }
        }

        /// <summary>
        /// 使用 taskkill /PID（非强制）尝试优雅停止
        /// 注意：这个方法已废弃，推荐使用 TryTaskKillWithSignalAsync
        /// </summary>
        [Obsolete("推荐使用 TryTaskKillWithSignalAsync")]
        public static async Task<bool> TryTaskKillGracefulAsync(int processId)
        {
            return await TryTaskKillWithSignalAsync(processId);
        }

        public static void KillProcess(CommandGroup group, Action updateStatus)
        {
            try
            {
                // 先终止子进程（如果存在且与父进程不同）
                int childProcessId = 0;
                int parentProcessId = 0;
                
                try
                {
                    childProcessId = group.Process?.Id ?? 0;
                }
                catch (InvalidOperationException)
                {
                    // 子进程已被外部终止
                    childProcessId = 0;
                }

                try
                {
                    parentProcessId = group.ParentProcess?.Id ?? 0;
                }
                catch (InvalidOperationException)
                {
                    // 父进程已被外部终止
                    parentProcessId = 0;
                }

                // 终止子进程
                if (childProcessId > 0 && childProcessId != parentProcessId)
                {
                    try
                    {
                        var childProcess = Process.GetProcessById(childProcessId);
                        if (!childProcess.HasExited)
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "taskkill.exe",
                                Arguments = $"/F /T /PID {childProcessId}",
                                CreateNoWindow = true,
                                UseShellExecute = false
                            }).WaitForExit();

                            RaiseOutput(group, $"进程 {childProcessId} 及其子进程已终止\n");
                        }
                    }
                    catch (ArgumentException)
                    {
                        // 进程不存在，已被外部终止
                        RaiseOutput(group, $"子进程 {childProcessId} 已被外部终止\n");
                    }
                }

                // 终止父进程（PowerShell）
                if (parentProcessId > 0)
                {
                    try
                    {
                        var parentProcess = Process.GetProcessById(parentProcessId);
                        if (!parentProcess.HasExited)
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "taskkill.exe",
                                Arguments = $"/F /T /PID {parentProcessId}",
                                CreateNoWindow = true,
                                UseShellExecute = false
                            }).WaitForExit();

                            RaiseOutput(group, $"PowerShell 进程 {parentProcessId} 已终止\n");
                        }
                    }
                    catch (ArgumentException)
                    {
                        // 进程不存在，已被外部终止
                        RaiseOutput(group, $"PowerShell 进程 {parentProcessId} 已被外部终止\n");
                    }
                }
                else if (childProcessId > 0)
                {
                    // 如果没有父进程引用，终止当前进程
                    try
                    {
                        var process = Process.GetProcessById(childProcessId);
                        if (!process.HasExited)
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "taskkill.exe",
                                Arguments = $"/F /T /PID {childProcessId}",
                                CreateNoWindow = true,
                                UseShellExecute = false
                            }).WaitForExit();

                            RaiseOutput(group, $"进程 {childProcessId} 及其子进程已终止\n");
                        }
                    }
                    catch (ArgumentException)
                    {
                        // 进程不存在，已被外部终止
                        RaiseOutput(group, $"进程 {childProcessId} 已被外部终止\n");
                    }
                }

                group.Process = null;
                group.ParentProcess = null;
                group.IsRunning = false;
                updateStatus?.Invoke();
            }
            catch (Exception ex)
            {
                RaiseOutput(group, $"终止进程失败: {ex.Message}\n");
                // 确保清理状态
                group.Process = null;
                group.ParentProcess = null;
                group.IsRunning = false;
                updateStatus?.Invoke();
            }
        }

        public static string ExtractWorkingDirectory(string command)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(command))
                    return null;

                string[] lines = command.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0)
                    return null;

                string firstLine = lines[0].Trim();
                
                if (firstLine.StartsWith("cd "))
                {
                    return firstLine.Substring(3).Trim();
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public static string ExtractCommand(string command)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(command))
                    return "";

                string[] lines = command.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                
                if (lines.Length == 0)
                    return "";
                
                if (lines[0].Trim().StartsWith("cd "))
                {
                    return string.Join(";", lines.Skip(1));
                }
                
                return string.Join(";", lines);
            }
            catch
            {
                return "";
            }
        }

        private static void RaiseOutput(CommandGroup group, string text)
        {
            OutputReceived?.Invoke(group, text);
        }
    }
}
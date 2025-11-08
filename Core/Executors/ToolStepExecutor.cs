using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FlashWorkflowFramework.Core.Models;
using FlashWorkflowFramework.Core.Utils;

namespace FlashWorkflowFramework.Core.Executors
{
    public class ToolStepExecutor : IStepExecutor
    {
        public string StepType => "tool";
        
        public async Task<StepResult> ExecuteAsync(WorkflowStep step, FlashWorkflowFramework.Core.Models.ExecutionContext context)
        {
            var result = new StepResult();
            
            try
            {
                // 获取所有命令参数
                var commands = step.Parameters.Where(p => p.Key.Equals("Command", StringComparison.OrdinalIgnoreCase))
                                          .Select(p => p.Value)
                                          .ToList();
                
                if (commands.Count == 0)
                {
                    throw new ArgumentException("tool步骤必须指定至少一个Command参数");
                }
                
                var output = new StringBuilder();
                var hasInteractiveCommand = false;
                
                foreach (var command in commands)
                {
                    var resolvedCommand = context.Variables.Resolve(command);
                    ConsoleLogger.Info($"执行命令: {resolvedCommand}", step.Name);
                    
                    // 检查是否是交互式命令
                    if (IsInteractiveCommand(resolvedCommand))
                    {
                        hasInteractiveCommand = true;
                        ConsoleLogger.Info($"检测到交互式命令，等待用户确认继续...", step.Name);
                        ConsoleLogger.Info($"命令内容: {resolvedCommand}", step.Name);
                        ConsoleLogger.Info($"按任意键继续执行后续命令...", step.Name);
                        
                        // 使用异步方式等待用户按键，避免阻塞
                        await Task.Run(() => Console.ReadKey());
                        continue;
                    }
                    
                    // 在Windows上执行命令
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {resolvedCommand}",
                        WorkingDirectory = context.SessionDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    
                    // 如果命令是相对路径，确保它在会话目录中执行
                    if (!resolvedCommand.Contains(":") && !resolvedCommand.StartsWith("\""))
                    {
                        // 对于相对路径命令，使用完整路径
                        processInfo.Arguments = $"/c cd /d \"{context.SessionDir}\" && {resolvedCommand}";
                        
                        // 如果路径包含正斜杠，将其转换为反斜杠（Windows路径格式）
                        if (resolvedCommand.Contains("/"))
                        {
                            resolvedCommand = resolvedCommand.Replace("/", "\\");
                            processInfo.Arguments = $"/c cd /d \"{context.SessionDir}\" && {resolvedCommand}";
                        }
                    }
                    
                    var process = Process.Start(processInfo);
                    
                    // 使用异步方式读取输出，避免长时间运行命令的阻塞问题
                    var outputBuilder = new StringBuilder();
                    var errorBuilder = new StringBuilder();
                    
                    // 设置异步读取输出和错误
                    process.OutputDataReceived += (sender, e) => {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            outputBuilder.AppendLine(e.Data);
                            ConsoleLogger.Info($"[CMD] {e.Data}", step.Name);
                        }
                    };
                    
                    process.ErrorDataReceived += (sender, e) => {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            errorBuilder.AppendLine(e.Data);
                            ConsoleLogger.Warning($"[CMD-ERROR] {e.Data}", step.Name);
                        }
                    };
                    
                    // 开始异步读取
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    
                    // 等待进程退出，设置超时时间为30分钟（适合刷机操作）
                    bool exited = process.WaitForExit(30 * 60 * 1000); // 30分钟超时
                    
                    if (!exited)
                    {
                        // 如果超时，尝试终止进程
                        try
                        {
                            process.Kill();
                            process.WaitForExit(5000); // 等待5秒确保进程完全退出
                        }
                        catch { }
                        
                        result.Success = false;
                        result.ExitCode = -1;
                        result.Output = outputBuilder.ToString();
                        ConsoleLogger.Warning($"命令执行超时: {resolvedCommand}", step.Name);
                        return result;
                    }
                    
                    // 获取最终输出
                    string outputData = outputBuilder.ToString();
                    string errorData = errorBuilder.ToString();
                    
                    // 如果有错误输出且退出代码非0，记录错误
                    if (process.ExitCode != 0)
                    {
                        result.Success = false;
                        result.ExitCode = process.ExitCode;
                        result.Output = outputData;
                        ConsoleLogger.Warning($"命令执行失败: {resolvedCommand}，退出代码: {process.ExitCode}", step.Name);
                        return result;
                    }
                }
                
                result.Success = true;
                result.ExitCode = 0;
                result.Output = output.ToString();
                result.InteractiveCommandWaiting = hasInteractiveCommand;
                ConsoleLogger.Success($"命令执行完成", step.Name);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex;
                ConsoleLogger.Error($"命令执行异常: {ex.Message}", step.Name);
            }
            
            return result;
        }
        
        private bool IsInteractiveCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
                return false;
                
            var lowerCommand = command.ToLowerInvariant().Trim();
            
            // 检查常见的交互式命令
            return lowerCommand.Equals("pause") ||
                   lowerCommand.StartsWith("pause ") ||
                   lowerCommand.Contains("choice") ||
                   lowerCommand.Contains("read") ||
                   lowerCommand.Contains("input");
        }
    }
}
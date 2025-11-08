using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlashWorkflowFramework.Core.Executors;
using FlashWorkflowFramework.Core.Models;
using FlashWorkflowFramework.Core.Utils;

namespace FlashWorkflowFramework.Core
{
    public class WorkflowExecutionEngine
    {
        private readonly string _baseTempPath;
        private readonly Dictionary<string, IStepExecutor> _executors;
        
        public WorkflowExecutionEngine(string baseTempPath = @"C:\FWF\temp")
        {
            _baseTempPath = baseTempPath;
            _executors = new Dictionary<string, IStepExecutor>();
            
            // 注册所有执行器
            RegisterExecutors();
        }
        
        private void RegisterExecutors()
        {
            var executorTypes = new[]
            {
                typeof(ToolStepExecutor),
                typeof(DownloadStepExecutor),
                typeof(FileOperationStepExecutor)
            };
            
            foreach (var executorType in executorTypes)
            {
                var executor = (IStepExecutor)Activator.CreateInstance(executorType);
                _executors[executor.StepType] = executor;
            }
        }
        
        public async Task<ExecutionResult> ExecuteFromZipAsync(string zipFilePath, ExecutionOptions options)
        {
            var result = new ExecutionResult();
            
            // 1. 创建会话目录
            var sessionDir = CreateSessionDirectory();
            ConsoleLogger.Info($"创建会话目录: {sessionDir}");
            
            try
            {
                // 2. 解压ZIP文件
                ConsoleLogger.Info($"解压ZIP文件: {zipFilePath}");
                await ZipExtractor.ExtractAsync(zipFilePath, sessionDir);
                
                // 3. 查找并解析工作流文件
                var workflowPath = Path.Combine(sessionDir, options.WorkflowFileName);
                if (!File.Exists(workflowPath))
                {
                    throw new FileNotFoundException($"工作流文件未找到: {workflowPath}");
                }
                
                ConsoleLogger.Info($"解析工作流文件: {workflowPath}");
                var workflow = await WorkflowParser.ParseAsync(workflowPath);
                
                // 4. 准备执行上下文
                var context = new FlashWorkflowFramework.Core.Models.ExecutionContext
                {
                    SessionDir = sessionDir,
                    OutputDir = options.OutputPath
                };
                
                // 设置内置变量
                context.Variables.SetVariable("SessionDir", sessionDir);
                context.Variables.SetVariable("OutputDir", options.OutputPath);
                context.Variables.SetVariable("TempDir", options.TempPath);
                
                // 设置工作流变量
                foreach (var variable in workflow.Variables)
                {
                    context.Variables.SetVariable(variable.Key, variable.Value);
                }
                
                // 设置命令行变量（覆盖工作流变量）
                foreach (var variable in options.Variables)
                {
                    context.Variables.SetVariable(variable.Key, variable.Value);
                }
                
                // 5. 确保输出目录存在
                if (!Directory.Exists(options.OutputPath))
                {
                    Directory.CreateDirectory(options.OutputPath);
                }
                
                // 6. 执行工作流
                result = await ExecuteWorkflowAsync(workflow, context, options);
            }
            finally
            {
                // 7. 清理临时文件
                // 只有在明确指定保留临时文件时才保留，否则总是清理（包括执行失败的情况）
                if (!options.KeepTempFiles)
                {
                    CleanupSession(sessionDir);
                }
            }
            
            return result;
        }
        
        private async Task<ExecutionResult> ExecuteWorkflowAsync(Workflow workflow, FlashWorkflowFramework.Core.Models.ExecutionContext context, ExecutionOptions options)
        {
            var result = new ExecutionResult();
            
            ConsoleLogger.Info($"开始执行工作流: {workflow.Name}");
            
            foreach (var step in workflow.Steps)
            {
                // 检查条件
                if (!string.IsNullOrEmpty(step.Condition))
                {
                    var conditionResult = EvaluateCondition(step.Condition, context);
                    if (!conditionResult)
                    {
                        ConsoleLogger.Info($"跳过步骤: {step.Name} (条件不满足)");
                        continue;
                    }
                }
                
                ConsoleLogger.Info($"执行步骤: {step.Name} ({step.Type})");
                
                // 查找执行器
                if (!_executors.TryGetValue(step.Type, out var executor))
                {
                    var error = new InvalidOperationException($"不支持的步骤类型: {step.Type}");
                    result.StepResults[step.Name] = new StepResult
                    {
                        Success = false,
                        Error = error
                    };
                    ConsoleLogger.Error(error.Message, step.Name);
                    result.Success = false;
                    result.ErrorMessage = error.Message;
                    return result;
                }
                
                // 执行步骤
                var stepResult = await executor.ExecuteAsync(step, context);
                result.StepResults[step.Name] = stepResult;
                
                // 如果步骤有交互式命令等待，更新结果状态
                if (stepResult.InteractiveCommandWaiting)
                {
                    result.InteractiveCommandWaiting = true;
                }
                
                if (!stepResult.Success)
                {
                    ConsoleLogger.Error($"步骤执行失败: {step.Name}", step.Name);
                    result.Success = false;
                    result.ErrorMessage = $"步骤 {step.Name} 执行失败: {stepResult.Error?.Message}";
                    return result;
                }
            }
            
            result.Success = true;
            ConsoleLogger.Success($"工作流执行完成: {workflow.Name}");
            return result;
        }
        
        private bool EvaluateCondition(string condition, FlashWorkflowFramework.Core.Models.ExecutionContext context)
        {
            // 简单的条件评估，支持基本的比较操作
            // 格式: $(VariableName) operator value
            // 支持的操作符: ==, !=, >, <, >=, <=
            
            // 解析条件
            var operators = new[] { ">=", "<=", "!=", "==", ">", "<" };
            string op = null;
            int opIndex = -1;
            
            foreach (var o in operators)
            {
                opIndex = condition.IndexOf(o);
                if (opIndex >= 0)
                {
                    op = o;
                    break;
                }
            }
            
            if (op == null)
            {
                // 没有操作符，检查变量是否存在且非空
                var varValue = context.Variables.Resolve(condition);
                return !string.IsNullOrEmpty(varValue);
            }
            
            var leftPart = condition.Substring(0, opIndex).Trim();
            var rightPart = condition.Substring(opIndex + op.Length).Trim();
            
            var leftValue = context.Variables.Resolve(leftPart);
            var rightValue = context.Variables.Resolve(rightPart);
            
            // 尝试转换为数字进行比较
            if (double.TryParse(leftValue, out var leftNum) && double.TryParse(rightValue, out var rightNum))
            {
                return op switch
                {
                    "==" => leftNum == rightNum,
                    "!=" => leftNum != rightNum,
                    ">" => leftNum > rightNum,
                    "<" => leftNum < rightNum,
                    ">=" => leftNum >= rightNum,
                    "<=" => leftNum <= rightNum,
                    _ => false
                };
            }
            
            // 字符串比较
            return op switch
            {
                "==" => string.Equals(leftValue, rightValue, StringComparison.OrdinalIgnoreCase),
                "!=" => !string.Equals(leftValue, rightValue, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
        
        private string CreateSessionDirectory()
        {
            var sessionDir = Path.Combine(_baseTempPath, $"session_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(sessionDir);
            return sessionDir;
        }
        
        private void CleanupSession(string sessionDir)
        {
            try
            {
                if (Directory.Exists(sessionDir))
                {
                    Directory.Delete(sessionDir, true);
                    ConsoleLogger.Info($"已清理临时目录: {sessionDir}");
                }
            }
            catch (Exception ex)
            {
                ConsoleLogger.Warning($"清理临时目录失败: {ex.Message}");
            }
        }
    }
}
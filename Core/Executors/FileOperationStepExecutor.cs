using System;
using System.IO;
using System.Threading.Tasks;
using FlashWorkflowFramework.Core.Models;
using FlashWorkflowFramework.Core.Utils;

namespace FlashWorkflowFramework.Core.Executors
{
    public class FileOperationStepExecutor : IStepExecutor
    {
        public string StepType => "FileOperation";
        
        public async Task<StepResult> ExecuteAsync(WorkflowStep step, FlashWorkflowFramework.Core.Models.ExecutionContext context)
        {
            var result = new StepResult();
            
            try
            {
                var operation = step.Parameters.GetValueOrDefault("Operation");
                if (string.IsNullOrEmpty(operation))
                {
                    throw new ArgumentException("FileOperation步骤必须指定Operation参数");
                }
                
                var source = context.Variables.Resolve(step.Parameters.GetValueOrDefault("Source"));
                var destination = context.Variables.Resolve(step.Parameters.GetValueOrDefault("Destination"));
                
                ConsoleLogger.Info($"执行文件操作: {operation}", step.Name);
                
                switch (operation)
                {
                    case "CopyFile":
                        await CopyFileAsync(source, destination, step.Name);
                        break;
                        
                    case "MoveFile":
                        await MoveFileAsync(source, destination, step.Name);
                        break;
                        
                    case "DeleteFile":
                        await DeleteFileAsync(source, step.Name);
                        break;
                        
                    case "CopyDirectory":
                        await CopyDirectoryAsync(source, destination, step.Name);
                        break;
                        
                    case "MoveDirectory":
                        await MoveDirectoryAsync(source, destination, step.Name);
                        break;
                        
                    case "DeleteDirectory":
                        await DeleteDirectoryAsync(source, step.Name);
                        break;
                        
                    case "CreateDirectory":
                        await CreateDirectoryAsync(source, step.Name);
                        break;
                        
                    default:
                        throw new ArgumentException($"不支持的文件操作: {operation}");
                }
                
                ConsoleLogger.Success($"文件操作完成: {operation}", step.Name);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex;
                ConsoleLogger.Error($"文件操作异常: {ex.Message}", step.Name);
            }
            
            return result;
        }
        
        private async Task CopyFileAsync(string source, string destination, string stepName)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(destination))
            {
                throw new ArgumentException("CopyFile操作必须指定Source和Destination参数");
            }
            
            if (!File.Exists(source))
            {
                throw new FileNotFoundException($"源文件不存在: {source}");
            }
            
            // 确保目标目录存在
            var destinationDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }
            
            ConsoleLogger.Info($"复制文件: {Path.GetFileName(source)} -> {destination}", stepName);
            
            using var sourceStream = File.OpenRead(source);
            using var destinationStream = File.Create(destination);
            await sourceStream.CopyToAsync(destinationStream);
        }
        
        private async Task MoveFileAsync(string source, string destination, string stepName)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(destination))
            {
                throw new ArgumentException("MoveFile操作必须指定Source和Destination参数");
            }
            
            if (!File.Exists(source))
            {
                throw new FileNotFoundException($"源文件不存在: {source}");
            }
            
            // 确保目标目录存在
            var destinationDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }
            
            ConsoleLogger.Info($"移动文件: {Path.GetFileName(source)} -> {destination}", stepName);
            
            await Task.Run(() => File.Move(source, destination));
        }
        
        private async Task DeleteFileAsync(string source, string stepName)
        {
            if (string.IsNullOrEmpty(source))
            {
                throw new ArgumentException("DeleteFile操作必须指定Source参数");
            }
            
            if (!File.Exists(source))
            {
                ConsoleLogger.Warning($"文件不存在，跳过删除: {source}", stepName);
                return;
            }
            
            ConsoleLogger.Info($"删除文件: {Path.GetFileName(source)}", stepName);
            
            await Task.Run(() => File.Delete(source));
        }
        
        private async Task CopyDirectoryAsync(string source, string destination, string stepName)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(destination))
            {
                throw new ArgumentException("CopyDirectory操作必须指定Source和Destination参数");
            }
            
            if (!Directory.Exists(source))
            {
                throw new DirectoryNotFoundException($"源目录不存在: {source}");
            }
            
            ConsoleLogger.Info($"复制目录: {Path.GetFileName(source)} -> {destination}", stepName);
            
            await Task.Run(() =>
            {
                // 如果目标目录已存在，先删除
                if (Directory.Exists(destination))
                {
                    Directory.Delete(destination, true);
                }
                
                // 创建目标目录
                Directory.CreateDirectory(destination);
                
                // 复制所有文件和子目录
                foreach (var file in Directory.GetFiles(source))
                {
                    var fileName = Path.GetFileName(file);
                    var destFile = Path.Combine(destination, fileName);
                    File.Copy(file, destFile, true);
                }
                
                foreach (var dir in Directory.GetDirectories(source))
                {
                    var dirName = Path.GetFileName(dir);
                    var destDir = Path.Combine(destination, dirName);
                    CopyDirectoryRecursive(dir, destDir);
                }
            });
        }
        
        private void CopyDirectoryRecursive(string source, string destination)
        {
            if (!Directory.Exists(destination))
            {
                Directory.CreateDirectory(destination);
            }
            
            foreach (var file in Directory.GetFiles(source))
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(destination, fileName);
                File.Copy(file, destFile, true);
            }
            
            foreach (var dir in Directory.GetDirectories(source))
            {
                var dirName = Path.GetFileName(dir);
                var destDir = Path.Combine(destination, dirName);
                CopyDirectoryRecursive(dir, destDir);
            }
        }
        
        private async Task MoveDirectoryAsync(string source, string destination, string stepName)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(destination))
            {
                throw new ArgumentException("MoveDirectory操作必须指定Source和Destination参数");
            }
            
            if (!Directory.Exists(source))
            {
                throw new DirectoryNotFoundException($"源目录不存在: {source}");
            }
            
            ConsoleLogger.Info($"移动目录: {Path.GetFileName(source)} -> {destination}", stepName);
            
            await Task.Run(() => Directory.Move(source, destination));
        }
        
        private async Task DeleteDirectoryAsync(string source, string stepName)
        {
            if (string.IsNullOrEmpty(source))
            {
                throw new ArgumentException("DeleteDirectory操作必须指定Source参数");
            }
            
            if (!Directory.Exists(source))
            {
                ConsoleLogger.Warning($"目录不存在，跳过删除: {source}", stepName);
                return;
            }
            
            ConsoleLogger.Info($"删除目录: {Path.GetFileName(source)}", stepName);
            
            await Task.Run(() => Directory.Delete(source, true));
        }
        
        private async Task CreateDirectoryAsync(string source, string stepName)
        {
            if (string.IsNullOrEmpty(source))
            {
                throw new ArgumentException("CreateDirectory操作必须指定Source参数");
            }
            
            if (Directory.Exists(source))
            {
                ConsoleLogger.Warning($"目录已存在，跳过创建: {source}", stepName);
                return;
            }
            
            ConsoleLogger.Info($"创建目录: {source}", stepName);
            
            await Task.Run(() => Directory.CreateDirectory(source));
        }
    }
}
using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FlashWorkflowFramework.Core.Models;
using FlashWorkflowFramework.Core.Utils;

namespace FlashWorkflowFramework.Core.Executors
{
    public class DownloadStepExecutor : IStepExecutor
    {
        public string StepType => "Download";
        
        public async Task<StepResult> ExecuteAsync(WorkflowStep step, FlashWorkflowFramework.Core.Models.ExecutionContext context)
        {
            var result = new StepResult();
            
            try
            {
                var url = context.Variables.Resolve(step.Parameters.GetValueOrDefault("Url"));
                var outputPath = context.Variables.Resolve(step.Parameters.GetValueOrDefault("Output"));
                var checksum = step.Parameters.GetValueOrDefault("Checksum");
                var retryCount = int.Parse(step.Parameters.GetValueOrDefault("RetryCount", "3"));
                
                if (string.IsNullOrEmpty(url))
                {
                    throw new ArgumentException("Download步骤必须指定Url参数");
                }
                
                if (string.IsNullOrEmpty(outputPath))
                {
                    throw new ArgumentException("Download步骤必须指定Output参数");
                }
                
                // 确保输出目录存在
                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }
                
                ConsoleLogger.Info($"开始下载: {url}", step.Name);
                ConsoleLogger.Info($"输出路径: {outputPath}", step.Name);
                
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(10);
                
                Exception lastError = null;
                for (int attempt = 1; attempt <= retryCount; attempt++)
                {
                    try
                    {
                        if (attempt > 1)
                        {
                            ConsoleLogger.Info($"重试下载 ({attempt}/{retryCount})", step.Name);
                        }
                        
                        await DownloadFileAsync(httpClient, url, outputPath, step.Name);
                        
                        // 验证校验和
                        if (!string.IsNullOrEmpty(checksum))
                        {
                            await VerifyChecksumAsync(outputPath, checksum, step.Name);
                        }
                        
                        ConsoleLogger.Success($"下载完成: {Path.GetFileName(outputPath)}", step.Name);
                        result.Success = true;
                        return result;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        ConsoleLogger.Warning($"下载失败 (尝试 {attempt}/{retryCount}): {ex.Message}", step.Name);
                        
                        if (attempt < retryCount)
                        {
                            await Task.Delay(2000 * attempt); // 指数退避
                        }
                    }
                }
                
                result.Success = false;
                result.Error = lastError;
                ConsoleLogger.Error($"下载失败，已达到最大重试次数: {lastError?.Message}", step.Name);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex;
                ConsoleLogger.Error($"下载异常: {ex.Message}", step.Name);
            }
            
            return result;
        }
        
        private async Task DownloadFileAsync(HttpClient client, string url, string outputPath, string stepName)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            
            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes > 0;
            
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            
            var buffer = new byte[8192];
            var totalRead = 0L;
            int read;
            
            while ((read = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                totalRead += read;
                
                if (canReportProgress && totalRead % (1024 * 1024) == 0) // 每MB报告一次进度
                {
                    var progress = (int)((totalRead * 100) / totalBytes);
                    ConsoleLogger.Progress(stepName, progress, $"已下载: {totalRead / (1024 * 1024)}MB");
                }
            }
        }
        
        private async Task VerifyChecksumAsync(string filePath, string checksum, string stepName)
        {
            if (string.IsNullOrEmpty(checksum))
                return;
            
            var parts = checksum.Split(':');
            if (parts.Length != 2)
            {
                throw new ArgumentException($"无效的校验和格式: {checksum}，应为 Type:Value");
            }
            
            var algorithm = parts[0].ToUpperInvariant();
            var expectedHash = parts[1].ToLowerInvariant();
            
            ConsoleLogger.Info($"验证校验和 ({algorithm}): {expectedHash}", stepName);
            
            using var stream = File.OpenRead(filePath);
            byte[] hashBytes;
            
            switch (algorithm)
            {
                case "MD5":
                    using (var md5 = MD5.Create())
                    {
                        hashBytes = await md5.ComputeHashAsync(stream);
                    }
                    break;
                case "SHA1":
                    using (var sha1 = SHA1.Create())
                    {
                        hashBytes = await sha1.ComputeHashAsync(stream);
                    }
                    break;
                case "SHA256":
                    using (var sha256 = SHA256.Create())
                    {
                        hashBytes = await sha256.ComputeHashAsync(stream);
                    }
                    break;
                default:
                    throw new ArgumentException($"不支持的校验和算法: {algorithm}");
            }
            
            var actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            
            if (actualHash != expectedHash)
            {
                throw new InvalidOperationException($"校验和不匹配，期望: {expectedHash}，实际: {actualHash}");
            }
            
            ConsoleLogger.Success($"校验和验证通过", stepName);
        }
    }
}
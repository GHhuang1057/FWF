using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using FlashWorkflowFramework.Core.Utils;

namespace FlashWorkflowFramework.Core.Utils
{
    public static class ZipExtractor
    {
        public static async Task ExtractAsync(string zipPath, string extractDir, string stepName = "ZIP解压")
        {
            if (!File.Exists(zipPath))
            {
                throw new FileNotFoundException($"ZIP文件不存在: {zipPath}");
            }
            
            // 确保目标目录存在
            if (!Directory.Exists(extractDir))
            {
                Directory.CreateDirectory(extractDir);
            }
            
            ConsoleLogger.Info($"开始解压ZIP文件: {Path.GetFileName(zipPath)}", stepName);
            
            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var totalEntries = archive.Entries.Count;
                var current = 0;
                
                foreach (var entry in archive.Entries)
                {
                    current++;
                    var progress = (current * 100) / totalEntries;
                    
                    if (current % 10 == 0 || current == totalEntries) // 每10个文件或最后一个文件更新一次进度
                    {
                        ConsoleLogger.Progress(stepName, progress, $"解压文件: {entry.FullName}");
                    }
                    
                    var fullPath = Path.Combine(extractDir, entry.FullName);
                    var directory = Path.GetDirectoryName(fullPath);
                    
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    // 如果是目录条目（以/结尾），跳过
                    if (entry.FullName.EndsWith("/"))
                    {
                        continue;
                    }
                    
                    // 解压文件
                    entry.ExtractToFile(fullPath, true);
                }
                
                ConsoleLogger.Success($"解压完成，共 {totalEntries} 个文件", stepName);
            });
        }
    }
}
using System;
using System.Threading.Tasks;
using FlashWorkflowFramework.Core;
using FlashWorkflowFramework.Core.Utils;

namespace FlashWorkflowFramework
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
            {
                ConsoleLogger.Initialize();
                
                // 显示GPL V3许可证信息
                Console.WriteLine("FWF  Copyright (C) 2025 huang1057");
                Console.WriteLine("This program comes with ABSOLUTELY NO WARRANTY; for details type `show w'.");
                Console.WriteLine("This is free software, and you are welcome to redistribute it");
                Console.WriteLine("under certain conditions; type `show c' for details.");
                Console.WriteLine();
                
                ConsoleLogger.Info("FlashWorkflowFramework 启动");
                
                // 解析命令行参数
                var options = CommandLineParser.Parse(args);
                
                // 创建执行引擎
                var engine = new WorkflowExecutionEngine();
                
                // 执行工作流
                var result = await engine.ExecuteFromZipAsync(
                    options.ZipFilePath, 
                    options);
                
                ConsoleLogger.Info($"执行完成，结果: {(result.Success ? "成功" : "失败")}");
                return result.Success ? 0 : 1;
            }
            catch (Exception ex)
            {
                ConsoleLogger.Error($"程序执行失败: {ex.Message}");
                return 1;
            }
        }
    }
}
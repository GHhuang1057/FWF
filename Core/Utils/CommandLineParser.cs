
using System;
using System.Collections.Generic;

namespace FlashWorkflowFramework.Core.Utils
{
    public class ExecutionOptions
    {
        public string ZipFilePath { get; set; } = string.Empty;
        public string OutputPath { get; set; } = @"C:\FWF\output";
        public string WorkflowFileName { get; set; } = "workflow.xml";
        public string TempPath { get; set; } = @"C:\FWF\temp";
        public Dictionary<string, string> Variables { get; set; } = new();
        public bool Verbose { get; set; } = false;
        public bool KeepTempFiles { get; set; } = false;
    }

    public static class CommandLineParser
    {
        public static ExecutionOptions Parse(string[] args)
        {
            var options = new ExecutionOptions();
            
            if (args.Length == 0)
            {
                throw new ArgumentException("必须指定ZIP文件路径");
            }
            
            // 第一个参数是ZIP文件路径
            options.ZipFilePath = args[0];
            
            // 解析其他参数
            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-o":
                    case "--output":
                        if (i + 1 >= args.Length)
                            throw new ArgumentException($"参数 {args[i]} 需要指定值");
                        options.OutputPath = args[++i];
                        break;
                        
                    case "-w":
                    case "--workflow":
                        if (i + 1 >= args.Length)
                            throw new ArgumentException($"参数 {args[i]} 需要指定值");
                        options.WorkflowFileName = args[++i];
                        break;
                        
                    case "-t":
                    case "--temp":
                        if (i + 1 >= args.Length)
                            throw new ArgumentException($"参数 {args[i]} 需要指定值");
                        options.TempPath = args[++i];
                        break;
                        
                    case "-v":
                    case "--verbose":
                        options.Verbose = true;
                        break;
                        
                    case "--keep-temp":
                        options.KeepTempFiles = true;
                        break;
                        
                    default:
                        // 处理变量参数 --var:Name=Value
                        if (args[i].StartsWith("--var:"))
                        {
                            var varArg = args[i].Substring(6); // 移除 "--var:"
                            var parts = varArg.Split('=', 2);
                            if (parts.Length != 2)
                                throw new ArgumentException($"变量参数格式错误: {args[i]}，应为 --var:Name=Value");
                            
                            options.Variables[parts[0]] = parts[1];
                        }
                        else
                        {
                            throw new ArgumentException($"未知参数: {args[i]}");
                        }
                        break;
                }
            }
            
            return options;
        }
    }
}
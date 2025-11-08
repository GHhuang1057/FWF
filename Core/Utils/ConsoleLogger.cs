using System;

namespace FlashWorkflowFramework.Core.Utils
{
    public static class ConsoleLogger
    {
        private static readonly object _lockObject = new object();
        
        public static void Initialize()
        {
            Console.Title = "FlashWorkflowFramework - 工作流执行日志";
            Console.WriteLine("=== FlashWorkflowFramework 启动 ===");
            Console.WriteLine($"开始时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine(new string('=', 80));
        }
        
        public static void Info(string message, string? stepName = null)
        {
            WriteMessage("INFO", message, stepName, ConsoleColor.White);
        }
        
        public static void Success(string message, string? stepName = null)
        {
            WriteMessage("SUCCESS", message, stepName, ConsoleColor.Green);
        }
        
        public static void Warning(string message, string? stepName = null)
        {
            WriteMessage("WARNING", message, stepName, ConsoleColor.Yellow);
        }
        
        public static void Error(string message, string? stepName = null)
        {
            WriteMessage("ERROR", message, stepName, ConsoleColor.Red);
        }
        
        public static void Progress(string stepName, int progress, string status = "")
        {
            lock (_lockObject)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[PROGRESS] ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"[{stepName}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{progress}%");
                
                if (!string.IsNullOrEmpty(status))
                {
                    Console.Write($" - {status}");
                }
                Console.WriteLine();
            }
        }
        
        private static void WriteMessage(string level, string message, string? stepName, ConsoleColor color)
        {
            lock (_lockObject)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
                Console.ForegroundColor = color;
                Console.Write($"[{level}] ");
                
                if (!string.IsNullOrEmpty(stepName))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"[{stepName}] ");
                }
                
                Console.ForegroundColor = color;
                Console.WriteLine(message);
            }
        }
    }
}
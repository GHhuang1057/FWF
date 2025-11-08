# FlashWorkflowFramework (FWF) 开发需求文档

## 1. 项目概述

### 1.1 项目名称
FlashWorkflowFramework (FWF) - 刷机包转换执行引擎

### 1.2 项目目标
开发一个基于.NET 9.0的命令行工具，能够解压指定的ZIP文件到固定目录，解析其中的XML工作流配置文件，并按照工作流执行命令和下载操作。

### 1.3 核心功能
- **ZIP解压**：将指定ZIP文件解压到固定目录（如C:\FWF）
- **XML解析**：解析解压后的XML工作流配置文件
- **命令执行**：在工作流中执行tool文件夹下的开发者工具
- **下载引擎**：根据XML配置下载传统刷机包
- **实时日志**：显示详细的执行过程和状态信息

## 2. 系统架构

### 2.1 工作流程
```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│  输入ZIP     │ -> │  解压到固定  │ -> │  解析XML    │ -> │  执行工作流  │
│  文件        │    │  目录       │    │  工作流     │    │  步骤       │
└─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘
         │                  │                  │                  │
         ▼                  ▼                  ▼                  ▼
   ┌───────────┐      ┌───────────┐      ┌───────────┐      ┌───────────┐
   │ 命令行参数 │      │ C:\FWF\   │      │ 工作流定义 │      │ 工具执行   │
   │ 解析      │      │ 解压目录  │      │ 解析      │      │ 下载引擎  │
   └───────────┘      └───────────┘      └───────────┘      └───────────┘
```

### 2.2 目录结构
```
C:\FWF\
├── temp\                  # 临时工作目录
│   ├── session_1\         # 会话工作目录
│   └── downloads\         # 下载文件存储
├── tools\                 # 系统工具目录（可选）
└── logs\                  # 日志文件目录

解压后的目录结构：
C:\FWF\temp\session_<timestamp>\
├── workflow.xml          # 工作流配置文件
├── tool\                 # 开发者提供的工具
│   ├── ozip_decrypt.exe
│   ├── payload-dumper.exe
│   └── ...
└── other_files\          # 其他可能需要的文件
```

## 3. 详细功能设计

### 3.1 命令行接口

#### 3.1.1 基本用法
```bash
# 基本用法：解压并执行ZIP中的工作流
fwf.exe "path/to/rom_package.zip"

# 指定输出目录
fwf.exe "rom.zip" --output "C:\FWF\output"

# 指定工作流文件名称（如果不是默认的workflow.xml）
fwf.exe "rom.zip" --workflow "custom_workflow.xml"

# 设置工作流变量
fwf.exe "rom.zip" --var:Device=raphael --var:AndroidVersion=13
```

#### 3.1.2 命令行参数
```bash
fwf.exe <zip_file> [options]

参数:
  <zip_file>             要处理的ZIP文件路径

选项:
  -o, --output <path>    输出目录路径（默认：C:\FWF\output）
  -w, --workflow <name>  工作流文件名（默认：workflow.xml）
  -t, --temp <path>      临时工作目录（默认：C:\FWF\temp）
  --var:<name>=<value>   设置工作流变量
  -v, --verbose          详细日志输出
  --keep-temp            保留临时文件（默认会清理）
```

### 3.2 核心执行流程

#### 3.2.1 程序入口
```csharp
using System;
using FlashWorkflowFramework.Core;

namespace FlashWorkflowFramework
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
            {
                ConsoleLogger.Initialize();
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
```

#### 3.2.2 执行引擎核心
```csharp
public class WorkflowExecutionEngine
{
    private readonly string _baseTempPath;
    
    public WorkflowExecutionEngine(string baseTempPath = @"C:\FWF\temp")
    {
        _baseTempPath = baseTempPath;
    }
    
    public async Task<ExecutionResult> ExecuteFromZipAsync(string zipFilePath, ExecutionOptions options)
    {
        // 1. 创建会话目录
        var sessionDir = CreateSessionDirectory();
        ConsoleLogger.Info($"创建会话目录: {sessionDir}");
        
        try
        {
            // 2. 解压ZIP文件
            ConsoleLogger.Info($"解压ZIP文件: {zipFilePath}");
            await ExtractZipAsync(zipFilePath, sessionDir);
            
            // 3. 查找并解析工作流文件
            var workflowPath = Path.Combine(sessionDir, options.WorkflowFileName);
            if (!File.Exists(workflowPath))
            {
                throw new FileNotFoundException($"工作流文件未找到: {workflowPath}");
            }
            
            ConsoleLogger.Info($"解析工作流文件: {workflowPath}");
            var workflow = await WorkflowParser.ParseAsync(workflowPath);
            
            // 4. 准备执行上下文
            var context = new ExecutionContext(sessionDir, options.OutputPath);
            foreach (var variable in options.Variables)
            {
                context.Variables.SetVariable(variable.Key, variable.Value);
            }
            
            // 5. 执行工作流
            return await ExecuteWorkflowAsync(workflow, context);
        }
        finally
        {
            // 6. 清理临时文件
            if (!options.KeepTempFiles)
            {
                CleanupSession(sessionDir);
            }
        }
    }
    
    private string CreateSessionDirectory()
    {
        var sessionDir = Path.Combine(_baseTempPath, $"session_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(sessionDir);
        return sessionDir;
    }
    
    private async Task ExtractZipAsync(string zipPath, string extractDir)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var totalEntries = archive.Entries.Count;
        var current = 0;
        
        foreach (var entry in archive.Entries)
        {
            current++;
            var progress = (current * 100) / totalEntries;
            ConsoleLogger.Info($"解压文件: {entry.FullName} ({progress}%)");
            
            var fullPath = Path.Combine(extractDir, entry.FullName);
            var directory = Path.GetDirectoryName(fullPath);
            
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            if (!entry.FullName.EndsWith("/"))
            {
                entry.ExtractToFile(fullPath, true);
            }
        }
        
        ConsoleLogger.Success($"解压完成，共 {totalEntries} 个文件");
    }
}
```

### 3.3 XML工作流格式

#### 3.3.1 完整Schema
```xml
<Workflow Name="ROM转换工作流" Version="1.0">
  <Variables>
    <Variable Name="Device" Value="raphael" />
    <Variable Name="AndroidVersion" Value="13" />
    <Variable Name="OutputName" Value="custom_rom" />
  </Variables>
  
  <Steps>
    <!-- 执行Tool文件夹中的工具 -->
    <Step Type="ExecuteTool" Name="解密OZIP文件">
      <Tool>ozip_decrypt.exe</Tool>
      <Arguments>"$(SessionDir)/rom.ozip" "$(SessionDir)/extracted"</Arguments>
      <WorkingDirectory>$(SessionDir)/tool</WorkingDirectory>
      <Timeout>300</Timeout>
    </Step>
    
    <!-- 下载传统刷机包 -->
    <Step Type="Download" Name="下载基础固件">
      <Url>https://example.com/firmware/$(Device)_base.zip</Url>
      <Output>$(SessionDir)/downloads/base_firmware.zip</Output>
      <Checksum Type="MD5">a1b2c3d4e5f678901234567890123456</Checksum>
      <RetryCount>3</RetryCount>
    </Step>
    
    <!-- 文件操作 -->
    <Step Type="FileOperation" Name="准备刷机包结构">
      <Operation>CopyDirectory</Operation>
      <Source>$(SessionDir)/extracted</Source>
      <Destination>$(OutputDir)/META-INF</Destination>
    </Step>
    
    <!-- 条件执行 -->
    <Step Type="ExecuteTool" Name="生成刷机脚本" Condition="$(AndroidVersion) >= 12">
      <Tool>script_generator.exe</Tool>
      <Arguments>"$(Device)" "$(AndroidVersion)" "$(OutputDir)"</Arguments>
    </Step>
  </Steps>
</Workflow>
```

### 3.4 执行器实现

#### 3.4.1 Tool执行器
```csharp
public class ToolStepExecutor : IStepExecutor
{
    public string StepType => "ExecuteTool";
    
    public async Task<StepResult> ExecuteAsync(WorkflowStep step, ExecutionContext context)
    {
        var toolName = step.Parameters["Tool"];
        var arguments = context.Variables.Resolve(step.Parameters["Arguments"]);
        var workingDir = context.Variables.Resolve(step.Parameters.GetValueOrDefault("WorkingDirectory"));
        var timeout = int.Parse(step.Parameters.GetValueOrDefault("Timeout", "300"));
        
        // 构建工具完整路径
        var toolPath = Path.Combine(context.SessionDir, "tool", toolName);
        if (!File.Exists(toolPath))
        {
            throw new FileNotFoundException($"工具未找到: {toolPath}");
        }
        
        ConsoleLogger.Info($"执行工具: {toolName} {arguments}", step.Name);
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = toolPath,
                Arguments = arguments,
                WorkingDirectory = workingDir ?? Path.Combine(context.SessionDir, "tool"),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        
        var output = new StringBuilder();
        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                output.AppendLine(e.Data);
                ConsoleLogger.Info($"[TOOL] {e.Data}", step.Name);
            }
        };
        
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                ConsoleLogger.Warning($"[TOOL-ERROR] {e.Data}", step.Name);
            }
        };
        
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        var completed = await Task.Run(() => process.WaitForExit(timeout * 1000));
        if (!completed)
        {
            process.Kill();
            throw new TimeoutException($"工具执行超时 ({timeout}秒)");
        }
        
        return new StepResult
        {
            Success = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            Output = output.ToString()
        };
    }
}
```

#### 3.4.2 下载执行器
```csharp
public class DownloadStepExecutor : IStepExecutor
{
    public string StepType => "Download";
    
    public async Task<StepResult> ExecuteAsync(WorkflowStep step, ExecutionContext context)
    {
        var url = context.Variables.Resolve(step.Parameters["Url"]);
        var outputPath = context.Variables.Resolve(step.Parameters["Output"]);
        var checksum = step.Parameters.GetValueOrDefault("Checksum");
        var retryCount = int.Parse(step.Parameters.GetValueOrDefault("RetryCount", "3"));
        
        // 确保输出目录存在
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!Directory.Exists(outputDir))
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
                return new StepResult { Success = true };
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
        
        return new StepResult { Success = false, Error = lastError };
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
            
            if (canReportProgress)
            {
                var progress = (int)((totalRead * 100) / totalBytes);
                ConsoleLogger.Info($"下载进度: {progress}% ({totalRead}/{totalBytes} bytes)", stepName);
            }
        }
    }
}
```

### 3.5 实时日志系统

#### 3.5.1 增强的日志系统
```csharp
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
    
    public static void Info(string message, string stepName = null)
    {
        WriteMessage("INFO", message, stepName, ConsoleColor.White);
    }
    
    public static void Success(string message, string stepName = null)
    {
        WriteMessage("SUCCESS", message, stepName, ConsoleColor.Green);
    }
    
    public static void Warning(string message, string stepName = null)
    {
        WriteMessage("WARNING", message, stepName, ConsoleColor.Yellow);
    }
    
    public static void Error(string message, string stepName = null)
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
    
    private static void WriteMessage(string level, string message, string stepName, ConsoleColor color)
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
```

## 4. 配置和部署

### 4.1 项目文件 (fwf.csproj)
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <AssemblyTitle>FlashWorkflowFramework</AssemblyTitle>
    <AssemblyDescription>刷机包转换执行引擎</AssemblyDescription>
    <AssemblyVersion>1.0.0.0</AssemblyVersion>
    <ApplicationIcon>fwf.ico</ApplicationIcon>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="System.IO.Compression" Version="4.3.0" />
  </ItemGroup>

</Project>
```

### 4.2 使用示例

#### 4.2.1 完整工作流示例
```xml
<!-- workflow.xml -->
<Workflow Name="MIUI转换工作流" Version="1.0">
  <Variables>
    <Variable Name="DeviceCode" Value="raphael" />
    <Variable Name="ROMVersion" Value="MIUI14" />
  </Variables>
  
  <Steps>
    <!-- 步骤1: 下载基础固件 -->
    <Step Type="Download" Name="下载MIUI固件">
      <Url>https://bigota.d.miui.com/V14.0.1.0.TFKCNXM/$(DeviceCode)_images_V14.0.1.0.TFKCNXM_20231120.0000.00_13.0_cn.zip</Url>
      <Output>$(SessionDir)/downloads/miui_base.zip</Output>
      <RetryCount>3</RetryCount>
    </Step>
    
    <!-- 步骤2: 解压下载的固件 -->
    <Step Type="ExecuteTool" Name="解压MIUI固件">
      <Tool>payload-dumper.exe</Tool>
      <Arguments>"$(SessionDir)/downloads/miui_base.zip" "$(SessionDir)/extracted"</Arguments>
    </Step>
    
    <!-- 步骤3: 处理系统镜像 -->
    <Step Type="ExecuteTool" Name="转换系统镜像">
      <Tool>img2simg.exe</Tool>
      <Arguments>"$(SessionDir)/extracted/system.img" "$(OutputDir)/system.img"</Arguments>
    </Step>
    
    <!-- 步骤4: 创建刷机脚本 -->
    <Step Type="FileOperation" Name="创建刷机脚本">
      <Operation>CopyFile</Operation>
      <Source>$(SessionDir)/templates/updater-script</Source>
      <Destination>$(OutputDir)/META-INF/com/google/android/updater-script</Destination>
    </Step>
  </Steps>
</Workflow>
```

#### 4.2.2 命令行调用
```bash
# 基本用法
fwf.exe "miui_rom.zip"

# 指定输出目录和工作流文件
fwf.exe "custom_rom.zip" --output "D:\output" --workflow "convert.xml"

# 设置设备变量并保留临时文件
fwf.exe "rom.zip" --var:Device=raphael --var:Version=13 --keep-temp
```

## 5. 错误处理和恢复

### 5.1 错误分类和处理
- **ZIP文件错误**：文件不存在、损坏、格式错误
- **XML解析错误**：格式错误、缺少必需元素
- **工具执行错误**：工具不存在、执行失败、超时
- **下载错误**：网络问题、文件不存在、校验失败
- **文件系统错误**：权限不足、磁盘空间不足

### 5.2 恢复策略
- 分步执行，失败时停止后续步骤
- 提供详细的错误信息和上下文
- 支持重试机制（特别是下载操作）
- 清理部分失败时产生的临时文件

## 6. 开发计划

### Phase 1: 基础框架 (1周)
- [ ] 项目结构和基础类设计
- [ ] ZIP解压功能
- [ ] 基础日志系统
- [ ] 命令行参数解析

### Phase 2: XML解析和执行引擎 (2周)
- [ ] XML工作流解析器
- [ ] 变量系统和上下文管理
- [ ] Tool执行器实现
- [ ] 基础文件操作执行器

### Phase 3: 下载引擎和高级功能 (1周)
- [ ] HTTP下载执行器
- [ ] 校验和验证
- [ ] 条件执行支持
- [ ] 进度报告增强

### Phase 4: 测试和优化 (1周)
- [ ] 单元测试和集成测试
- [ ] 错误处理完善
- [ ] 性能优化
- [ ] 用户文档

## 7. 验收标准

### 功能验收
- [ ] 能够正确解压ZIP文件到指定目录
- [ ] 能够解析XML工作流配置文件
- [ ] 能够执行tool文件夹中的工具
- [ ] 能够下载网络文件并验证校验和
- [ ] 实时显示详细的执行日志

### 可靠性验收
- [ ] 正确处理各种错误情况
- [ ] 临时文件正确清理
- [ ] 网络中断时能够重试下载
- [ ] 工具执行超时能够正确处理

### 兼容性验收
- [ ] 支持Windows 10/11系统
- [ ] 正确处理中文字符和路径
- [ ] 单文件发布正常运行
- [ ] 支持常见的ZIP压缩格式

---

**文档版本**: 3.0  
**最后更新**: 2024-01-20  
**目标框架**: .NET 9.0  
**输出类型**: 控制台应用程序 (fwf.exe)
using System.Collections.Generic;

namespace FlashWorkflowFramework.Core.Models
{
    public class Workflow
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public Dictionary<string, string> Variables { get; set; } = new();
        public List<WorkflowStep> Steps { get; set; } = new();
    }

    public class WorkflowStep
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Condition { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new();
    }

    public class ExecutionResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, StepResult> StepResults { get; set; } = new();
        public bool InteractiveCommandWaiting { get; set; }
    }

    public class StepResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public Exception? Error { get; set; }
        public bool InteractiveCommandWaiting { get; set; }
    }

    public class ExecutionContext
    {
        public string SessionDir { get; set; } = string.Empty;
        public string OutputDir { get; set; } = string.Empty;
        public VariableCollection Variables { get; set; } = new();
    }

    public class VariableCollection
    {
        private readonly Dictionary<string, string> _variables = new();
        
        public void SetVariable(string name, string value)
        {
            _variables[name] = value;
        }
        
        public string GetVariable(string name)
        {
            return _variables.TryGetValue(name, out var value) ? value : string.Empty;
        }
        
        public string Resolve(string template)
        {
            if (string.IsNullOrEmpty(template))
                return template;
            
            string result = template;
            
            // 支持 ${variable} 格式
            foreach (var variable in _variables)
            {
                result = result.Replace($"${{{variable.Key}}}", variable.Value);
            }
            
            // 支持 $(variable) 格式
            foreach (var variable in _variables)
            {
                result = result.Replace($"$({variable.Key})", variable.Value);
            }
            
            return result;
        }
    }
}
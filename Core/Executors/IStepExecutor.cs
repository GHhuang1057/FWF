using System.Threading.Tasks;
using FlashWorkflowFramework.Core.Models;

namespace FlashWorkflowFramework.Core.Executors
{
    public interface IStepExecutor
    {
        string StepType { get; }
        Task<StepResult> ExecuteAsync(WorkflowStep step, FlashWorkflowFramework.Core.Models.ExecutionContext context);
    }
}
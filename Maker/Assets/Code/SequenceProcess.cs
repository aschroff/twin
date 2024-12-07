using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Code
{
    public class SequenceProcess : Process
    {
        [SerializeField]
        private List<ProcessSync> processes = new List<ProcessSync>();

        public override ProcessResult Execute(string variant = "")
        {
            ExecuteProcessesAsync();
            return new ProcessResult();
        }

        private async Task ExecuteProcessesAsync()
        {
            foreach (var process in processes)
            {
                await ExecuteProcessAsync(process);
            }
        }

        private async Task ExecuteProcessAsync(ProcessSync process)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            process.ExecuteCompleted += () => taskCompletionSource.SetResult(true);
            process.ExecuteSync();
            await taskCompletionSource.Task;
        }
    }
}
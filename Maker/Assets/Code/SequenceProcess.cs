using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System;

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

        private async Task ExecuteProcessAsyncOld(ProcessSync process)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            process.ExecuteCompleted += () => taskCompletionSource.SetResult(true);
            process.ExecuteSync();
            await taskCompletionSource.Task;
        }
        
        private async Task ExecuteProcessAsync(ProcessSync process)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            Action handler = null;
            handler = () =>
            {
                taskCompletionSource.SetResult(true);
                process.ExecuteCompleted -= handler;
            };
            process.ExecuteCompleted += handler;
            process.ExecuteSync();
            await taskCompletionSource.Task;
        }
    }
}
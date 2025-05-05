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
            ExecuteProcessesAsync(variant);
            return new ProcessResult();
        }

        private async Task ExecuteProcessesAsync(string variant = "")
        {
            foreach (var process in processes)
            {
                await ExecuteProcessAsync(process, variant);
            }
        }
        
        private async Task ExecuteProcessAsync(ProcessSync process, string variant = "")
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            Action handler = null;
            handler = () =>
            {
                taskCompletionSource.SetResult(true);
                process.ExecuteCompleted -= handler;
            };
            process.ExecuteCompleted += handler;
            process.ExecuteSync(variant);
            await taskCompletionSource.Task;
        }
    }
}
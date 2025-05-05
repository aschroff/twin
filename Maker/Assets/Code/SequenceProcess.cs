using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System;

namespace Code
{
    /// <summary>
    /// A process that executes a sequence of synchronized processes asynchronously.
    /// </summary>
    /// <remarks>
    /// This class manages a list of processes and ensures they are executed in order, 
    /// waiting for each process to complete before starting the next (synchronized).
    /// </remarks>
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
        

        /// <summary>
        /// Executes a single process asynchronously and waits for its completion.
        /// </summary>
        /// <param name="process">The process to be executed, represented by a <see cref="ProcessSync"/> object.</param>
        /// <returns>
        /// A <see cref="Task"/> that represents the asynchronous operation. The task completes when the process signals its completion.
        /// </returns>
        /// <remarks>
        /// This method uses a <see cref="TaskCompletionSource{TResult}"/> to await the completion of the process. 
        /// It subscribes to the <c>ExecuteCompleted</c> event of the process and ensures proper unsubscription to avoid memory leaks.
        /// </remarks>
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
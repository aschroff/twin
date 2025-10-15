namespace Code
{
    public abstract class ProcessSync: Process
    {
        public event System.Action ExecuteCompleted;
        
        public abstract ProcessResult ExecuteSync(string variant = "");
        
        protected void OnExecuteCompleted()
        {
            ExecuteCompleted?.Invoke();
        }
    }
}
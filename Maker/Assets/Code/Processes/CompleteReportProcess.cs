namespace Code.Processes
{
    public class CompleteReportProcess: QuickHelpProcess
    {
        protected override void CallAI(AI.AI ai,string variant = "")
        {
            ai.CompleteReport(); 
        }  
        
    }
}
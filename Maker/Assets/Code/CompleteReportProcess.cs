using System.Collections;
using System.Collections.Generic;
using Lean.Gui;
using UnityEngine;

namespace Code
{
    public class CompleteReportProcess: QuickHelpProcess
    {
        protected override void CallAI(AI ai)
        {
            ai.CompleteReport(); 
        }  
        
        
        
    }
}
using System.Collections;
using System.Collections.Generic;
using Lean.Gui;
using UnityEngine;

namespace Code
{
    public class ShortSummaryProcess: QuickHelpProcess
    {
        protected override void CallAI(AI ai)
        {
            ai.Summary(); 
        }  
        
        
        
    }
}
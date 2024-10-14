using System.Collections;
using System.Collections.Generic;
using Lean.Gui;
using UnityEngine;

namespace Code
{
    public class TreatmentProcess: QuickHelpProcess
    {
        protected override void CallAI(AI ai)
        {
            ai.Treatment(); 
        }  
        
        
        
    }
}
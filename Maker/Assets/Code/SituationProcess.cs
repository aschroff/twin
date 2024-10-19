using System.Collections;
using System.Collections.Generic;
using Lean.Gui;
using UnityEngine;

namespace Code
{
    public class SituationProcess: QuickHelpProcess
    {
        public string language = "English";
        protected override void CallAI(AI.AI ai,string variant = "")
        {
            ai.Situation(variant); 
        }  
        
        
        
    }
}
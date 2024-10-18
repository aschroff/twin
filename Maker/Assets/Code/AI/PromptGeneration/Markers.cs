using System;
using UnityEditor;

namespace Code.AI.PromptGeneration
{
    public class Markers: Tools, IRoot
    {

        public override string GetBegin()
        {
            return "Following is a list of meaning for lines painted on the body. The value in brackets are RGB codes of the colours: ";
        }

        public string Chapter(AI.Help help)
        {
            return Contribute(help);
        }
        
        public override string Contribute(global::Code.AI.AI.Help help)
        {
            return ContributeFromChildren(help);
        }
        
        
    }
    
    
}
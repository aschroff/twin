using UnityEngine;

namespace Code.AI.PromptGeneration
{
    public interface IPromptContributingGameObject
    {
        public abstract string Contribute(global::Code.AI.AI.Help help);
        
    }
}
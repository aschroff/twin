using System;
using Castle.Core.Configuration;
using UnityEngine;

namespace Code.AI.PromptGeneration
{
    public abstract class Tools : MonoBehaviour
    {
        
        public abstract string Contribute(global::Code.AI.AI.Help help);

        public abstract string GetBegin();

        protected  string Combine(string soFar, string toAdd)
        {
           return soFar + "\n" + toAdd; 
        }
        
        protected string ContributeFromChildren(global::Code.AI.AI.Help help)
        {
            string result = GetBegin();
            for (int i = this.transform.childCount - 1; i >= 1; i--)
            {
                GameObject child = this.transform.GetChild(i).gameObject;
                foreach (IPromptContributingGameObject contributor in child.GetComponents<IPromptContributingGameObject>())
                {
                    result = Combine(result, contributor.Contribute(help));
                }
            }
            return result;
        }
    }
}
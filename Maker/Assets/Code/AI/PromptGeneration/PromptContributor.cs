using System.Collections.Generic;
using UnityEngine;
using  System.Linq;


namespace Code.AI.PromptGeneration
{
    public static class PromptContributor
    {
        private static List<IRoot> FindAllRoots() 
        {
            //IEnumerable<IRoot> rootObjects = Object.FindObjectsByType<PromptContributingGameObject>(true);
            IEnumerable<IRoot> rootObjects = Object.FindObjectsOfType<MonoBehaviour>(true)
                .OfType<IRoot>();
            return new List<IRoot>(rootObjects);
        }
        
        public static string GeneratePrompt(AI.Help help)
        {
            List<IRoot> roots = FindAllRoots();
            string prompt = "";
            foreach (IRoot root in roots)
            {

                    prompt += root.Chapter(help) + "\n ";
                
            }
            return prompt;
        }
    }
}
using CW.Common;
using PaintIn3D;
using UnityEngine;
using UnityEngine.UI;

namespace Code.AI.PromptGeneration
{
    public class Marker: MonoBehaviour, IPromptContributingGameObject
    {
        public new string Contribute(global::Code.AI.AI.Help help)
        {
            CwDemoButton button = gameObject.GetComponent<CwDemoButton>();
            GameObject tool = button.IsolateTarget.gameObject;
            CwPaintSphere marker = tool.GetComponent<CwPaintSphere>();
            return marker.name + " (RGB: " + marker.Color.r + ", " + marker.Color.g + ", " + marker.Color.b +
                   ") " + GetMarkerText(); 
        }
        
        private string GetMarkerText()
        {
            foreach (Text text in this.GetComponentsInChildren<Text>())
            {
                if (text.enabled && text.text != "Placeholder")
                {   
                    if (text.text == "")
                    {
                        return "has no meaning";
                    }
                    return "has the follwing meaning: " + text.text;
                }
            }
            return "has a unknown meaning";
        }
    }
}
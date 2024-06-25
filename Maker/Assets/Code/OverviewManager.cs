using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OverviewManager : MonoBehaviour
{
    
    public void Show(bool visible)
    {   
        RectTransform recttransform = this.gameObject.transform.parent.GetComponent<RectTransform>();
        float width = recttransform.rect.width;
        if (visible & recttransform.anchoredPosition.x  <= 0)
        {
            
        }
        else if (!visible  & recttransform.anchoredPosition.x  <= 0)
        {
            recttransform.anchoredPosition += new Vector2(width, 0);
        }  
        else if (!visible & recttransform.anchoredPosition.x  > 0)
        {
            
        } 
        else if (visible  & recttransform.anchoredPosition.x  > 0)
        {
            recttransform.anchoredPosition += new Vector2(-width, 0);
        }  
    }
    public void toggleShow()
    {
        RectTransform recttransform = this.gameObject.transform.parent.GetComponent<RectTransform>();
        bool newVisible = (recttransform.anchoredPosition.x > 0);
        Show(newVisible);
    }
}

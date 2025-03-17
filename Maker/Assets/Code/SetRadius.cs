using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SetRadius : MonoBehaviour
{

    [SerializeField] private GameObject gameObjectText;
    [SerializeField] private float multiplicator;



    public void SetRadiusOfPaint(System.Int32 index)
    {
        gameObjectText.GetComponent<PaintIn3D.CwPaintSphere>().Radius = (index+1) * multiplicator;
    }


}

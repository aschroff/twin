using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Model : MonoBehaviour
{
    private SMPLX model;
    private string model_info;
    void Awake() 
    {
        model = this.gameObject.transform.GetComponent<SMPLX>();
        model.Awake(); // initialize member values in Editor mode

        int shapes, expressions, poseCorrectives;
        model.GetModelInfo(out shapes, out expressions, out poseCorrectives);
        model_info = string.Format("Model: {0} beta shapes, {1} expressions, {2} pose correctives", shapes, expressions, poseCorrectives);
    }
}

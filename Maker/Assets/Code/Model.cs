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

    public void overall_random()
    {
        for (int i=0; i<SMPLX.NUM_BETAS; i++)
        {
            model.betas[i] = Random.Range(-2.0f, 2.0f);
        }
        model.SetBetaShapes();
        
    }
    public void face_random()
    {
        for (int i=0; i<SMPLX.NUM_EXPRESSIONS; i++)
        {
            model.expressions[i] = Random.Range(-2.0f, 2.0f);
        }
        model.SetExpressions();
        
    }
    
    public void t_arms()
    {
        model.SetBodyPose(SMPLX.BodyPose.T);
        
    }
    
    public void flat_hands()
    {
        model.SetHandPose(SMPLX.HandPose.Flat);
        
    }
    
    public void reset()
    {
        for (int i=0; i<SMPLX.NUM_EXPRESSIONS; i++)
        {
            model.expressions[i] = 0.0f;
        }
        model.SetExpressions();
        for (int i=0; i<SMPLX.NUM_BETAS; i++)
        {
            model.betas[i] = 0.0f;
        }
        model.SetBetaShapes();
        model.SetBodyPose(SMPLX.BodyPose.A);
    }
    
}

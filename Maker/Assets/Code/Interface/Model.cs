using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Model : MonoBehaviour
{
    private SMPLX model;
    private string model_info;
    private SMPLX.HandPose handPose;
    private SMPLX.BodyPose bodyPose;
    void Awake() 
    {
        model = this.gameObject.transform.GetComponent<SMPLX>();
        model.Awake(); // initialize member values in Editor mode

        int shapes, expressions, poseCorrectives;
        model.GetModelInfo(out shapes, out expressions, out poseCorrectives);
        model_info = string.Format("Model: {0} beta shapes, {1} expressions, {2} pose correctives", shapes, expressions, poseCorrectives);
        handPose = SMPLX.HandPose.Flat;
        bodyPose = SMPLX.BodyPose.T;  
        model.SetHandPose(handPose);
        model.SetBodyPose(bodyPose); 
    }


    private float[] betasFromPython = new float[]
    {
    1.5893277f,  3.600594f,  -1.6865557f,  -1.2408743f,  -1.0364962f,
    -0.8183566f,   -1.9653333f,  -2.632217f,  -1.1107891f, 0.5654215f
    };


    public void overall_random()
    {
        for (int i = 0; i < SMPLX.NUM_BETAS; i++)
        {
            model.betas[i] = betasFromPython[i];
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
    
    public void toggle_hands()
    {
        if (handPose == SMPLX.HandPose.Flat)
        {
            handPose = SMPLX.HandPose.Relaxed;
        }
        else
        {
            handPose = SMPLX.HandPose.Flat;
        }
        model.SetHandPose(handPose);
    }
    
    public void toggle_arms()
    {
        if (bodyPose == SMPLX.BodyPose.A)
        {
            bodyPose = SMPLX.BodyPose.T;
        }
        else
        {
            bodyPose = SMPLX.BodyPose.A;
        }
        model.SetBodyPose(bodyPose); 
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

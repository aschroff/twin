using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]

public class TwinNavigation : MonoBehaviour
{
    
    private Camera mainCamera;
    private float CameraZDistance;
    private Vector3 mPrevPos = Vector3.zero;
    private Vector3 mPosDelta = Vector3.zero;
    private Vector3 TouchVector;
    private Vector3 OriginalPlace;


    void Start()
    {
        mainCamera = Camera.main;
        CameraZDistance =
            mainCamera.WorldToScreenPoint(transform.position).z; //z axis of the game object for screen view

    }

    private void Update()
    {
        if (Input.GetMouseButton(1))
        {
            mPosDelta = Input.mousePosition - mPrevPos;
            if(Vector3.Dot(transform.up, Vector3.up) >= 0)
            {
                transform.Rotate(transform.up, -Vector3.Dot(mPosDelta, mainCamera.transform.right), Space.World);
            }
            else
            {
                transform.Rotate(transform.up, Vector3.Dot(mPosDelta, mainCamera.transform.right), Space.World);
            }
        }

        mPrevPos = Input.mousePosition;
    }

    private void OnMouseDown()
    {
        Debug.Log("TwinNavigation - OnMouseDown");
        TouchVector = Input.mousePosition;
         OriginalPlace = this.transform.position;
    }


    private void OnMouseDrag()
    {
        

        Vector3 ScreenPosition =
            new Vector3(Input.mousePosition.x, Input.mousePosition.y, CameraZDistance); //z axis added to screen point 
        Vector3 NewWorldPosition =
            mainCamera.ScreenToWorldPoint(ScreenPosition); //Screen point converted to world point
        Vector3 TouchPosition = new Vector3(TouchVector.x, TouchVector.y, CameraZDistance); //z axis added to screen point 
        Vector3 TouchPositionWorld =
            mainCamera.ScreenToWorldPoint(TouchPosition); //Screen point converted to world point
        this.transform.position = OriginalPlace + NewWorldPosition - TouchPositionWorld;

    }

}

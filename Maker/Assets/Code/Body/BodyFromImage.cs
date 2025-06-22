using Code.Processes;
using Code.Processes.Meshcapade;
using CW.Common;
using PaintIn3D;
using UnityEngine;

namespace Code.Body
{
    public class BodyFromImage : MonoBehaviour
    {
        [SerializeField] private Process bodyFromImageProcess;
        [SerializeField] private InitialitzeMeshcapadeImageProcess initalizeProcess;
        private Texture2D textureBody;
        private string imagePath;
        private void SelectImage(int maxSize)
        {
            NativeGallery.GetImageFromGallery((path) =>
            {
                Debug.Log("Image path: " + path);
                if (path != null)
                {
                    Texture2D texture = NativeGallery.LoadImageAtPath(path, maxSize, false);
                    if (texture == null)
                    {
                        Debug.Log("Couldn't load texture from " + path);
                        return;
                    }
                    else
                    {
                        Debug.Log("Successfully loaded texture from " + path);
                        textureBody = texture;
                        imagePath = path;
                    }
                }
            });
		
        }

        private void StartProcess()
        {
            if (bodyFromImageProcess != null)
            {
                bodyFromImageProcess.Execute();
            }
            else
            {
                Debug.LogError("BodyFromImageProcess is not assigned.");
            }
        }

        private void SetImage()
        {
            initalizeProcess.imagePath = imagePath;
        }
        
        public void SelectImageAndStartProcess()
        {
            
            SelectImage(1024);
            SetImage();
            StartProcess();
        }
    }
}
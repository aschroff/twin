using System.Collections;
using UnityEngine;
using PaintCore;
using System.IO;
using UnityEngine.UI;
using Lean.Gui;

namespace Code
{
    public class SkinProcess: Process
    {

        public override ProcessResult Execute(string variant = "")
        {
            Body body = getBody();
            CwPaintableTexture bodyTexture = body.gameObject.transform.parent.GetComponentInChildren<CwPaintableTexture>();
            byte[] bodyBytes = bodyTexture.GetPngData();
            DataPersistenceManager dataManager = getDataManager();
            name = dataManager.selectedProfileId;
            string fullPath =  Path.Combine(Application.persistentDataPath,name, "skin_" + name + ".png");
            File.WriteAllBytes(fullPath, bodyBytes);
            NativeGallery.SaveImageToGallery(bodyBytes, "twinAlbum", "skin_" + name + ".png", ( success, path ) => Debug.Log( "Media save result: " + success + " " + path ) );
            LeanPulse notification = getNotification();
             
#if UNITY_EDITOR
            string message = "Skin saved  in application data under ..." + Path.Combine(
                name, "skin_" + name + ".png");
#else
			string message = "Skin saved in Pictures as " + "skin_" + name + ".png";
#endif
            foreach (Text text in notification.gameObject.GetComponentsInChildren<Text>())
            {
                text.text = message;
            }
            notification.Pulse();
            return new ProcessResult();
        }
        
    }
}
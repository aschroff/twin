using System.Collections;
using System.Collections.Generic;
using PaintIn3D;
using UnityEngine;
using Lean.Common;
using Lean.Touch;
public class Body : MonoBehaviour,ItemFile, IDataPersistence
{
   [SerializeField] public GameObject mainCamera;
   public  void handleChange(string profile)
   {
      P3dPaintableTexture texture = this.gameObject.transform.GetComponent<P3dPaintableTexture>();
      texture.Save();
      texture.SaveName = profile;
      texture.Clear();
      texture.Load();
   }
   
   public  void handleCopyChange(string profile)
   {
      Debug.Log("Copy profile: " + profile);
      P3dPaintableTexture texture = this.gameObject.transform.GetComponent<P3dPaintableTexture>();
      texture.Save();
      texture.SaveName = profile;
      texture.Save();
      Debug.Log("end copy profile");
   }
   public  void handleDelete(string profile)
   {
      P3dPaintableTexture.ClearSave(profile);
      P3dPaintableTexture texture = this.gameObject.transform.GetComponent<P3dPaintableTexture>();
      if (texture.SaveName == profile)
      {
         texture.Clear();
      }
      
   }
   public GameObject relatedGameObject()
   {
      return this.gameObject;
   }
   
   public void LoadData(ConfigData data)
   {
      LeanPitchYaw control = this.GetComponent<LeanPitchYaw>();
      control.Yaw = data.yaw;
      control.Pitch =  data.pitch;
      mainCamera.transform.position = data.positionCamera;
      LeanPinchCamera camera = mainCamera.GetComponent<LeanPinchCamera>();
      camera.Zoom = data.sizeCamera;
   }

   public void SaveData(ConfigData data)
   {
      LeanPitchYaw control = this.GetComponent<LeanPitchYaw>();
      data.yaw = control.Yaw;
      data.pitch = control.Pitch;
      data.positionCamera = mainCamera.transform.position;
      LeanPinchCamera camera = mainCamera.GetComponent<LeanPinchCamera>();
      data.sizeCamera = camera.Zoom;
   }
   
   
   public void HandleExport()
   {
      P3dPaintableTexture bodyTexture = this.transform.parent.GetComponentInChildren<P3dPaintableTexture>();
      byte[] bodyBytes = bodyTexture.GetPngData();
      NativeGallery.Permission permission = NativeGallery.SaveImageToGallery(bodyBytes, "twinAlbum", "export.png", ( success, path ) => Debug.Log( "Media save result: " + success + " " + path ) );
   }
   

}

using Lean.Common;
using Lean.Touch;
using PaintCore;
using UnityEngine;

namespace Code.Body
{
   public class Body : MonoBehaviour,ItemFile, IDataPersistence
   {
      [SerializeField] public GameObject mainCamera;
      [SerializeField] public int height;
      [SerializeField] public int weight;
      [SerializeField] public string gender;
      public  void handleChange(string profile)
      {
         CwPaintableTexture texture = this.gameObject.transform.GetComponent<CwPaintableTexture>();
         texture.Save();
         texture.SaveName = profile;
         texture.Clear();
         texture.Load();
      }
   
      public  void handleCopyChange(string profile)
      {
         Debug.Log("Copy profile: " + profile);
         CwPaintableTexture texture = this.gameObject.transform.GetComponent<CwPaintableTexture>();
         texture.Save();
         texture.SaveName = profile;
         texture.Save();
         Debug.Log("end copy profile");
      }
      public  void handleDelete(string profile)
      {
         CwPaintableTexture.ClearSave(profile);
         CwPaintableTexture texture = this.gameObject.transform.GetComponent<CwPaintableTexture>();
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
   
   

   

   }
}

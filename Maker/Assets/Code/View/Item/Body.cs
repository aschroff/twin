using System.Collections;
using System.Collections.Generic;
using PaintCore;
using UnityEngine;
using Lean.Common;
using Lean.Touch;
public class Body : MonoBehaviour,ItemFile, IDataPersistence
{
   [SerializeField] public GameObject mainCamera;

   public  void handleChange(string profile)
   {
      CwPaintableTexture texture = PaintableTexture();
      // The painted texture is kept in PlayerPrefs, which a test cannot redirect the way it
      // redirects the data directory - so the name has to be resolved, or a test run would read
      // and overwrite the real user's paintings.
      string saveName = PaintableSaveNameOverride.Resolve(profile);
      // ask before saving: saving the twin we are leaving writes to the same key when the twin
      // is re-opened, and would make the cache look present even when it is not
      bool hasSavedTexture = CwCommon.SaveExists(saveName);
      PaintTextureSaver.Save(texture);
      texture.SaveName = saveName;
      texture.Clear();
      if (hasSavedTexture)
      {
         texture.Load();
         return;
      }
      // No cached texture for this twin on this device - it came from an import or a template.
      // The painted texture travels with the twin as a file, so load it from there and put it
      // into the cache, which makes the next load take the fast path again.
      byte[] paintedTexture = TwinTextureFile.Read(profile);
      if (paintedTexture == null)
      {
         Debug.Log("Twin " + profile + " has neither a cached nor a stored texture.");
         return;
      }
      Debug.Log("Loading the stored texture of twin " + profile + ".");
      texture.LoadFromData(paintedTexture);
      PaintTextureSaver.Save(texture);
   }

   /// <summary>Writes the painted texture into the twin directory, so it can be handed to
   /// another device (the in-app cache stays on this one). Called before exporting.</summary>
   public void StoreTextureFile(string profile)
   {
      TwinTextureFile.Write(profile, PaintableTexture().GetPngData());
   }

   private CwPaintableTexture PaintableTexture()
   {
      return this.gameObject.transform.GetComponent<CwPaintableTexture>();
   }

   public  void handleCopyChange(string profile)
   {
      Debug.Log("Copy profile: " + profile);
      CwPaintableTexture texture = this.gameObject.transform.GetComponent<CwPaintableTexture>();
      PaintTextureSaver.Save(texture);
      texture.SaveName = PaintableSaveNameOverride.Resolve(profile);
      PaintTextureSaver.Save(texture);
      Debug.Log("end copy profile");
   }
   public  void handleDelete(string profile)
   {
      string saveName = PaintableSaveNameOverride.Resolve(profile);
      CwPaintableTexture.ClearSave(saveName);
      CwPaintableTexture texture = this.gameObject.transform.GetComponent<CwPaintableTexture>();
      if (texture.SaveName == saveName)
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

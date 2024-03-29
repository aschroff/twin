using System.Collections;
using System.Collections.Generic;
using PaintIn3D;
using UnityEngine;

public class Body : MonoBehaviour,ItemFile, IDataPersistence
{
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
      P3dPaintableTexture texture = this.gameObject.transform.GetComponent<P3dPaintableTexture>();
      texture.Save();
      texture.SaveName = profile;
      texture.Save();
   }
   public  void handleDelete(string profile)
   {
      P3dPaintableTexture.ClearSave(profile);
   }
   public GameObject relatedGameObject()
   {
      return this.gameObject;
   }
   
   public void LoadData(ConfigData data)
   {
     
   }

   public void SaveData(ConfigData data)
   {
      
   }
}

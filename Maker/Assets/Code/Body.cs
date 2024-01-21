using System.Collections;
using System.Collections.Generic;
using PaintIn3D;
using UnityEngine;

public class Body : ItemFile, IDataPersistence
{
   public override void handleChange(string Profile)
   {
      P3dPaintableTexture texture = this.gameObject.transform.GetComponent<P3dPaintableTexture>();
      texture.SaveName = Profile;
      texture.Load();
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

using UnityEngine;
using UnityEngine.UI;
using PaintIn3D;
using CW.Common;

public class Sticker : MonoBehaviour

{
	[SerializeField] private Texture2D loadedTexture;

    [System.Obsolete]
    public void SelectAndUseNewTexture()
    {
        Debug.Log("SelectAndUseNewTexture " + this.name);
		SelectTexture(1024);
		GameObject icon = this.transform.FindChild("Icon").gameObject;
		Image iconImage = icon.GetComponent<Image>();
        iconImage.sprite = Sprite.Create(loadedTexture,new Rect(0, 0, 512, 512), new Vector2());
		CwDemoButton button = gameObject.GetComponent<CwDemoButton>();
		GameObject tool = button.IsolateTarget.gameObject;
		P3dPaintDecal sticker = tool.GetComponent<P3dPaintDecal>();
		sticker.Texture = loadedTexture;

	}

	private void SelectTexture(int maxSize)
	{
		NativeGallery.Permission permission = NativeGallery.GetImageFromGallery((path) =>
		{
			Debug.Log("Image path: " + path);
			if (path != null)
			{
				// Create Texture from selected image
				Texture2D texture = NativeGallery.LoadImageAtPath(path, maxSize);
				if (texture == null)
				{
					Debug.Log("Couldn't load texture from " + path);
					return;
				}
				loadedTexture = texture;
			}
		});

		Debug.Log("Permission result: " + permission);
	}



}

using UnityEngine;
using UnityEngine.UI;
using PaintIn3D;
using CW.Common;
using System.IO;

public class Sticker : MonoBehaviour

{
	[SerializeField] private Texture2D loadedTexture;

	//[System.Obsolete]
    public void SelectAndUseNewTexture()
    {
        Debug.Log("SelectAndUseNewTexture " + this.name);
		SelectTexture(1024);

	}


	private void setTexture()
	{
		if (loadedTexture != null)
		{
			GameObject icon = this.transform.Find("Icon").gameObject;
			Image iconImage = icon.GetComponent<Image>();
			//int width = Mathf.Min(loadedTexture.width, 512);
			//int height = Mathf.Min(loadedTexture.height, 512);
			int width = loadedTexture.width;
			int height = loadedTexture.height;

			Sprite sprite = Sprite.Create(loadedTexture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100.0f);
			iconImage.sprite = sprite;
			CwDemoButton button = gameObject.GetComponent<CwDemoButton>();
			GameObject tool = button.IsolateTarget.gameObject;
			P3dPaintDecal sticker = tool.GetComponent<P3dPaintDecal>();
			sticker.Texture = loadedTexture;
		}
	}

	private void Start()
	{
		string fullPath = Path.Combine(Application.persistentDataPath, this.gameObject.GetComponent<Item>().getId() + ".png");

		if (File.Exists(fullPath))
		{
			byte[] imageBytes = File.ReadAllBytes(fullPath);
			loadedTexture = new Texture2D(1024, 1024);
			loadedTexture.LoadImage(imageBytes);

			setTexture();
		}
		

	}

	private void SelectTexture(int maxSize)
	{
		NativeGallery.Permission permission = NativeGallery.GetImageFromGallery((path) =>
		{
			Debug.Log("Image path: " + path);
			if (path != null)
			{
				// Create Texture from selected image
				Texture2D texture = NativeGallery.LoadImageAtPath(path, maxSize, false);
				if (texture == null)
				{
					Debug.Log("Couldn't load texture from " + path);
					return;
				}
				else
				{
					loadedTexture = texture;
					SaveTexture();
					setTexture();

				}
			}
		});

		Debug.Log("Permission result: " + permission);
	}

	private void SaveTexture()


	{

		string fullPath = Path.Combine(Application.persistentDataPath, this.gameObject.GetComponent<Item>().getId() + ".png");
		byte[] textureBytes = loadedTexture.EncodeToPNG();
		File.WriteAllBytes(fullPath, textureBytes);
		NativeGallery.Permission permission = NativeGallery.SaveImageToGallery(
			fullPath, "GalleryMaker", Path.GetFileName(fullPath), (success, path) => Debug.Log("Media save! result: " + success + " " + path)
			);
		Debug.Log("Permission result: " + permission);
	}


}


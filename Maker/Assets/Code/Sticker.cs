using UnityEngine;
using UnityEngine.UI;
using PaintIn3D;
using CW.Common;
using System.IO;
using System.Runtime.InteropServices;

public class Sticker : ItemHash, ItemFile

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
	
	
	public override void handleAwake()
	{
		Debug.Log("Handle Awake called.");
		string fullPath = get_path();

		if (File.Exists(fullPath))
		{
			byte[] imageBytes = File.ReadAllBytes(fullPath);
			loadedTexture = new Texture2D(1024, 1024);
			loadedTexture.LoadImage(imageBytes);

			setTexture();
			Register();
		}
		else
		{
			Unregister();
			loadedTexture = this.transform.parent.gameObject.GetComponent<StickerRepo>().defaultTexture;
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
					Register();

					/*resetModification
					for (int j = 0; j < FolderHash().transform.childCount; j++)
					{

						GameObject child = FolderHash().transform.GetChild(j).gameObject;
						child.transform.SetPositionAndRotation(child.transform.localPosition, Quaternion.Euler(0, 0, 0));


					}*/
				}
			}
		});

		Debug.Log("Permission result: " + permission);
	}

	private void SaveTexture()

	{

		string fullPath = get_path();
		byte[] textureBytes = loadedTexture.EncodeToPNG();
		File.WriteAllBytes(fullPath, textureBytes);
		NativeGallery.Permission permission = NativeGallery.SaveImageToGallery(
			fullPath, "GalleryMaker", Path.GetFileName(fullPath), (success, path) => Debug.Log("Media save! result: " + success + " " + path)
			);
		Debug.Log("Permission result: " + permission);
	}


	private void Register()


	{
		int intHash = this.gameObject.GetComponent<Item>().getHash();
		if (is_registered(intHash) == false)
		{
			Debug.Log("Start Register");
			GameObject decalhash = Instantiate(Resources.Load("Decal Hash", typeof(GameObject))) as GameObject;
			decalhash.transform.SetParent(FolderHash().transform);
			Debug.Log("INstantiated");
			P3dTextureHash textureHash = decalhash.GetComponent<P3dTextureHash>();
			Debug.Log("tex hash created");
			textureHash.Texture = loadedTexture;
			//Debug.Log("Texture assigned " + hash);
			//int intHash = this.gameObject.GetComponent<Item>().getHash();
			Debug.Log("hash" + intHash.ToString());
			textureHash.Hash = new P3dHash(intHash);
			Debug.Log("Key assigned");
		}
	}

	private void Unregister()
	
	{
		int intHash = this.gameObject.GetComponent<Item>().getHash();
		if (is_registered(intHash) == true)
		{
			Debug.Log("Start Unregister");
			Destroy(get_registration(intHash));
			Debug.Log("End Unregister");
		}
	}
	
	public bool IsInstanceOfPrefabWithName(GameObject obj, string name)
	{
		TwinPrefab twinPrefab = obj.GetComponent<TwinPrefab>();
		if (twinPrefab != null)
		{
			return twinPrefab.IsInstanceOfPrefabWithName(name);
		}
		return false;
	}

	private bool is_registered(int hash)


	{
		for (int j = 0; j < FolderHash().transform.childCount; j++) {

			GameObject child = FolderHash().transform.GetChild(j).gameObject;
			if (IsInstanceOfPrefabWithName(child, "Decal Hash"))
            {
				P3dTextureHash hashPrefab = child.GetComponent<P3dTextureHash>();

				if (hashPrefab.Hash.ToString() == hash.ToString())
				{
					Debug.Log("found");
					return true;
				};
            }

		}
		Debug.Log("not found");
		return false;
	}

	private GameObject get_registration(int hash)


	{
		for (int j = 0; j < FolderHash().transform.childCount; j++) {

			GameObject child = FolderHash().transform.GetChild(j).gameObject;
			if (IsInstanceOfPrefabWithName(child, "Decal Hash"))
			{
				P3dTextureHash hashPrefab = child.GetComponent<P3dTextureHash>();

				if (hashPrefab.Hash.ToString() == hash.ToString())
				{
					Debug.Log("found");
					return child;
				};
			}

		}
		Debug.Log("not found");
		return null;
	}
	
	private string get_path()
	{   
		DataPersistenceManager dataManager = this.transform.parent.gameObject.GetComponent<StickerRepo>().dataManager;
		string profile = dataManager.selectedProfileId;
		return Path.Combine(Application.persistentDataPath,profile,
			this.gameObject.GetComponent<Item>().getId() + ".png");
		
	}
	
	
	public  void handleChange(string profile)
	{

	}
   
	public  void handleCopyChange(string profile)
	{
		Texture2D defaultTexture = this.transform.parent.gameObject.GetComponent<StickerRepo>().defaultTexture;
		if (loadedTexture != defaultTexture)
		{
			SaveTexture();
		}
	}
	public  void handleDelete(string profile)
	{
		P3dPaintableTexture.ClearSave(profile);
	}
	public GameObject relatedGameObject()
	{
		return this.gameObject;
	}
	
}


using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ItemHash : MonoBehaviour
{
    protected GameObject FolderHash()
    {
        return this.transform.parent.gameObject.GetComponent<StickerRepo>().folderHash;
    }

    public abstract void handleAwake();

}

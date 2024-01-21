using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ItemFile: MonoBehaviour
{
    public abstract void handleChange(string profile);
    public abstract void handleDelete(string profile);
}

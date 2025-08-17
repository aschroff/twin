using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ItemFile
{
    public abstract void handleCopyChange(string profile);
    public abstract void handleChange(string profile);
    public abstract void handleDelete(string profile);
}

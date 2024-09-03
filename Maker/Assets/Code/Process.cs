using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProcessResult
{
   public int code = 0;

}

public abstract class Process : MonoBehaviour
{
   public abstract ProcessResult execute();

}

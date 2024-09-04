using System.Collections;
using System.Collections.Generic;
using Code;
using UnityEngine;

public class ProcessResult
{
   public int code = 0;

}

public abstract class Process : MonoBehaviour
{
   
   public abstract ProcessResult Execute();

   private ProcessManager processManager()
   {
      return this.gameObject.transform.parent.gameObject.GetComponent<ProcessManager>();
   }

   protected ViewManager getViewmanager()
   {
      return processManager().viewManager;
   }
   protected StandardViewManager getStandardViewManager()
   {
      return processManager().standarViewManager;
   }
   
   protected Recorder getRecorder()
   {
      return processManager().recorder;
   }
   
   protected DataPersistenceManager getDataManager()
   {
      return processManager().dataManager;
   }

   public void Handle()
   {
      Execute();
   }
}

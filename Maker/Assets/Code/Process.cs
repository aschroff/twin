using System.Collections;
using System.Collections.Generic;
using Code;
using Code.AI;
using Lean.Gui;
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

   protected Body getBody()
   {
      return processManager().body;
   }
   
   protected LeanPulse getNotification()
   {
      return processManager().notification;
   }

   protected AI getAI()
   {
      return processManager().ai;
   }
   
   public void Handle()
   {
      Execute();
   }
}

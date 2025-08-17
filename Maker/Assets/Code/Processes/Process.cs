using Lean.Gui;
using UnityEngine;

namespace Code.Processes
{
   public class ProcessResult
   {
      public int code = 0;

   }

   public abstract class Process : MonoBehaviour
   {

   
      public abstract ProcessResult Execute(string variant = "");
   


      protected ProcessManager processManager()
      {
         return this.gameObject.GetComponentInParent<ProcessManager>();;
      }

      protected ViewManager getViewmanager()
      {
         return processManager().viewManager;
      }
      protected StandardViewManager getStandardViewManager()
      {
         return processManager().standardViewManager;
      }
   
      protected Recorder getRecorder()
      {
         return processManager().recorder;
      }
   
      protected DataPersistenceManager getDataManager()
      {
         return processManager().dataManager;
      }

      protected Body.Body getBody()
      {
         return processManager().body;
      }
   
      protected LeanPulse getNotification()
      {
         return processManager().notification;
      }

      protected AI.AI getAI()
      {
         return processManager().ai;
      }
   
      protected PartManager getPartManager()
      {
         return processManager().partManager;
      }
   
      protected SettingsManager getSettingsManager()
      {
         return processManager().settingsManager;
      }
   
      public void Handle(string variant = "")
      {
         Debug.Log("Process Status: Handle");
         Execute(variant);
      }
   }
}
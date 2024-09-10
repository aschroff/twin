using UnityEngine;
using Lean.Gui;

namespace Code
{
    public class ProcessManager: MonoBehaviour
    {
        [SerializeField] public ViewManager viewManager;
        [SerializeField] public StandardViewManager standarViewManager;
        [SerializeField] public Recorder recorder;
        [SerializeField] public DataPersistenceManager dataManager;
        [SerializeField] public Body body;
        [SerializeField] public LeanPulse notification;
    }
}
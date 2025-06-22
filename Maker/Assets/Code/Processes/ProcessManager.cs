using Lean.Gui;
using UnityEngine;

namespace Code.Processes
{
    public class ProcessManager: MonoBehaviour
    {
        [SerializeField] public ViewManager viewManager;
        [SerializeField] public StandardViewManager standardViewManager;
        [SerializeField] public Recorder recorder;
        [SerializeField] public DataPersistenceManager dataManager;
        [SerializeField] public Body.Body body;
        [SerializeField] public LeanPulse notification;
        [SerializeField] public AI.AI ai;
        [SerializeField] public PartManager partManager;
        [SerializeField] public SettingsManager settingsManager;
    }
}
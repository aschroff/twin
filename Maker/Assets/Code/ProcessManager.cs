using UnityEngine;

namespace Code
{
    public class ProcessManager: MonoBehaviour
    {
        [SerializeField] public ViewManager viewManager;
        [SerializeField] public StandardViewManager standarViewManager;
        [SerializeField] public Recorder recorder;
        [SerializeField] public DataPersistenceManager dataManager;
    }
}
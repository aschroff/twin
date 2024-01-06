using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;

public class FileManager : MonoBehaviour
{
    [SerializeField] public DataPersistenceManager dataManager;
    private Dictionary<string, string> profiles = new Dictionary<string, string>();

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

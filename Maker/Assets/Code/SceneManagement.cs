using UnityEngine;
using System.Collections.Generic;
public class SceneManagement: MonoBehaviour
{
    [System.Serializable]
    public class View  {
        public string name;
        public float positionCamera_x;
        public float positionCamera_y;
        public float positionCamera_z;
        public float sizeCamera;
        public float pitch;
        public float yaw;
        
    }
    [SerializeField] public List<View> views = new List<View>();
    [SerializeField] public GameObject mainCamera;
    [SerializeField] public GameObject body;
    
}
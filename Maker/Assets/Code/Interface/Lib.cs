using System.Collections;
using System.Collections.Generic;
using UnityEngine;




public class Lib : MonoBehaviour
{
    public GameObject cube;
    public float zoomSpeed = 0.3f;
    public float rotationspeed;
    public Material materialNew;
    public Camera cam;

    public Material matFinding;
    private GameObject currentFinding;

    void Start()
    {
        MeshCollider mf = (MeshCollider) cube.GetComponent(typeof(MeshCollider));
        Debug.Log(mf.sharedMesh.vertices.Length);
    }

    public void SaveTexture(Texture2D tex)
    {

        //then Save To Disk as PNG
        byte[] bytes = tex.EncodeToPNG();
        var dirPath = "D:\\dev\\temp\\SaveImages";
        if (!System.IO.Directory.Exists(dirPath))
        {
            System.IO.Directory.CreateDirectory(dirPath);
        }
        System.IO.File.Delete(dirPath + "\\Image" + ".png");
        System.IO.File.WriteAllBytes(dirPath + "\\Image" + ".png", bytes);
    }

    public GameObject DrawFinding(Vector3 x1, Vector3 x2, Vector3 x3)
    {
        GameObject finding = new GameObject("FindingDrawn");
        finding.transform.SetParent(cube.transform);


        MeshRenderer meshRendererFinding = finding.AddComponent<MeshRenderer>();
        meshRendererFinding.material = matFinding;

        MeshFilter meshFilterFinding = finding.AddComponent<MeshFilter>();


        Mesh meshFinding = new Mesh();
        meshFinding.vertices = new Vector3[] { x1, x2, x3 };
        meshFinding.triangles = new int[] { 0, 1, 2 };


        meshFilterFinding.mesh = meshFinding;
        (finding.AddComponent(typeof(MeshCollider)) as MeshCollider).sharedMesh = meshFinding;


        meshFinding.RecalculateBounds();
        meshFinding.RecalculateNormals();
        return (finding);
    }

    public void AddToFinding(Vector3 x1, Vector3 x2, Vector3 x3)
    {
        MeshFilter FilterCurrentFinding = currentFinding.GetComponent<MeshFilter>();
        Mesh meshCurrentFinding = FilterCurrentFinding.mesh;

        GameObject finding = new GameObject("FindingDrawnTemp");
        finding.transform.SetParent(currentFinding.transform);

        MeshRenderer meshRendererFinding = finding.AddComponent<MeshRenderer>();
        meshRendererFinding.material = matFinding;

        MeshFilter meshFilterFinding = finding.AddComponent<MeshFilter>();


        Mesh meshAdd = new Mesh();
        meshAdd.vertices = new Vector3[] { x1, x2, x3 };
        meshAdd.triangles = new int[] { 0, 1, 2 };

        meshFilterFinding.mesh = meshAdd;
        (finding.AddComponent(typeof(MeshCollider)) as MeshCollider).sharedMesh = meshAdd;

        CombineInstance[] combine = new CombineInstance[2];

        combine[0].mesh = meshCurrentFinding;
        combine[0].transform = FilterCurrentFinding.transform.localToWorldMatrix;

        combine[1].mesh = meshAdd;
        combine[1].transform = meshFilterFinding.transform.localToWorldMatrix;
        meshFilterFinding.gameObject.SetActive(false);


        Mesh tempMesh = new Mesh();
        tempMesh.CombineMeshes(combine);
        FilterCurrentFinding.mesh = tempMesh;

        meshCurrentFinding.RecalculateBounds();
        meshCurrentFinding.RecalculateNormals();

    }

    public void ProcessFinding(RaycastHit hit, Mesh mesh, bool add)
    {

        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        Vector3 p0 = vertices[triangles[hit.triangleIndex * 3 + 0]];
        Vector3 p1 = vertices[triangles[hit.triangleIndex * 3 + 1]];
        Vector3 p2 = vertices[triangles[hit.triangleIndex * 3 + 2]];



        Transform hitTransform = hit.collider.transform;
        Vector3 p0n = hitTransform.TransformPoint(p0);
        Vector3 p1n = hitTransform.TransformPoint(p1);
        Vector3 p2n = hitTransform.TransformPoint(p2);

        if (add)
        {
            AddToFinding(p0n, p1n, p2n);
        }
        else
        {
            currentFinding = DrawFinding(p0n, p1n, p2n);
        }
    }

    public void PaintFinding(float p0x, float p0y, float p1x, float p1y, float p2x, float p2y)
    {

        SkinnedMeshRenderer twin = cube.GetComponent(typeof(SkinnedMeshRenderer)) as SkinnedMeshRenderer;

        Material material;

        if (twin != null)
        {
            material = twin.materials[0];
            if (material != null)
            {
                Texture2D skin = (Texture2D)material.mainTexture;

                Texture2D textureFinding = (Texture2D)materialNew.mainTexture;

                int startX = (int)((System.Math.Min(p0x, System.Math.Min(p1x, p2x))) * skin.width - textureFinding.width / 2);
                int startY = (int)((System.Math.Min(p0y, System.Math.Min(p1y, p2y))) * skin.height - textureFinding.height / 2);
                Texture2D skinnew = AddFindingTexture(skin, textureFinding, startX, startY);
                twin.materials[0].mainTexture = skinnew;
                SaveTexture(skinnew);
            };
        }

    }

    // Update is called once per frame
    void Update()
    {

        if (Input.GetAxis("Mouse ScrollWheel") > 0)
        {
            Debug.Log("Lib - Mouse ScrollWheel");
            cam.fieldOfView--;
        }
        if (Input.GetAxis("Mouse ScrollWheel") < 0)
        {
            Debug.Log("Lib - Mouse ScrollWheel");
            cam.fieldOfView++;
        }


        if ((Input.touchCount == 1) && (Input.GetTouch(0).tapCount < 2))
        {
            Debug.Log("Lib - touchCount == 1");
            Touch touch = Input.GetTouch(0);

            Vector3 Rotation = new Vector3();
            Rotation.y = -1 * touch.deltaPosition.x;
            this.transform.Rotate(Rotation * Time.deltaTime * rotationspeed);



        }
        else if (Input.touchCount == 2)
        {
            Debug.Log("Lib - touchCount == 2");
            Touch touch1 = Input.GetTouch(0);
            Touch touch2 = Input.GetTouch(1);
            Vector2 Touch1PrevPos = touch1.position - touch1.deltaPosition;
            Vector2 Touch2PrevPos = touch2.position - touch2.deltaPosition;

            float prevTouchDeltaMag = (Touch1PrevPos - Touch2PrevPos).magnitude;
            float touchDeltaMag = (touch1.position - touch2.position).magnitude;

            float deltaMagDif = prevTouchDeltaMag - touchDeltaMag;

            cam.fieldOfView += deltaMagDif * zoomSpeed;
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, 22f, 130f);

        }

        else if ((Input.GetMouseButtonDown(2)) | ((Input.touchCount == 1) && (Input.GetTouch(0).tapCount == 2)))
        {
            Debug.Log("Lib - GetMouseButtonDown(2)");



            RaycastHit hit;
            if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out hit))
            {
                return;
            }

            MeshCollider meshCollider = hit.collider as MeshCollider;
            if (meshCollider == null || meshCollider.sharedMesh == null)
                return;


            Mesh mesh = meshCollider.sharedMesh;

            ProcessFinding(hit, mesh, false);


            float p0x = mesh.uv[mesh.triangles[hit.triangleIndex * 3 + 0]].x;
            float p0y = mesh.uv[mesh.triangles[hit.triangleIndex * 3 + 0]].y;
            float p1x = mesh.uv[mesh.triangles[hit.triangleIndex * 3 + 1]].x;
            float p1y = mesh.uv[mesh.triangles[hit.triangleIndex * 3 + 1]].y;
            float p2x = mesh.uv[mesh.triangles[hit.triangleIndex * 3 + 2]].x;
            float p2y = mesh.uv[mesh.triangles[hit.triangleIndex * 3 + 2]].y;

            if (Input.GetKey("b"))
            {
                PaintFinding(p0x, p0y, p1x, p1y, p2x, p2y);
            }

            
        }
        else if (Input.GetMouseButton(2))
        {
            Debug.Log("Lib - GetMouseButton(2)");
            Debug.Log(Input.mousePosition);

            RaycastHit hit;
            if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out hit))
            {
                return;
            }

            MeshCollider meshCollider = hit.collider as MeshCollider;
            if (meshCollider == null || meshCollider.sharedMesh == null)
                return;


            Mesh mesh = meshCollider.sharedMesh;

            ProcessFinding(hit, mesh, true);
        }
    }
    public static Texture2D AddFindingTexture(Texture2D skin, Texture2D finding, int startPositionX, int startPositionY)
    {
        Texture2D newTex = new Texture2D(skin.height, skin.width, TextureFormat.RGBA32, false);
        //Copy old texture pixels into new one
        newTex.SetPixels(skin.GetPixels());



        //only read and rewrite the area of the watermark
        for (int x = startPositionX; x < newTex.width; x++)
        {
            for (int y = startPositionY; y < newTex.height; y++)
            {
                if (x - startPositionX < finding.width && y - startPositionY < finding.height)
                {
                    var bgColor = newTex.GetPixel(x, y);
                    var wmColor = finding.GetPixel(x - startPositionX, y - startPositionY);

                    var finalColor = Color.Lerp(bgColor, wmColor, wmColor.a / 1.0f);

                    newTex.SetPixel(x, y, finalColor);
                }
            }
        }

        newTex.Apply();
        return newTex;
    }


}

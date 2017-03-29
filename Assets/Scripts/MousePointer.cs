using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VR;

public class MousePointer : MonoBehaviour
{

    public Text text;
    public GameObject objectsGroup;
    public GameObject snapPointObject;
    private GameObject snapsGroup;
    private Camera mainCamera;
    private Material material;
    private bool dragging = false;
    private float pointerZ;
    private float defaultPointerZ = 5;
    private GameObject draggedObject;
    private List<Snap> snaps;
    private Vector3 hitPoint;
    private Vector3 hitNormal;
    private Transform hitTransform;
    private bool flipNormal = false;
    public bool doVertexSnaps;
    public bool dontRayWhenDragging;

    public float snapDistance = 5;  // snap radius in pixels

    struct Snap
    {
        public Vector3 position;
        public Vector3 normal;
    }

    void Awake()
    {
    }

    void Start()
    {
        mainCamera = transform.parent.GetComponent<Camera>();
        material = gameObject.GetComponent<Renderer>().sharedMaterial;
        VRSettings.showDeviceView = true;
        snaps = GetUserSnaps();
        pointerZ = defaultPointerZ;
        //Screen.SetResolution(VRSettings.eyeTextureWidth, VRSettings.eyeTextureHeight, false);
        //Debug.Log((" width: " + VRSettings.eyeTextureWidth + "  height: " + VRSettings.eyeTextureHeight));
    }

    void Update()
    {
        bool rayDidHit = false;
        if (!(dragging && dontRayWhenDragging))
        {
            // Ray against objects in the scene.
            RaycastHit rayCastHit;
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            rayDidHit = Physics.Raycast(ray, out rayCastHit, 1000);
            if (rayDidHit)
            {
                hitPoint = rayCastHit.point;
                hitNormal = rayCastHit.normal;
                hitTransform = rayCastHit.transform;
                pointerZ = mainCamera.WorldToScreenPoint(hitPoint).z;
                transform.position = hitPoint;
                transform.up = flipNormal ? -hitNormal : hitNormal;
            }
        }

        // Snap to closest snap in screenspace, if any within radius.
        var adjustedMousePosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, pointerZ);
        foreach (var snap in snaps)
        {
            var screenPosition = mainCamera.WorldToScreenPoint(snap.position);
            if (!(Mathf.Pow(Input.mousePosition.x - screenPosition.x, 2) +
                  Mathf.Pow(Input.mousePosition.y - screenPosition.y, 2) < snapDistance * snapDistance)) continue;
            adjustedMousePosition = screenPosition;
            pointerZ = screenPosition.z;
            transform.up = dragging ? snap.normal : -snap.normal;   // simulate male-female
            transform.up = flipNormal ? -transform.up : transform.up;
            break;
        }
        transform.position = mainCamera.ScreenToWorldPoint(adjustedMousePosition);


        if (dragging)
        {
            // When dragging, RMB flips the dragged object along the pointer normal.
            if (Input.GetMouseButtonDown(1))
            {
                flipNormal = !flipNormal;
            }

            // Dragged object released.
            if (Input.GetMouseButtonUp(0))
            {
                dragging = false;
                flipNormal = false;
                draggedObject.transform.parent = objectsGroup.transform;
                draggedObject.GetComponent<Collider>().enabled = true;  // re-enable collider
                DestroyVertexSnapsGroup();
                snaps = GetUserSnaps();
            }
        }
        else  // not dragging
        {
            if (rayDidHit)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    dragging = true;
                    draggedObject = hitTransform.gameObject;
                    draggedObject.GetComponent<Collider>().enabled = false;  // disable collider -> disables raycasting against this object
                    hitTransform.parent = transform; // parent object to pointer

                    snaps = new List<Snap>();
                    if (doVertexSnaps)
                    {
                        snaps.AddRange(GetVertexSnaps());
                    }
                    snaps.AddRange(GetUserSnaps());

                    material.color = new Color(0.11f, 0.88f, 0.09f, 0.62f);
                }
                else
                {
                    material.color = new Color(0.63f, 0.89f, 0.56f, 0.58f);
                }

                // Create snap point at pointer.
                if (Input.GetMouseButtonUp(1))
                {
                    var snap = new Snap();
                    snap.position = hitPoint;
                    snap.normal = hitNormal;
                    var snapObject = InstantiateSnapObject(snap);
                    snapObject.transform.parent = hitTransform;
                    snaps = GetUserSnaps();
                }
            }
            else  // no ray hit, not dragging
            {
                pointerZ = defaultPointerZ;
                transform.position = mainCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, pointerZ));
                transform.forward = Vector3.up;
                material.color = new Color(0, 0, 0, .3f);
            }
        }

        //text.text = (" width: " + VRSettings.eyeTextureWidth + "  height: " + VRSettings.eyeTextureHeight);
        text.text = ("x: " + Input.mousePosition.x + " y: " + Input.mousePosition.y);
    }

    // Generates snaps at vertices, on-the-fly.
    private List<Snap> GetVertexSnaps()
    {
        var meshFilters = new List<MeshFilter>();
        foreach (Transform child in objectsGroup.transform)
        {
            var meshFilter = child.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                meshFilters.Add(meshFilter);
            }
        }
        var vertexSnaps = new List<Snap>();
        foreach (var meshFilter in meshFilters)
        {
            var tmpTransform = meshFilter.transform;
            var vertices = meshFilter.mesh.vertices;
            var normals = meshFilter.mesh.normals;
            for (int i = 0; i < vertices.Length; i++)
            {
                var snap = new Snap();
                snap.position = tmpTransform.TransformPoint(vertices[i]);
                snap.normal = tmpTransform.TransformVector(normals[i]);
                vertexSnaps.Add(snap);
            }
        }
        DestroyVertexSnapsGroup();
        snapsGroup = new GameObject();
        foreach (var snap in vertexSnaps)
        {
            var snapObject = InstantiateSnapObject(snap);
            snapObject.transform.parent = snapsGroup.transform;
        }

        return vertexSnaps;
    }

    // Gathers all valid user-created snaps from object in the scene.
    private List<Snap> GetUserSnaps()
    {
        var userSnaps = new List<Snap>();
        var snapObjects = objectsGroup.GetComponentsInChildren<SnapObject>();
        foreach (var snapObject in snapObjects)
        {
            var snap = new Snap();
            snap.position = snapObject.transform.position;
            snap.normal = snapObject.transform.up;
            userSnaps.Add(snap);
        }

        return userSnaps;
    }

    private void DestroyVertexSnapsGroup()
    {
        if (snapsGroup != null)
        {
            DestroyImmediate(snapsGroup);
        }
    }

    private GameObject InstantiateSnapObject(Snap snap)
    {
        var snapObject = Instantiate(snapPointObject) as GameObject;
        snapObject.transform.position = snap.position;
        snapObject.transform.up = snap.normal;
        return snapObject;
    }
}

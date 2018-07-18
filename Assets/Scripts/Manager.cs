#region Copyright

// Copyright 2018 Shawn Slater
// The primary script for managing
// and rendering the data.
// File: Manager.cs

#endregion

#region Includes

using UnityEngine;
using System;
using System.Collections;

#endregion

public class Manager : MonoBehaviour {


    #region Constants
    

    public bool isDataLocal;

    public static readonly Matrix4x4[] faceMatrices = new Matrix4x4[]{
        new Matrix4x4(new Vector4(0, 0, -1, 0), new Vector4(0, 1, 0, 0), new Vector4(-1, 0, 0, 0), new Vector4(0, 0 ,0, 1)),
        new Matrix4x4(new Vector4(0, 0, 1, 0), new Vector4(0, 1, 0, 0), new Vector4(1, 0, 0, 0), new Vector4(0, 0, 0, 1)),
        new Matrix4x4(new Vector4(1, 0, 0, 0), new Vector4(0, 0, -1, 0), new Vector4(0, -1, 0, 0), new Vector4(0, 0, 0, 1)),
        new Matrix4x4(new Vector4(1, 0, 0, 0), new Vector4(0, 0, 1, 0), new Vector4(0, 1, 0, 0), new Vector4(0, 0, 0, 1)),
        new Matrix4x4(new Vector4(1, 0, 0, 0), new Vector4(0, 1, 0, 0), new Vector4(0, 0, -1, 0), new Vector4(0, 0, 0, 1)),
        new Matrix4x4(new Vector4(-1, 0, 0, 0), new Vector4(0, 1, 0, 0), new Vector4(0, 0, 1, 0), new Vector4(0, 0, 0, 1))
    };


    #endregion

    #region Fields

    private GameObject CameraSets;
    private bool DataLoaded;
    private Vector3Int nodePosition;
    public RenderTexture[] renderTex;
    
    public ComputeShader HoleFill;
    public ComputeShader CFlatten;

    private int _updateKernel;
    private int _updateKernel2;
    
    public Node[] curSet;
    private Node cNode;
    
    public Node[,,] node;
    private float[] delta = new float[3];
    
    private bool rendered;
    private bool firstRun;

    //private string dataLocation;

    public Session Session { get; private set; }

    public GameObject meshObject;
    public Camera MainCamera { get; private set; }
    private Camera[] nodeCameras;

    private Vector3 cameraPosition;
    public GameObject LoadingSlider;
    public static Manager instance;

    public string dataPath;
    #endregion


    #region Methods


    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this);
        }
        else
        {
            Destroy(this);
        }
    }

    // Use this for initialization
    private IEnumerator Start () {

        //dataLocation = string.Empty;
        SetLoadingUI(false);
        yield return (StartSession());

    }


    IEnumerator StartSession()
    {
        DataLoaded = false;
        
        firstRun = true;

        // Test Location
        string sessionName = "The Room";

        dataPath = (isDataLocal) ? Application.dataPath + "/Resources/" : "http://www.animagin.com/VILE/Sessions/";
        dataPath += sessionName + "/";

        Session = new Session();
        SetLoadingUI(true);
        
        // Load in session data file
        // 
        yield return Session.LoadSession();

        Debug.Log("Session Loaded");
        SetLoadingUI(false);

        DataLoaded = true;
    }
    


    public bool SetCameraData()
    {

        if (!CameraSets)
        {
            CameraSets = GameObject.FindGameObjectWithTag("MainCamera");
            if (!CameraSets)
            {
                Debug.LogError("Missing camera sets."); return false;
            }

            // assign main camera
            MainCamera = Camera.main;
            if (!MainCamera)
            {
                Debug.LogError("Missing primary camera."); return false;
            }
        }


        int camCount = CameraSets.transform.childCount;
        
        // assign node cameras
        if (camCount == 8)
        {
            nodeCameras = new Camera[camCount];
            if (Session != null)
            {
                renderTex = new RenderTexture[9];
                Vector2Int ImageSize = Session.ImageSize;
                for (int i = 0; i < 8; ++i)
                {
                    renderTex[i] = new RenderTexture(ImageSize.x, ImageSize.y, 0)
                    {
                        format = RenderTextureFormat.ARGB32,
                        anisoLevel = 0,
                        enableRandomWrite = true
                    };
                    if (!renderTex[i].Create())
                    {
                        Debug.LogError("Render texture creation error");
                        return false;
                    }
                    nodeCameras[i] = CameraSets.transform.GetChild(i).GetComponent<Camera>();
                    nodeCameras[i].clearFlags = CameraClearFlags.SolidColor;
                    nodeCameras[i].backgroundColor = new Color(0, 1, 0, 0);
                    nodeCameras[i].renderingPath = RenderingPath.DeferredShading;
                    nodeCameras[i].useOcclusionCulling = true;
                    nodeCameras[i].allowHDR = true;
                    nodeCameras[i].targetDisplay = 0;
                    nodeCameras[i].fieldOfView = Session.CameraProperties.FieldOfView;
                    nodeCameras[i].nearClipPlane = Session.CameraProperties.NearPlane;
                    nodeCameras[i].farClipPlane = Session.CameraProperties.FarPlane;
                    nodeCameras[i].targetTexture = renderTex[i];
                }

                renderTex[8] = new RenderTexture(ImageSize.x, ImageSize.y, 0)
                {
                    format = RenderTextureFormat.ARGB32,
                    anisoLevel = 0,
                    enableRandomWrite = true
                };
                if (!renderTex[8].Create())
                {
                    Debug.LogError("Render texture creation error");
                    return false;
                }

                MainCamera.clearFlags = CameraClearFlags.SolidColor;
                MainCamera.backgroundColor = new Color(0, 0, 0, 0);
                MainCamera.renderingPath = RenderingPath.Forward;
                MainCamera.useOcclusionCulling = true;
                MainCamera.allowHDR = true;
                MainCamera.targetDisplay = 0;
                MainCamera.fieldOfView = Session.CameraProperties.FieldOfView;
                MainCamera.nearClipPlane = Session.CameraProperties.NearPlane;
                MainCamera.farClipPlane = Session.CameraProperties.FarPlane;

            }
            else
            {
                Debug.LogError("Session data does not exist."); return false;
            }
        }
        else
        {
            Debug.LogError("Node camera count should be 8. Current number is: " + nodeCameras.Length + "."); return false;
        }
        
        return true;
    }


    private void SetLoadingUI(bool set)
    {
        if (LoadingSlider)
        {
            LoadingSlider.SetActive(set);
        }
    }

    // Where in our voxel grid set in the user
    bool MovedNodePosition()
    {

        // Get Camera position
        Vector3 p = MainCamera.transform.localPosition;
        if (p != cameraPosition)
        {
            cameraPosition = new Vector3(p.x, p.y, p.z);
            return true;
        }
        else
        {
            return false;
        }
        // Check The Camera position placement in the voxel grid

        
    }

    bool CheckData(bool positionExists)
    {
        if (!positionExists) return false;


        return true;
    }


    // The real meat of the application.
    // Here we will render out the data needed for the viewer
    public bool RenderData()
    {
        rendered = false;
        Vector2Int size = Session.ImageSize;
        GetNewNodes();

        Resolution res = Screen.currentResolution;
        Screen.SetResolution(size.x, size.y, false);

        if(nodeCameras != null)
        {
            for (int i = 0; i < 8; ++i)
            {
                if (nodeCameras[i] != null)
                {
                    //nodeCameras[i].Render();
                    FillHoles(i);
                }
                else
                {
                    Debug.LogError("Camera " + i + " cannot be rendered.");
                }
            }
        }
        else
        {
            Debug.LogError("No Render Cameras.");
        }
       

        Screen.SetResolution(res.width, res.height, false);
        
        //flatten
        Vector3 camDiff = (CameraSets.transform.localPosition - curSet[0].pos) * Session.NodeSpacing;
        delta[0] = Mathf.Abs(camDiff.x);
        delta[1] = Mathf.Abs(camDiff.y);
        delta[2] = Mathf.Abs(camDiff.z);
        
        if (!Flatten(ref delta))
        {
            Debug.LogError("Flatten error.");
            return false;
        }

        // fill final holes
        if (!FillHoles(8))
        {
            Debug.LogError("Final fill hole error.");
            return false;
        }
            
        rendered = true;
        return rendered;
    }


    void GetNewNodes()
    {
        // Get the position of the camera / user

        Vector3 pos = MainCamera.transform.localPosition;

        #region Boundary Check
        float buffer = 0.2F;
        if (pos.x < Session.BBox.xMin + buffer)
            pos = new Vector3(Session.BBox.xMin + buffer, pos.y, pos.z);
        if (pos.x > Session.BBox.xMax - buffer)
            pos = new Vector3(Session.BBox.xMax - buffer, pos.y, pos.z);

        if (pos.y < Session.BBox.yMin + buffer)
            pos = new Vector3(pos.x, Session.BBox.yMin + buffer, pos.z);
        if (pos.y > Session.BBox.yMax - buffer)
            pos = new Vector3(pos.x, Session.BBox.yMax - buffer, pos.z);

        if (pos.z < Session.BBox.zMin + buffer)
            pos = new Vector3(pos.x, pos.y, Session.BBox.zMin + buffer);
        if (pos.z > Session.BBox.zMax - buffer)
            pos = new Vector3(pos.x, pos.y, Session.BBox.zMax - buffer);
        #endregion

        // what is the closest node to us?
        float s = Session.NodeSpacing;
        Vector3Int P = new Vector3Int((int)((pos.x - Session.BBox.xMin) * s), (int)((pos.y - Session.BBox.yMin) * s), (int)((pos.z - Session.BBox.zMin) * s));
       
        cNode = node[P.x, P.y, P.z]; // closest node

        Node[] oldSet = new Node[8];

        if (!firstRun)
        {
            for (int i = 0; i < 8; i++)
            {
                oldSet[i] = curSet[i];
            }
        }

        // where are we in relation to the closest node? 
        // If the value is negative then behing that axis, otherwise ahead
        // Closest node is always one of 8 corners in its cube area

        Vector3 r = transform.position - cNode.pos;

        // Optimization
        int pxp1 = P.x + 1;
        int pxm1 = P.x - 1;
        int pyp1 = P.y + 1;
        int pym1 = P.y - 1;
        int pzp1 = P.z + 1;
        int pzm1 = P.z - 1;

        #region node position assignment
        if (r.x > 0f) // Check right nodes
        {
            if (r.y > 0f) // Check up nodes
            {
               
                if (r.z > 0f) // Check front nodes +++
                {
                    curSet[0] = cNode;
                    curSet[1] = node[pxp1, P.y, P.z];
                    curSet[2] = node[P.x, P.y, pzp1];
                    curSet[3] = node[pxp1, P.y, pzp1];
                    curSet[4] = node[P.x, pyp1, P.z];
                    curSet[5] = node[pxp1, pyp1, P.z];
                    curSet[6] = node[P.x, pyp1, pzp1];
                    curSet[7] = node[pxp1, pyp1, pzp1];
                }
                else // Check back nodes ++-
                {
                    curSet[0] = node[P.x, P.y, pzm1];
                    curSet[1] = node[pxp1, P.y, pzm1];
                    curSet[2] = cNode;
                    curSet[3] = node[pxp1, P.y, P.z];
                    curSet[4] = node[pxp1, pyp1, pzm1];
                    curSet[5] = node[P.x, pyp1, pzm1];
                    curSet[6] = node[P.x, pyp1, P.z];
                    curSet[7] = node[pxp1, pyp1, P.z];
                }
            }
            else // Check down nodes 
            {
                if (r.z > 0f) // Check front nodes +-+
                {
                    curSet[0] = node[P.x, pym1, P.z];
                    curSet[1] = node[pxp1, pym1, P.z];
                    curSet[2] = node[P.x, pym1, pzp1];
                    curSet[3] = node[pxp1, pym1, pzp1];
                    curSet[4] = cNode;
                    curSet[5] = node[pxp1, P.y, P.z];
                    curSet[6] = node[P.x, P.y, pzp1];
                    curSet[7] = node[pxp1, P.y, pzp1];
                }
                else // Check back nodes +--
                {
                    curSet[0] = node[P.x, pym1, pzm1];
                    curSet[1] = node[pxp1, pym1, pzm1];
                    curSet[2] = node[P.x, pym1, P.z];
                    curSet[3] = node[pxp1, pym1, P.z];
                    curSet[4] = node[P.x, P.y, pzm1];
                    curSet[5] = node[pxp1, P.y, pzm1];
                    curSet[6] = cNode;
                    curSet[7] = node[pxp1, P.y, P.z];
                }
            }
        }
        else //checked left nodes
        {
            if (r.y > 0f) // Check up nodes
            {
                if (r.z > 0f)  // Check front nodes -++
                {
                    curSet[0] = node[pxm1, P.y, P.z];
                    curSet[1] = cNode;
                    curSet[2] = node[pxm1, P.y, pzp1];
                    curSet[3] = node[P.x, P.y, pzp1];
                    curSet[4] = node[pxm1, pyp1, P.z];
                    curSet[5] = node[P.x, pyp1, P.z];
                    curSet[6] = node[pxm1, pyp1, pzp1];
                    curSet[7] = node[P.x, pyp1, pzp1];
                }
                else  // Check back nodes -+- 
                {
                    curSet[0] = node[pxm1, P.y, pzm1];
                    curSet[1] = node[P.x, P.y, pzm1];
                    curSet[2] = node[pxm1, P.y, P.z];
                    curSet[3] = cNode;
                    curSet[4] = node[pxm1, pyp1, pzm1];
                    curSet[5] = node[P.x, pyp1, pzm1];
                    curSet[6] = node[pxm1, pyp1, P.z];
                    curSet[7] = node[P.x, pyp1, P.z];

                }
            }
            else // Check down nodes
            {
                if (r.z > 0f)  // Check front nodes --+
                {
                    curSet[0] = node[pxm1, pym1, P.z];
                    curSet[1] = node[P.x, pym1, P.z];
                    curSet[2] = node[pxm1, pym1, pzp1];
                    curSet[3] = node[P.x, pym1, pzp1];
                    curSet[4] = node[pxm1, P.y, P.z];
                    curSet[5] = cNode;
                    curSet[6] = node[pxm1, P.y, pzp1];
                    curSet[7] = node[P.x, P.y, pzp1];
                }
                else  // Check back nodes ---
                {
                    curSet[0] = node[pxm1, pym1, pzm1];
                    curSet[1] = node[P.x, pym1, pzm1];
                    curSet[2] = node[pxm1, pym1, P.z];
                    curSet[3] = node[P.x, pym1, P.z];
                    curSet[4] = node[pxm1, P.y, pzm1];
                    curSet[5] = node[P.x, P.y, pzm1];
                    curSet[6] = node[pxm1, P.y, P.z];
                    curSet[7] = cNode;
                }
            }
        }
        #endregion

        for (int i = 0; i < 8; ++i)
        {
            if (curSet[i].Loaded)
            { // if loaded then show the node

                curSet[i].SwitchLayer(i);
                curSet[i].NodeDisplay(true);
            }
            else
            {
                if (!curSet[i].Loading) // Is not loaded and not currently loading from system
                {
                    StartCoroutine(curSet[i].LoadNode(i));
                }
            }

        }



        // show only face pieces in current list that are needed based on camera rotation
        #region Face Check

        bool[] faceDisplay = new bool[6];
        Vector3 cRot = MainCamera.transform.rotation.eulerAngles;
        
        faceDisplay[0] = ((cRot.y > 345 || cRot.y < 195) && (cRot.x > 255 || cRot.x < 105));
        faceDisplay[1] = ((cRot.y > 165 || cRot.y < 15) && (cRot.x > 255 || cRot.x < 105));
        faceDisplay[2] = (cRot.x > 165 || cRot.x < 15);
        faceDisplay[3] = (cRot.x > 345 || cRot.x < 195);
        faceDisplay[4] = ((cRot.y > 255 || cRot.y < 105) && (cRot.x > 255 || cRot.x < 105));
        faceDisplay[5] = ((cRot.y > 75 && cRot.y < 285) && (cRot.x > 255 || cRot.x < 105));

        #endregion



        if (!firstRun)
        {
            if (oldSet == null)
                return;

            for (int i = 0; i < 8; ++i)
            {
                curSet[i].ShowFaces(ref faceDisplay);
                bool avail = false;
                for (int j = 0; j < 8; ++j)
                {
                    
                    if (oldSet[i].id == curSet[j].id)
                    { // any old node in current set?
                        avail = true;
                    }
                }
                if (avail == false)
                {
                    oldSet[i].NodeDisplay(false);
                }
            }
        }
        else
        {
            for (int i = 0; i < 8; ++i)
            {
                curSet[i].ShowFaces(ref faceDisplay);

            }

            firstRun = false;
        }
        oldSet = null;
    }


    // Update is called once per frame
    // If session data is loaded then run the data
    void Update()
    {
        // If the data has been loaded then 
        if (DataLoaded)
        {
            if (rendered == true)
            {
                /*
                if (CheckData(MovedNodePosition())) // Read in needed files
                {
                   // RenderData();
                }
                */
                RenderData();
            }
        }
    }


    private void OnDisable()
    {
        if (renderTex != null) {
            for (int i = 0; i < 9; ++i)
            {
                if (renderTex[i] != null)
                    renderTex[i].Release();
            }
        }
    }


    void OnGUI()
    {
        if (DataLoaded)
        {
            if (!renderTex[8])
            {
                Debug.LogError("Assign a Texture in the inspector."); return;
            }
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), renderTex[8], ScaleMode.StretchToFill, false, Screen.width / Screen.height);
        }
    }

    #endregion


    // Below are the compute shading algorithms sent to the GPU
    #region Compute Shading

    bool FillHoles(int i)
    {
        int _updateKernel = HoleFill.FindKernel("HoleFill");
        if (_updateKernel == -1)
        {
            Debug.LogError("Failed to find HoleFill kernel");
            return false;
        }

        HoleFill.SetInt("size", Session.ImageSize.x);
        HoleFill.SetTexture(_updateKernel, "Pixels", renderTex[i]);
        HoleFill.Dispatch(_updateKernel, 1024, 1, 1);
        
        return true;
    }

    bool Flatten(ref float[] delta)
    {

        int _updateKernel = CFlatten.FindKernel("Flatten");
        if (_updateKernel == -1)
        {
            Debug.LogError("Failed to find Flatten kernel");
            return false;
        }

        CFlatten.SetFloats("delta", delta);
        if (renderTex == null)
        {
            Debug.LogError("ERROR - No render texture");
            return false;
        }
        else
        {
            CFlatten.SetTexture(_updateKernel, "t0", renderTex[0]);
            CFlatten.SetTexture(_updateKernel, "t1", renderTex[1]);
            CFlatten.SetTexture(_updateKernel, "t2", renderTex[2]);
            CFlatten.SetTexture(_updateKernel, "t3", renderTex[3]);
            CFlatten.SetTexture(_updateKernel, "t4", renderTex[4]);
            CFlatten.SetTexture(_updateKernel, "t5", renderTex[5]);
            CFlatten.SetTexture(_updateKernel, "t6", renderTex[6]);
            CFlatten.SetTexture(_updateKernel, "t7", renderTex[7]);
            CFlatten.SetTexture(_updateKernel, "Pixels", renderTex[8]);
            CFlatten.Dispatch(_updateKernel, 32, 32, 1);
            return true;
        }
    }
    #endregion

}

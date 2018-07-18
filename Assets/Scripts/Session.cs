#region Copyright

// Copyright 2018 Shawn Slater
// The script that handles session loading
// and storage management.
// File: Session.cs

#endregion

#region Includes

using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

#endregion


public struct CameraProperties
{
    public float FieldOfView;
    public float NearPlane;
    public float FarPlane;
    public Matrix4x4 projectionMatrix;
}

public struct BoundingBox
{
    public float xMin;
    public float xMax;
    public float yMin;
    public float yMax;
    public float zMin;
    public float zMax;
}

public class Session
{


    #region Fields
    public bool DisplayDebug = true;
    public int[] meshIndices;
    private Manager manager;
    #endregion

    #region Properties


    public string SessionName { get; private set; }
    public Vector2Int ImageSize { get; private set; }
    public Vector3Int GridDimensions { get; private set; }
    public int NodeCount { get; private set; }
    public Vector3Int UserGridPosition { get; set; }
    public bool[,,] NodeMatrix { get; private set; }
    public CameraProperties CameraProperties { get; private set; }
    public float NodeSpacing { get; private set; }
    public string AssetPath { get; private set; }

    public BoundingBox BBox { get; private set; }
    #endregion

    #region Methods
  

    public IEnumerator LoadSession()
    {
        manager = Manager.instance;
        AssetPath = manager.dataPath;

        // Make sure the Root location exists\
        if(manager.isDataLocal)
            if (!Directory.Exists(AssetPath))
                yield break;


        Debug.Log("AssetPath: " + AssetPath);

        if (DisplayDebug) Debug.Log("Loading session.dat file");

        // Get the session data
        yield return ParseSessionFile();
        
       
        // We will need to grab the file information from the directory to load them into the system
        // We are currently using .png files. We can alter this later for optimizations

        //Debug.LogError("Setting Camera Data");

        // Set cameras
        if (!manager.SetCameraData()) yield break;

        //Debug.LogError("Camera Data Set");
        yield return LoadNodes();
    }




    ///////////////
    /* Parse session.dat file should contain the following
     * 
     * Session name
     * //Session paths
     * Image size x,y
     * Number of nodes
     * NodeSet Dimensions (x,y,z)
     * Starting grid position
     * Octree of node locations (boolean location, change to bits later)
     */
    ///////////////

    private IEnumerator ParseSessionFile()
    {

        string sessionFile = AssetPath + "Session.dat";

        if (DisplayDebug) Debug.Log("sessionFile: " + sessionFile);
       
        
        // Stream in file
        using (WWW www = new WWW(sessionFile))
        {
            yield return www;
            sessionFile = www.text;
        }

        List<string> lines = new List<string>(
        sessionFile.Split(new string[] { "\r", "\n" },StringSplitOptions.RemoveEmptyEntries));
        // remove comment lines...
        lines = lines
            .Where(line => !(line.StartsWith("//")
                            || line.StartsWith("#")))
            .ToList();

        int lineCount = 0;
        string[] split;

        // Session name
        SessionName = lines[lineCount++];
        if (SessionName == string.Empty || SessionName.Length < 1)
        {
            Debug.LogError("Session file is empty");
            yield break;
        }
        else
        {
            if (DisplayDebug) Debug.Log("Session name: " + SessionName);
        }
        //


        // Camera properties
        // Set Camera Properties from imported data

        CameraProperties camProp = new CameraProperties();

        float fov = Convert.ToSingle(lines[lineCount++]);
        camProp.FieldOfView = fov;
        float near = Convert.ToSingle(lines[lineCount++]);
        camProp.NearPlane = near;
        float far = Convert.ToSingle(lines[lineCount++]);
        camProp.FarPlane = far;

        float ac = 1f / Mathf.Tan((fov * 0.5f) * (Mathf.PI / 180f));
        float bc = -far / (far - (near * 2f));
        float cc = -2f * (far * near) / (far - near);

        camProp.projectionMatrix = new Matrix4x4(new Vector4(ac, 0, 0, 0), new Vector4(0, ac, 0, 0), new Vector4(0, 0, bc, -1), new Vector4(0, 0, cc, 0));
        CameraProperties = camProp;
        //


        // Session paths
        //

        // Image size x,y
        split = lines[lineCount++].Split(',');
        if (split.Length != 2)
        {
            Debug.LogError("Session Parse Error: Image Size - " + split[0]);
            yield break;
        }

        ImageSize = new Vector2Int(Convert.ToInt32(split[0]),
                                    Convert.ToInt32(split[1]));

        if (DisplayDebug) Debug.Log("Image Size: " + ImageSize.ToString());
        //

        // Number of images
        NodeCount = Convert.ToInt32(lines[lineCount++]);
        if (NodeCount < 8) { yield break; }

        if (DisplayDebug) Debug.Log("Node Count: " + NodeCount);


        // We need to know how far apart to space these files
        NodeSpacing = 1.0f / Convert.ToSingle(lines[lineCount++]);
        //

        // Grid bounding box

        split = lines[lineCount++].Split(',');
        if (split.Length != 2) {  yield break; }
        float xmin = Convert.ToSingle(split[0]);
        float xmax = Convert.ToSingle(split[1]);

        split = lines[lineCount++].Split(',');
        if (split.Length != 2) { yield break; }
        float ymin = Convert.ToSingle(split[0]);
        float ymax = Convert.ToSingle(split[1]);

        split = lines[lineCount++].Split(',');
        if (split.Length != 2) { yield break; }
        float zmin = Convert.ToSingle(split[0]);
        float zmax = Convert.ToSingle(split[1]);

        BBox = new BoundingBox()
        {
            xMin = xmin,
            xMax = xmax,
            yMin = ymin,
            yMax = ymax,
            zMin = zmin,
            zMax = zmax
        };


        //

        // Dimensions of the grid x,y,z
        split = lines[lineCount++].Split(',');
        if (split.Length != 3) { yield break; }
        GridDimensions = new Vector3Int(Convert.ToInt32(split[0]),
                                        Convert.ToInt32(split[1]),
                                        Convert.ToInt32(split[2]));
        if (DisplayDebug) Debug.Log("Grid Dimensions: " + GridDimensions.ToString());
        //

        // Starting User Grid Position
        split = lines[lineCount++].Split(',');
        if (split.Length != 3) { yield break; }
        UserGridPosition = new Vector3Int(Convert.ToInt32(split[0]),
                                            Convert.ToInt32(split[1]),
                                            Convert.ToInt32(split[2]));

        if (DisplayDebug) Debug.Log("User Grid Position: " + UserGridPosition.ToString());
        //

        // Octree of node locations
        NodeMatrix = new bool[GridDimensions.x, GridDimensions.y, GridDimensions.z];

        for (int x = 0; x < GridDimensions.x; ++x)
        {
            for (int y = 0; y < GridDimensions.y; ++y)
            {
                //byte[] value = BitConverter.GetBytes(Convert.ToUInt64(reader.ReadLine()));

                // b = new BitArray(value);
                BitArray b = new BitArray(
                    BitConverter.GetBytes(Convert.ToUInt64(lines[lineCount++]))
                    );

                bool[] bits = new bool[b.Count];
                b.CopyTo(bits, 0);

                if (bits.Length < GridDimensions.z)
                {
                    Debug.LogError("Bit Length Error: " + bits.Length);
                    yield break;
                }
                for (int z = 0; z < GridDimensions.z; ++z)
                {
                    NodeMatrix[x, y, z] = bits[z];
                }
            }
        }


        // Set cached mesh index array
        int area = ImageSize.x * ImageSize.y;
        meshIndices = new int[area];
        for (int i = 0; i < area; i++)
        {
            meshIndices[i] = i;
        }
          
        yield return null;
    }


    

    private IEnumerator LoadNodes()
    {
        Manager m = Manager.instance;
        if (m == null) yield break;

        if (DisplayDebug) Debug.Log("Loading nodes");

        string dPath = AssetPath + "Data/";


        // Get the asset address paths
        // Stream in file
        string nodeAddressPath = AssetPath + "NodeAddressList.dat";
        string longAddressList;

        using (WWW www = new WWW(nodeAddressPath))
        {
            yield return www;
            longAddressList = www.text;
        }

        List<string> addressLines = new List<string>(
        longAddressList.Split(new string[] { "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries));
        // remove comment lines...
        addressLines = addressLines
            .Where(line => !(line.StartsWith("//")
                            || line.StartsWith("#")))
            .ToList();

        if (DisplayDebug) Debug.Log("max number of possible nodes: " + addressLines.Count);

        // Set aside the known possible set of nodes
        // We can restructure this later to be quicker as it would be preferable to stream these large datasets

        m.node = new Node[GridDimensions.x, GridDimensions.y, GridDimensions.z];
        
        // Parse the file names to know which nodes they are to be placed in
        // as well as define the boundaries. We can put that info in a text file as well.
        // It may be possible to create an octree of bits to be able to reference later


        bool[] edges = new bool[6];

        for (int i = 0; i < addressLines.Count; i++)
        {
            // file extension check
           
                //Debug.Log(file.Name);
                string tName = addressLines[i];
                tName = tName.Substring(0, tName.Length - 4);

                Vector3 nVec = StringToVector3(tName);

                // define edges but use a file to read in later with all of the info
                // <define edges>
                edges[0] = (nVec.x == BBox.xMax) ? true : false; // +x
                edges[1] = (nVec.x == BBox.xMin) ? true : false; // -x
                edges[2] = (nVec.y == BBox.yMax) ? true : false; // +y
                edges[3] = (nVec.y == BBox.yMin) ? true : false; // -y
                edges[4] = (nVec.z == BBox.zMax) ? true : false; // +z
                edges[5] = (nVec.z == BBox.zMin) ? true : false; // -z
                // </define edges>


                Vector3Int P = new Vector3Int(
                    (int)((nVec.x - BBox.xMin) * NodeSpacing),
                    (int)((nVec.y - BBox.yMin) * NodeSpacing),
                    (int)((nVec.z - BBox.zMin) * NodeSpacing)
                );

                // Debug.Log("Making node in pos: " + P.ToString());
                m.node[P.x, P.y, P.z] = ScriptableObject.CreateInstance<Node>();
                m.node[P.x, P.y, P.z].SetNode(AssetPath + "/Data/" + addressLines[i], nVec, ImageSize, edges, i);

        }
        
        // now get the nodes in the area to load
        m.curSet = new Node[8];
        m.RenderData();

    }


    // For now, we are identifying the data by the name
    // Find a better way to locate data for loading
    public static Vector3 StringToVector3(string sVector)
    {

        // Remove the parentheses
        if (sVector.StartsWith("(") && sVector.EndsWith(")"))
        {
            sVector = sVector.Substring(1, sVector.Length - 2);
        }

        // split the items
        string[] sArray = sVector.Split(',');

        // store as a Vector3
        Vector3 result = new Vector3(
            float.Parse(sArray[0]),
            float.Parse(sArray[1]),
            float.Parse(sArray[2]));

        return result;
    }

    #endregion  
}

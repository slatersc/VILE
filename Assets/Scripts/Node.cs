using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;


public struct FragSet
{
    public Color[] tex;
    public Color[] vecs;
}

public class Node : ScriptableObject
{
  
    private int sizeArea;
    
    private GameObject[] Meshes;
    

    public Vector3 pos;
    public int id;
    private string path;
    
    public bool[] edges;

    
    private FragSet[] frags;

    Vector2Int size;

    public bool Loaded;
    public bool Loading;

    public int layer;

    private Manager manager;


    public void SetNode(string _path, Vector3 _pos, Vector2Int _size, bool[] _edges, int _id)
    {
        manager = Manager.instance;
        layer = -99;
        Loaded = false;
        Loading = false;

        id = _id;
        path = _path;
        size = _size;
        edges = _edges;
        pos = _pos;
    }

    public void SwitchLayer(int _layer)
    {
        if (layer != _layer)
        {
            layer = _layer + 10; // layers start at layer 10 in the editor

            if (Loaded == true && Meshes != null) // change layers
            {
                for (int i = 0; i < Meshes.Length; ++i)
                {
                    Meshes[i].layer = layer;
                }
            }
        }

    }
    
    public IEnumerator LoadNode(int _layer)
    {

        Loading = true;
        sizeArea = (size.x * size.y);
        
        if (layer != _layer)
        {
            layer = _layer + 10; // layers start at layer 10 in the editor
        }

        Debug.Log("path: " + path);

        Texture2D dMap = new Texture2D(size.x * 3, size.y * 4, TextureFormat.RGB24, false);

        using (WWW www = new WWW(path))
        {
            yield return www;
            dMap = www.texture;
            dMap.Apply();
        }
        

        // Split data to textures and verts
        frags = new FragSet[6];
        Texture2D face = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);

        Color[] dVec = dMap.GetPixels();

        int counter = -1;
        for (int i = 0; i < 6; i++)
        {
            frags[i].tex = new Color[sizeArea];
            for (int j = 0; j < sizeArea; j++) {
                frags[i].tex[j] = dVec[++counter];
            }
           
            yield return null;
        }

        for (int i = 0; i < 6; i++)
        {
            frags[i].vecs = new Color[sizeArea];
            for (int j = 0; j < sizeArea; j++) {
                frags[i].vecs[j] = dVec[++counter];
            }

            yield return null;
        }
        
        yield return UnprojectCPU();

        Loaded = true;
        Loading = false;
        
    }


    public void ShowFaces(ref bool[] faceDisplay)
    {
        if (Loaded && Meshes != null)
        {
            for (int i=0; i<6; ++i)
            {
                Meshes[i].SetActive(faceDisplay[i]);
            }
        }
    }


    private float DecodeFloatRGB(ref Color c)
    {
        return (c.r + c.g * 0.003921568627451f + c.b * 1.537870049980777e-5f);
    }
    

    private Vector3 Lerp3(Vector3 a, Vector3 b, float t)
    {
        float diff = 1.0f - t;
        return new Vector3((diff * a.x + t * b.x), (diff * a.y + t * b.y), (diff * a.z + t * b.z));
    }


    public IEnumerator UnprojectCPU()
    {
       
        // Position matrix
        Matrix4x4 nPos = new Matrix4x4(new Vector4(1, 0, 0, 0), new Vector4(0, 1, 0, 0), new Vector4(0, 0, 1, 0), new Vector4(pos.x, pos.y, pos.z, 1));
        
        Meshes = new GameObject[6];

        int m = (size.x == 512) ? 9 : (size.x == 1024) ? 10 : (size.x == 2048) ? 11 : 12;
        float sizeDiv = (float)(2 / (float)size.x);

        
        for (int i = 0; i < 6; ++i)
        {
            yield return null;
            // Get PVI matrix
            Matrix4x4 mPVI = (manager.Session.CameraProperties.projectionMatrix * ((nPos * Manager.faceMatrices[i]).inverse)).inverse;

            Mesh mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32};
            Vector3[] verts = new Vector3[sizeArea];
            
            for (int j = 0; j < sizeArea; ++j)
            {
               
                int y = j >> 9;
                int x = (j & (size.x - 1));

                float x1 = ((float)x * sizeDiv) - 1.0f;
                float y1 = ((float)y * sizeDiv) - 1.0f;
                verts[j] = Lerp3(UnProjectVertex(ref x1, ref y1, -1.0f, ref mPVI),
                    UnProjectVertex(ref x1, ref y1, 1.0f, ref mPVI),
                    DecodeFloatRGB(ref frags[i].vecs[j])
                );
            }

            mesh.vertices = verts;
            mesh.colors = frags[i].tex;
            mesh.SetIndices(manager.Session.meshIndices, MeshTopology.Points, 0);
            Meshes[i] = Instantiate(manager.meshObject, Vector3.zero, Quaternion.Euler(Vector3.zero));
            Meshes[i].GetComponent<MeshFilter>().mesh = mesh;
            Meshes[i].layer = layer;

        }
    }
    
    public void NodeDisplay(bool display)
    {
        if (Loaded && Meshes != null)
        {
            for (int i = 0; i < Meshes.Length; ++i)
            {
                Meshes[i].SetActive(display);
            }
        }
    }


    
    private Vector3 UnProjectVertex(ref float x, ref float y, float z, ref Matrix4x4 PVI)
    {
        float vx = PVI.m00 * x + PVI.m01 * y + PVI.m02 * z + PVI.m03;
        float vy = PVI.m10 * x + PVI.m11 * y + PVI.m12 * z + PVI.m13;
        float vz = PVI.m20 * x + PVI.m21 * y + PVI.m22 * z + PVI.m23;
        float vw = PVI.m30 * x + PVI.m31 * y + PVI.m32 * z + PVI.m33;

        float div = (1.0f / vw);
        vx *= div;
        vy *= div;
        vz *= div;

        return new Vector3(vx,vy,vz);
    }
    
}



/*
public class ThreadedJob
{
    private bool m_IsDone = false;
    private object m_Handle = new object();
    private System.Threading.Thread m_Thread = null;
    public bool IsDone
    {
        get
        {
            bool tmp;
            lock (m_Handle)
            {
                tmp = m_IsDone;
            }
            return tmp;
        }
        set
        {
            lock (m_Handle)
            {
                m_IsDone = value;
            }
        }
    }
    public virtual void Start()
    {
        m_Thread = new System.Threading.Thread(Run);
        m_Thread.Start();
    }
    public virtual void Abort()
    {
        m_Thread.Abort();
    }

    protected virtual void ThreadFunction() { }

    protected virtual void OnFinished() { }

    public virtual bool Update()
    {
        if (IsDone)
        {
            OnFinished();
            return true;
        }
        return false;
    }
    public IEnumerator WaitFor()
    {
        while (!Update())
        {
            yield return null;
        }
    }
    private void Run()
    {
        ThreadFunction();
        IsDone = true;
    }
}

public class AssetLoad : ThreadedJob
{
    public float3[] points;  // arbitary job data
    public Color[] colors; // arbitary job data
    public Matrix4x4 VP; // primary view-projection matrix
    public Vector3 mainPos; // position of the main camera
    public Vector3 nodePos; // position of the node
    public bool finished = false;

    protected override void ThreadFunction()
    {

        // Do your threaded task. DON'T use the Unity API here
         for (int i = 0; i < 100000000; i++)
         {
             InData[i % InData.Length] += InData[(i + 1) % InData.Length];
         }
    }
    protected override void OnFinished()
    {
        finished = true;
    }
}
*/

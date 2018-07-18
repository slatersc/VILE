using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class TextureToStripConverter : MonoBehaviour {


    public Texture2D[] tMaps;
    public Texture2D[] vMaps;
    public Texture2D[] testText;

    public int size = 512; // size of current

    // Use this for initialization
    private IEnumerator Start()
    {
        Debug.Log("Conversion starting.");

        float time = Time.fixedTime;
        string outLocation = Application.dataPath + "/Resources/The Room/Data/";

        if (tMaps.Length != vMaps.Length)
            yield break;



        int sizeA = size * size;
        int area = sizeA * 12;

        testText = new Texture2D[tMaps.Length];

        for (int t = 0; t < tMaps.Length; ++t)
        {
            if (tMaps[t].name != vMaps[t].name)
                yield break;

            Color[] colorData = new Color[area];
            int counter = 0;

            for (int i = 0; i < 6; ++i)
            {
                // Load in images
                Texture2D tface = new Texture2D(size, size, TextureFormat.RGB24, false); // change to ASTC later
                Graphics.CopyTexture(tMaps[t], 0, 0, (i % 3) * size, ((i < 3) ? 0 : 1) * size, size, size, tface, 0, 0, 0, 0);
                tface.Apply();
                Color[] tex = tface.GetPixels();
                tface = null;

                for (int j = 0; j < tex.Length; ++j)
                {
                    colorData[counter++] = tex[j];
                }
                yield return null;

            }

            for (int i = 0; i < 6; ++i)
            {
                // Load in vertex section
                Texture2D vface = new Texture2D(size, size, TextureFormat.RGBA32, false); // change to ASTC later
                Graphics.CopyTexture(vMaps[t], 0, 0, (i % 3) * size, ((i < 3) ? 0 : 1) * size, size, size, vface, 0, 0, 0, 0);
                vface.Apply();
                Color[] vert = vface.GetPixels();
                vface = null;

                for (int j = 0; j < vert.Length; ++j)
                {
                    colorData[counter++] = new Color(vert[j].r, vert[j].g, vert[j].b);
                }
                yield return null;

            }
            
            // Output new file
            Texture2D outputFile = new Texture2D(size * 3, size * 4, TextureFormat.RGB24, false); // change to ASTC later
            outputFile.SetPixels(colorData);
            outputFile.Apply();

            File.WriteAllBytes(outLocation + tMaps[t].name + ".png", outputFile.EncodeToPNG());    //app path n1!  
            /*
            var bytes = outputFile.EncodeToPNG();
            var file = File.Open(outLocation + tMaps[t].name + ".png", FileMode.Create);
            var binary = new BinaryWriter(file);
            binary.Write(bytes);
            file.Close();
            */

            testText[t] = new Texture2D(size, size, TextureFormat.RGB24, false);

            Color[] tCol = outputFile.GetPixels();
            testText[t].SetPixels(0,0,size,size,tCol);
            testText[t].Apply();



            yield return null;

            
        }



        
        time = Time.fixedTime - time;

        Debug.Log("Conversion complete. Time taken: " + time.ToString() +  "seconds.");
        yield return null;
        
    }
    
}

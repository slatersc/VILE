using UnityEngine;
using System.Collections;
using System.IO;

public class CameraController : MonoBehaviour
{
  //  public Camera[] cams = new Camera[6];
	public GameObject bounds;
	public string sceneName = "The Room";
	public Vector2 yRange;
	public Vector2 xRange;
	public Vector2 zRange;
	public float spacing;
	public int size;
	public float clippingMin = 0.01f; // smaller maxes provide more accurate data but cannot see as far
	public float clippingMax = 50.01f; // smaller maxes provide more accurate data but cannot see as far
	// Use this for initialization
	IEnumerator Start (){
		/*
		for (uint i=2; i<6; ++i) {
			cams[i].gameObject.SetActive(false);
		}
		*/
		Debug.Log ("Script has been started");

		yield return new WaitForSeconds(2.0f);
		yield return TakeShots(); 
	}


	IEnumerator TakeShots()
	{
		int boundNum = bounds.transform.childCount;

		// set the size of map values



		Directory.CreateDirectory(Application.dataPath + "/Resources/" + sceneName + "/V/");
		Directory.CreateDirectory(Application.dataPath + "/Resources/" + sceneName + "/T/");


		// find places to put maps
		// draw gizmose for now

		// int stagger = 0; // use later to stagger it

		int cubeNum = 0;

		Time.timeScale = 0f;
		for(int i=0; i<2; ++i) {
			cubeNum = 0;
			
			for (float y = yRange.x; y <= yRange.y; y += spacing)
			{
				for (float x = xRange.x; x <= xRange.y; x += spacing)
				{
					for (float z = zRange.x; z <= zRange.y; z += spacing) // height
					{

						this.transform.position = new Vector3(x, y, z);
						if (bounds.gameObject.transform.GetComponent<Collider>().bounds.Contains(this.transform.position))
						{
							bool safe = true;
							for (int p = 0; p < boundNum; ++p)
							{
								if (bounds.gameObject.transform.GetChild(p).GetComponent<Collider>().bounds.Contains(this.transform.position))
								{
									safe = false;
								}
							}
							if (safe)
							{
								//Draw a sphere
								//GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
								//sphere.transform.position = this.transform.position;

								//string tag = (i==0)?"TEX":"VERT";
								/*
								string type = ".png"; //(i == 0) ? ".jpg" : ".png";

								string fileName = this.transform.position.ToString("F2") + type;
								string path;
								if (i == 0){ path = Application.dataPath + "/Resources/" + sceneName + "/T/" + fileName; }
								else{ path = Application.dataPath + "/Resources/" + sceneName + "/V/" + fileName; }

								// Needed for capture but commented out for current testing
								
								if (i == 0) { CubemapCapture.TriggerCubemapCapture(this.transform.position, size, path, clippingMin, clippingMax); }
								else { CubemapCaptureDepth.TriggerCubemapCapture(this.transform.position, size, path, clippingMin, clippingMax);}
								*/
								cubeNum++;
							  
							}
						}
						yield return null;
					}
				}
			}
		}

		Debug.Log("ALL DONE: " + cubeNum.ToString());
	}
	
}
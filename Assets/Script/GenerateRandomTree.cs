using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class GenerateRandomTree : MonoBehaviour
{
    // Start is called before the first frame update
    private Terrain terrain;
    public int treeNum = 160000;
    public int generateTreeSeed = 0;
    private TreeInstance[] newTreeInstance;

    [ContextMenu("CreateRandomTree")]
    void GenerateTree()
    {
        Debug.Log("Start Generate Random Tree In Tree");
        GameObject terrainObject = GameObject.Find("Terrain");
        if (null == terrainObject)
        {
            Debug.LogError("terrainObject is null");
            return;
        }

        terrain = terrainObject.GetComponent<Terrain>();
        if(null == terrain)
        {
            Debug.LogError("terraincomponent is null");
            return;
        }

        if (terrain.terrainData.treePrototypes.Length <= 0)
        {
            Debug.LogError("there is not any tree prototypes");
            return;
        }

        Random.InitState(generateTreeSeed);
        
        newTreeInstance = new TreeInstance[treeNum];
        for (int Index = 0; Index < treeNum; Index++)
        {
            newTreeInstance[Index].prototypeIndex = 0;
            newTreeInstance[Index].color = new Color32(255, 255, 255, 255);
            newTreeInstance[Index].widthScale = 1.0f;
            newTreeInstance[Index].heightScale = 1.0f;
            newTreeInstance[Index].rotation = 0.0f;
            newTreeInstance[Index].lightmapColor = new Color32(255, 255, 255, 255);
            float instancePosX = Random.Range(0.0f, 1.0f);
            float instancePosZ = Random.Range(0.0f, 1.0f);
            newTreeInstance[Index].position = new Vector3(instancePosX, 0, instancePosZ);
        }

        terrain.terrainData.SetTreeInstances(newTreeInstance, true);
        terrain.Flush();
    }
}

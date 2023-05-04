using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;




public class RenderMassTree : MonoBehaviour
{
    private Mesh treeMesh;
    private Terrain terrain;
    private GameObject terrainObject;
    
    //tree render data
    [NonSerialized]
    public QuadTree quadTree;
    public Material treeMaterial;
    public float range = 2000.0f;
    private ComputeBuffer bufferWithArgs;
    private ComputeBuffer meshPropertyArrayBuffer;
    private Bounds drawIndirectBounds;
    
    private ComputeShader cullShader;
    private SphereCollider sphereBounds;
    private int cullTreeKernel;
    private Camera mainCamera;
    private ComputeBuffer instanceDataBuffer;
    private ComputeBuffer posVisibleBuffer;
    private ComputeBuffer visiblleCountBuffer;
    private ComputeBuffer treeNodeCullFlagBuffer;
    private int visibleCount = 0;
    private int visibleCluter = 0;
    private HZBRender hzbRender;
    private List<int> firstIndexs = new List<int>();
    private List<int> secondIndexs = new List<int>();
    private int indexCount = 0;
    public float PlantScale = 10.0f;

    [ContextMenu("BuildTreeRenderData")]
    void BuildTreeRenderData()
    {
        if (!CollectTreeMesh())
            return;
        
        Vector3 terrainPos = terrainObject.GetComponent<Transform>().position;
        float posXMax = terrainPos.x + terrain.terrainData.size.x;
        float posXMin = terrainPos.x;

        float posYMax = terrainPos.y + terrain.terrainData.size.y;
        float posYMin = terrainPos.y;

        float posZMax = terrainPos.z + terrain.terrainData.size.z;
        float posZMin = terrainPos.z;

        TreeInstance[] treeInstances = terrain.terrainData.treeInstances;
        Vector3[] allTreePos = new Vector3[treeInstances.Length];
        for(int index = 0; index < treeInstances.Length; index++)
        {
            Vector3 virtualPos = treeInstances[index].position;
            Vector3 worldPos = new Vector3();
            worldPos.x = Mathf.Lerp(posXMin, posXMax, virtualPos.x);
            worldPos.y = Mathf.Lerp(posYMin, posYMax, virtualPos.y);
            worldPos.z = Mathf.Lerp(posZMin, posZMax, virtualPos.z);
            allTreePos[index] = worldPos;
        }

        quadTree = new QuadTree(allTreePos, new Vector2(posXMin, posZMin), new Vector2(posXMax, posZMax));
    }
    
    
    bool CollectTreeMesh()
    {
        terrainObject = GameObject.Find("Terrain");
        if (null == terrainObject)
        {
            Debug.LogError("terrainObject is null");
            return false;
        }

        terrain = terrainObject.GetComponent<Terrain>();
        if (null == terrain)
        {
            Debug.LogError("terraincomponent is null");
            return false;
        }

        if (terrain.terrainData.treePrototypes.Length <= 0)
        {
            Debug.LogError("there is not any tree prototypes");
            return false;
        }

        GameObject treePrefabObj = terrain.terrainData.treePrototypes[0].prefab;
        treeMesh = treePrefabObj.GetComponent<MeshFilter>().sharedMesh;
        sphereBounds = treePrefabObj.GetComponent<SphereCollider>();
        if (!treeMesh)
        {
            Debug.LogError("treeMesh is empty");
            return false;
        }

        return true;
    }

    void Start()
    {
        if (!CollectTreeMesh())
            return;
        
        BuildTreeRenderData();
        
        if (!treeMaterial)
        {
            Debug.LogError("treeMaterial is empty");
            return;
        }
        
        
        
        /* GPU Cull Setting*/
        mainCamera = Camera.main;
        InstanceData[] instanceDatas = quadTree.instanceDatas;
        instanceDataBuffer = new ComputeBuffer(instanceDatas.Length, sizeof(float) * 4);
        instanceDataBuffer.SetData(instanceDatas);
        
        posVisibleBuffer = new ComputeBuffer(instanceDatas.Length, sizeof(float) * 3);
        visiblleCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.IndirectArguments);
        treeNodeCullFlagBuffer = new ComputeBuffer(quadTree.leafId, sizeof(int));

        cullShader = Resources.Load<ComputeShader>("Shader/CullFrustumCs");
        cullTreeKernel = cullShader.FindKernel("CSMain");
        cullShader.SetVector("bounds", new Vector4(sphereBounds.center.x, sphereBounds.center.y, sphereBounds.center.z, sphereBounds.radius * PlantScale));
        cullShader.SetVector("cameraWorldDirection", mainCamera.transform.forward);
        cullShader.SetBuffer(cullTreeKernel, "instanceDatas", instanceDataBuffer);
        cullShader.SetBuffer(cullTreeKernel, "posVisibleBuffer", posVisibleBuffer);
        cullShader.SetInt("allCount", instanceDatas.Length);
        
        /* Draw Indirect Setting*/
        // bufferWithArgs
        bufferWithArgs = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);

        // posBuffer
        drawIndirectBounds = new Bounds(Vector3.zero, new Vector3(range, range, range));
        treeMaterial.SetBuffer("posBuffer", posVisibleBuffer);
        hzbRender = GetComponent<HZBRender>();
    }

    // Update is called once per frame
    void Update()
    {
        treeMaterial.SetFloat("_Scale", PlantScale);
        
        Profiler.BeginSample("UpdateHzb");
        hzbRender.UpdateHzb();
        Profiler.EndSample();

        Profiler.BeginSample("Cull");
        //Gpu cull(frustum cull and hzb cull)
        Cull();
        Profiler.EndSample();

        Profiler.BeginSample("DrawMassInstances");
        // DrawInstances
        DrawInstanceIndirect();
        Profiler.EndSample();
    }

    
    private void Cull()
    {
        Frustum frustum = new Frustum(mainCamera);
        Profiler.BeginSample("FrustumCull");
        int[] treeNodeCullFlags = quadTree.GetCullResult(frustum, false, false);
        Profiler.EndSample();
        
        Profiler.BeginSample("GPU Cull");
        if (cullShader)
        {
            treeNodeCullFlagBuffer.SetData(treeNodeCullFlags);
            visibleCluter = 0;
            foreach (var flag in treeNodeCullFlags)
            {
                if (flag == 0)
                    visibleCluter++;
            }
            cullShader.SetBuffer(cullTreeKernel, "treeNodeCullFlag", treeNodeCullFlagBuffer);
            
            Vector4[] frustumPlanes = new Vector4[6];
            frustumPlanes[0] = frustum.farPlane;
            frustumPlanes[1] = frustum.nearPlane;
            frustumPlanes[2] = frustum.leftPlane;
            frustumPlanes[3] = frustum.rightPlane;
            frustumPlanes[4] = frustum.upPlane;
            frustumPlanes[5] = frustum.bottomPlane;
            cullShader.SetVectorArray("frustumPlanes", frustumPlanes);

            int[] args = new int[1] { 0 };
            visiblleCountBuffer.SetData(args);
            
            var m = GL.GetGPUProjectionMatrix( Camera.main.projectionMatrix,false) * Camera.main.worldToCameraMatrix;
            
            float[] mlist = new float[] {
                m.m00,m.m10,m.m20,m.m30,
                m.m01,m.m11,m.m21,m.m31,
                m.m02,m.m12,m.m22,m.m32,
                m.m03,m.m13,m.m23,m.m33
            };

            if (hzbRender != null && hzbRender.hzbTexture != null)
            {
                cullShader.SetTexture(cullTreeKernel,"hizTexture", hzbRender.hzbTexture);
                cullShader.SetFloat("hizMapSize", hzbRender.hzbTexture.width);
                cullShader.SetInt("hizMapLevelCount", hzbRender.hzbLevelCount - 1);
            }
            
            InstanceData[] instanceDatas = quadTree.instanceDatas;
            cullShader.SetFloats("worldToViewProject", mlist);
            cullShader.SetBuffer(cullTreeKernel, "bufferWithArgs", visiblleCountBuffer);
     
            cullShader.Dispatch(cullTreeKernel, 1500 / 16 + 1, 1500 / 16 + 1, 1);
            
            int[] data = new int[1];
            visiblleCountBuffer.GetData(data);
            visibleCount = data[0];
            uint[] resultArgs = new uint[5] { treeMesh.GetIndexCount(0), (uint)visibleCount, 0, 0, 0 };
            bufferWithArgs.SetData(resultArgs);
        }
        Profiler.EndSample();
    }

    private void DrawInstanceIndirect()
    {
        if(treeMesh && treeMaterial)
        {
            Graphics.DrawMeshInstancedIndirect(treeMesh, 0, treeMaterial, drawIndirectBounds, bufferWithArgs, 0, null, ShadowCastingMode.Off, false);
        }
    }

    
    private void OnGUI()
    {
        InstanceData[] instanceDatas = quadTree.instanceDatas;
        Rect allTreeNumRect = new Rect(new Vector2(20, 20), new Vector2(150, 30));
        GUI.TextField(allTreeNumRect, "allCount: " + instanceDatas.Length);
        Rect visibleTreeNumRect = new Rect(new Vector2(20, 40), new Vector2(150, 30));
        GUI.TextField(visibleTreeNumRect, "visibleCount: " + visibleCount);
        Rect visibleClusterNumRect = new Rect(new Vector2(20, 60), new Vector2(150, 30));
        GUI.TextField(visibleClusterNumRect, "visibleClusterCount: " + visibleCluter);
    }

    private void OnDisable()
    {
        if(bufferWithArgs != null && bufferWithArgs.IsValid())
        {
            bufferWithArgs.Release();
        }

        if (meshPropertyArrayBuffer != null && meshPropertyArrayBuffer.IsValid())
        {
            meshPropertyArrayBuffer.Release();
        }
    }
}

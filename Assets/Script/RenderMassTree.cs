using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;


public class Frustum
{
    public Vector4 farPlane;
    public Vector4 nearPlane;
    public Vector4 leftPlane;
    public Vector4 rightPlane;
    public Vector4 upPlane;
    public Vector4 bottomPlane;

    public Frustum(Camera camera)
    {
        // Get far plane four point
        Vector3[] points = new Vector3[4];
        Transform transform = camera.transform;
        float distance = camera.farClipPlane;
        float halfFovRad = Mathf.Deg2Rad * camera.fieldOfView * 0.5f;
        float upLen = distance * Mathf.Tan(halfFovRad);
        float rightLen = upLen * camera.aspect;
        Vector3 farCenterPoint = transform.position + distance * transform.forward;
        Vector3 up = upLen * transform.up;
        Vector3 right = rightLen * transform.right;
        points[0] = farCenterPoint - up - right;//left-bottom
        points[1] = farCenterPoint - up + right;//right-bottom
        points[2] = farCenterPoint + up - right;//left-up
        points[3] = farCenterPoint + up + right;//right-up
        
        Vector3 cameraPosition = transform.position;
        //left hand rule
        leftPlane = GetPlane(cameraPosition, points[0], points[2]);//left
        rightPlane = GetPlane(cameraPosition, points[3], points[1]);//right
        bottomPlane = GetPlane(cameraPosition, points[1], points[0]);//bottom
        upPlane = GetPlane(cameraPosition, points[2], points[3]);//up
        nearPlane = GetPlane(-transform.forward, transform.position + transform.forward * camera.nearClipPlane);//near
        farPlane = GetPlane(transform.forward, transform.position + transform.forward * camera.farClipPlane);//far

    }
    
    public static Vector4 GetPlane(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
        return GetPlane(normal, a);
    }
    
    public static Vector4 GetPlane(Vector3 normal, Vector3 point)
    {
        return new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, point));
    }
}

public class RenderMassTree : MonoBehaviour
{
    private Vector3[] allTreePos;
    private Mesh treeMesh;
    private Terrain terrain;
    private GameObject terrainObject;
    public Material treeMaterial;
    public float range = 2000.0f;
    private ComputeBuffer bufferWithArgs;
    private ComputeBuffer meshPropertyArrayBuffer;
    private Bounds drawIndirectBounds;
    
    private ComputeShader cullShader;
    private SphereCollider sphereBounds;
    private int cullTreeKernel;
    private Camera mainCamera;
    private ComputeBuffer posAllBuffer;
    private ComputeBuffer posVisibleBuffer;
    private ComputeBuffer visiblleCountBuffer;
    private int visibleCount = 0;
    private HZBRender hzbRender;
    
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

        if (!treeMaterial)
        {
            Debug.LogError("treeMaterial is empty");
            return;
        }

        
        Vector3 terrainPos = terrainObject.GetComponent<Transform>().position;
        float posXMax = terrainPos.x + terrain.terrainData.size.x;
        float posXMin = terrainPos.x;

        float posYMax = terrainPos.y + terrain.terrainData.size.y;
        float posYMin = terrainPos.y;

        float posZMax = terrainPos.z + terrain.terrainData.size.z;
        float posZMin = terrainPos.z;

        TreeInstance[] treeInstances = terrain.terrainData.treeInstances;
        allTreePos = new Vector3[treeInstances.Length];
        for(int index = 0; index < treeInstances.Length; index++)
        {
            Vector3 virtualPos = treeInstances[index].position;
            Vector3 worldPos = new Vector3();
            worldPos.x = Mathf.Lerp(posXMin, posXMax, virtualPos.x);
            worldPos.y = Mathf.Lerp(posYMin, posYMax, virtualPos.y);
            worldPos.z = Mathf.Lerp(posZMin, posZMax, virtualPos.z);
            allTreePos[index] = worldPos;
        }
        
        /* GPU Cull Setting*/
        mainCamera = Camera.main;
        posAllBuffer = new ComputeBuffer(allTreePos.Length, sizeof(float) * 3);
        posAllBuffer.SetData(allTreePos);
        
        posVisibleBuffer = new ComputeBuffer(allTreePos.Length, sizeof(float) * 3);
        visiblleCountBuffer = new ComputeBuffer(1, sizeof(int));
        
        cullShader = Resources.Load<ComputeShader>("Shader/CullFrustumCs");
        cullTreeKernel = cullShader.FindKernel("CSMain");
        cullShader.SetVector("bounds", new Vector4(sphereBounds.center.x, sphereBounds.center.y, sphereBounds.center.z, sphereBounds.radius));
        cullShader.SetBuffer(cullTreeKernel, "posAllBuffer", posAllBuffer);
        cullShader.SetBuffer(cullTreeKernel, "posVisibleBuffer", posVisibleBuffer);
        cullShader.SetInt("allCount", posAllBuffer.count);
        
        /* Draw Indirect Setting*/
        // bufferWithArgs
        bufferWithArgs = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);

        // posBuffer
        meshPropertyArrayBuffer = new ComputeBuffer((int)treeInstances.Length, sizeof(float) * 3);
        meshPropertyArrayBuffer.SetData(allTreePos);

        //treeMaterial.SetBuffer("posBuffer", meshPropertyArrayBuffer);
        drawIndirectBounds = new Bounds(Vector3.zero, new Vector3(range, range, range));

        hzbRender = GetComponent<HZBRender>();
    }

    // Update is called once per frame
    void Update()
    {
        //Gpu cull(frustum cull and hzb cull)
        GPUCull();

        // DrawInstances
        DrawInstanceIndirect();
    }
    

    private void GPUCull()
    {
        if (cullShader)
        {
            Frustum frustum = new Frustum(mainCamera);
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

            cullShader.SetFloats("worldToViewProject", mlist);
            cullShader.SetBuffer(cullTreeKernel, "bufferWithArgs", visiblleCountBuffer);
            cullShader.Dispatch(cullTreeKernel, allTreePos.Length / 64 + 1, 1, 1);
        }
    }

    private void DrawInstanceIndirect()
    {
        if(treeMesh && treeMaterial)
        {
            treeMaterial.SetBuffer("posBuffer", posVisibleBuffer);
            int[] data = new int[1];
            visiblleCountBuffer.GetData(data);
            visibleCount = data[0];

            uint[] args = new uint[5] { treeMesh.GetIndexCount(0), (uint)visibleCount, 0, 0, 0 };
            bufferWithArgs.SetData(args);
            
            Graphics.DrawMeshInstancedIndirect(treeMesh, 0, treeMaterial, drawIndirectBounds, bufferWithArgs, 0, null, ShadowCastingMode.Off, false);
        }
    }

    private void OnGUI()
    {
        Rect allTreeNumRect = new Rect(new Vector2(20, 20), new Vector2(150, 30));
        GUI.TextField(allTreeNumRect, "allCount: " + allTreePos.Length);
        Rect visibleTreeNumRect = new Rect(new Vector2(20, 40), new Vector2(150, 30));
        GUI.TextField(visibleTreeNumRect, "visibleCount: " + visibleCount);
    }

    private void OnDisable()
    {
        if(bufferWithArgs.IsValid())
        {
            bufferWithArgs.Release();
        }

        if (meshPropertyArrayBuffer.IsValid())
        {
            meshPropertyArrayBuffer.Release();
        }
    }
}

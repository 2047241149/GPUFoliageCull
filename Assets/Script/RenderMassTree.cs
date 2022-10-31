using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RenderMassTree : MonoBehaviour
{
    public Mesh treeMesh;
    public Material treeMaterial;
    public float range = 1000.0f;
    public uint generateTreeNum = 10000;
    private ComputeBuffer bufferWithArgs;
    private ComputeBuffer meshPropertyArrayBuffer;
    private Bounds drawIndirectBounds;


    struct MeshInstanceProperty
    {
        public Matrix4x4 mat;
        public Color color;

        public static int Size()
        {
            return sizeof(float) * 4 * 4 + sizeof(float) * 4;
        }
    }

    void Start()
    {
        if(!treeMesh)
        {
            Debug.LogError("treeMesh is empty");
            return;
        }

        if (!treeMaterial)
        {
            Debug.LogError("treeMaterial is empty");
            return;
        }

        bufferWithArgs = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
        uint[] args = new uint[5] { treeMesh.GetIndexCount(0), generateTreeNum, 0, 0, 0 };
        bufferWithArgs.SetData(args);

        MeshInstanceProperty[] meshInstancePropertys = new MeshInstanceProperty[generateTreeNum];
        for(uint index = 0; index < generateTreeNum; index++)
        {
            MeshInstanceProperty property = new MeshInstanceProperty();
            property.color = new Color(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), 1.0f);
            Vector3 pos = new Vector3(Random.Range(0, range), 0.0f, Random.Range(0, range));
            property.mat = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
            meshInstancePropertys[index] = property;
        }

        meshPropertyArrayBuffer = new ComputeBuffer((int)generateTreeNum, MeshInstanceProperty.Size());
        meshPropertyArrayBuffer.SetData(meshInstancePropertys);

        treeMaterial.SetBuffer("meshBuffer", meshPropertyArrayBuffer);

        drawIndirectBounds = new Bounds(Vector3.zero, new Vector3(range, range, range));
    }

    // Update is called once per frame
    void Update()
    {
        if(treeMesh && treeMaterial)
        {
            Graphics.DrawMeshInstancedIndirect(treeMesh, 0, treeMaterial, drawIndirectBounds, bufferWithArgs);
        }

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

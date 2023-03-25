
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TreeEditor;
using UnityEngine;


static class QuadTreeUtil
{
    public const int QUAD_TREE_NODE_MAX_NUM = 128;
}

[Serializable]
public class TreeNode
{
    public TreeNode[] childs;
    public Vector3 boxMin;
    public Vector3 boxMax;
    public bool bLeafNode = false;
    public List<int> treeIndices;
    private QuadTree tree;
    public int leafId = -1;

    public TreeNode(QuadTree inTree, List<int> inIndices, Vector2 regionMin, Vector2 regionMax)
    {
        tree = inTree;
        treeIndices = inIndices;
        RefreshBoundingBox();
        if (inIndices.Count <= QuadTreeUtil.QUAD_TREE_NODE_MAX_NUM)
        {
            leafId = inTree.leafId;
            inTree.leafId++;
            bLeafNode = true;
            return;
        }

        BuildChildren(regionMin, regionMax);
    }

    
    void RefreshBoundingBox()
    {
        boxMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        boxMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        Vector3[] allTreePos = tree.alllTreePos;


        foreach (var instanceIndex in treeIndices)
        {
            Vector3 pos = allTreePos[instanceIndex];
            boxMin = Vector3.Min(boxMin, pos);
            boxMax = Vector3.Max(boxMax, pos);
        }
    }

    void BuildChildren(Vector2 regionMin, Vector2 regionMax)
    {
        childs = new TreeNode[4];
        
        Vector2 center = (regionMin + regionMax) / 2.0f;
        
        //left up
        Vector2 leftupMin = new Vector2(regionMin.x, center.y);
        Vector2 leftupMax = new Vector2(center.x, regionMax.y);
        childs[0] = new TreeNode(tree, GetRegionInstances(leftupMin, leftupMax), leftupMin, leftupMax);
        
        //left down
        Vector2 leftdownMin = regionMin;
        Vector2 leftdownMax = center;
        childs[1] = new TreeNode(tree, GetRegionInstances(leftdownMin, leftdownMax), leftdownMin, leftdownMax);
        
        //right up
        Vector2 rightupMin = center;
        Vector2 rightupMax = regionMax;
        childs[2] = new TreeNode(tree, GetRegionInstances(rightupMin, rightupMax), rightupMin, rightupMax);
        
        //right down
        Vector2 rightdownMin = new Vector2(center.x, regionMin.y);
        Vector2 rightdownMax = new Vector2(regionMax.x, center.y);
        childs[3] = new TreeNode(tree, GetRegionInstances(rightdownMin, rightdownMax), rightdownMin, rightdownMax);
    }
    
    List<int> GetRegionInstances(Vector2 min, Vector2 max)
    {
        List<int> treeIndexs = new List<int>();
        
        foreach (var instanceIndex in treeIndices)
        {
            Vector3 pos = tree.alllTreePos[instanceIndex];
            if (pos.x >= min.x && pos.z >= min.y && pos.x < max.x && pos.z < max.y)
            {
                treeIndexs.Add(instanceIndex);
            }
        }

        return treeIndexs;
    }
}

[Serializable]
public struct TreeClusterData
{
    int[] instances;
    int instanceNum;

    public TreeClusterData(List<int> treeIndices, int fixedInstanceSize)
    {
        instances = new int[fixedInstanceSize];
        instanceNum = Math.Min(instances.Length, treeIndices.Count);

        for (int index = 0; index < instanceNum; index++)
        {
            instances[index] = treeIndices[index];
        }
    }
}



[Serializable]
public struct InstanceData
{
    public Vector4 instance;
    public InstanceData(Vector3 inPos, int clusterId)
    {
        instance = new Vector4(inPos.x, inPos.y, inPos.z, (float)clusterId);
    }
}


[Serializable]
public class QuadTree
{
    public Vector3[] alllTreePos;
    public InstanceData[] instanceDatas;
    private int[] treeCullFlags;
    public TreeNode rootNode;
    public int leafId;

    public QuadTree(Vector3[] inAllTreePos, Vector2 regionMin, Vector2 regionMax)
    {
        List<int> indices = inAllTreePos.Select((item, index) => index).ToList();
        alllTreePos = inAllTreePos;
        leafId = 0;
        rootNode = new TreeNode(this, indices, regionMin, regionMax);
        BuildTreeClusterData();
    }

    void BuildTreeClusterData()
    {
        List<InstanceData> instanceDataList = new List<InstanceData>();
        CollectLeafNodeClusterData(instanceDataList, rootNode);
        instanceDatas = instanceDataList.ToArray();
    }

    void CollectLeafNodeClusterData(List<InstanceData> instanceDatas, TreeNode node)
    {
        if(null == instanceDatas || null == node)
            return;

        if (node.bLeafNode)
        {
            foreach (var index in node.treeIndices)
            {
                instanceDatas.Add(new InstanceData(alllTreePos[index], node.leafId));
            }
            return;
        }

        if (node.childs != null)
        {
            CollectLeafNodeClusterData(instanceDatas, node.childs[0]);
            CollectLeafNodeClusterData(instanceDatas, node.childs[1]);
            CollectLeafNodeClusterData(instanceDatas, node.childs[2]);
            CollectLeafNodeClusterData(instanceDatas, node.childs[3]);
        }
    }

    void CullTreeNode(TreeNode node, Vector4[] frustrumPlanes)
    {
        Vector3 boxCenter = (node.boxMax + node.boxMin) / 2.0f;
        Vector3 boxExtend = node.boxMax - boxCenter;
        bool bCull = false;
        
        foreach (var plane in frustrumPlanes)
        {
            float r = boxExtend[0] * Math.Abs(plane.x) + boxExtend[1] * Math.Abs(plane.y) +
                      boxExtend[2] * Math.Abs(plane.z);

            float distance = Frustum.GetSignDistance(plane, boxCenter);
            if (distance >= r)
            {
                bCull = true;
                break;
            }
        }

        if (bCull)
        {
            SetNodeCull(node);
        }
        else
        {
            if (!node.bLeafNode)
            {
                if (node.childs != null)
                {
                    CullTreeNode(node.childs[0], frustrumPlanes);
                    CullTreeNode(node.childs[1], frustrumPlanes);
                    CullTreeNode(node.childs[2], frustrumPlanes);
                    CullTreeNode(node.childs[3], frustrumPlanes);
                }
            }
            else
            {
                treeCullFlags[node.leafId] = 0;
            }
        }
        
    }

    void SetNodeCull(TreeNode node)
    {
        if (node.bLeafNode)
        {
            treeCullFlags[node.leafId] = 1;
        }
        else
        {
            if (node.childs != null)
            {
                SetNodeCull(node);
            }
        }
    }

    public int[] GetCullResult(Frustum frustum)
    {
        if (null == treeCullFlags)
        {
            treeCullFlags = Enumerable.Repeat<int>(0, leafId).ToArray();
        }

        Vector4[] planes = new Vector4[6];
        planes[0] = frustum.farPlane;
        planes[1] = frustum.nearPlane;
        planes[2] = frustum.upPlane;
        planes[3] = frustum.bottomPlane;
        planes[4] = frustum.leftPlane;
        planes[5] = frustum.rightPlane;
        CullTreeNode(rootNode, planes);
        return treeCullFlags;
    }
}

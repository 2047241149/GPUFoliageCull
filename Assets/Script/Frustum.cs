using System;
using UnityEngine;


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

    public static float GetSignDistance(Vector4 plane, Vector3 pos)
    {
        Vector3 normal = new Vector3(plane.x, plane.y, plane.z);
        return Vector3.Dot(normal, pos) + plane.w;
    }
}
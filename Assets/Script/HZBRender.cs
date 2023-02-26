using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HZBRender : MonoBehaviour
{
    public RenderTexture hzbTexture;
    public Texture depthTexture;
    private Material hzbBuildMat;
    public int hzbLevelCount = 0;

    // Start is called before the first frame update
    void Start()
    {
        hzbBuildMat = Resources.Load("HZBMat") as Material;
        Camera.main.depthTextureMode |= DepthTextureMode.Depth;

        hzbTexture = new RenderTexture(1024, 1024, 0, RenderTextureFormat.RFloat);
        hzbTexture.autoGenerateMips = false;

        hzbTexture.useMipMap = true;
        hzbTexture.filterMode = FilterMode.Point;
        hzbTexture.Create();
    }

    // Update is called once per frame
    void Update()
    {
        depthTexture = Shader.GetGlobalTexture("_CameraDepthTexture");
        int w = hzbTexture.width;
        int h = hzbTexture.height;
        hzbLevelCount = 0;
        RenderTexture lastRt = null;
        RenderTexture tempRT;

        while (h > 8)
        {
            hzbBuildMat.SetVector("_InvSize",new Vector4(1.0f / w, 1.0f / h, 0, 0));

            tempRT = RenderTexture.GetTemporary(w, h, 0, hzbTexture.format);
            tempRT.filterMode = FilterMode.Point;

            if (null == lastRt)
            {
                Graphics.Blit(depthTexture, tempRT);
            }
            else
            {
                hzbBuildMat.SetTexture("_MainTex", lastRt);
                Graphics.Blit(null, tempRT, hzbBuildMat);
                RenderTexture.ReleaseTemporary(lastRt);
            }
            
            Graphics.CopyTexture(tempRT, 0, 0, hzbTexture, 0, hzbLevelCount);
            lastRt = tempRT;

            w /= 2;
            h /= 2;
            hzbLevelCount++;
        }
        
        RenderTexture.ReleaseTemporary(lastRt);
    }
}

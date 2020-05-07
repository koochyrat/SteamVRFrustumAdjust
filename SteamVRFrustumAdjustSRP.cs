using UnityEngine;
using Valve.VR;
#if UNITY_2018
using UnityEngine.Experimental.Rendering;
#else
using UnityEngine.Rendering;
#endif

//Place this script on the Camera (eye) object.
//for canted headsets like pimax, calculate proper culling matrix to avoid objects being culled too early at far edges
//prevents objects popping in and out of view
[AddComponentMenu("SteamVR/SteamVRFrustumAdjustSRP")]
public class SteamVRFrustumAdjustSRP : MonoBehaviour
{
    private bool isCantedFov = false;
    private Camera m_Camera;
    private Matrix4x4 projectionMatrix;

    void OnEnable()
    {
#if UNITY_2018
        RenderPipeline.beginCameraRendering += RenderPipeline_beginCameraRendering;
#else
        RenderPipelineManager.beginCameraRendering += RenderPipelineManager_beginCameraRendering;
#endif
        m_Camera = GetComponent<Camera>();
        HmdMatrix34_t eyeToHeadL = SteamVR.instance.hmd.GetEyeToHeadTransform(EVREye.Eye_Left);
        if (eyeToHeadL.m0 < 1)  //m0 = 1 for parallel projections
        {
            isCantedFov = true;
            float l_left = 0.0f, l_right = 0.0f, l_top = 0.0f, l_bottom = 0.0f;
            SteamVR.instance.hmd.GetProjectionRaw(EVREye.Eye_Left, ref l_left, ref l_right, ref l_top, ref l_bottom);
            float eyeYawAngle = Mathf.Acos(eyeToHeadL.m0);  //since there are no x or z rotations, this is y only. 10 deg on Pimax
            float eyeHalfFov = Mathf.Atan(SteamVR.instance.tanHalfFov.x);
            float tanCorrectedEyeHalfFovH = Mathf.Tan(eyeYawAngle + eyeHalfFov);

            //increase horizontal fov by the eye rotation angles
            projectionMatrix.m00 = 1 / tanCorrectedEyeHalfFovH;  //m00 = 0.1737 for Pimax

            //because of canting, vertical fov increases towards the corners. calculate the new maximum fov otherwise culling happens too early at corners
            float eyeFovLeft = Mathf.Atan(-l_left);
            float tanCorrectedEyeHalfFovV = SteamVR.instance.tanHalfFov.y * Mathf.Cos(eyeFovLeft) / Mathf.Cos(eyeFovLeft + eyeYawAngle);
            projectionMatrix.m11 = 1 / tanCorrectedEyeHalfFovV;   //m11 = 0.3969 for Pimax

            //set the near and far clip planes
            projectionMatrix.m22 = - (m_Camera.farClipPlane + m_Camera.nearClipPlane) / (m_Camera.farClipPlane - m_Camera.nearClipPlane);
            projectionMatrix.m23 = - 2 * m_Camera.farClipPlane * m_Camera.nearClipPlane / (m_Camera.farClipPlane - m_Camera.nearClipPlane);
            projectionMatrix.m32 = -1;
        }
        else
            isCantedFov = false;
    }

    void OnDisable()
    {
#if UNITY_2018
        RenderPipeline.beginCameraRendering -= RenderPipeline_beginCameraRendering;
#else
        RenderPipelineManager.beginCameraRendering -= RenderPipelineManager_beginCameraRendering;
#endif
        if (isCantedFov)
        {
            isCantedFov = false;
            m_Camera.ResetCullingMatrix();
        }
    }

#if UNITY_2018
    private void RenderPipeline_beginCameraRendering(Camera camera)
#else
    private void RenderPipelineManager_beginCameraRendering(ScriptableRenderContext context, Camera camera)
#endif
    {
        if (isCantedFov)
        {
            camera.cullingMatrix = projectionMatrix * camera.worldToCameraMatrix;
        }
    }
}

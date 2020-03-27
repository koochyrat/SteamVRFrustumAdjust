using UnityEngine;
using Valve.VR;

//Place this script on the Camera (eye) object.
//for canted headsets like pimax, calculate proper culling matrix to avoid objects being culled too early at far edges
//prevents objects popping in and out of view
[AddComponentMenu("SteamVR/SteamVRFrustumAdjust")]
public class SteamVRFrustumAdjust : MonoBehaviour
{
    private bool isCantedFov = false;
    private float m00;
    private Camera m_Camera;

    void OnEnable()
    {
        m_Camera = GetComponent<Camera>();
        HmdMatrix34_t eyeToHeadL = SteamVR.instance.hmd.GetEyeToHeadTransform(EVREye.Eye_Left);
        if (eyeToHeadL.m0 < 1)
        {
            isCantedFov = true;
            float eyeYawAngle = Mathf.Acos(eyeToHeadL.m0);  //since there are no x or z rotations, this is y only. 10 deg on Pimax
            float eyeHalfFov = Mathf.Atan(SteamVR.instance.tanHalfFov.x);
            float tanCorrectedEyeHalfFov = Mathf.Tan(eyeYawAngle + eyeHalfFov);
            m00 = 1 / tanCorrectedEyeHalfFov;  //m00 = 0.1737 for Pimax
        }
        else
            isCantedFov = false;
    }

    void OnDisable()
    {
        if (isCantedFov)
        {
            isCantedFov = false;
            m_Camera.ResetCullingMatrix();
        }
    }

    void OnPreCull()
    {
        if(isCantedFov)
        {
            Matrix4x4 projectionMatrix = m_Camera.projectionMatrix;
            projectionMatrix.m00 = m00;
            m_Camera.cullingMatrix = projectionMatrix * m_Camera.worldToCameraMatrix;
        }
    }
}

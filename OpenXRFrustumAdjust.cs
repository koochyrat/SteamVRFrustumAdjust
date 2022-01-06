using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

//Place this script on the Camera (eye) object.
//for canted headsets like pimax, calculate proper culling matrix to avoid objects being culled too early at far edges
//prevents objects popping in and out of view
public class OpenXRFrustumAdjust : MonoBehaviour
{
    private bool isCantedFov = false;
    private Camera m_Camera;
    private Matrix4x4 projectionMatrix = Matrix4x4.identity;
    private bool isInit = false;    //below views are only valid if true
    private OpenXRNativeWrapper.XrView leftView;
    private OpenXRNativeWrapper.XrView rightView;

    // Update is called once per frame
    void OnEnable()
    {
        m_Camera = GetComponent<Camera>();
        isInit = false; //when reenabling, do init again
    }

    void Update()
    {
        //need to wait until view info is available. run once only each time enabled
        if(!isInit && OpenXRNativeWrapper.isInit)
        {

            RenderPipelineManager.beginCameraRendering += RenderPipelineManager_beginCameraRendering;

            leftView = OpenXRNativeWrapper.views[0];
            rightView = OpenXRNativeWrapper.views[1];
            Vector3 leftRot = leftView.pose.orientation.eulerAngles;    //take note this is in a right-handed coord system
            if (leftRot.y > 0)
            {
                isCantedFov = true;
                Vector2 halfFov = new Vector2(
                    Mathf.Max(-leftView.fov.angleLeft, leftView.fov.angleRight, -rightView.fov.angleLeft, rightView.fov.angleRight),
                    Mathf.Max(leftView.fov.angleUp, -leftView.fov.angleDown, rightView.fov.angleUp, -rightView.fov.angleDown)
                );
                float eyeYawAngle = leftRot.y * Mathf.Deg2Rad;
                float eyeHalfFov = halfFov.x;
                float tanCorrectedEyeHalfFovH = Mathf.Tan(eyeYawAngle + eyeHalfFov);

                //increase horizontal fov by the eye rotation angles
                projectionMatrix.m00 = 1 / tanCorrectedEyeHalfFovH;  //m00 = 0.1737 for Pimax

                //because of canting, vertical fov increases towards the corners. calculate the new maximum fov otherwise culling happens too early at corners
                float eyeFovLeft = -leftView.fov.angleLeft;
                float tanCorrectedEyeHalfFovV = Mathf.Tan(halfFov.y) * Mathf.Cos(eyeFovLeft) / Mathf.Cos(eyeFovLeft + eyeYawAngle);
                projectionMatrix.m11 = 1 / tanCorrectedEyeHalfFovV;   //m11 = 0.3969 for Pimax

                //set the near and far clip planes
                projectionMatrix.m22 = -(m_Camera.farClipPlane + m_Camera.nearClipPlane) / (m_Camera.farClipPlane - m_Camera.nearClipPlane);
                projectionMatrix.m23 = -2 * m_Camera.farClipPlane * m_Camera.nearClipPlane / (m_Camera.farClipPlane - m_Camera.nearClipPlane);
                projectionMatrix.m32 = -1;
            }
            else
                isCantedFov = false;

            isInit = true;
        }
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= RenderPipelineManager_beginCameraRendering;

        if (isCantedFov)
        {
            isCantedFov = false;
            m_Camera.ResetCullingMatrix();
        }
    }

    //this only gets called for built-in renderer
    void OnPreCull()
    {
        if (isCantedFov)
        {
            m_Camera.cullingMatrix = projectionMatrix * m_Camera.worldToCameraMatrix;
        }
    }

    //below is for URP/HDRP. need to call OnPreCull manually
    private void RenderPipelineManager_beginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        OnPreCull();
    }
}
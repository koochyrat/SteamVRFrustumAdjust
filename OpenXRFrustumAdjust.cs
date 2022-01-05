using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR.Features;
#if UNITY_EDITOR
using UnityEditor.XR.OpenXR.Features;
#endif
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
                    Mathf.Max(-leftView.fov.angleUp, leftView.fov.angleDown, -rightView.fov.angleUp, rightView.fov.angleDown)
                );
                float eyeYawAngle = leftRot.y;
                float eyeHalfFov = halfFov.x;
                float tanCorrectedEyeHalfFovH = Mathf.Tan(eyeYawAngle + eyeHalfFov);

                //increase horizontal fov by the eye rotation angles
                projectionMatrix.m00 = 1 / tanCorrectedEyeHalfFovH;  //m00 = 0.1737 for Pimax

                //because of canting, vertical fov increases towards the corners. calculate the new maximum fov otherwise culling happens too early at corners
                float eyeFovLeft = -leftView.fov.angleLeft;
                float tanCorrectedEyeHalfFovV = MathF.Tan(halfFov.y) * Mathf.Cos(eyeFovLeft) / Mathf.Cos(eyeFovLeft + eyeYawAngle);
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

#if UNITY_EDITOR
[OpenXRFeature(UiName = "OpenXR Culling Fix")]
#endif
public class OpenXRNativeWrapper : OpenXRFeature
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int _xrGetInstanceProcFunc(ulong instance, string name, out IntPtr addr);
    [MarshalAs(UnmanagedType.FunctionPtr)]
    internal _xrGetInstanceProcFunc _xrGetInstanceProcAddr;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int _xrCreateReferenceSpaceFunc(ulong session, in XrReferenceSpaceCreateInfo createInfo, out ulong space);
    [MarshalAs(UnmanagedType.FunctionPtr)]
    internal _xrCreateReferenceSpaceFunc _xrCreateReferenceSpace;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int _xrDestroySpaceFunc(ulong space);
    [MarshalAs(UnmanagedType.FunctionPtr)]
    internal _xrDestroySpaceFunc _xrDestroySpace;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int _xrLocateViewsFunc(ulong session, in XrViewLocateInfo viewLocateInfo, out XrViewState viewState, int viewCapacityInput, out int viewCountOutput, ref XrView views);
    [MarshalAs(UnmanagedType.FunctionPtr)]
    internal _xrLocateViewsFunc _xrLocateViews;

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct XrPosef
    {
        public Quaternion orientation;
        public Vector3 position;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct XrFovf
    {
        public float angleLeft;
        public float angleRight;
        public float angleUp;
        public float angleDown;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct XrView
    {
        public const int TYPE = 7;  //XR_TYPE_VIEW
        public int type;
        public IntPtr next;
        public XrPosef pose;
        public XrFovf fov;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct XrReferenceSpaceCreateInfo
    {
        public const int TYPE = 37; // XR_TYPE_REFERENCE_SPACE_CREATE_INFO = 37;
        public int type;
        public IntPtr next;
        public int referenceSpaceType; //1 for view
        public XrPosef poseInReferenceSpace;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct XrViewLocateInfo
    {
        public const int TYPE = 6;  // XR_TYPE_VIEW_LOCATE_INFO = 6;
        public int type;
        public IntPtr next;
        public int viewConfigurationType;  //2 for stereo
        public long displayTime;
        public ulong space;    //handle to XrSpace
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct XrViewState
    {
        public const int TYPE = 11; //XR_TYPE_VIEW_STATE
        public int type;
        public IntPtr next;
        public ulong viewStateFlags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct XrFrameState
    {
        public const int TYPE = 44; //XR_TYPE_FRAME_STATE
        public int type;
        IntPtr next;
        long predictedDisplayTime;
        long predictedDisplayPeriod;
        uint shouldRender;  //0 false, 1 true
    }

    private ulong xrInstance;
    private ulong xrSession;
    private ulong xrSpace;
    public static bool isInit;    //dont read views until this is true
    //this contains the stereo views, left then right eye
    public static XrView[] views = new XrView[] { new XrView { type = XrView.TYPE }, new XrView { type = XrView.TYPE } };

    protected override bool OnInstanceCreate(ulong xrInstance)
    {
        this.xrInstance = xrInstance;
        return true;
    }

    protected void Init()
    {
        //the mother of all functions. through this we can get the address of every possible OpenXR function
        _xrGetInstanceProcAddr = Marshal.GetDelegateForFunctionPointer<_xrGetInstanceProcFunc>(xrGetInstanceProcAddr);
        int res;
        IntPtr newProcAddr;

        res = _xrGetInstanceProcAddr(xrInstance, "xrDestroySpace", out newProcAddr);
        Debug.Assert(res == 0);
        _xrDestroySpace = Marshal.GetDelegateForFunctionPointer<_xrDestroySpaceFunc>(newProcAddr);

        res = _xrGetInstanceProcAddr(xrInstance, "xrCreateReferenceSpace", out newProcAddr);
        Debug.Assert(res == 0);
        _xrCreateReferenceSpace = Marshal.GetDelegateForFunctionPointer<_xrCreateReferenceSpaceFunc>(newProcAddr);

        res = _xrGetInstanceProcAddr(xrInstance, "xrLocateViews", out newProcAddr);
        Debug.Assert(res == 0);
        _xrLocateViews = Marshal.GetDelegateForFunctionPointer<_xrLocateViewsFunc>(newProcAddr);
    }
    protected override void OnSessionBegin(ulong xrSession)
    {
        Init();

        this.xrSession = xrSession;

        int res;

        XrPosef xrPose = new XrPosef { orientation = Quaternion.identity, position = Vector3.zero };
        //create a reference space of type view so that we can get the left and right eye transforms relative to head
        XrReferenceSpaceCreateInfo createInfo = new XrReferenceSpaceCreateInfo { type = XrReferenceSpaceCreateInfo.TYPE, next = IntPtr.Zero, referenceSpaceType = 1, poseInReferenceSpace = xrPose };
        res = _xrCreateReferenceSpace(xrSession, in createInfo, out xrSpace);
        Debug.Assert(res == 0);

        //OpenXR spec says never to put time = 0, but it seems to work. we are taking view space anyway which is constant with time. viewConfigurationType 2 means stereo
        XrViewLocateInfo viewLocateInfo = new XrViewLocateInfo { type = XrViewLocateInfo.TYPE, next = IntPtr.Zero, viewConfigurationType = 2, displayTime = 0, space = xrSpace };
        XrViewState viewState;
        int viewOutputN;
        res = _xrLocateViews(xrSession, in viewLocateInfo, out viewState, views.Length, out viewOutputN, ref views[0]); //this retrieves all the parameters of the left and right eyes
        Debug.Assert(res == 0);
        isInit = (res == 0 && viewOutputN == 2);
        Debug.Assert(isInit);

        res = _xrDestroySpace(xrSpace);
        Debug.Assert(res == 0);
    }
}

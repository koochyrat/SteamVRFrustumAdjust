using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
#if UNITY_EDITOR
using UnityEditor.XR.OpenXR.Features;
#endif

#if UNITY_EDITOR
[OpenXRFeature(UiName = "OpenXR Culling Fix",
                OpenxrExtensionStrings = "XR_EXT_hand_tracking XR_EXT_hand_joints_motion_range")]
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

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int _xrGetSystemFunc(ulong instance, in XrSystemGetInfo getInfo, out ulong systemId);
    [MarshalAs(UnmanagedType.FunctionPtr)]
    internal _xrGetSystemFunc _xrGetSystem;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int _xrGetSystemPropertiesFunc(ulong instance, ulong systemId, ref XrSystemProperties properties);
    [MarshalAs(UnmanagedType.FunctionPtr)]
    internal _xrGetSystemPropertiesFunc _xrGetSystemProperties;

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct XrSystemGetInfo
    {
        public const int TYPE = 4;
        public int type;
        public IntPtr next;
        public int formFactor; //1 for vr
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct XrSystemGraphicsProperties
    {
        public int maxSwapchainImageHeight;
        public int maxSwapchainImageWidth;
        public int maxLayerCount;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct XrSystemTrackingProperties
    {
        public int orientationTracking; //0 false 1 true
        public int positionTracking;    //0 false 1 true
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct XrSystemProperties
    {
        public const int TYPE = 5;
        public int type;
        public IntPtr next;
        public ulong systemId;
        public int vendorId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public char[] systemName;
        public XrSystemGraphicsProperties graphicsProperties;
        public XrSystemTrackingProperties trackingProperties;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct XrSystemHandTrackingPropertiesEXT
    {
        public const int TYPE = 1000051000; // XR_TYPE_SYSTEM_HAND_TRACKING_PROPERTIES_EXT = 1000051000
        public int type;
        public IntPtr next;
        public int supportsHandTracking;
    }

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
    private ulong xrSystemId;
    public static bool isInit;    //dont read views until this is true
    //this contains the stereo views, left then right eye
    public static XrView[] views = new XrView[] { new XrView { type = XrView.TYPE }, new XrView { type = XrView.TYPE } };

    protected override bool OnInstanceCreate(ulong xrInstance)
    {
        //foreach (string s in OpenXRRuntime.GetAvailableExtensions())
        //    Debug.Log(s);
        foreach (string s in OpenXRRuntime.GetEnabledExtensions())
            Debug.Log(s);
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

        res = _xrGetInstanceProcAddr(xrInstance, "xrGetSystem", out newProcAddr);
        Debug.Assert(res == 0);
        _xrGetSystem = Marshal.GetDelegateForFunctionPointer<_xrGetSystemFunc>(newProcAddr);

        res = _xrGetInstanceProcAddr(xrInstance, "xrGetSystemProperties", out newProcAddr);
        Debug.Assert(res == 0);
        _xrGetSystemProperties = Marshal.GetDelegateForFunctionPointer<_xrGetSystemPropertiesFunc>(newProcAddr);
    }
    protected override void OnSessionBegin(ulong xrSession)
    {
        Init();

        this.xrSession = xrSession;

        int res;


        ulong systemId;
        res = _xrGetSystem(xrInstance, new XrSystemGetInfo { type = XrSystemGetInfo.TYPE, next = IntPtr.Zero, formFactor = 1 }, out systemId);
        Debug.Assert(res == 0);

        XrSystemHandTrackingPropertiesEXT handy = new XrSystemHandTrackingPropertiesEXT { type = XrSystemHandTrackingPropertiesEXT.TYPE, next = IntPtr.Zero };
        IntPtr pnt = Marshal.AllocHGlobal(Marshal.SizeOf(handy));
        Marshal.StructureToPtr(handy, pnt, false);
        XrSystemProperties sysProp = new XrSystemProperties { type = XrSystemProperties.TYPE, next = pnt };
        res = _xrGetSystemProperties(xrInstance, systemId, ref sysProp);
        Debug.Assert(res == 0);
        handy = Marshal.PtrToStructure<XrSystemHandTrackingPropertiesEXT>(pnt);
        Marshal.FreeHGlobal(pnt);

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

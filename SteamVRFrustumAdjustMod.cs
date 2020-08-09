using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Kch
{
    //This script has been specially made for modding use in MelonLoader.
    //It removes the reference to SteamVR or Valve.VR components and directly references OpenVR so that it is easier to load in
    //without figuring out which assembly the game puts it in.

    //Place this script on the Camera (eye) object.
    //for canted headsets like pimax, calculate proper culling matrix to avoid objects being culled too early at far edges
    //prevents objects popping in and out of view
    public class SteamVRFrustumAdjustMod : MonoBehaviour
    {
        [DllImportAttribute("openvr_api", EntryPoint = "VR_GetGenericInterface", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr GetGenericInterface([In, MarshalAs(UnmanagedType.LPStr)] string pchInterfaceVersion, ref int peError);

        private bool isCantedFov = false;
        private Camera m_Camera;
        private Matrix4x4 projectionMatrix;
        private IVRSystem FnTable;
        private bool isInit = false;

        void OnEnable()
        {
            if (!isInit)
            {
                var eError = 0;
                var pInterface = GetGenericInterface("FnTable:" + "IVRSystem_020", ref eError);
                if (pInterface != IntPtr.Zero && eError == 0)
                {
                    FnTable = (IVRSystem)Marshal.PtrToStructure(pInterface, typeof(IVRSystem));
                    isInit = true;
                }
                else
                {
                    Debug.LogError("Unable to initialize OpenVR");
                    return;
                }
            }
            m_Camera = GetComponent<Camera>();
            HmdMatrix34_t eyeToHeadL = FnTable.GetEyeToHeadTransform(EVREye.Eye_Left);
            if (eyeToHeadL.m0 < 1)  //m0 = 1 for parallel projections
            {
                isCantedFov = true;
                float l_left = 0.0f, l_right = 0.0f, l_top = 0.0f, l_bottom = 0.0f;
                FnTable.GetProjectionRaw(EVREye.Eye_Left, ref l_left, ref l_right, ref l_top, ref l_bottom);
                float r_left = 0.0f, r_right = 0.0f, r_top = 0.0f, r_bottom = 0.0f;
                FnTable.GetProjectionRaw(EVREye.Eye_Right, ref r_left, ref r_right, ref r_top, ref r_bottom);
                Vector2 tanHalfFov = new Vector2(
                    Mathf.Max(-l_left, l_right, -r_left, r_right),
                    Mathf.Max(-l_top, l_bottom, -r_top, r_bottom));
                float eyeYawAngle = Mathf.Acos(eyeToHeadL.m0);  //since there are no x or z rotations, this is y only. 10 deg on Pimax
                float eyeHalfFov = Mathf.Atan(tanHalfFov.x);
                float tanCorrectedEyeHalfFovH = Mathf.Tan(eyeYawAngle + eyeHalfFov);

                //increase horizontal fov by the eye rotation angles
                projectionMatrix.m00 = 1 / tanCorrectedEyeHalfFovH;  //m00 = 0.1737 for Pimax

                //because of canting, vertical fov increases towards the corners. calculate the new maximum fov otherwise culling happens too early at corners
                float eyeFovLeft = Mathf.Atan(-l_left);
                float tanCorrectedEyeHalfFovV = tanHalfFov.y * Mathf.Cos(eyeFovLeft) / Mathf.Cos(eyeFovLeft + eyeYawAngle);
                projectionMatrix.m11 = 1 / tanCorrectedEyeHalfFovV;   //m11 = 0.3969 for Pimax

                //set the near and far clip planes
                projectionMatrix.m22 = -(m_Camera.farClipPlane + m_Camera.nearClipPlane) / (m_Camera.farClipPlane - m_Camera.nearClipPlane);
                projectionMatrix.m23 = -2 * m_Camera.farClipPlane * m_Camera.nearClipPlane / (m_Camera.farClipPlane - m_Camera.nearClipPlane);
                projectionMatrix.m32 = -1;
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
            if (isCantedFov)
            {
                m_Camera.cullingMatrix = projectionMatrix * m_Camera.worldToCameraMatrix;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct IVRSystem
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate void _Dummy();
        [MarshalAs(UnmanagedType.FunctionPtr)]
        internal _Dummy Dummy0;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        internal _Dummy Dummy1;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate void _GetProjectionRaw(EVREye eEye, ref float pfLeft, ref float pfRight, ref float pfTop, ref float pfBottom);
        [MarshalAs(UnmanagedType.FunctionPtr)]
        internal _GetProjectionRaw GetProjectionRaw;

        [MarshalAs(UnmanagedType.FunctionPtr)]
        internal _Dummy Dummy3;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate HmdMatrix34_t _GetEyeToHeadTransform(EVREye eEye);
        [MarshalAs(UnmanagedType.FunctionPtr)]
        internal _GetEyeToHeadTransform GetEyeToHeadTransform;
    }

    public enum EVREye
    {
        Eye_Left = 0,
        Eye_Right = 1,
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct HmdMatrix34_t
    {
        public float m0; //float[3][4]
        public float m1;
        public float m2;
        public float m3;
        public float m4;
        public float m5;
        public float m6;
        public float m7;
        public float m8;
        public float m9;
        public float m10;
        public float m11;
    }
}
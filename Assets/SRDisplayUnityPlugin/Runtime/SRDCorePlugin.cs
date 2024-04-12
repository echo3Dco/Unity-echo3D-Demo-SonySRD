/*
 * Copyright 2019,2020,2021,2023 Sony Corporation
 */

using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

using SRD.Utils;

namespace SRD.Core
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    internal static class SRDStartup
    {
        static SRDStartup()
        {
            SRDCorePlugin.ShowNativeLog();
        }
    }
#endif  // UNITY_EDITOR

    internal static class SRDCorePlugin
    {
        public static bool LinkXrLibraryWin64()
        {
            //return XRRuntimeAPI.LinkXrLibraryWin64();
            return true;
        }

        public static int ShowMessageBox(string title, string message, Action<string> debugLogFunc = null)
        {
            if (debugLogFunc != null)
            {
                debugLogFunc(message);
            }
            return XRRuntimeAPI.ShowMessageBox(SRDApplicationWindow.GetSelfWindowHandle(), title, message);
        }

        [AOT.MonoPInvokeCallback(typeof(XRRuntimeAPI.DebugLogDelegate))]
        private static void RuntimeDebugLogCallback(string message, SrdXrLogLevels log_levels)
        {
            switch (log_levels)
            {
                case SrdXrLogLevels.LOG_LEVELS_TRACE:
                case SrdXrLogLevels.LOG_LEVELS_DEBUG:
                case SrdXrLogLevels.LOG_LEVELS_INFO:
                    Debug.Log(message);
                    break;
                case SrdXrLogLevels.LOG_LEVELS_WARN:
                    Debug.LogWarning(message);
                    break;
                case SrdXrLogLevels.LOG_LEVELS_ERR:
                    Debug.LogError(message);
                    break;
                default:
                    break;
            }
        }

        public static SrdXrResult ShowNativeLog()
        {
            return XRRuntimeAPI.SetDebugLogCallback(RuntimeDebugLogCallback);
        }

        public static void UnlinkXrLibraryWin64()
        {
            //XRRuntimeAPI.UnlinkXrLibraryWin64();
        }

        public static SrdXrResult CreateSession()
        {
            return XRRuntimeAPI.StartSession();
        }

        public static void DestroySession()
        {
            XRRuntimeAPI.EndSession();
        }

        public static void Update()
        {
            XRRuntimeAPI.UpdateTrackingResultCache();
        }

        public static SrdXrResult EndFrame(IntPtr session, bool callInMainThread = true, bool callinRenderThread = false)
        {
            var end_info = new SrdXrFrameEndInfo();
            if (callinRenderThread)
            {
                GL.IssuePluginEvent(XRRuntimeAPI.GetEndFramePtr(session), 0);
            }
            if (callInMainThread)
            {
                //return XRRuntimeAPI.EndFrame(session, ref end_info);
                return SrdXrResult.SUCCESS;
            }
            return SrdXrResult.SUCCESS;
        }

        public static SrdXrResult GetXrSystemError(out SrdXrSystemError error)
        {
            return XRRuntimeAPI.GetXrSystemError(out error);
        }

        public static SrdXrResult GetXrSystemErrorNum(out UInt16 num)
        {
            return XRRuntimeAPI.GetXrSystemErrorNum(out num);
        }

        public static SrdXrResult GetXrSystemErrorList(SrdXrSystemError[] errors)
        {
            return XRRuntimeAPI.GetXrSystemErrorList((ushort)errors.Length, errors);
        }

        public static void GenerateTextureAndShaders(IntPtr session, ref SrdXrTexture leftTextureData, ref SrdXrTexture rightTextureData, ref SrdXrTexture outTextureData)
        {
            GL.IssuePluginEvent(XRRuntimeAPI.GetGenerateTextureAndShadersPtr(leftTextureData.texture, ref leftTextureData, ref rightTextureData, ref outTextureData), 0);
        }

        public static SrdXrResult ShowCameraWindow(bool show)
        {
            return XRRuntimeAPI.ShowCameraWindow(show);
        }

        public static SrdXrResult GetPauseHeadPose(out bool pause)
        {
            return XRRuntimeAPI.GetPauseHeadPose(out pause);
        }

        public static SrdXrResult SetPauseHeadPose(bool pause)
        {
            return XRRuntimeAPI.SetPauseHeadPose(pause);
        }

        public static SrdXrResult EnableStereo(bool enable)
        {
            return XRRuntimeAPI.EnableStereo(enable);
        }

        public static SrdXrResult GetFacePose(IntPtr session,
                                              out Pose headPose, out Pose eyePoseL, out Pose eyePoseR)
        {
            var result = XRRuntimeAPI.GetCachedHeadPose(out var xrHeadPose);
            headPose = ToUnityPose(xrHeadPose.pose);
            eyePoseL = ToUnityPose(xrHeadPose.left_eye_pose);
            eyePoseR = ToUnityPose(xrHeadPose.right_eye_pose);

            return result;
        }

        public static SrdXrResult GetProjectionMatrix(IntPtr session, float nearClip, float farClip,
                                                      out Matrix4x4 headProjectionMatrix, 
                                                      out Matrix4x4 eyeProjectionMatrixL, 
                                                      out Matrix4x4 eyeProjectionMatrixR)
        {
            var result = XRRuntimeAPI.GetProjectionMatrix(nearClip, farClip, out var xrProjMat);
            headProjectionMatrix = ToUnityMatrix4x4(xrProjMat.projection);
            eyeProjectionMatrixL = ToUnityMatrix4x4(xrProjMat.left_projection);
            eyeProjectionMatrixR = ToUnityMatrix4x4(xrProjMat.right_projection);
            return result;
        }

        public static SrdXrResult GetTargetMonitorRectangle(out SrdXrRect rect)
        {
            return XRRuntimeAPI.GetTargetMonitorRectangle(out rect);
        }

        public static SrdXrResult GetDisplaySpec(out SrdXrSRDData displaySpec)
        {
            return XRRuntimeAPI.GetDisplaySpec(out displaySpec);
        }

        public static SrdXrResult SelectDevice(out SrdXrDeviceInfo device)
        {
            device = new SrdXrDeviceInfo();
            SrdXrResult result = XRRuntimeAPI.GetRealDeviceNum(out var deviceCount);
            if (result != SrdXrResult.SUCCESS)
            {
                return result;
            }
            if (deviceCount == 0)
            {
                return SrdXrResult.ERROR_DEVICE_NOT_FOUND;
            }

            var devices = new SrdXrDeviceInfo[deviceCount];
            result = XRRuntimeAPI.EnumerateRealDevices(deviceCount, devices);
            if (result != SrdXrResult.SUCCESS)
            {
                return result;
            }

            if (deviceCount == 1)
            {
                result = XRRuntimeAPI.SelectDevice(0);
                if (result == SrdXrResult.SUCCESS)
                {
                    device = devices[0];
                }
            }
            else
            {
                var item_list = new string[deviceCount];
                for (var i = 0; i < deviceCount; ++i)
                {
                    item_list[i] = devices[i].product_id;
                    if (!String.IsNullOrWhiteSpace(devices[i].device_serial_number))
                    {
                        item_list[i] += ' ' + devices[i].device_serial_number;
                    }
                }

                var device_index = XRRuntimeAPI.ShowComboBoxDialog(
                    SRDApplicationWindow.GetSelfWindowHandle(), item_list, (int)deviceCount);

                if (device_index < 0)
                {
                    return SrdXrResult.ERROR_USER_CANCEL;
                }
                else if (deviceCount <= device_index)
                {
                    return SrdXrResult.ERROR_RUNTIME_FAILURE;
                }

                result = XRRuntimeAPI.SelectDevice((uint)device_index);
                if (result == SrdXrResult.SUCCESS)
                {
                    device = devices[device_index];
                }
            }
            return result;
        }

        public const SrdXrCrosstalkCorrectionMode DefaultCrosstalkCorrectionMode = SrdXrCrosstalkCorrectionMode.GRADATION_CORRECTION_MEDIUM;

        public static SrdXrResult SetCrosstalkCorrectionMode(SrdXrCrosstalkCorrectionMode mode = DefaultCrosstalkCorrectionMode)
        {
            return XRRuntimeAPI.SetCrosstalkCorrectionMode(mode);
        }

        public static SrdXrResult GetCrosstalkCorrectionMode(out SrdXrCrosstalkCorrectionMode mode)
        {
            return XRRuntimeAPI.GetCrosstalkCorrectionMode(out mode);
        }

        public static void SetColorSpaceSettings(ColorSpace colorSpace, GraphicsDeviceType graphicsAPI, RenderPipelineType renderPipeline)
        {
            Debug.Assert((colorSpace == ColorSpace.Gamma) || (colorSpace == ColorSpace.Linear));

            var unityGamma = 2.2f;
            int input_gamma_count = (colorSpace != ColorSpace.Gamma) ? 0 : 1;
            int output_gamma_count = input_gamma_count;
            if ((!SRDCorePlugin.IsARGBHalfSupported()) && (colorSpace == ColorSpace.Linear) && (graphicsAPI == GraphicsDeviceType.Direct3D11))
            {
                output_gamma_count = 1;
            }

            XRRuntimeAPI.SetColorSpace(input_gamma_count, output_gamma_count, unityGamma);
        }

        public static bool GetCountOfSupportedDevices(out Int32 size)
        {
            return XRRuntimeAPI.GetCountOfSupportedDevices(out size);
        }

        public static bool GetPanelSpecOfSupportedDevices(supported_panel_spec[] panel_specs)
        {
            return XRRuntimeAPI.GetPanelSpecOfSupportedDevices(panel_specs, panel_specs.Length);
        }

        public static SrdXrResult GetPerformancePriorityEnabled(out bool enable)
        {
            return XRRuntimeAPI.GetPerformancePriorityEnabled(out enable);
        }

        public static SrdXrResult GetLensShiftEnabled(out bool enable)
        {
            return XRRuntimeAPI.GetLensShiftEnabled(out enable);
        }

        public static SrdXrResult SetLensShiftEnabled(bool enable)
        {
            return XRRuntimeAPI.SetLensShiftEnabled(enable);
        }

        public static bool IsARGBHalfSupported()
        {
            UInt16 major = 0, minor = 0, revision = 0;
            var ret = XRRuntimeAPI.GetXrRuntimeVersion(out major, out minor, out revision);
            if (ret != SrdXrResult.SUCCESS)
            {
                return false;
            }
            var version = major * 10000 + minor * 100 + revision;
            return (version < 20101) ? false : true;
        }

        private struct XRRuntimeAPI
        {
            const string dll_path = SRD.Utils.SRDHelper.SRDConstants.XRRuntimeWrapperDLLName;

            [DllImport(dll_path, EntryPoint = "get_BeginFrame_func")]
            public static extern IntPtr GetBeginFramePtr(IntPtr session);

            [DllImport(dll_path, EntryPoint = "get_EndFrame_func")]
            public static extern IntPtr GetEndFramePtr(IntPtr session);

            [DllImport(dll_path, EntryPoint = "get_GenerateTextureAndShaders_func")]
            public static extern IntPtr GetGenerateTextureAndShadersPtr(IntPtr session, [In] ref SrdXrTexture left_texture, [In] ref SrdXrTexture right_texture, [In] ref SrdXrTexture render_target);

            [DllImport(dll_path, EntryPoint = "srd_xrShowMessageBox", CharSet = CharSet.Unicode)]
            public static extern int ShowMessageBox(IntPtr hWnd, string title, string msg);

            [DllImport(dll_path, EntryPoint = "srd_xr_extEnumerateDevices")]
            public static extern SrdXrResult EnumerateDevices(UInt32 load_count, [In, Out] SrdXrDeviceInfo[] devices);

            [DllImport(dll_path, EntryPoint = "srd_xr_extGetDeviceNum")]
            public static extern SrdXrResult GetDeviceNum([In, Out] ref UInt32 num);

            [DllImport(dll_path, EntryPoint = "srd_xr_extEnumerateRealDevices")]
            public static extern SrdXrResult EnumerateRealDevices(UInt32 load_count, [In, Out] SrdXrDeviceInfo[] devices);

            [DllImport(dll_path, EntryPoint = "srd_xr_extGetRealDeviceNum")]
            public static extern SrdXrResult GetRealDeviceNum(out UInt32 num);

            [DllImport(dll_path, EntryPoint = "srd_xr_SelectDevice")]
            public static extern SrdXrResult SelectDevice(UInt32 device_index);

            [DllImport(dll_path, EntryPoint = "srd_xr_StartSession")]
            public static extern SrdXrResult StartSession();

            [DllImport(dll_path, EntryPoint = "srd_xr_EndSession")]
            public static extern SrdXrResult EndSession();

            [DllImport(dll_path, EntryPoint = "srd_xr_EnableStereo")]
            public static extern SrdXrResult EnableStereo([MarshalAs(UnmanagedType.U1)] bool enable);

            [DllImport(dll_path, EntryPoint = "srd_xr_UpdateTrackingResultCache")]
            public static extern SrdXrResult UpdateTrackingResultCache();

            [DllImport(dll_path, EntryPoint = "srd_xr_GetCachedPose")]
            public static extern SrdXrResult GetCachedPose(SrdXrPoseId pose_id, out SrdXrPosef pose);

            [DllImport(dll_path, EntryPoint = "srd_xr_GetCachedHeadPose")]
            public static extern SrdXrResult GetCachedHeadPose(out SrdXrHeadPosef pose);

            [DllImport(dll_path, EntryPoint = "srd_xr_GetProjectionMatrix")]
            public static extern SrdXrResult GetProjectionMatrix(float near_clip, float far_clip, out SrdXrProjectionMatrix data);

            [DllImport(dll_path, EntryPoint = "srd_xr_GetTargetMonitorRectangle")]
            public static extern SrdXrResult GetTargetMonitorRectangle(out SrdXrRect rect);

            [DllImport(dll_path, EntryPoint = "srd_xr_GetDisplaySpec")]
            public static extern SrdXrResult GetDisplaySpec(out SrdXrSRDData data);

            [DllImport(dll_path, EntryPoint = "srd_xr_extSetColorSpace")]
            public static extern void SetColorSpace(int input_gamma_count, int output_gamma_count, float gamma);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void DebugLogDelegate([MarshalAs(UnmanagedType.LPStr)] string str, SrdXrLogLevels log_levels);
            [DllImport(dll_path, EntryPoint = "srd_xr_SetDebugLogCallback")]
            public static extern SrdXrResult SetDebugLogCallback(DebugLogDelegate debug_log_delegate);

            [DllImport(dll_path, EntryPoint = "srd_xr_GetXrSystemError")]
            public static extern SrdXrResult GetXrSystemError([Out] out SrdXrSystemError error);

            [DllImport(dll_path, EntryPoint = "srd_xr_GetXrSystemErrorNum")]
            public static extern SrdXrResult GetXrSystemErrorNum([Out] out UInt16 num);

            [DllImport(dll_path, EntryPoint = "srd_xr_GetXrSystemErrorList")]
            public static extern SrdXrResult GetXrSystemErrorList(UInt16 num, [Out] SrdXrSystemError[] errors);

            [DllImport(dll_path, EntryPoint = "srd_xr_GetXrRuntimeVersion")]
            public static extern SrdXrResult GetXrRuntimeVersion([Out] out UInt16 major, [Out] out UInt16 mainor, [Out] out UInt16 revision);

            [DllImport(dll_path, EntryPoint = "srd_ax_SetCameraWindowEnabled")]
            public static extern SrdXrResult ShowCameraWindow([MarshalAs(UnmanagedType.U1)] bool show);

            [DllImport(dll_path, EntryPoint = "srd_ax_GetPauseHeadPose")]
            public static extern SrdXrResult GetPauseHeadPose([MarshalAs(UnmanagedType.U1)] out bool pause);

            [DllImport(dll_path, EntryPoint = "srd_ax_SetPauseHeadPose")]
            public static extern SrdXrResult SetPauseHeadPose([MarshalAs(UnmanagedType.U1)] bool pause);

            [DllImport(dll_path, EntryPoint = "srd_ax_GetCrosstalkCorrectionMode")]
            public static extern SrdXrResult GetCrosstalkCorrectionMode(out SrdXrCrosstalkCorrectionMode mode);

            [DllImport(dll_path, EntryPoint = "srd_ax_SetCrosstalkCorrectionMode")]
            public static extern SrdXrResult SetCrosstalkCorrectionMode(SrdXrCrosstalkCorrectionMode mode);

            [DllImport(dll_path, EntryPoint = "srd_ax_ShowComboBoxDialog", CharSet = CharSet.Unicode)]
            public static extern Int64 ShowComboBoxDialog(IntPtr hWnd, [In, MarshalAs(UnmanagedType.LPArray,
                                              ArraySubType = UnmanagedType.LPWStr)] string[] item_list, Int32 size);

            [DllImport(dll_path, EntryPoint = "srd_ax_GetCountOfSupportedDevices")]
            [return: MarshalAs(UnmanagedType.U1)]
            public static extern bool GetCountOfSupportedDevices(out Int32 size);

            [DllImport(dll_path, EntryPoint = "srd_ax_GetPanelSpecOfSupportedDevices")]
            [return: MarshalAs(UnmanagedType.U1)]
            public static extern bool GetPanelSpecOfSupportedDevices([Out] supported_panel_spec[] panel_specs, Int32 size);

            [DllImport(dll_path, EntryPoint = "srd_ax_GetPerformancePriorityEnabled")]
            public static extern SrdXrResult GetPerformancePriorityEnabled([MarshalAs(UnmanagedType.U1)] out bool enable);

            [DllImport(dll_path, EntryPoint = "srd_ax_GetLensShiftEnabled")]
            public static extern SrdXrResult GetLensShiftEnabled([MarshalAs(UnmanagedType.U1)] out bool enable);

            [DllImport(dll_path, EntryPoint = "srd_ax_SetLensShiftEnabled")]
            public static extern SrdXrResult SetLensShiftEnabled([MarshalAs(UnmanagedType.U1)] bool enable);

        }


        private static Pose ToUnityPose(SrdXrPosef p)
        {
            return new Pose(ToUnityVector(p.position), ToUnityQuaternion(p.orientation));
        }

        private static Vector3 ToUnityVector(SrdXrVector3f v)
        {
            return new Vector3(v.x, v.y, v.z);
        }

        private static Quaternion ToUnityQuaternion(SrdXrQuaternionf q)
        {
            return new Quaternion(q.x, q.y, q.z, q.w);
        }

        private static Matrix4x4 ToUnityMatrix4x4(SrdXrMatrix4x4f m)
        {
            var ret = new Matrix4x4();
            for (var i = 0; i < 16; i++)
            {
                ret[i] = m.matrix[i];
            }
            return ret;
        }

    }

    public enum SrdXrResult
    {
        SUCCESS = 0,
        ERROR_RUNTIME_NOT_FOUND = -1,
        ERROR_VALIDATION_FAILURE = -2,
        ERROR_RUNTIME_FAILURE = -3,
        ERROR_FUNCTION_UNSUPPORTED = -4,
        ERROR_HANDLE_INVALID = -5,
        ERROR_SESSION_CREATED = -6,
        ERROR_SESSION_READY = -7,
        ERROR_SESSION_STARTING = -8,
        ERROR_SESSION_RUNNING = -9,
        ERROR_SESSION_STOPPING = -10,
        ERROR_SESSION_NOT_CREATE = -11,
        ERROR_SESSION_NOT_READY = -12,
        ERROR_SESSION_NOT_RUNNING = -13,
        ERROR_SESSION_STILL_USED = -14,
        ERROR_POSE_INVALID = -15,
        ERROR_SET_DATA_FAILURE = -16,
        ERROR_GET_DATA_FAILURE = -17,
        ERROR_FILE_ACCESS_ERROR = -18,
        ERROR_DEVICE_NOT_FOUND = -19,
        ERROR_RUNTIME_UNSUPPORTED = -20,

        // Following error codes are plugin-defined error codes
        ERROR_USER_CANCEL = -2001,

        RESULT_MAX_ENUM = 0x7FFFFFFF
    };

    public enum SrdXrSystemErrorResult
    {
        SYSTEM_ERROR_RESULT_SUCCESS = 0,
        SYSTEM_ERROR_RESULT_WARNING = 1,
        SYSTEM_ERROR_RESULT_ERROR = 2,
    };

    public enum SrdXrSystemErrorCode
    {
        SYSTEM_ERROR_SUCCESS = 0,
        SYSTEM_ERROR_NO_AVAILABLE_DEVICE = 1,
        SYSTEM_ERROR_DEVICE_LOST = 2,
        SYSTEM_ERROR_DEVICE_BUSY = 3,
        SYSTEM_ERROR_INVALID_DATA = 4,
        SYSTEM_ERROR_NO_DATA = 5,
        SYSTEM_ERROR_OPERATION_FAILED = 6,
        SYSTEM_ERROR_USB_NOT_CONNECTED = 7,
        SYSTEM_ERROR_CAMERA_WITH_USB20 = 8,
        SYSTEM_ERROR_NO_USB_OR_NO_POWER = 9,
        SYSTEM_ERROR_ANOTHER_APPLICATION_RUNNING = -10002,
    };

    public enum SrdXrPlatformId
    {
        PLATFORM_ID_SRD = 0,
        PLATFORM_ID_MAX_ENUM = 0x7FFFFFFF
    };

    public enum SrdXrViewConfigurationType
    {
        VIEW_CONFIGURATION_TYPE_PRIMARY_MONO = 1,
        VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO = 2,
        VIEW_CONFIGURATION_TYPE_MAX_ENUM = 0x7FFFFFFF
    };

    public enum SrdXrEnvironmentBlendMode
    {
        ENVIRONMENT_BLEND_MODE_OPAQUE = 1,
        ENVIRONMENT_BLEND_MODE_ADDITIVE = 2,
        ENVIRONMENT_BLEND_MODE_ALPHA_BLEND = 3,
        ENVIRONMENT_BLEND_MODE_MAX_ENUM = 0x7FFFFFFF
    };

    public enum SrdXrSessionState
    {
        SESSION_STATE_UNKNOWN = 0,
        SESSION_STATE_IDLE = 1,
        SESSION_STATE_READY = 2,
        SESSION_STATE_SYNCHRONIZED = 3,
        SESSION_STATE_VISIBLE = 4,
        SESSION_STATE_FOCUSED = 5,
        SESSION_STATE_STOPPING = 6,
        SESSION_STATE_LOSS_PENDING = 7,
        SESSION_STATE_EXITING = 8,
        SESSION_STATE_MAX_ENUM = 0x7FFFFFFF
    };

    public enum SrdXrCoordinateSystem
    {
        COORDINATE_SYSTEM_RIGHT_Y_UP_Z_BACK = 0,
        COORDINATE_SYSTEM_RIGHT_Y_UP_Z_FORWARD = 1,
        COORDINATE_SYSTEM_LEFT_Y_UP_Z_FORWARD = 2,
        COORDINATE_SYSTEM_LEFT_Z_UP_X_FORWARD = 3,
        COORDINATE_SYSTEM_MAX_ENUM = 0x7FFFFFFF
    };

    public enum SrdXrCoordinateUnit
    {
        COORDINATE_UNIT_METER = 0,
        COORDINATE_UNIT_CENTIMETER = 1,
        COORDINATE_UNIT_MILLIMETER = 2,
        COORDINATE_UNIT_MAX_ENUM = 0x7FFFFFFF
    };

    public enum SrdXrGraphicsAPI
    {
        GRAPHICS_API_GL = 0,
        GRAPHICS_API_DirectX = 1,
        GRAPHICS_API_MAX_ENUM = 0x7FFFFFFF
    };

    public enum SrdXrCompositionLayerFlags
    {
        COMPOSITION_LAYER_CORRECT_CHROMATIC_ABERRATION_BIT = 0x00000001,
        COMPOSITION_LAYER_BLEND_TEXTURE_SOURCE_ALPHA_BIT = 0x00000002,
        COMPOSITION_LAYER_UNPREMULTIPLIED_ALPHA_BIT = 0x00000004
    }

    public enum SrdXrLogLevels
    {
        LOG_LEVELS_TRACE = 0,
        LOG_LEVELS_DEBUG = 1,
        LOG_LEVELS_INFO = 2,
        LOG_LEVELS_WARN = 3,
        LOG_LEVELS_ERR = 4,
        LOG_LEVELS_CRITICAL = 5,
        LOG_LEVELS_OFF = 6,
        LOG_LEVELS_MAX_ENUM = 0x7FFFFFFF
    };

    public enum SrdXrDeviceConnectionState
    {
        DEVICE_NOT_CONNECTED = 0,
        DEVICE_CONNECTED = 1,
    };

    public enum SrdXrDevicePowerState
    {
        DEVICE_POWER_OFF = 0,
        DEVICE_POWER_ON = 1,
    };

    public enum SrdXrPoseId
    {
        POSE_ID_HEAD = 0,
        POSE_ID_LEFT_EYE = 1,
        POSE_ID_RIGHT_EYE = 2,
    };

    public enum SrdXrCrosstalkCorrectionMode
    {
        DISABLED = 0,
        DEPENDS_ON_APPLICATION = 1,
        GRADATION_CORRECTION_MEDIUM = 2,
        GRADATION_CORRECTION_ALL = 3,
        GRADATION_CORRECTION_HIGH_PRECISE = 4,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrSessionCreateInfo
    {
        public SrdXrPlatformId platform_id;
        public SrdXrCoordinateSystem coordinate_system;
        public SrdXrCoordinateUnit coordinate_unit;
        public SrdXrDeviceInfo device;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrSessionBeginInfo
    {
        public SrdXrViewConfigurationType primary_view_configuration_type;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrEventDataBuffer
    {
        public SrdXrSessionState state;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrCompositionLayerBaseHeader
    {
        public SrdXrCompositionLayerFlags layer_flags;
        public IntPtr space;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrFrameEndInfo
    {
        public Int64 display_time;
        public UInt32 layer_count;
        public IntPtr layers;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrViewLocateInfo
    {
        public SrdXrViewConfigurationType view_configuration_type;
        public Int64 display_time;
        public IntPtr space;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrPosef
    {
        public SrdXrQuaternionf orientation;
        public SrdXrVector3f position;

        public SrdXrPosef(SrdXrQuaternionf in_orientation, SrdXrVector3f in_position)
        {
            orientation = in_orientation;
            position = in_position;
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrHeadPosef
    {
        public SrdXrPosef pose;
        public SrdXrPosef left_eye_pose;
        public SrdXrPosef right_eye_pose;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrFovf
    {
        public float angle_left;
        public float angle_right;
        public float angle_up;
        public float angle_down;

        public SrdXrFovf(float in_angle)
        {
            angle_left = in_angle;
            angle_right = in_angle;
            angle_up = in_angle;
            angle_down = in_angle;
        }
        public SrdXrFovf(float in_angle_left, float in_angle_right, float in_angle_up, float in_angle_down)
        {
            angle_left = in_angle_left;
            angle_right = in_angle_right;
            angle_up = in_angle_up;
            angle_down = in_angle_down;
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrView
    {
        public SrdXrPosef pose;
        public SrdXrFovf fov;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrProjectionMatrixInfo
    {
        public SrdXrGraphicsAPI graphics_api;
        public SrdXrCoordinateSystem coordinate_system;
        public float near_clip;  // The unit is arbitrary
        public float far_clip;  // The unit is arbitrary
        public bool reversed_z;

        public SrdXrProjectionMatrixInfo(SrdXrGraphicsAPI in_graphics_api, SrdXrCoordinateSystem in_coordinate_system, float in_near_clip, float in_far_clip, bool in_reversed_z)
        {
            graphics_api = in_graphics_api;
            coordinate_system = in_coordinate_system;
            near_clip = in_near_clip;
            far_clip = in_far_clip;
            reversed_z = in_reversed_z;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrDisplaySize
    {
        public float width_m;
        public float height_m;

        public SrdXrDisplaySize(float in_width_m, float in_height_m)
        {
            width_m = in_width_m;
            height_m = in_height_m;
        }

        public static implicit operator Vector2(SrdXrDisplaySize display)
        {
            return new Vector2(display.width_m, display.height_m);
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrRect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;

        public SrdXrRect(int in_left, int in_top, int in_right, int in_bottom)
        {
            left = in_left;
            top = in_top;
            right = in_right;
            bottom = in_bottom;
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrDisplayResolution
    {
        public UInt32 width;
        public UInt32 height;
        public UInt32 area;

        public SrdXrDisplayResolution(UInt32 in_width, UInt32 in_height)
        {
            width = in_width;
            height = in_height;
            area = width * height;
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrProjectionMatrix
    {
        public SrdXrMatrix4x4f projection;
        public SrdXrMatrix4x4f left_projection;
        public SrdXrMatrix4x4f right_projection;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrDisplayLocateInfo
    {
        public Int64 display_time;
        public IntPtr space;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrDisplay
    {
        public SrdXrDisplaySize display_size;
        public SrdXrDisplayResolution display_resolution;
        public SrdXrPosef display_pose;

        public SrdXrDisplay(SrdXrDisplaySize in_display_size, SrdXrDisplayResolution in_display_resolution, SrdXrPosef in_display_pose)
        {
            display_size = in_display_size;
            display_resolution = in_display_resolution;
            display_pose = in_display_pose;
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrTexture
    {
        public IntPtr texture;
        public UInt32 width;
        public UInt32 height;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrQuaternionf
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public SrdXrQuaternionf(float in_x, float in_y, float in_z, float in_w)
        {
            x = in_x;
            y = in_y;
            z = in_z;
            w = in_w;
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrVector3f
    {
        public float x;
        public float y;
        public float z;

        public SrdXrVector3f(float in_x, float in_y, float in_z)
        {
            x = in_x;
            y = in_y;
            z = in_z;
        }

        public static SrdXrVector3f operator +(SrdXrVector3f a, SrdXrVector3f b)
        => new SrdXrVector3f(a.x + b.x, a.y + b.y, a.z + b.z);

        public static SrdXrVector3f operator -(SrdXrVector3f a, SrdXrVector3f b)
        => new SrdXrVector3f(a.x - b.x, a.y - b.y, a.z - b.z);

        public static SrdXrVector3f operator *(SrdXrVector3f a, float b)
        => new SrdXrVector3f(a.x * b, a.y * b, a.z * b);

        public static SrdXrVector3f operator /(SrdXrVector3f a, float b)
        => new SrdXrVector3f(a.x / b, a.y / b, a.z / b);

        public float Dot(SrdXrVector3f a)
        {
            return x * a.x + y * a.y + z * a.z;
        }

        public void Normalize()
        {
            float length = (float)Math.Sqrt(x * x + y * y + z * z);
            x /= length;
            y /= length;
            z /= length;
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrVector4f
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public SrdXrVector4f(float in_x, float in_y, float in_z, float in_w)
        {
            x = in_x;
            y = in_y;
            z = in_z;
            w = in_w;
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrMatrix4x4f
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4 * 4)]
        public float[] matrix;

        public SrdXrMatrix4x4f(SrdXrVector4f in_x, SrdXrVector4f in_y, SrdXrVector4f in_z, SrdXrVector4f in_w)
        {
            matrix = new float[4 * 4];

            matrix[4 * 0 + 0] = in_x.x;
            matrix[4 * 0 + 1] = in_x.y;
            matrix[4 * 0 + 2] = in_x.z;
            matrix[4 * 0 + 3] = in_x.w;
            matrix[4 * 1 + 0] = in_y.x;
            matrix[4 * 1 + 1] = in_y.y;
            matrix[4 * 1 + 2] = in_y.z;
            matrix[4 * 1 + 3] = in_y.w;
            matrix[4 * 2 + 0] = in_z.x;
            matrix[4 * 2 + 1] = in_z.y;
            matrix[4 * 2 + 2] = in_z.z;
            matrix[4 * 2 + 3] = in_z.w;
            matrix[4 * 3 + 0] = in_w.x;
            matrix[4 * 3 + 1] = in_w.y;
            matrix[4 * 3 + 2] = in_w.z;
            matrix[4 * 3 + 3] = in_w.w;
        }
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct SrdXrDeviceInfo
    {
        public UInt32 device_index;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string device_serial_number;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string product_id;
        public SrdXrRect target_monitor_rectangle;
        public SrdXrRect primary_monitor_rectangle;

        public SrdXrDeviceInfo(UInt32 in_device_index, string in_device_serial_number
                               , string in_product_id, SrdXrRect in_target_monitor_rectangle, SrdXrRect in_primary_monitor_rectangle)
        {
            device_index = in_device_index;
            device_serial_number = in_device_serial_number;
            product_id = in_product_id;
            target_monitor_rectangle = in_target_monitor_rectangle;
            primary_monitor_rectangle = in_primary_monitor_rectangle;
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrSRDData
    {
        public SrdXrDisplaySize display_size;
        public SrdXrDisplayResolution display_resolution;
        public float display_tilt_rad;

        public SrdXrSRDData(SrdXrDisplaySize in_display_size, float in_display_tilt_rad
                            , SrdXrDisplayResolution in_display_resolution)
        {
            display_size = in_display_size;
            display_tilt_rad = in_display_tilt_rad;
            display_resolution = in_display_resolution;
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SrdXrDeviceState
    {
        public SrdXrDeviceConnectionState connection_state;
        public SrdXrDevicePowerState power_state;

        public SrdXrDeviceState(SrdXrDeviceConnectionState in_connection_state, SrdXrDevicePowerState in_power_state)
        {
            connection_state = in_connection_state;
            power_state = in_power_state;
        }
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct supported_panel_spec
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string device_name; // max 15 characters
        public float width;    // in meter
        public float height;   // in meter
        public float angle;    // in radian
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct SrdXrSystemError
    {
        [MarshalAs(UnmanagedType.U1)]
        public SrdXrSystemErrorResult result;
        [MarshalAs(UnmanagedType.I4)]
        public SrdXrSystemErrorCode code;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string msg;
    };
}

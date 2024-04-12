/*
 * Copyright 2019,2020,2023 Sony Corporation
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEditor;
using SRD.Utils;

namespace SRD.Core
{
    internal interface ISRDSubsystem : IDisposable
    {
        void Start();
        void Stop();
    }

    internal class SRDSessionHandler
    {
        private static SRDSessionHandler _instance;
        public static SRDSessionHandler Instance
        {
            get
            {
                if(_instance == null)
                {
                    _instance = new SRDSessionHandler();
                }
                return _instance;
            }
        }

        private IntPtr _sessionHandle;
        public static IntPtr SessionHandle
        {
            get { return Instance._sessionHandle; }
        }

        private bool _isSessionRunning = false;
        public static bool IsSessionRunning
        {
            get { return Instance._isSessionRunning; }
        }

        private bool _isLibraryLinked = true;

        // PrevSessionState, CurrSessionState
        public Action<SrdXrSessionState, SrdXrSessionState> OnSessionStateChangedEvent;

        private List<ISRDSubsystem> _srdSubsystems;

        private SrdXrSessionState _prevState;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#else
        [UnityEngine.RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void InitializeOnLoad()
        {
            Application.quitting += () =>
            {
                if(Instance._isSessionRunning)
                {
                    var result = Instance.DestroySession();
                    if(result)
                    {
                        Instance._isSessionRunning = false;
                    }
                }
            };
        }

        private SRDSessionHandler()
        {
            var isLinked = SRDCorePlugin.LinkXrLibraryWin64();
            if(!isLinked && _isLibraryLinked)
            {
                SRDHelper.PopupMessageAndForceToTerminate(SRDHelper.SRDMessages.DLLNotFoundError);
                _isLibraryLinked = false;
                return;
            }
            _isLibraryLinked = isLinked;

            _prevState = SrdXrSessionState.SESSION_STATE_MAX_ENUM;

            _srdSubsystems = new List<ISRDSubsystem>();
        }

        ~SRDSessionHandler()
        {
            SRDCorePlugin.DestroySession();
        }

        internal bool CreateSession()
        {
            if(_sessionHandle != IntPtr.Zero)
            {
                return true;
            }
            var result = CreateSession(out _sessionHandle);
            return result;
        }

        static public bool CreateSession(out IntPtr sessionHandle)
        {
            sessionHandle = IntPtr.Zero;

            if(SRDProjectSettings.IsRunWithoutSRDisplayMode())
            {
                return true;
            }

            var result = SRDCorePlugin.CreateSession();
            if (result != SrdXrResult.SUCCESS)
            {
                var errorToMessage = new Dictionary<SrdXrResult, string>()
                {
                    { SrdXrResult.ERROR_RUNTIME_NOT_FOUND, SRDHelper.SRDMessages.DLLNotFoundError},
                    { SrdXrResult.ERROR_VALIDATION_FAILURE, SRDHelper.SRDMessages.NoDeviceSelectedError},
                    { SrdXrResult.ERROR_DEVICE_NOT_FOUND, SRDHelper.SRDMessages.DisplayConnectionError},
                    { SrdXrResult.ERROR_RUNTIME_UNSUPPORTED, SRDHelper.SRDMessages.OldRuntimeUnsupportedError},
                    { SrdXrResult.ERROR_SESSION_NOT_CREATE, SRDHelper.SRDMessages.UnknownError},
                    { SrdXrResult.ERROR_SESSION_STILL_USED, SRDHelper.SRDMessages.AppConflictionError},
                };

                if (errorToMessage.ContainsKey(result))
                {
                    SRDHelper.PopupMessageAndForceToTerminate(errorToMessage[result]);
                }
                else
                {
                    CheckSystemError();
                }

                DestroySession(sessionHandle);
                return false;
            }

            return SRDSettings.Load();
        }

        internal bool Start()
        {
            foreach(var subsystem in _srdSubsystems)
            {
                subsystem.Start();
            }

            if(Instance._isSessionRunning)
            {
                return true;
            }
            var result = BeginSession(_sessionHandle);
            if(result)
            {
                Instance._isSessionRunning = true;
            }
            return result;
        }

        static public bool BeginSession(IntPtr sessionHandle)
        {
            SRDCorePlugin.SetColorSpaceSettings(QualitySettings.activeColorSpace, SystemInfo.graphicsDeviceType, SRDHelper.renderPipelineType);
            return true;
        }

        static public void CheckSystemError()
        {
            if(SRDProjectSettings.IsRunWithoutSRDisplayMode())
            {
                return;
            }

            var result = SRDCorePlugin.GetXrSystemError(out var systemError);

            if(result != SrdXrResult.SUCCESS)
            {
                SRDHelper.PopupMessageAndForceToTerminate(SRDHelper.SRDMessages.UnknownError);
            }

            if (systemError.code == SrdXrSystemErrorCode.SYSTEM_ERROR_SUCCESS)
            {
                return;
            }

            var errorToMessage = new Dictionary<SrdXrSystemErrorCode, string>()
            {
                { SrdXrSystemErrorCode.SYSTEM_ERROR_NO_AVAILABLE_DEVICE, SRDHelper.SRDMessages.DeviceNotFoundError},
                { SrdXrSystemErrorCode.SYSTEM_ERROR_DEVICE_LOST, SRDHelper.SRDMessages.DeviceInterruptionError},
                { SrdXrSystemErrorCode.SYSTEM_ERROR_DEVICE_BUSY, SRDHelper.SRDMessages.AppConflictionError},
                { SrdXrSystemErrorCode.SYSTEM_ERROR_OPERATION_FAILED, SRDHelper.SRDMessages.DeviceConnectionError},
                { SrdXrSystemErrorCode.SYSTEM_ERROR_USB_NOT_CONNECTED, SRDHelper.SRDMessages.DeviceConnectionError},
                { SrdXrSystemErrorCode.SYSTEM_ERROR_CAMERA_WITH_USB20, SRDHelper.SRDMessages.USB3ConnectionError},
                { SrdXrSystemErrorCode.SYSTEM_ERROR_NO_USB_OR_NO_POWER, SRDHelper.SRDMessages.DeviceConnectionError},
                { SrdXrSystemErrorCode.SYSTEM_ERROR_ANOTHER_APPLICATION_RUNNING, SRDHelper.SRDMessages.AppConflictionError },
            };
            var msg = errorToMessage.ContainsKey(systemError.code) ? errorToMessage[systemError.code] : SRDHelper.SRDMessages.UnknownError;
            SRDHelper.PopupMessageAndForceToTerminate(msg);
        }

        internal bool Stop()
        {
            foreach(var subsystem in _srdSubsystems)
            {
                subsystem.Stop();
            }
            _srdSubsystems.Clear();

            return true;
        }

        static public bool EndSession(IntPtr sessionHandle)
        {
            return true;
        }

        internal bool DestroySession()
        {
            if(_isSessionRunning)
            {
                var result = EndSession(SRDSessionHandler.SessionHandle);
                if(result)
                {
                    _isSessionRunning = false;
                }
            }
            if(DestroySession(_sessionHandle))
            {
                OnSessionStateChangedEvent = null;
                _sessionHandle = IntPtr.Zero;
                return true;
            }
            return false;
        }

        static public bool DestroySession(IntPtr sessionHandle)
        {
            SRDCorePlugin.DestroySession();
            return true;
        }

        public void RegisterSubsystem(ISRDSubsystem subSystem)
        {
            _srdSubsystems.Add(subSystem);
        }

        public void RemoveSubsystem(ISRDSubsystem subSystem)
        {
            _srdSubsystems.Remove(subSystem);
        }
    }
}

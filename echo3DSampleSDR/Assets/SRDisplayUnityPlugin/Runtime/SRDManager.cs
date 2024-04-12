/*
 * Copyright 2019,2020,2021,2023 Sony Corporation
 */


using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

using SRD.Utils;


namespace SRD.Core
{
    /// <summary>
    /// A core component for Spatial Reality Display that manages the session with SRD Runtime.
    /// </summary>
    //[ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class SRDManager : MonoBehaviour
    {
        static private bool initializedPlugin = false;
        static private bool isScheduledToTerminateTheApplication = false;
        /// <summary>
        /// A flag for SR Rendering
        /// </summary>
        /// <remarks>
        /// If this is disable, SR Rendering is turned off.
        /// </remarks>
        [Tooltip("If this is disable, SR Rendering is turned off.")]
        public bool IsSRRenderingActive = true;

        private bool prevIsSRRenderingActive = false;

        /// <summary>
        /// A flag for Spatial Clipping
        /// </summary>
        /// <remarks>
        /// If this is disable, the spatial clipping is turned off.
        /// </remarks>
        [Tooltip("If this is disable, the spatial clipping is turned off.")]
        public bool IsSpatialClippingActive = true;

        /// <summary>
        /// A flag for Crosstalk Correction
        /// </summary>
        /// <remarks>
        /// If this is disable, the crosstalk correction is turned off.
        /// </remarks>
        [Tooltip("ELF-SR1 exclusive function. If this is disable, the crosstalk correction is turned off.")]
        public bool IsCrosstalkCorrectionActive = true;

        /// <summary>
        /// Crosstalk Correction Type
        /// </summary>
        /// <remarks>
        /// This is valid only if the crosstalk correction is active.
        /// </remarks>
        [Tooltip("ELF-SR1 exclusive function. This is valid only if the crosstalk correction is active.")]
        public SrdXrCrosstalkCorrectionType CrosstalkCorrectionType;

        private SRDCrosstalkCorrection _srdCrosstalkCorrection;

        private SRDSystemDescription _description;


        public enum ScalingMode
        {
            [InspectorName("Scaled size")]
            ScaledSize,
            [InspectorName("Original size")]
            OriginalSize
        };
        public ScalingMode _scalingMode = ScalingMode.ScaledSize;

        [AttributeUsage(AttributeTargets.Field)]
        private sealed class GizmoSizeSelectionParameterAttribute : PropertyAttribute { }

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(GizmoSizeSelectionParameterAttribute))]
        private sealed class GizmoSizeSelectionParameterAttributeDrawer : PropertyDrawer
        {
            private readonly int[] _enumValues;
            private readonly GUIContent[] _enumAppearances;

            public GizmoSizeSelectionParameterAttributeDrawer()
            {
                Int32 size;
                if (!SRDCorePlugin.GetCountOfSupportedDevices(out size))
                {
                    return;
                }

                var panel_specs = new supported_panel_spec[size];
                if(!SRDCorePlugin.GetPanelSpecOfSupportedDevices(panel_specs))
                {
                    return;
                }

                _enumValues = new int[size];
                _enumAppearances = new GUIContent[size];
                for(int i = 0; i < size; ++i)
                {
                    _enumValues[i] = i;
                    _enumAppearances[i] = new GUIContent(panel_specs[i].device_name);
                }
            }

            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                var isActiveProperty = property.serializedObject.FindProperty("_scalingMode");
                var isActive = (isActiveProperty != null) && (ScalingMode.OriginalSize == (ScalingMode)isActiveProperty.enumValueIndex);
                EditorGUI.BeginDisabledGroup(!isActive);
                EditorGUI.indentLevel++;
                using(new EditorGUI.PropertyScope(position, label, property))
                {
                    property.intValue = EditorGUI.IntPopup(position, label, property.intValue, _enumAppearances, _enumValues);
                }
                EditorGUI.indentLevel--;
                EditorGUI.EndDisabledGroup();

            }
        }
#endif

        [GizmoSizeSelectionParameter, Tooltip("This is valid only if the Scaling Mode is Original size.")]
        [SerializeField] private int _GIZMOSize = 0;

        private const float _minScaleInInspector = 0.1f;
        private const float _maxScaleInInspector = 1000.0f; // float.MaxValue;
        [SerializeField]
        [Range(_minScaleInInspector, _maxScaleInInspector), Tooltip("The scale of SRDisplay View Space")]
        private float _SRDViewSpaceScale = 1.0f;

        /// <summary>
        /// The scale of SRDisplay View Space
        /// </summary>
        public float SRDViewSpaceScale
        {
            get
            {
                if(_SRDViewSpaceScale <= 0)
                {
                    Debug.LogWarning(String.Format("Wrong SRDViewSpaceScale: {0} \n SRDViewSpaceScale must be 0+. Now SRDViewSpaceScale is forced to 1.0.", _SRDViewSpaceScale));
                    _SRDViewSpaceScale = 1.0f;
                }
                return _SRDViewSpaceScale;
            }
            set
            {
                _SRDViewSpaceScale = value;
            }
        }

        #region Events
        /// <summary>
        /// A UnityEvent callback containing SRDisplayViewSpaceScale.
        /// </summary>
        [System.Serializable]
        public class SRDViewSpaceScaleChangedEvent : UnityEvent<float> { };
        /// <summary>
        /// An API of <see cref="SRDManager.SRDViewSpaceScaleChangedEvent"/>. Callbacks that are registered to this are called when SRDViewSpaceScale is changed.
        /// </summary>
        public SRDViewSpaceScaleChangedEvent OnSRDViewSpaceScaleChangedEvent;

        /// <summary>
        /// A UnityEvent callback containing a flag that describe FaceTrack is success or not in this frame.
        /// </summary>
        [System.Serializable]
        public class SRDFaceTrackStateEvent : UnityEvent<bool> { };
        /// <summary>
        /// An API of <see cref="SRDManager.SRDFaceTrackStateEvent"/>. Callbacks that are registered to this are called in every frame.
        /// </summary>
        public SRDFaceTrackStateEvent OnFaceTrackStateEvent;
        #endregion

        private Transform _presence;
        /// <summary>
        /// Transform of presence in real world. </param>
        /// </summary>
        public Transform Presence {  get { return _presence; } }

        private Utils.DisplayEdges _displayEdges;
        /// <summary>
        /// Contains the positions of Spatial Reality Display edges and center.
        /// </summary>
        public Utils.DisplayEdges DisplayEdges {  get { return _displayEdges; } }

        private Coroutine _srRenderingCoroutine = null;

        private SRDCoreRenderer _srdCoreRenderer;
        internal SRDCoreRenderer SRDCoreRenderer { get { return _srdCoreRenderer; } }

        private bool _isPerformancePriorityEnabled = false;
        internal bool IsPerformanceProirityEnabled { get { return _isPerformancePriorityEnabled; } }
        private bool _isLensShiftEnabled = false;
        internal bool IsLensShiftEnabled { get { return _isLensShiftEnabled; } }

        #region APIs
        /// <summary>
        /// An api to show/remove the CameraWindow
        /// </summary>
        /// <param name="isOn">The flag to show the CameraWindow. If this is true, the CameraWindow will open. If this is false, the CameraWindow will close.</param>
        /// <returns>Success or not.</returns>
        public bool ShowCameraWindow(bool isOn)
        {
            var res = SRDCorePlugin.ShowCameraWindow(isOn);
            return (SrdXrResult.SUCCESS == res);
        }

        #endregion

        #region MainFlow

        void Awake()
        {
            if (!initializedPlugin)
            {
                initializedPlugin = SRDSessionHandler.Instance.CreateSession();
                SRDSettings.LoadBodyBounds();
            }

            {
                var presenceName = SRDHelper.SRDConstants.PresenceGameObjDefaultName;
                var presenceObj = SRDSceneEnvironment.GetOrCreateChild(this.transform, presenceName);
                _presence = presenceObj.transform;
                _presence.localPosition = Vector3.zero;
                _presence.localRotation = Quaternion.identity;
                _presence.localScale = Vector3.one;
                if (_scalingMode == ScalingMode.ScaledSize)
                {
                    _presence.localScale /= Utils.SRDSettings.DeviceInfo.BodyBounds.ScaleFactor;
                }
            }

            UpdateSettings();
            foreach (var cond in GetForceQuitConditions())
            {
                if (cond.IsForceQuit)
                {
                    ForceQuitWithAssertion(cond.ForceQuitMessage);
                }
            }

            if (SRDProjectSettings.IsRunWithoutSRDisplayMode())
            {
                _description = new SRDSystemDescription(FaceTrackerSystem.Mouse,
                                                        EyeViewRendererSystem.UnityRenderCam,
                                                        StereoCompositerSystem.PassThrough);
            }
            else
            {
                _description = new SRDSystemDescription(FaceTrackerSystem.SRD,
                                                        EyeViewRendererSystem.UnityRenderCam,
                                                        StereoCompositerSystem.SRD);
            }

            SRDCorePlugin.GetPerformancePriorityEnabled(out _isPerformancePriorityEnabled);
            SRDCorePlugin.SetLensShiftEnabled(true);
            SRDCorePlugin.GetLensShiftEnabled(out _isLensShiftEnabled);

            _srdCoreRenderer = new SRDCoreRenderer(_description);
            _srdCoreRenderer.OnSRDFaceTrackStateEvent += (bool result) =>
            {
#if DEVELOPMENT_BUILD
                //Debug.LogWarning("No data from FaceRecognition: See the DebugWindow with F10");
#endif
                if(OnFaceTrackStateEvent != null)
                {
                    OnFaceTrackStateEvent.Invoke(result);
                }
            };

            _srdCrosstalkCorrection = new SRDCrosstalkCorrection();

            SRDSessionHandler.Instance.RegisterSubsystem(_srdCoreRenderer);
            SRDSessionHandler.Instance.Start();
        }

        void OnEnable()
        {
            SRDCorePlugin.EnableStereo(IsSRRenderingActive);
            CreateDisplayEdges();
            _srdCrosstalkCorrection.Init(ref IsCrosstalkCorrectionActive, ref CrosstalkCorrectionType);
            StartSRRenderingCoroutine();
        }

        void OnDisable()
        {
            SRDCorePlugin.EnableStereo(false);
            SRDSessionHandler.Instance.Stop();
            if (isScheduledToTerminateTheApplication)
            {
                SRDSessionHandler.Instance.DestroySession();
            }
            StopSRRenderingCoroutine();
        }

        void Update()
        {
            if (initializedPlugin)
            {
                SRDSessionHandler.CheckSystemError();

                if (prevIsSRRenderingActive != IsSRRenderingActive)
                {
                    SRDCorePlugin.EnableStereo(IsSRRenderingActive);
                    prevIsSRRenderingActive = IsSRRenderingActive;
                }
            }
            UpdateScaleIfNeeded();
            _srdCrosstalkCorrection.HookUnityInspector(ref IsCrosstalkCorrectionActive, ref CrosstalkCorrectionType);
        }

        void OnValidate()
        {
            UpdateScaleIfNeeded();
        }

        void LateUpdate()
        {
            _srdCoreRenderer.Update(_presence.transform, IsSpatialClippingActive);
        }

        void OnDestroy()
        {
            _srdCoreRenderer.Dispose();
        }

        #endregion

        #region RenderingCoroutine

        private void StartSRRenderingCoroutine()
        {
            if(_srRenderingCoroutine == null)
            {
                _srRenderingCoroutine = StartCoroutine(SRRenderingCoroutine());
            }
        }

        private void StopSRRenderingCoroutine()
        {
            if(_srRenderingCoroutine != null)
            {
                StopCoroutine(_srRenderingCoroutine);
                _srRenderingCoroutine = null;
            }
        }

        private IEnumerator SRRenderingCoroutine()
        {
            var yieldEndOfFrame = new WaitForEndOfFrame();
            while(true)
            {
                yield return yieldEndOfFrame;
                //_srdCoreRenderer.Composite(IsSRRenderingActive);
                _srdCoreRenderer.Composite(true);
            }
        }
        #endregion


        #region Utils

        private void UpdateSettings()
        {
            QualitySettings.maxQueuedFrames = 0;
        }

        private void CreateDisplayEdges()
        {
            if(_displayEdges != null)
            {
                return;
            }
            SRDSettings.LoadBodyBounds();
            _displayEdges = new Utils.DisplayEdges(_presence, 
                                                   Utils.SRDSettings.DeviceInfo.BodyBounds);
        }

        private void UpdateScaleIfNeeded()
        {
            var viewSpaceScale = Vector3.one * SRDViewSpaceScale;
            if (this.transform.localScale != viewSpaceScale)
            {
                this.transform.localScale = viewSpaceScale;

                if(OnSRDViewSpaceScaleChangedEvent != null)
                {
                    OnSRDViewSpaceScaleChangedEvent.Invoke(SRDViewSpaceScale);
                }
            }
        }

        struct ForceQuitCondition
        {
            public bool IsForceQuit;
            public string ForceQuitMessage;
            public ForceQuitCondition(bool isForceQuit, string forceQuitMessage)
            {
                IsForceQuit = isForceQuit;
                ForceQuitMessage = forceQuitMessage;
            }
        }

        private List<ForceQuitCondition> GetForceQuitConditions()
        {
            var ret = new List<ForceQuitCondition>();

            var isGraphicsAPINotSupported = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore &&
                                            SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Direct3D11;
            ret.Add(new ForceQuitCondition(isGraphicsAPINotSupported,
                                           "Select unsupported GraphicsAPI: GraphicsAPI must be DirectX11 or OpenGLCore."));

            var isSRPNotSupportedVersion = false;
#if !UNITY_2019_1_OR_NEWER
            isSRPNotSupportedVersion = (UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset != null);
#endif
            ret.Add(new ForceQuitCondition(isSRPNotSupportedVersion,
                                           "SRP in Spatial Reality Display is supported in over 2019.1 only"));
            return ret;
        }

        private void ForceQuitWithAssertion(string assertionMessage)
        {
            Debug.LogAssertion(assertionMessage);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #endregion

        #region GIZMO

        private void OnDrawGizmos()
        {
            if(!this.enabled)
            {
                return;
            }

            // Draw SRDisplay View Space
            Utils.SRDSettings.BodyBounds bodyBounds;
            if (_scalingMode == ScalingMode.ScaledSize)
            {
                bodyBounds = Utils.SRDSettings.SRDDeviceInfo.DefaultBodyBounds;

            }
            else if (_scalingMode == ScalingMode.OriginalSize)
            {
                Int32 size;
                if (!SRDCorePlugin.GetCountOfSupportedDevices(out size))
                {
                    return;
                }

                var panel_specs = new supported_panel_spec[size];
                if (!SRDCorePlugin.GetPanelSpecOfSupportedDevices(panel_specs))
                {
                    return;
                }

                if ((_GIZMOSize < 0) || (size <= _GIZMOSize))
                {
                    return;
                }

                var width = panel_specs[_GIZMOSize].width;
                var height = panel_specs[_GIZMOSize].height;

                var panelRect = new Vector2(width, height);
                bodyBounds = new Utils.SRDSettings.BodyBounds(panelRect, panel_specs[_GIZMOSize].angle);
            }
            else
            {
                return;
            }

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(bodyBounds.Center, bodyBounds.BoxSize);
            Gizmos.color = Color.cyan;
            for(var i = 0; i < 4; i++)
            {
                var from = i % 4;
                var to = (i + 1) % 4;
                Gizmos.DrawLine(bodyBounds.EdgePositions[from],
                                bodyBounds.EdgePositions[to]);
            }
        }
#endregion
    }

    public enum SrdXrCrosstalkCorrectionType
    {
        GRADATION_CORRECTION_MEDIUM = 0,
        GRADATION_CORRECTION_ALL = 1,
        GRADATION_CORRECTION_HIGH_PRECISE = 2,
    }
}











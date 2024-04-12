/*
 * Copyright 2019,2020,2023 Sony Corporation
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using SRD.Utils;

namespace SRD.Core
{
    internal interface ISRDStereoCompositer : ISRDSubsystem
    {
        bool RegisterSourceStereoTextures(Texture renderTextureL, Texture renderTextureR);
        void RenderStereoComposition(bool IsSRRenderingActive);
    }

    internal class SRDStereoCompositer: ISRDStereoCompositer
    {
        private SrdXrTexture _srdSideBySide;
        private SrdXrTexture _srdOut;
        private RenderTexture _outTexture;

        private Texture _sceneLeft;
        private RenderTexture _sideBySide;
        private Material _leftAndRightToSideBySide;

        public SRDStereoCompositer()
        {
            _leftAndRightToSideBySide = new Material(Shader.Find("Custom/LeftAndRightToSideBySide"));
            _leftAndRightToSideBySide.hideFlags = HideFlags.HideAndDontSave;
        }

        public bool RegisterSourceStereoTextures(Texture renderTextureL, Texture renderTextureR)
        {
            if ((renderTextureL == null) || (renderTextureR == null))
            {
                Debug.LogError("RenderTextures are not set. Set renderTextures with RenderStereoComposition function");
                return false;
            }

            var width = SRDSettings.DeviceInfo.ScreenRect.Width;
            var height = SRDSettings.DeviceInfo.ScreenRect.Height;

            var div = 1;
            if (SRDSceneEnvironment.GetSRDManager().IsPerformanceProirityEnabled)
            {
                div = 2;
            }
            var bufferFormat = SRDCorePlugin.IsARGBHalfSupported() ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
            if (_sideBySide == null)
            {
                var width2 = SRDSettings.DeviceInfo.ScreenRect.Width * 2;

                var RenderTextureDepth = 24;
                var readWrite = (QualitySettings.desiredColorSpace == ColorSpace.Linear) ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.Default;
                _sideBySide = new RenderTexture(width2 / div, height / div, RenderTextureDepth, bufferFormat,
                                              readWrite);
                _sideBySide.Create();
                _srdSideBySide.texture = _sideBySide.GetNativeTexturePtr();
            }

            if (_outTexture == null)
            {
                _outTexture = new RenderTexture(width, height, depth: 24, bufferFormat);
                _outTexture.filterMode = FilterMode.Point;
                _outTexture.Create();
                _srdOut.texture = _outTexture.GetNativeTexturePtr();
            }

            _srdSideBySide.width = _srdOut.width = (uint)width;
            _srdSideBySide.height = _srdOut.height = (uint)height;

            SRDCorePlugin.GenerateTextureAndShaders(SRDSessionHandler.SessionHandle, ref _srdSideBySide, ref _srdSideBySide, ref _srdOut);

            _leftAndRightToSideBySide.mainTexture = renderTextureL;
            _leftAndRightToSideBySide.SetTexture("_RightTex", renderTextureR);

            _sceneLeft = renderTextureL;
            return true;
        }

        public void RenderStereoComposition(bool IsSRRenderingActive)
        {
            RenderTexture backBuffer = null;

            Graphics.Blit(_sceneLeft, _sideBySide, _leftAndRightToSideBySide);
            SRDCorePlugin.EndFrame(SRDSessionHandler.SessionHandle, false, true);
            Graphics.Blit(_outTexture, backBuffer);
        }

        public void Start()
        {
            // do nothing
        }

        public void Stop()
        {
            if(_sideBySide != null)
            {
                _sideBySide.Release();
                MonoBehaviour.Destroy(_sideBySide);
            }
            if(_outTexture != null)
            {
                _outTexture.Release();
                MonoBehaviour.Destroy(_outTexture);
            }
        }

        public void Dispose()
        {
            // do nothing
        }

    }

    internal class SRDPassThroughStereoCompositer : ISRDStereoCompositer
    {
        private Texture _leftTexture;
        private Texture _rightTexture;

        public SRDPassThroughStereoCompositer()
        {
        }
        public bool RegisterSourceStereoTextures(Texture renderTextureL, Texture renderTextureR)
        {
            _leftTexture = renderTextureL;
            _rightTexture = renderTextureR;
            return true;
        }

        public void RenderStereoComposition(bool IsSRRenderingActive)
        {
            Graphics.Blit(_leftTexture, (RenderTexture)null);
        }

        public void Start()
        {
            // do nothing
        }

        public void Stop()
        {
            // do nothing
        }

        public void Dispose()
        {
            // do nothing
        }

    }

}

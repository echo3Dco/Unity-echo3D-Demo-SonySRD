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

    internal interface ISRDFaceTracker : ISRDSubsystem
    {
        void UpdateState(Transform srdWorldOrigin);
        // return a current user face pose in the Unity World Coordinate
        SrdXrResult GetCurrentFacePose(out FacePose facePose);
        SrdXrResult GetCurrentProjMatrix(float nearClip, float farClip, out FaceProjectionMatrix projMat);
    }

    internal class SRDFaceTracker : ISRDFaceTracker
    {
        private FacePose _prevFacePose;
        private FaceProjectionMatrix _prevProjMat;
        private Transform _currentOrigin;

        public SRDFaceTracker()
        {
            _prevFacePose = CreateDefaultFacePose();
            _prevProjMat = CreateDefaultProjMatrix();
        }

        public void UpdateState(Transform srdWorldOrigin)
        {
            _currentOrigin = srdWorldOrigin;
            SRDCorePlugin.Update();
            /*
            SRDCorePlugin.BeginFrame(SRDSessionHandler.SessionHandle,
                                     callInMainThread: true, callinRenderThread: true);
             */
        }

        public SrdXrResult GetCurrentFacePose(out FacePose facePose)
        {
            var xrResult = SRDCorePlugin.GetFacePose(SRDSessionHandler.SessionHandle,
                                                     out var headPose, out var eyePoseL, out var eyePoseR);
            if ((xrResult == SrdXrResult.SUCCESS)
            ||  (xrResult == SrdXrResult.ERROR_POSE_INVALID))
            {
                facePose = new FacePose(headPose, eyePoseL, eyePoseR);
                _prevFacePose = facePose;
            }
            else
            {
                facePose = _prevFacePose;
            }
            facePose = facePose.GetTransformedBy(_currentOrigin);
            return xrResult;
        }

        public SrdXrResult GetCurrentProjMatrix(float nearClip, float farClip, out FaceProjectionMatrix projMat)
        {
            var xrResult = SRDCorePlugin.GetProjectionMatrix(SRDSessionHandler.SessionHandle, nearClip, farClip,
                                                             out var headMat, out var leftMat, out var rightMat);
            if ((xrResult == SrdXrResult.SUCCESS)
            ||  (xrResult == SrdXrResult.ERROR_POSE_INVALID))
            {
                projMat = new FaceProjectionMatrix(headMat, leftMat, rightMat);
                _prevProjMat = projMat;
            }
            else
            {
                projMat = _prevProjMat;
            }
            return xrResult;
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

        public static FacePose CreateDefaultFacePose()
        {
            var facePose = new FacePose();
            facePose.HeadPose.position = new Vector3(0f, 0.2f, -0.3f);

            var dispCenter = Utils.SRDSettings.DeviceInfo.BodyBounds.Center;
            var forward = dispCenter - facePose.HeadPose.position;
            var up = Vector3.Cross(forward, Vector3.right); // left hand rule
            facePose.HeadPose.rotation = Quaternion.LookRotation(forward, up);

            facePose.UpdateWithNewHeadPose(facePose.HeadPose, dispCenter);
            return facePose;
        }

        public static FaceProjectionMatrix CreateDefaultProjMatrix()
        {
            var aspect = (float)SRD.Utils.SRDSettings.DeviceInfo.ScreenRect.Width / (float)SRD.Utils.SRDSettings.DeviceInfo.ScreenRect.Height;
            var projMat = Matrix4x4.Perspective(40f, aspect, 0.3f, 100f);
            return new FaceProjectionMatrix(projMat, projMat, projMat);
        }
    }


    internal class MouseBasedFaceTracker : ISRDFaceTracker
    {
        private FacePose _facePose;
        private Transform _currentOrigin;

        private Vector3 _focus;
        private Vector3 _prevMousePos;

        private Matrix4x4 _posTrackCoordTdispCenerCoord;
        private Matrix4x4 _dispCenerCoordTposTrackCoord;

        private readonly float MinFocusToPosition = 0.35f;
        private readonly float MaxFocusToPosition = 1.2f;
        private readonly float MovableConeHalfAngleDeg = 35.0f;

        enum MouseButtonDown
        {
            MBD_LEFT = 0, MBD_RIGHT, MBD_MIDDLE,
        };

        public MouseBasedFaceTracker()
        {
            _facePose = SRDFaceTracker.CreateDefaultFacePose();
            _focus = Utils.SRDSettings.DeviceInfo.BodyBounds.Center;

            var dispCenterPose = new Pose(Utils.SRDSettings.DeviceInfo.BodyBounds.Center, Quaternion.Euler(-45f, 180f, 0f));
            _posTrackCoordTdispCenerCoord = SRDHelper.PoseToMatrix(dispCenterPose);
            _dispCenerCoordTposTrackCoord = SRDHelper.PoseToMatrix(SRDHelper.InvPose(dispCenterPose));
        }

        public void UpdateState(Transform srdWorldOrigin)
        {
            _currentOrigin = srdWorldOrigin;
            var deltaWheelScroll = Input.GetAxis("Mouse ScrollWheel");
            var focusToPosition = _facePose.HeadPose.position - _focus;
            var updatedFocusToPosition = focusToPosition * (1.0f - deltaWheelScroll);
            if(updatedFocusToPosition.magnitude > MinFocusToPosition && updatedFocusToPosition.magnitude < MaxFocusToPosition)
            {
                _facePose.HeadPose.position = _focus + updatedFocusToPosition;
            }

            if(Input.GetMouseButtonDown((int)MouseButtonDown.MBD_RIGHT))
            {
                _prevMousePos = Input.mousePosition;
            }
            if(Input.GetMouseButton((int)MouseButtonDown.MBD_RIGHT))
            {
                var currMousePos = Input.mousePosition;
                var diff = currMousePos - _prevMousePos;
                diff.z = 0f;
                if(diff.magnitude > Vector3.kEpsilon)
                {
                    diff /= 1000f;
                    var posInDispCoord = _dispCenerCoordTposTrackCoord.MultiplyPoint3x4(_facePose.HeadPose.position);

                    var coneAngleFromNewPos = Vector3.Angle(Vector3.forward, posInDispCoord + diff);
                    if(Mathf.Abs(coneAngleFromNewPos) > MovableConeHalfAngleDeg)
                    {
                        var tangentLineInMovableCone = (new Vector2(-posInDispCoord.y, posInDispCoord.x)).normalized;
                        var diffXY = new Vector2(diff.x, diff.y);
                        if(Vector2.Angle(diffXY, tangentLineInMovableCone) > 90f)
                        {
                            tangentLineInMovableCone = -tangentLineInMovableCone;
                        }
                        diff = Vector2.Dot(tangentLineInMovableCone, diffXY) * tangentLineInMovableCone;
                    }
                    posInDispCoord += diff;
                    var coneRadianInCurrentZ = posInDispCoord.z * Mathf.Tan(Mathf.Deg2Rad * MovableConeHalfAngleDeg);
                    var radian = (new Vector2(posInDispCoord.x, posInDispCoord.y)).magnitude;
                    if(radian > coneRadianInCurrentZ)
                    {
                        posInDispCoord.x *= (coneRadianInCurrentZ / radian);
                        posInDispCoord.y *= (coneRadianInCurrentZ / radian);
                    }
                    posInDispCoord.z = Mathf.Sqrt(Mathf.Pow(updatedFocusToPosition.magnitude, 2f) - Mathf.Pow(((Vector2)posInDispCoord).magnitude, 2f));
                    _facePose.HeadPose.position = _posTrackCoordTdispCenerCoord.MultiplyPoint3x4(posInDispCoord);
                }

                _prevMousePos = currMousePos;
            }

            _facePose.HeadPose.rotation = Quaternion.LookRotation(_focus - _facePose.HeadPose.position, Vector3.up);
            _facePose.UpdateWithNewHeadPose(_facePose.HeadPose, _focus);
        }

        public SrdXrResult GetCurrentFacePose(out FacePose facePose)
        {
            facePose = _facePose.GetTransformedBy(_currentOrigin);
            return SrdXrResult.SUCCESS;
        }

        public SrdXrResult GetCurrentProjMatrix(float nearClip, float farClip, out FaceProjectionMatrix projMatrix)
        {
            var posInDispCoord = _dispCenerCoordTposTrackCoord.MultiplyPoint3x4(_facePose.HeadPose.position);
            var maxAng = 0f;
            foreach(var edge in Utils.SRDSettings.DeviceInfo.BodyBounds.EdgePositions)
            {
                var toEdge = edge - posInDispCoord;
                var toCenter = Utils.SRDSettings.DeviceInfo.BodyBounds.Center - posInDispCoord;
                var ang = Vector3.Angle(toCenter, toEdge);
                if(ang > maxAng)
                {
                    maxAng = ang;
                }
            }
            var projMat = Matrix4x4.Perspective(maxAng * 2.0f, 1f, nearClip, farClip);
            projMatrix = new FaceProjectionMatrix(projMat, projMat, projMat);
            return SrdXrResult.SUCCESS;
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


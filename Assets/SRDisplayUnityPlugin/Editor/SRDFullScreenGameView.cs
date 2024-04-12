﻿/*
 * Copyright 2019,2020,2021,2023 Sony Corporation
 */

#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;

using System.Runtime.InteropServices;

using UnityEngine;
using UnityEditor;

using SRD.Core;
using SRD.Utils;
using SRD.Editor.AsssemblyWrapper;

namespace SRD.Editor
{
    internal class SRDFullScreenGameView
    {
        const string FullScreenMenuPath = "SpatialRealityDisplay/SRDisplay GameView (Full Screen)";
        const string SRDGameViewName = "SRD Game View";
        const string TemporalyGameViewName = "Temporary Game View";
        const string ForceCloseGameViewMessage = "Multiple GameViews cannot be open at the same time in Spatial Reality Display. Force closes the GameView tabs.";

        private static SrdXrDeviceInfo targetDevice;
        private static EditorApplication.CallbackFunction OnPostClosingTempGameView;

        private static IEnumerable<EditorWindow> EnumGameViews()
        {
            return GameView.GetGameViews().AsEnumerable();
        }

        private static IEnumerable<EditorWindow> EnumSRDGameViews()
        {
            return EnumGameViews().Where(w => w.name == SRDGameViewName);
        }

        private static IEnumerable<EditorWindow> EnumUnityGameViews()
        {
            return EnumGameViews().Where(w => w.name != SRDGameViewName);
        }

        private static void TakeOneUnityGameView()
        {
            var unityGameViews = EnumUnityGameViews()
                                 .Reverse()
                                 .Skip(1);

            foreach (var view in unityGameViews)
            {
                Debug.Log(ForceCloseGameViewMessage);
                view.Close();
            }
        }

        private static void CloseAllUnityGameView()
        {
            var unityGameViews = EnumUnityGameViews();

            foreach (var view in unityGameViews)
            {
                Debug.Log(ForceCloseGameViewMessage);
                view.Close();
            }
        }

        private static void CloseAllSRDGameView()
        {
            var srdGameViews = EnumSRDGameViews();

            foreach (var view in srdGameViews)
            {
                view.Close();
            }
        }

        private static void HideToolbarOfAllSRDGameView()
        {
            var srdGameViews = EnumSRDGameViews();

            foreach (var view in srdGameViews)
            {
                var gameView = new GameView(view);
                gameView.showToolbar = false;
            }
        }

        private static bool SRDGameViewExists()
        {
            return EnumSRDGameViews().Count() != 0;
        }

#if UNITY_EDITOR_WIN
        [MenuItem(FullScreenMenuPath + " _F11", true)]
#endif
        private static bool ValidateMenuItem_ExecuteFullScreen()
        {
            Menu.SetChecked(FullScreenMenuPath, SRDGameViewExists());

            // SRD GameView cannot be changed in Play Mode
            return !EditorApplication.isPlaying;
        }

#if UNITY_EDITOR_WIN
        [MenuItem(FullScreenMenuPath + " _F11", false, 2001)]
#endif
        private static void ExecuteFullScreen()
        {
            if(EditorApplication.isPlaying)
            {
                Debug.Log("SRDisplay GameView cannot be changed in Play Mode");
                return;
            }

            if(SRDGameViewExists())
            {
                CloseAllSRDGameView();
            }
            else
            {
                SrdXrResult result = SRDCorePlugin.SelectDevice(out var device);
                if(result == SrdXrResult.ERROR_USER_CANCEL)
                {
                    return;
                }
                else if(result != SrdXrResult.SUCCESS)
                {
                    var errorToMessage = new Dictionary<SrdXrResult, string>()
                    {
                        { SrdXrResult.ERROR_RUNTIME_NOT_FOUND, SRDHelper.SRDMessages.DLLNotFoundError},
                        { SrdXrResult.ERROR_DEVICE_NOT_FOUND, SRDHelper.SRDMessages.FullscreenGameViewError},
                        { SrdXrResult.ERROR_RUNTIME_UNSUPPORTED, SRDHelper.SRDMessages.OldRuntimeUnsupportedError},
                    };
                    var msg = errorToMessage.ContainsKey(result) ? errorToMessage[result] : SRDHelper.SRDMessages.UnknownError;
                    SRDCorePlugin.ShowMessageBox("Error", msg, Debug.LogError);
                    return;
                }

                targetDevice = device;

                if(Prepare())
                {
                    CloseAllUnityGameView();
                    SetupGameView();
                }
                else
                {
                    OnPostClosingTempGameView += SetupGameViewAfterCloseTempGameView;
                }
            }
        }

        [InitializeOnLoadMethod]
        static void InitializeOnLoad()
        {
            EditorApplication.update += CloseUnityGameView;

            // GameView's showToolbar flag is restored to true when the script is recompiled, so set false again. 
            EditorApplication.delayCall += () =>
            {
                HideToolbarOfAllSRDGameView();
            };
        }

        private static Vector2Int TargetScreenSize
        {
            get 
            {
                var targetScreen = targetDevice.target_monitor_rectangle;
                return new Vector2Int(targetScreen.right - targetScreen.left,
                                      targetScreen.bottom - targetScreen.top);
            }
        }

        private static Rect TargetScreenRect
        {
            get
            {
                var targetScreen = targetDevice.target_monitor_rectangle;
                return new Rect(targetScreen.left, 
                                targetScreen.top,
                                targetScreen.right - targetScreen.left, 
                                targetScreen.bottom - targetScreen.top);
            }
        }

        private static bool Prepare()
        {
            var screenSize = TargetScreenSize;

            // Set the SRD screen size to project settings
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = screenSize.x;
            PlayerSettings.defaultScreenHeight = screenSize.y;

            bool ready = GameViewSizeList.IsReadyDestinationSize(screenSize);
            if (!ready)
            {
                EditorApplication.update += CreateTemporaryGameView;
            }
            return ready;
        }

        private static void CloseUnityGameView()
        {
            // normal gameviews only
            if(!SRDGameViewExists())
            {
                if(EditorApplication.isPlaying)
                {
                    TakeOneUnityGameView();
                }
            }
            // SRD GameView opened
            else
            {
                CloseAllUnityGameView();
            }
        }

        private static void CreateTemporaryGameView()
        {
            // SRD Screen Size is added to GameViewSizes List by creating temporary GameView with Toolbar. 
            var gameView = new GameView(TemporalyGameViewName);
            gameView.showToolbar = true;
            gameView.position = new Rect(TargetScreenRect.position, Vector2.zero);
            gameView.ShowWithPopupMenu();

            EditorApplication.update -= CreateTemporaryGameView;
            EditorApplication.update += CloseTemporaryGameView;
        }

        private static void CloseTemporaryGameView()
        {
            // Close Temporary GameView that finished the task of updating GameViewSizes.
            var tmpGameViews = EnumGameViews().Where(w => w.name == TemporalyGameViewName);
            foreach (var view in tmpGameViews)
            {
                view.Close();
            }

            if(GameViewSizeList.IsReadyDestinationSize(TargetScreenSize))
            {
            }
            else
            {
                Debug.LogWarning("Fail to create destination size GameView. If you have a wrong size of SRDisplayGameView, please re-open SRDisplayGameView.");
            }

            EditorApplication.update -= CloseTemporaryGameView;
            if(OnPostClosingTempGameView != null)
            {
                OnPostClosingTempGameView.Invoke();
            }
        }

        private static void SetupGameView()
        {
            var gameView = new GameView(SRDGameViewName);
            gameView.scale = 1.0f;
            gameView.targetDisplay = 0;
            gameView.noCameraWarning = false;
            gameView.showToolbar = false;
            gameView.rectangle = TargetScreenRect;
            gameView.ShowWithPopupMenu();
        }

        private static void SetupGameViewAfterCloseTempGameView()
        {
            CloseAllUnityGameView();
            SetupGameView();
            OnPostClosingTempGameView -= SetupGameViewAfterCloseTempGameView;
        }
    }
}

#endif

/*
 * Copyright 2019,2020,2023 Sony Corporation
 */

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using SRD.Core;

namespace SRD.Utils
{
    internal class SRDApplicationWindow
    {
#if !UNITY_EDITOR
#if UNITY_2019_1_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void FitSRDDisplay()
        {
            SRDCorePlugin.ShowNativeLog();

            if(SRDProjectSettings.IsRunWithoutSRDisplayMode())
            {
                return;
            }

            SrdXrResult result = SRDCorePlugin.SelectDevice(out var device);
            if (result == SrdXrResult.ERROR_USER_CANCEL)
            {
                Application.Quit();
                return;
            }
            else if (result != SrdXrResult.SUCCESS)
            {
                var errorToMessage = new Dictionary<SrdXrResult, string>()
                {
                    { SrdXrResult.ERROR_RUNTIME_NOT_FOUND, SRDHelper.SRDMessages.DLLNotFoundError},
                    { SrdXrResult.ERROR_DEVICE_NOT_FOUND, SRDHelper.SRDMessages.DisplayConnectionError},
                    { SrdXrResult.ERROR_RUNTIME_UNSUPPORTED, SRDHelper.SRDMessages.OldRuntimeUnsupportedError},
                };
                var msg = errorToMessage.ContainsKey(result) ? errorToMessage[result] : SRDHelper.SRDMessages.UnknownError;
                SRDHelper.PopupMessageAndForceToTerminate(msg);
                return;
            }

            var target = device.target_monitor_rectangle;
            var position = new Vector2Int(target.left, target.top);
            var resolution = new Vector2Int(target.right - target.left, target.bottom - target.top);

            var hWnd = GetSelfWindowHandle();
            User32.LPRECT rect;
            User32.GetWindowRect(hWnd, out rect);

            if(position.x == rect.left && position.y == rect.top &&
                    resolution.x == (rect.right - rect.left) && resolution.y == (rect.bottom - rect.top) &&
                    resolution.x == Screen.width && resolution.y == Screen.height)
            {
                return;
            }

            User32.SetWindowPos(hWnd, 0,
                                position.x, position.y,
                                resolution.x, resolution.y, 0x0040);
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            //Screen.fullScreen = true;
            Screen.SetResolution(resolution.x, resolution.y, true);
        }
#endif  // !UNITY_EDITOR

        public static IntPtr GetSelfWindowHandle()
        {
            var wsVisible = 0x10000000;
            var thisProcess = System.Diagnostics.Process.GetCurrentProcess();
            var hWnd = User32.GetTopWindow(IntPtr.Zero);

            while(hWnd != IntPtr.Zero)
            {
                int processId;
                User32.GetWindowThreadProcessId(hWnd, out processId);
                if(processId == thisProcess.Id)
                {
                    if((User32.GetWindowLong(hWnd, -16) & wsVisible) != 0)
                    {
                        return hWnd;
                    }
                }
                hWnd = User32.GetWindow(hWnd, 2);
            }
            return IntPtr.Zero;
        }

        private struct User32
        {
            [DllImport("user32.dll")]
            public static extern IntPtr GetTopWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern IntPtr GetWindow(IntPtr hWnd, uint wCmd);

            [DllImport("user32.dll")]
            public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll")]
            public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

            [DllImport("user32.dll")]
            public static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int y, int cx, int cy, int uFlags);

            [DllImport("user32.dll")]
            public static extern bool GetWindowRect(IntPtr hwnd, out LPRECT lpRect);

            [StructLayout(LayoutKind.Sequential)]
            public struct LPRECT
            {
                public int left;
                public int top;
                public int right;
                public int bottom;
            }
        }
    }
}

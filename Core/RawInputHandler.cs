using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace ESP32_DS4_Controller_WPF.Core
{
    public class RawInputHandler
    {
        // Raw Input Device Types
        private const uint RIM_TYPEKEYBOARD = 1;
        private const uint RIM_TYPEMOUSE = 0;

        // Raw Input Header
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        // Raw Keyboard Input
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWKEYBOARD
        {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VKey;
            public uint Message;
            public ulong ExtraInformation;
        }

        // Raw Mouse Input
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWMOUSE
        {
            public ushort usFlags;
            public uint ulButtons;
            public short usButtonFlags;
            public short usButtonData;
            public uint ulRawButtons;
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }

        // Raw Input Structure
        [StructLayout(LayoutKind.Explicit)]
        private struct RAWINPUT
        {
            [FieldOffset(0)]
            public RAWINPUTHEADER header;
            [FieldOffset(24)]
            public RAWKEYBOARD keyboard;
            [FieldOffset(24)]
            public RAWMOUSE mouse;
        }

        // Raw Input Device
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        private const uint RIDEV_NOLEGACY = 0x00000030;
        private const uint RIDEV_INPUTSINK = 0x00000100;
        private const uint RID_INPUT = 0x10000003;

        [DllImport("user32.dll")]
        private static extern bool RegisterRawInputDevices([MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        public delegate void OnKeyEventDelegate(int vKey, bool isPressed);
        public delegate void OnMouseMoveDelegate(int deltaX, int deltaY);

        public event OnKeyEventDelegate OnKeyEvent;
        public event OnMouseMoveDelegate OnMouseMove;

        private IntPtr windowHandle;
        private Dictionary<int, bool> keyStates = new Dictionary<int, bool>();
        private int mouseXAccum = 0;
        private int mouseYAccum = 0;
        private const int MOUSE_THRESHOLD = 5;

        public RawInputHandler(IntPtr hwnd)
        {
            windowHandle = hwnd;
            RegisterRawInputDevices();
        }

        private void RegisterRawInputDevices()
        {
            RAWINPUTDEVICE[] devices = new RAWINPUTDEVICE[2];

            // Keyboard
            devices[0].usUsagePage = 0x01;
            devices[0].usUsage = 0x06;
            devices[0].dwFlags = RIDEV_NOLEGACY | RIDEV_INPUTSINK;
            devices[0].hwndTarget = windowHandle;

            // Mouse
            devices[1].usUsagePage = 0x01;
            devices[1].usUsage = 0x02;
            devices[1].dwFlags = RIDEV_INPUTSINK;
            devices[1].hwndTarget = windowHandle;

            if (!RegisterRawInputDevices(devices, 2, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
            {
                throw new Exception("Failed to register raw input devices");
            }
        }

        public void ProcessRawInput(IntPtr lParam)
        {
            uint pcbSize = 0;
            GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref pcbSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

            if (pcbSize == 0)
                return;

            IntPtr buffer = Marshal.AllocHGlobal((int)pcbSize);

            try
            {
                GetRawInputData(lParam, RID_INPUT, buffer, ref pcbSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));
                RAWINPUT input = Marshal.PtrToStructure<RAWINPUT>(buffer);

                if (input.header.dwType == RIM_TYPEKEYBOARD)
                {
                    ProcessKeyboardInput(input);
                }
                else if (input.header.dwType == RIM_TYPEMOUSE)
                {
                    ProcessMouseInput(input);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private void ProcessKeyboardInput(RAWINPUT input)
        {
            int vKey = input.keyboard.VKey;
            bool isPressed = (input.keyboard.Flags & 0x01) == 0;

            if (!keyStates.ContainsKey(vKey))
                keyStates[vKey] = !isPressed;

            bool stateChanged = keyStates[vKey] != isPressed;
            keyStates[vKey] = isPressed;

            if (stateChanged)
            {
                OnKeyEvent?.Invoke(vKey, isPressed);
            }
        }

        private void ProcessMouseInput(RAWINPUT input)
        {
            int deltaX = input.mouse.lLastX;
            int deltaY = input.mouse.lLastY;

            mouseXAccum += deltaX;
            mouseYAccum += deltaY;

            if (Math.Abs(mouseXAccum) >= MOUSE_THRESHOLD || Math.Abs(mouseYAccum) >= MOUSE_THRESHOLD)
            {
                int sendX = mouseXAccum;
                int sendY = mouseYAccum;

                mouseXAccum = 0;
                mouseYAccum = 0;

                OnMouseMove?.Invoke(sendX, sendY);
            }
        }

        public static string VKeyToString(int vKey)
        {
            try
            {
                System.Windows.Input.Key wpfKey = KeyInterop.KeyFromVirtualKey(vKey);
                return wpfKey.ToString();
            }
            catch
            {
                return $"0x{vKey:X2}";
            }
        }

        public static int StringToVKey(string keyName)
        {
            try
            {
                if (Enum.TryParse<System.Windows.Input.Key>(keyName, out var key))
                {
                    return (int)key;
                }
            }
            catch { }
            return -1;
        }
    }
}

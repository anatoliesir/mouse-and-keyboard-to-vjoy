using System;
using System.Runtime.InteropServices;

namespace MouseToVJoy.Helpers
{
    public struct RawMouseData
    {
        public int LastX { get; set; }
        public int LastY { get; set; }

        public RawMouseData(int lastX, int lastY)
        {
            LastX = lastX;
            LastY = lastY;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr Param;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RawMouse
    {
        public ushort Flags;
        public ushort ButtonFlags;
        public ushort ButtonData;
        public uint RawButtons;
        public int LastX;
        public int LastY;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct RawInput
    {
        [FieldOffset(0)]
        public RawInputHeader Header;

        [FieldOffset(16)] // Alignment offset for 32-bit systems
        private RawMouse _mouse86;

        [FieldOffset(24)] // Alignment offset for 64-bit systems
        private RawMouse _mouse64;

        public RawMouse Mouse => IntPtr.Size == 8 ? _mouse64 : _mouse86;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CursorPoint
    {
        public int X;
        public int Y;

        public CursorPoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CursorClipRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
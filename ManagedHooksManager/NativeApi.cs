using System;
using System.Runtime.InteropServices;

namespace ManagedHooksManager
{
    internal struct NativeApi
    {
        [DllImport("imagehlp.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern IntPtr ImageDirectoryEntryToDataEx(
            [In] IntPtr Base,
            [In] [MarshalAs(UnmanagedType.U1)] bool MappedAsImage,
            [In] [MarshalAs(UnmanagedType.U2)] short DirectoryEntry,
            [Out][MarshalAs(UnmanagedType.U4)] out int Size,
            [Out] out IntPtr FoundHeader);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string ModuleName);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr LoadLibrary(
            [In] [MarshalAs(UnmanagedType.LPWStr)] string ModuleName);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool VirtualProtect(
            [In] IntPtr Address,
            [In] [MarshalAs(UnmanagedType.SysUInt)] IntPtr Size,
            [In] [MarshalAs(UnmanagedType.U4)] int NewProtect,
            [Out] [MarshalAs(UnmanagedType.U4)] out int OldProtect);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.SysUInt)]
        public static extern IntPtr VirtualQuery(
            [In] IntPtr Address,
            [Out] IntPtr Buffer,
            [In] [MarshalAs(UnmanagedType.SysUInt)] IntPtr Length);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        public static extern IntPtr VirtualAlloc(
            [In] IntPtr Address,
            [In] [MarshalAs(UnmanagedType.SysUInt)] IntPtr Size,
            [In] [MarshalAs(UnmanagedType.U4)] int AllocationType,
            [In] [MarshalAs(UnmanagedType.U4)] int Protect);

        public const short IMAGE_DIRECTORY_ENTRY_EXPORT = 0; // Export Directory

        public const int PAGE_READWRITE = 0x04;
        public const int PAGE_WRITECOPY = 0x08;
        public const int PAGE_EXECUTE = 0x10;
        public const int PAGE_EXECUTE_READ = 0x20;
        public const int PAGE_EXECUTE_READWRITE = 0x40;

        public const int MEM_COMMIT = 0x00001000;
        public const int MEM_RESERVE = 0x00002000;
        public const int MEM_FREE = 0x00010000;

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_EXPORT_DIRECTORY
        {
            public int Characteristics;
            public int TimeDateStamp;
            public short MajorVersion;
            public short MinorVersion;
            public int Name;
            public int Base;
            public int NumberOfFunctions;
            public int NumberOfNames;
            public int AddressOfFunctions;
            public int AddressOfNames;
            public int AddressOfNameOrdinals;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        public struct MEMORY_BASIC_INFORMATION64
        {
            public long BaseAddress;
            public long AllocationBase;
            public int AllocationProtect;
            public int __alignment1;
            public long RegionSize;
            public int State;
            public int Protect;
            public int Type;
            public int __alignment2;
        }
    }
}

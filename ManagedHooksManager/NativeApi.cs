using System;
using System.Runtime.InteropServices;
using System.Security;

namespace ManagedHooksManager
{
    [SuppressUnmanagedCodeSecurity]
    internal static class NativeApi
    {
        [DllImport("imagehlp.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr ImageDirectoryEntryToDataEx(
            [In] IntPtr Base,
            [In] [MarshalAs(UnmanagedType.U1)] bool MappedAsImage,
            [In] short DirectoryEntry,
            [Out] out int Size,
            [Out] out IntPtr FoundHeader);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GetModuleHandle([In] string ModuleName);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr LoadLibrary([In] string ModuleName);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Ansi, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GetProcAddress([In] IntPtr Module, [In] string ProcName);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool VirtualProtect(
            [In] IntPtr Address,
            [In] IntPtr Size,
            [In] int NewProtect,
            [Out] out int OldProtect);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr VirtualQuery(
            [In] IntPtr Address,
            [Out] IntPtr Buffer,
            [In] IntPtr Length);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr VirtualAlloc(
            [In] IntPtr Address,
            [In] IntPtr Size,
            [In] int AllocationType,
            [In] int Protect);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FlushInstructionCache(
            [In] IntPtr Process,
            [In] IntPtr BaseAddress,
            [In] IntPtr Size);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int RoGetActivationFactory(
            [In] IntPtr activatableClassId,
            [In] ref Guid iid,
            [Out] out IntPtr factory);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int RoResolveNamespace(
            [In] IntPtr name,
            [In] IntPtr windowsMetaDataDir,
            [In] int packageGraphDirsCount,
            [In] IntPtr packageGraphDirs,
            [Out] out int metaDataFilePathsCount,
            [Out] out IntPtr metaDataFilePaths,
            [Out] out int subNamespacesCount,
            [Out] out IntPtr subNamespaces);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int DllGetActivationFactory(
            [In] IntPtr activatableClassId,
            [Out] out IntPtr factory);
    }
}

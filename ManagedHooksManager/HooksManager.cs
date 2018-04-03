using System;
using System.Runtime.InteropServices;

namespace ManagedHooksManager
{
    public sealed class HooksManager
    {
        private ApiHook<RoGetActivationFactory> _roGetActivationFactoryHook;
        private ApiHook<RoResolveNamespace> _roResolveNamespaceHook;

        public void SetupHooks()
        {
            _roGetActivationFactoryHook = new ApiHook<RoGetActivationFactory>("combase", "RoGetActivationFactory");
            _roGetActivationFactoryHook.HookEatTableOfModule(HookedRoGetActivationFactory);

            _roResolveNamespaceHook = new ApiHook<RoResolveNamespace>("WinTypes", "RoResolveNamespace");
            _roResolveNamespaceHook.HookEatTableOfModule(HookedRoResolveNamespace);
        }

        private uint HookedRoGetActivationFactory(
            string activatableClassId,
            ref Guid iid,
            out object factory)
        {
            var roGetActivationFactory = _roGetActivationFactoryHook.OriginalFunction;

            var result = roGetActivationFactory(activatableClassId, ref iid, out factory);

            return result;
        }

        private uint HookedRoResolveNamespace(
            string name,
            string windowsMetaDataDir,
            uint packageGraphDirsCount,
            IntPtr packageGraphDirs,
            out uint metaDataFilePathsCount,
            out IntPtr metaDataFilePaths,
            out uint subNamespacesCount,
            out IntPtr subNamespaces)
        {
            var roResolveNamespace = _roResolveNamespaceHook.OriginalFunction;

            var result = roResolveNamespace(
                name,
                windowsMetaDataDir,
                packageGraphDirsCount,
                packageGraphDirs,
                out metaDataFilePathsCount,
                out metaDataFilePaths,
                out subNamespacesCount,
                out subNamespaces);

            return result;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate uint RoGetActivationFactory(
            [In] [MarshalAs(UnmanagedType.HString)] string activatableClassId,
            [In] ref Guid iid,
            [Out] [MarshalAs(UnmanagedType.IInspectable)] out object factory);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate uint RoResolveNamespace(
            [In] [MarshalAs(UnmanagedType.HString)] string name,
            [In] [MarshalAs(UnmanagedType.HString)] string windowsMetaDataDir,
            [In] uint packageGraphDirsCount,
            [In] IntPtr packageGraphDirs,
            [Out] out uint metaDataFilePathsCount,
            [Out] out IntPtr metaDataFilePaths,
            [Out] out uint subNamespacesCount,
            [Out] out IntPtr subNamespaces);
    }
}

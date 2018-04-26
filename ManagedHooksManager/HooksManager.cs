using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;

namespace ManagedHooksManager
{
    public sealed class HooksManager
    {
        private ApiHook<NativeApi.RoGetActivationFactory> _roGetActivationFactoryHook;
        private ApiHook<NativeApi.RoResolveNamespace> _roResolveNamespaceHook;

        public void SetupHooks()
        {
            _roGetActivationFactoryHook = new ApiHook<NativeApi.RoGetActivationFactory>("combase", "RoGetActivationFactory");
            _roGetActivationFactoryHook.HookEatTableOfModule(HookedRoGetActivationFactory);

            _roResolveNamespaceHook = new ApiHook<NativeApi.RoResolveNamespace>("WinTypes", "RoResolveNamespace");
            _roResolveNamespaceHook.HookEatTableOfModule(HookedRoResolveNamespace);
        }

        private int HookedRoResolveNamespace(
            IntPtr name,
            IntPtr windowsMetaDataDir,
            int packageGraphDirsCount,
            IntPtr packageGraphDirs,
            out int metaDataFilePathsCount,
            out IntPtr metaDataFilePaths,
            out int subNamespacesCount,
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

            if (result != NativeConstants.RO_E_METADATA_NAME_NOT_FOUND &&
                result != NativeConstants.APPMODEL_ERROR_NO_PACKAGE)
            {
                return result;
            }

            var assemblyDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var assemblyDirHString = WindowsRuntimeMarshal.StringToHString(assemblyDir);

            packageGraphDirs = Marshal.AllocHGlobal(IntPtr.Size);
            packageGraphDirsCount = 1;

            try
            {
                Marshal.WriteIntPtr(packageGraphDirs, assemblyDirHString);
                result = roResolveNamespace(
                    name,
                    windowsMetaDataDir,
                    packageGraphDirsCount,
                    packageGraphDirs,
                    out metaDataFilePathsCount,
                    out metaDataFilePaths,
                    out subNamespacesCount,
                    out subNamespaces);
            }
            finally
            {
                WindowsRuntimeMarshal.FreeHString(assemblyDirHString);
                Marshal.FreeHGlobal(packageGraphDirs);
            }

            return result;
        }

        private int HookedRoGetActivationFactory(
            IntPtr activatableClassId,
            ref Guid iid,
            out IntPtr factory)
        {
            var roGetActivationFactory = _roGetActivationFactoryHook.OriginalFunction;

            var result = roGetActivationFactory(activatableClassId, ref iid, out factory);
            if (result != NativeConstants.REGDB_E_CLASSNOTREG)
                return result;

            return result;
        }
    }
}

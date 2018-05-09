using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;

namespace ManagedHooksManager
{
    public sealed class HooksManager
    {
        private readonly ConcurrentDictionary<string, string> _winRtTypesRegistry;
        private readonly string _executingAssemblyDir;

        private ApiHook<NativeApi.RoGetActivationFactory> _roGetActivationFactoryHook;
        private ApiHook<NativeApi.RoResolveNamespace> _roResolveNamespaceHook;

        public HooksManager()
        {
            _winRtTypesRegistry = new ConcurrentDictionary<string, string>();
            _executingAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        public void RegisterWinRtType(string assemblyQualifiedWinRtTypeName)
        {
            var winRtType = Type.GetType(assemblyQualifiedWinRtTypeName, throwOnError: true);
            RegisterWinRtType(winRtType);
        }

        public void RegisterWinRtType<T>()
        {
            RegisterWinRtType(typeof(T));
        }

        public void RegisterWinRtType(Type winRtType)
        {
            if (winRtType == null)
                throw new ArgumentNullException(nameof(winRtType));

            var implementationPath = Path.ChangeExtension(winRtType.Assembly.Location, "dll");

            _winRtTypesRegistry.AddOrUpdate(
                winRtType.FullName,
                typeName => implementationPath,
                (typeName, registeredPath) =>
                {
                    System.Diagnostics.Debug.Fail(
                      $"Type {typeName} already registered.",
                      $"This type has already been registered with implementation file \"{registeredPath}\". " +
                      $"Current registration attempt with file \"{implementationPath}\" will be ignored.");
                    return registeredPath;
                });
        }

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

            var assemblyDirHString = WindowsRuntimeMarshal.StringToHString(_executingAssemblyDir);

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

            var activatableClassName = WindowsRuntimeMarshal.PtrToStringHString(activatableClassId);
            if (!_winRtTypesRegistry.TryGetValue(activatableClassName, out var implementationPath))
            {
                System.Diagnostics.Debug.Fail($"Type {activatableClassName} is not registered.");
                return result;
            }

            var implementationModule = NativeApi.LoadLibrary(implementationPath);
            if (implementationModule == IntPtr.Zero)
            {
                System.Diagnostics.Debug.Fail($"Failed to load implementation file \"{implementationPath}\".");
                return result;
            }

            var getActivationFactoryProc = NativeApi.GetProcAddress(implementationModule, "DllGetActivationFactory");
            if (getActivationFactoryProc == IntPtr.Zero)
            {
                System.Diagnostics.Debug.Fail($"Implementation file \"{implementationPath}\" does not export DllGetActivationFactory function.");
                return result;
            }

            var getActivationFactoryDelegate = Marshal.GetDelegateForFunctionPointer<
                NativeApi.DllGetActivationFactory>(getActivationFactoryProc);

            result = getActivationFactoryDelegate(activatableClassId, out var factoryObject);
            if (result != 0)
            {
                System.Diagnostics.Debug.Fail($"Failed to get activation factory for type {activatableClassName}.");
                return result;
            }

            result = Marshal.QueryInterface(factoryObject, ref iid, out factory);
            Marshal.Release(factory);

            return result;
        }
    }
}

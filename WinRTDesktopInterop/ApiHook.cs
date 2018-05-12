using System;
using System.Runtime.InteropServices;

namespace WinRTDesktopInterop
{
    internal sealed class ApiHook<TDelegate>
    {
        private readonly string _moduleName;
        private readonly string _exportName;

        private HookTrampoline _trampoline;

        public ApiHook(string moduleName, string exportName)
        {
            _moduleName = moduleName;
            _exportName = exportName;
        }

        public TDelegate OriginalFunction { get; private set; }

        public void HookEatTableOfModule(TDelegate newFunc)
        {
            var moduleBase = NativeApi.LoadLibrary(_moduleName);
            if (moduleBase == IntPtr.Zero)
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error(), new IntPtr(-1));

            var imageExportDirectoryPtr = NativeApi.ImageDirectoryEntryToDataEx(
                moduleBase,
                true,
                NativeConstants.IMAGE_DIRECTORY_ENTRY_EXPORT,
                out _,
                out _);

            if (imageExportDirectoryPtr == IntPtr.Zero)
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error(), new IntPtr(-1));

            var imageExportDirectory = Marshal.PtrToStructure<
                NativeStructs.IMAGE_EXPORT_DIRECTORY>(imageExportDirectoryPtr);

            if (!TryFindExportNameOrdinal(imageExportDirectory, moduleBase, out var nameOrdinal, out var originalFuncRva))
                throw new EntryPointNotFoundException($"Export name {_exportName} could not be found in module {_moduleName}.");

            var newFuncHandle = GCHandle.Alloc(newFunc);

            var newFuncPointer = Marshal.GetFunctionPointerForDelegate(newFunc);
            var newFuncRva = GetNewFunctionRva(moduleBase, newFuncPointer);

            var addrOfFuncRva = moduleBase + imageExportDirectory.AddressOfFunctions + nameOrdinal * sizeof(int);
            if (!TryWriteFunctionRva(addrOfFuncRva, newFuncRva))
            {
                newFuncHandle.Free();
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error(), new IntPtr(-1));
            }

            OriginalFunction = Marshal.GetDelegateForFunctionPointer<TDelegate>(moduleBase + originalFuncRva);
        }

        private bool TryFindExportNameOrdinal(
            NativeStructs.IMAGE_EXPORT_DIRECTORY imageExportDirectory,
            IntPtr moduleBase,
            out short nameOrdinal,
            out int functionRva)
        {
            var nameRvas = new int[imageExportDirectory.NumberOfNames];
            Marshal.Copy(moduleBase + imageExportDirectory.AddressOfNames, nameRvas, 0, nameRvas.Length);

            var nameOrdinals = new short[imageExportDirectory.NumberOfNames];
            Marshal.Copy(moduleBase + imageExportDirectory.AddressOfNameOrdinals, nameOrdinals, 0, nameOrdinals.Length);

            var funcAddrs = new int[imageExportDirectory.NumberOfFunctions];
            Marshal.Copy(moduleBase + imageExportDirectory.AddressOfFunctions, funcAddrs, 0, funcAddrs.Length);

            nameOrdinal = -1;
            functionRva = -1;

            for (int i = 0; i < imageExportDirectory.NumberOfNames; i++)
            {
                var funcName = Marshal.PtrToStringAnsi(moduleBase + nameRvas[i]);

                if (string.Equals(funcName, _exportName, StringComparison.OrdinalIgnoreCase))
                {
                    nameOrdinal = nameOrdinals[i];
                    functionRva = funcAddrs[nameOrdinal];
                    break;
                }
            }

            return nameOrdinal != -1;
        }

        private int GetNewFunctionRva(IntPtr moduleBase, IntPtr newFuncAddress)
        {
            if (IntPtr.Size == sizeof(int))
                return newFuncAddress.ToInt32() - moduleBase.ToInt32();

            _trampoline = new HookTrampoline(moduleBase, newFuncAddress);
            return _trampoline.CreateX64Trampoline();
        }

        private bool TryWriteFunctionRva(IntPtr addrOfFuncAddress, int funcRva)
        {
            var eatFuncEntrySize = new IntPtr(sizeof(int));

            var protectResult = NativeApi.VirtualProtect(
                addrOfFuncAddress,
                eatFuncEntrySize,
                NativeConstants.PAGE_WRITECOPY,
                out var oldProtect);

            if (!protectResult)
                return false;

            Marshal.WriteInt32(addrOfFuncAddress, funcRva);

            protectResult = NativeApi.VirtualProtect(
                addrOfFuncAddress,
                eatFuncEntrySize,
                oldProtect,
                out _);

            return protectResult;
        }
    }
}

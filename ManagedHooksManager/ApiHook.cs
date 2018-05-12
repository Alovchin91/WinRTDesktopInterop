﻿using System;
using System.Runtime.InteropServices;

namespace ManagedHooksManager
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
                throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());

            var imageExportDirectoryPtr = NativeApi.ImageDirectoryEntryToDataEx(
                moduleBase,
                true,
                NativeConstants.IMAGE_DIRECTORY_ENTRY_EXPORT,
                out _,
                out _);

            if (imageExportDirectoryPtr == IntPtr.Zero)
                throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());

            var imageExportDirectory = Marshal.PtrToStructure<
                NativeStructs.IMAGE_EXPORT_DIRECTORY>(imageExportDirectoryPtr);

            if (!TryFindExportNameOrdinal(imageExportDirectory, moduleBase, out var nameOrdinal, out var originalFuncRva))
                throw new EntryPointNotFoundException($"Export name {_exportName} could not be found in module {_moduleName}.");

            var newFuncPointer = Marshal.GetFunctionPointerForDelegate(newFunc);
            var newFuncRva = GetNewFunctionRva(moduleBase, newFuncPointer);

            var eatFuncEntrySize = new IntPtr(sizeof(int));
            var addrOfFuncAddress = moduleBase + imageExportDirectory.AddressOfFunctions + nameOrdinal * sizeof(int);

            var protectResult = NativeApi.VirtualProtect(
                addrOfFuncAddress,
                eatFuncEntrySize,
                NativeConstants.PAGE_WRITECOPY,
                out var oldProtect);

            if (!protectResult)
                throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());

            Marshal.WriteInt32(addrOfFuncAddress, newFuncRva);
            GCHandle.Alloc(newFunc);

            protectResult = NativeApi.VirtualProtect(
                addrOfFuncAddress,
                eatFuncEntrySize,
                oldProtect,
                out _);

            if (!protectResult)
                throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());

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
    }
}

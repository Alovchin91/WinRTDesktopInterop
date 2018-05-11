using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ManagedHooksManager
{
    internal sealed class ApiHook<TDelegate>
    {
        private const long TrampolineMemoryRegionSize = 0x10000L;

        private readonly string _moduleName;
        private readonly string _exportName;

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

            var moduleBaseAddress = moduleBase.ToInt64();

            if (!TryFindExportNameOrdinal(imageExportDirectory, moduleBaseAddress, out var nameOrdinal, out var originalFuncRva))
                throw new EntryPointNotFoundException($"Export name {_exportName} could not be found in module {_moduleName}.");

            var newFuncPointer = Marshal.GetFunctionPointerForDelegate(newFunc);

            int newFuncRva = IntPtr.Size == Marshal.SizeOf<int>()
                ? newFuncPointer.ToInt32() - moduleBase.ToInt32()
                : CreateX64Trampoline(moduleBaseAddress, newFuncPointer);

            var eatFuncEntrySize = new IntPtr(Marshal.SizeOf<int>());

            var addrOfFuncAddress = new IntPtr(
                moduleBaseAddress + imageExportDirectory.AddressOfFunctions + nameOrdinal * Marshal.SizeOf<int>());

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

            OriginalFunction = Marshal.GetDelegateForFunctionPointer<TDelegate>(
                new IntPtr(moduleBaseAddress + originalFuncRva));
        }

        private bool TryFindExportNameOrdinal(
            NativeStructs.IMAGE_EXPORT_DIRECTORY imageExportDirectory,
            long moduleBaseAddress,
            out short nameOrdinal,
            out int functionRva)
        {
            var nameRvas = new int[imageExportDirectory.NumberOfNames];
            Marshal.Copy(new IntPtr(moduleBaseAddress + imageExportDirectory.AddressOfNames), nameRvas, 0, nameRvas.Length);

            var nameOrdinals = new short[imageExportDirectory.NumberOfNames];
            Marshal.Copy(new IntPtr(moduleBaseAddress + imageExportDirectory.AddressOfNameOrdinals), nameOrdinals, 0, nameOrdinals.Length);

            var funcAddrs = new int[imageExportDirectory.NumberOfFunctions];
            Marshal.Copy(new IntPtr(moduleBaseAddress + imageExportDirectory.AddressOfFunctions), funcAddrs, 0, funcAddrs.Length);

            nameOrdinal = -1;
            functionRva = -1;

            for (int i = 0; i < imageExportDirectory.NumberOfNames; i++)
            {
                var funcName = Marshal.PtrToStringAnsi(new IntPtr(moduleBaseAddress + nameRvas[i]));

                if (string.Equals(funcName, _exportName, StringComparison.OrdinalIgnoreCase))
                {
                    nameOrdinal = nameOrdinals[i];
                    functionRva = funcAddrs[nameOrdinal];
                    break;
                }
            }

            return nameOrdinal != -1;
        }

        private static int CreateX64Trampoline(long moduleBaseAddress, IntPtr newFuncPointer)
        {
            var memoryInfoStructSize = Marshal.SizeOf<NativeStructs.MEMORY_BASIC_INFORMATION64>();
            var memoryInfoStructPtr = Marshal.AllocHGlobal(memoryInfoStructSize);

            var trampolineAddress = IntPtr.Zero;
            var trampoline = GetTrampolineInstructions(newFuncPointer);

            var memoryRegionSize = new IntPtr(TrampolineMemoryRegionSize);

            var tryMemoryBlock = moduleBaseAddress;
            var tryMemoryBound = moduleBaseAddress + uint.MaxValue;

            do
            {
                tryMemoryBlock += TrampolineMemoryRegionSize;

                var memoryBlockPtr = new IntPtr(tryMemoryBlock);

                var structSize = NativeApi.VirtualQuery(
                    memoryBlockPtr,
                    memoryInfoStructPtr,
                    new IntPtr(memoryInfoStructSize));

                if (structSize == IntPtr.Zero)
                    throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());

                var memoryInfo = Marshal.PtrToStructure<
                    NativeStructs.MEMORY_BASIC_INFORMATION64>(memoryInfoStructPtr);

                if (memoryInfo.State == NativeConstants.MEM_FREE &&
                    memoryInfo.RegionSize >= TrampolineMemoryRegionSize)
                {
                    trampolineAddress = NativeApi.VirtualAlloc(
                        memoryBlockPtr,
                        memoryRegionSize,
                        NativeConstants.MEM_COMMIT | NativeConstants.MEM_RESERVE,
                        NativeConstants.PAGE_EXECUTE_READWRITE);
                }
            }
            while (trampolineAddress == IntPtr.Zero && tryMemoryBlock <= tryMemoryBound);

            Marshal.FreeHGlobal(memoryInfoStructPtr);

            if (trampolineAddress == IntPtr.Zero)
                throw new InvalidOperationException("Failed to allocate space for trampoline.");

            Marshal.Copy(trampoline, 0, trampolineAddress, trampoline.Length);
            int newFuncRva = unchecked((int)(trampolineAddress.ToInt64() - moduleBaseAddress));

            var protectResult = NativeApi.VirtualProtect(
                trampolineAddress,
                memoryRegionSize,
                NativeConstants.PAGE_EXECUTE_READ,
                out _);

            if (!protectResult)
                throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());

            var processHandle = Process.GetCurrentProcess().Handle;
            NativeApi.FlushInstructionCache(processHandle, trampolineAddress, memoryRegionSize);

            return newFuncRva;
        }

        private static byte[] GetTrampolineInstructions(IntPtr newFuncPointer)
        {
            var processorArchitecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");

            if (string.Equals(processorArchitecture, "amd64", StringComparison.OrdinalIgnoreCase))
            {
                var trampoline = new byte[]
                {
                    0x68, 0xcc, 0xcc, 0xcc, 0xcc,                       // push   dword low
                    0xc7, 0x44, 0x24, 0x04, 0xcc, 0xcc, 0xcc, 0xcc,     // mov    DWORD PTR [rsp+4], dword high
                    0xc3                                                // ret
                };

                var newFuncAddressBytes = BitConverter.GetBytes(newFuncPointer.ToInt64());

                Array.Copy(newFuncAddressBytes, 0, trampoline, 1, 4);
                Array.Copy(newFuncAddressBytes, 4, trampoline, 9, 4);

                return trampoline;
            }

            throw new PlatformNotSupportedException($"Processor architecture {processorArchitecture} is not supported.");
        }
    }
}

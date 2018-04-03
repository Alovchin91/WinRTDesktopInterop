using System;
using System.Runtime.InteropServices;

namespace ManagedHooksManager
{
    internal sealed class ApiHook<TDelegate>
    {
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
                NativeApi.IMAGE_DIRECTORY_ENTRY_EXPORT,
                out var _,
                out var _);

            if (imageExportDirectoryPtr == IntPtr.Zero)
                throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());

            var imageExportDirectory = Marshal.PtrToStructure<
                NativeApi.IMAGE_EXPORT_DIRECTORY>(imageExportDirectoryPtr);

            var moduleBaseAddress = moduleBase.ToInt64();

            var namesRvasA = new int[imageExportDirectory.NumberOfNames];
            Marshal.Copy(new IntPtr(moduleBaseAddress + imageExportDirectory.AddressOfNames), namesRvasA, 0, namesRvasA.Length);

            var nameOrdinalsA = new short[imageExportDirectory.NumberOfNames];
            Marshal.Copy(new IntPtr(moduleBaseAddress + imageExportDirectory.AddressOfNameOrdinals), nameOrdinalsA, 0, nameOrdinalsA.Length);

            var funcAddrsA = new int[imageExportDirectory.NumberOfFunctions];
            Marshal.Copy(new IntPtr(moduleBaseAddress + imageExportDirectory.AddressOfFunctions), funcAddrsA, 0, funcAddrsA.Length);

            for (int i = 0; i < imageExportDirectory.NumberOfNames; i++)
            {
                var funcName = Marshal.PtrToStringAnsi(new IntPtr(moduleBaseAddress + namesRvasA[i]));

                if (!string.Equals(funcName, _exportName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var ordinal = nameOrdinalsA[i];

                var addrOfFuncAddress = new IntPtr(
                    moduleBaseAddress + imageExportDirectory.AddressOfFunctions + ordinal * Marshal.SizeOf<int>());

                var newFuncPointer = Marshal.GetFunctionPointerForDelegate(newFunc);

                int newFuncRva = IntPtr.Size == Marshal.SizeOf<int>()
                    ? Convert.ToInt32(newFuncPointer.ToInt64() - moduleBaseAddress)
                    : CreateX64Trampoline(moduleBaseAddress, newFuncPointer);

                var eatFuncEntrySize = new IntPtr(Marshal.SizeOf<int>());

                var protectResult = NativeApi.VirtualProtect(
                    addrOfFuncAddress,
                    eatFuncEntrySize,
                    NativeApi.PAGE_WRITECOPY,
                    out var oldProtect);

                if (!protectResult)
                    throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());

                Marshal.Copy(new int[] { newFuncRva }, 0, addrOfFuncAddress, 1);
                GCHandle.Alloc(newFunc);

                protectResult = NativeApi.VirtualProtect(
                    addrOfFuncAddress,
                    eatFuncEntrySize,
                    oldProtect,
                    out oldProtect);

                OriginalFunction = Marshal.GetDelegateForFunctionPointer<TDelegate>(
                    new IntPtr(moduleBaseAddress + funcAddrsA[ordinal]));

                break;
            }
        }

        private static int CreateX64Trampoline(long moduleBaseAddress, IntPtr newFuncPointer)
        {
            int newFuncRva = 0;

            var memoryInfoStructSize = Marshal.SizeOf<NativeApi.MEMORY_BASIC_INFORMATION64>();
            var memoryInfoStructPtr = Marshal.AllocHGlobal(memoryInfoStructSize);

            var trampolineAddress = IntPtr.Zero;
            var trampoline = GetTrampolineInstructions(newFuncPointer);
            var trampolineSize = new IntPtr(trampoline.Length);

            const long virtual2Gb = 2 * 1024 * 1024 * 1024L;
            const long tryMemoryStep = 0x10000L;

            var tryMemoryBlock = moduleBaseAddress;

            while (trampolineAddress == IntPtr.Zero &&
                   tryMemoryBlock - moduleBaseAddress < virtual2Gb)
            {
                tryMemoryBlock += tryMemoryStep;

                var memoryBlockPtr = new IntPtr(tryMemoryBlock);

                var structSize = NativeApi.VirtualQuery(
                    memoryBlockPtr,
                    memoryInfoStructPtr,
                    new IntPtr(memoryInfoStructSize));

                if (structSize == IntPtr.Zero)
                    throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());

                var memoryInfo = Marshal.PtrToStructure<
                    NativeApi.MEMORY_BASIC_INFORMATION64>(memoryInfoStructPtr);

                if (memoryInfo.State == NativeApi.MEM_FREE &&
                    memoryInfo.RegionSize >= trampoline.Length)
                {
                    trampolineAddress = NativeApi.VirtualAlloc(
                        memoryBlockPtr,
                        trampolineSize,
                        NativeApi.MEM_COMMIT | NativeApi.MEM_RESERVE,
                        NativeApi.PAGE_READWRITE);

                    if (trampolineAddress != IntPtr.Zero)
                        break;
                }
            }

            Marshal.FreeHGlobal(memoryInfoStructPtr);

            if (trampolineAddress == IntPtr.Zero)
                throw new InvalidOperationException("Failed to allocate space for trampoline in +/-2GB range.");

            Marshal.Copy(trampoline, 0, trampolineAddress, trampoline.Length);
            newFuncRva = Convert.ToInt32(trampolineAddress.ToInt64() - moduleBaseAddress);

            var success = NativeApi.VirtualProtect(
                trampolineAddress,
                trampolineSize,
                NativeApi.PAGE_EXECUTE_READ,
                out var oldProtect);

            if (!success)
                throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());

            return newFuncRva;
        }

        private static byte[] GetTrampolineInstructions(IntPtr newFuncPointer)
        {
            var processorArchitecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");

            if (string.Equals(processorArchitecture, "amd64", StringComparison.OrdinalIgnoreCase))
            {
                var trampoline = new byte[]
                {
                    0x50,                                                       // push   rax
                    0x48, 0xB8, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc, // movabs rax,0xcccccccccccccccc
                    0x48, 0x87, 0x04, 0x24,                                     // xchg   QWORD PTR [rsp],rax
                    0xC3                                                        // ret
                };

                var newFuncAddressBytes = BitConverter.GetBytes(newFuncPointer.ToInt64());

                int newFuncAddressStartIndex = Array.IndexOf<byte>(trampoline, 0xcc);
                Array.Copy(newFuncAddressBytes, 0, trampoline, newFuncAddressStartIndex, newFuncAddressBytes.Length);

                return trampoline;
            }

            throw new PlatformNotSupportedException($"Processor architecture {processorArchitecture} is not supported.");
        }
    }
}

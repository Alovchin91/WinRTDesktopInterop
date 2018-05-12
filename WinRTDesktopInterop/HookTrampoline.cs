using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinRTDesktopInterop
{
    internal sealed class HookTrampoline
    {
        private const int TrampolineMemoryRegionSize = 0x10000;

        private readonly IntPtr _moduleBase;
        private readonly IntPtr _targetAddress;

        public HookTrampoline(IntPtr moduleBase, IntPtr targetAddress)
        {
            _moduleBase = moduleBase;
            _targetAddress = targetAddress;
        }

        public int CreateX64Trampoline()
        {
            var memoryInfoStructSize = Marshal.SizeOf<NativeStructs.MEMORY_BASIC_INFORMATION64>();
            var memoryInfoStructPtr = Marshal.AllocHGlobal(memoryInfoStructSize);

            var trampolineAddress = IntPtr.Zero;
            var trampoline = GetTrampolineInstructions(_targetAddress);

            var memoryRegionSize = new IntPtr(TrampolineMemoryRegionSize);

            var tryMemoryBlock = _moduleBase;
            var tryMemoryBound = _moduleBase.ToInt64() + uint.MaxValue;

            do
            {
                tryMemoryBlock += TrampolineMemoryRegionSize;

                var structSize = NativeApi.VirtualQuery(
                    tryMemoryBlock,
                    memoryInfoStructPtr,
                    new IntPtr(memoryInfoStructSize));

                if (structSize == IntPtr.Zero)
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error(), new IntPtr(-1));

                var memoryInfo = Marshal.PtrToStructure<
                    NativeStructs.MEMORY_BASIC_INFORMATION64>(memoryInfoStructPtr);

                if (memoryInfo.State == NativeConstants.MEM_FREE &&
                    memoryInfo.RegionSize >= TrampolineMemoryRegionSize)
                {
                    trampolineAddress = NativeApi.VirtualAlloc(
                        tryMemoryBlock,
                        memoryRegionSize,
                        NativeConstants.MEM_COMMIT | NativeConstants.MEM_RESERVE,
                        NativeConstants.PAGE_EXECUTE_READWRITE);
                }
            }
            while (trampolineAddress == IntPtr.Zero && tryMemoryBlock.ToInt64() <= tryMemoryBound);

            Marshal.FreeHGlobal(memoryInfoStructPtr);

            if (trampolineAddress == IntPtr.Zero)
                throw new InvalidOperationException("Failed to allocate space for trampoline.");

            Marshal.Copy(trampoline, 0, trampolineAddress, trampoline.Length);
            uint newFuncRva = Convert.ToUInt32(trampolineAddress.ToInt64() - _moduleBase.ToInt64());

            var protectResult = NativeApi.VirtualProtect(
                trampolineAddress,
                memoryRegionSize,
                NativeConstants.PAGE_EXECUTE_READ,
                out _);

            if (!protectResult)
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error(), new IntPtr(-1));

            var processHandle = Process.GetCurrentProcess().Handle;
            NativeApi.FlushInstructionCache(processHandle, trampolineAddress, memoryRegionSize);

            return unchecked((int)newFuncRva);
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

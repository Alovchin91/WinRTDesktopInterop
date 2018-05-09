using System.Runtime.InteropServices;

namespace ManagedHooksManager
{
    namespace NativeStructs
    {
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

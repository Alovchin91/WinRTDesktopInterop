namespace ManagedHooksManager
{
    internal static class NativeConstants
    {
        public const short IMAGE_DIRECTORY_ENTRY_EXPORT = 0; // Export Directory

        public const int PAGE_READWRITE = 0x04;
        public const int PAGE_WRITECOPY = 0x08;
        public const int PAGE_EXECUTE = 0x10;
        public const int PAGE_EXECUTE_READ = 0x20;
        public const int PAGE_EXECUTE_READWRITE = 0x40;

        public const int MEM_COMMIT = 0x00001000;
        public const int MEM_RESERVE = 0x00002000;
        public const int MEM_FREE = 0x00010000;

        public const int RO_E_METADATA_NAME_NOT_FOUND = unchecked((int)0x8000000F);
        public const int APPMODEL_ERROR_NO_PACKAGE = unchecked((int)0x80073D54);
        public const int REGDB_E_CLASSNOTREG = unchecked((int)0x80040154);
    }
}

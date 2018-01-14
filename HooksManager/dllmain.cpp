// dllmain.cpp : Defines the entry point for the DLL application.
#include "stdafx.h"
#include "HooksManager.h"


BOOL APIENTRY DllMain(HMODULE hModule,
                      DWORD   dwReasonForCall,
                      LPVOID  lpReserved)
{
	UNREFERENCED_PARAMETER(lpReserved);

	if (DLL_PROCESS_ATTACH == dwReasonForCall)
	{
		if (!::DetourIsHelperProcess())
		{
			::DetourRestoreAfterWith();
		}

		::DisableThreadLibraryCalls(hModule);

		::InitializeOriginalFunctionPointers();
	}

	return TRUE;
}

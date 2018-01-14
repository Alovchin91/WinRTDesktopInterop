// HooksManager.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
#include "HooksManager.h"


#define VERIFY_EXPR_RETURN_HRESULT(expr) \
    if (!(expr)) { return hr; }

#define DETOUR_VERIFY(expr) \
    if (NO_ERROR != (expr)) { ::DetourTransactionAbort(); return false; }

#define DETOUR_ATTACH(ppPointer, pDetour) \
    DETOUR_VERIFY(::DetourAttach(&reinterpret_cast<PVOID&>(ppPointer), pDetour))

#define DETOUR_DETACH(ppPointer, pDetour) \
    DETOUR_VERIFY(::DetourDetach(&reinterpret_cast<PVOID&>(ppPointer), pDetour))


static fnRoGetActivationFactory* g_pfnRoGetActivationFactory = nullptr;
static fnRoResolveNamespace* g_pfnRoResolveNamespace = nullptr;


// Forward declarations
fnRoGetActivationFactory HookedRoGetActivationFactory;
fnRoResolveNamespace HookedRoResolveNamespace;


template <typename TFunc>
inline void DetourFindFunctionT(const char* pszModule, const char* pszFunction, TFunc** pfnStorage)
{
	*pfnStorage = static_cast<TFunc*>(::DetourFindFunction(pszModule, pszFunction));
}


void InitializeOriginalFunctionPointers()
{
	DetourFindFunctionT("combase", "RoGetActivationFactory", &g_pfnRoGetActivationFactory);

	DetourFindFunctionT("WinTypes", "RoResolveNamespace", &g_pfnRoResolveNamespace);
}


bool InstallGetActivationFactoryHook()
{
	DETOUR_VERIFY(::DetourTransactionBegin());

	DETOUR_VERIFY(::DetourUpdateThread(::GetCurrentThread()));

	DETOUR_ATTACH(g_pfnRoGetActivationFactory, ::HookedRoGetActivationFactory);

	DETOUR_ATTACH(g_pfnRoResolveNamespace, ::HookedRoResolveNamespace);

	DETOUR_VERIFY(::DetourTransactionCommit());

	return true;
}


bool RemoveGetActivationFactoryHook()
{
	DETOUR_VERIFY(::DetourTransactionBegin());

	DETOUR_VERIFY(::DetourUpdateThread(::GetCurrentThread()));

	DETOUR_DETACH(g_pfnRoGetActivationFactory, ::HookedRoGetActivationFactory);

	DETOUR_DETACH(g_pfnRoResolveNamespace, ::HookedRoResolveNamespace);

	DETOUR_VERIFY(::DetourTransactionCommit());

	return true;
}


std::wstring_view GetCurrentModuleDirectory()
{
	static std::wstring moduleDir;

	if (moduleDir.empty())
	{
		std::wstring moduleFileName(MAX_PATH, L'\0');

		if (0 == ::GetModuleFileNameW(nullptr, moduleFileName.data(), MAX_PATH))
			return std::wstring_view();

		moduleDir = moduleFileName.substr(0, moduleFileName.rfind(L'\\'));
	}

	return moduleDir;
}


HRESULT WINAPI HookedRoGetActivationFactory(HSTRING activatableClassId, REFIID iid, void** factory)
{
	HRESULT hr = g_pfnRoGetActivationFactory(activatableClassId, iid, factory);

	if (REGDB_E_CLASSNOTREG != hr)
		return hr;

	std::wstring modulePath(GetCurrentModuleDirectory());
	VERIFY_EXPR_RETURN_HRESULT(!modulePath.empty());

	winrt::hstring classIdHstring;
	winrt::attach_abi(classIdHstring, activatableClassId);

	const std::wstring_view classIdView = classIdHstring;
	modulePath.append(L"\\").append(classIdView, 0, classIdView.rfind(L'.')).append(L".dll");

	HMODULE hImplModule = ::CoLoadLibrary(modulePath.data(), FALSE);
	VERIFY_EXPR_RETURN_HRESULT(nullptr != hImplModule);

	const pfnDllGetActivationFactory dllGetActivationFactory =
		reinterpret_cast<pfnDllGetActivationFactory>(::GetProcAddress(hImplModule, "DllGetActivationFactory"));
	VERIFY_EXPR_RETURN_HRESULT(nullptr != dllGetActivationFactory);

	winrt::com_ptr<IUnknown> unknownFactory;
	hr = dllGetActivationFactory(activatableClassId, reinterpret_cast<void**>(&unknownFactory));

	if (S_OK == hr)
		hr = unknownFactory->QueryInterface(iid, factory);

	return hr;
}


HRESULT WINAPI HookedRoResolveNamespace(
	const HSTRING name,
	const HSTRING windowsMetaDataDir,
	const DWORD packageGraphDirsCount,
	const HSTRING* packageGraphDirs,
	DWORD* metaDataFilePathsCount,
	HSTRING** metaDataFilePaths,
	DWORD* subNamespacesCount,
	HSTRING** subNamespaces)
{
	HRESULT hr = g_pfnRoResolveNamespace(
		name,
		windowsMetaDataDir,
		packageGraphDirsCount,
		packageGraphDirs,
		metaDataFilePathsCount,
		metaDataFilePaths,
		subNamespacesCount,
		subNamespaces);

	if (RO_E_METADATA_NAME_NOT_FOUND != hr)
		return hr;

	winrt::hstring moduleDir(GetCurrentModuleDirectory());
	VERIFY_EXPR_RETURN_HRESULT(!moduleDir.empty());

	constexpr DWORD applicationDirsCount = 1;
	HSTRING applicationDirs[applicationDirsCount]{ winrt::get_abi(moduleDir) };

	hr = g_pfnRoResolveNamespace(
		name,
		windowsMetaDataDir,
		applicationDirsCount,
		applicationDirs,
		metaDataFilePathsCount,
		metaDataFilePaths,
		subNamespacesCount,
		subNamespaces);

	return hr;
}

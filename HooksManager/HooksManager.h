#pragma once


typedef HRESULT WINAPI fnRoGetActivationFactory(
	HSTRING activatableClassId,
	REFIID iid,
	void** factory);


typedef HRESULT WINAPI fnRoResolveNamespace(
	const HSTRING name,
	const HSTRING windowsMetaDataDir,
	const DWORD packageGraphDirsCount,
	const HSTRING* packageGraphDirs,
	DWORD* metaDataFilePathsCount,
	HSTRING** metaDataFilePaths,
	DWORD* subNamespacesCount,
	HSTRING** subNamespaces);


typedef HRESULT (WINAPI * pfnDllGetActivationFactory)(
	HSTRING activatableClassId,
	void** factory);


void InitializeOriginalFunctionPointers();


extern "C"
{
	__declspec(dllexport) bool InstallGetActivationFactoryHook();
	__declspec(dllexport) bool RemoveGetActivationFactoryHook();
}

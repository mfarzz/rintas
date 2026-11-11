#define COBJMACROS
#include <windows.h>
#include <shobjidl.h>
#include <stdio.h>
#include <wchar.h>

typedef HRESULT (STDAPICALLTYPE *GetClassObjectFn)(REFCLSID, REFIID, void **);
static const CLSID CLSID_Rintas = {0x145e9c63,0x2d18,0x4b2e,{0x90,0x53,0xf4,0x55,0xc1,0x3a,0x8a,0x10}};

int main(void) {
    HMODULE dll = LoadLibraryW(L"RintasExplorer.dll");
    GetClassObjectFn getClass;
    IClassFactory *factory = NULL;
    IExplorerCommand *command = NULL;
    LPWSTR title = NULL;
    LPWSTR icon = NULL;
    EXPCMDSTATE state = ECS_DISABLED;
    HRESULT result;
    if (!dll) { puts("DLL tidak dapat dimuat"); return 1; }
    getClass = (GetClassObjectFn)GetProcAddress(dll, "DllGetClassObject");
    if (!getClass) { puts("DllGetClassObject tidak diekspor"); return 2; }
    result = getClass(&CLSID_Rintas, &IID_IClassFactory, (void **)&factory);
    if (FAILED(result)) { puts("Class factory gagal"); return 3; }
    result = IClassFactory_CreateInstance(factory, NULL, &IID_IExplorerCommand, (void **)&command);
    if (FAILED(result)) { puts("IExplorerCommand gagal"); return 4; }
    result = IExplorerCommand_GetTitle(command, NULL, &title);
    if (FAILED(result) || !title || lstrcmpW(title, L"Bagikan dengan Rintas") != 0) { puts("Judul menu salah"); return 5; }
    CoTaskMemFree(title);
    result = IExplorerCommand_GetState(command, NULL, FALSE, &state);
    if (FAILED(result) || state != ECS_ENABLED) { puts("Status menu salah"); return 6; }
    result = IExplorerCommand_GetIcon(command, NULL, &icon);
    if (FAILED(result) || !icon || (!wcsstr(icon, L"RintasLight.ico") && !wcsstr(icon, L"RintasDark.ico"))) { puts("Ikon menu salah"); return 7; }
    CoTaskMemFree(icon);
    IExplorerCommand_Release(command);
    IClassFactory_Release(factory);
    FreeLibrary(dll);
    puts("Ekstensi Explorer lulus uji COM dasar");
    return 0;
}

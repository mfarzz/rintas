#define COBJMACROS
#include <windows.h>
#include <shobjidl.h>
#include <shellapi.h>

static HINSTANCE g_module;
static LONG g_objects;
static const CLSID CLSID_Rintas = {0x145e9c63,0x2d18,0x4b2e,{0x90,0x53,0xf4,0x55,0xc1,0x3a,0x8a,0x10}};

typedef struct { IExplorerCommand iface; LONG refs; } RintasCommand;
typedef struct { IClassFactory iface; LONG refs; } RintasFactory;

static HRESULT STDMETHODCALLTYPE cmdQuery(IExplorerCommand *self, REFIID iid, void **out);
static ULONG STDMETHODCALLTYPE cmdAddRef(IExplorerCommand *self);
static ULONG STDMETHODCALLTYPE cmdRelease(IExplorerCommand *self);
static HRESULT STDMETHODCALLTYPE cmdTitle(IExplorerCommand *self, IShellItemArray *items, LPWSTR *title);
static HRESULT STDMETHODCALLTYPE cmdIcon(IExplorerCommand *self, IShellItemArray *items, LPWSTR *icon);
static HRESULT STDMETHODCALLTYPE cmdTip(IExplorerCommand *self, IShellItemArray *items, LPWSTR *tip);
static HRESULT STDMETHODCALLTYPE cmdName(IExplorerCommand *self, GUID *name);
static HRESULT STDMETHODCALLTYPE cmdState(IExplorerCommand *self, IShellItemArray *items, BOOL slow, EXPCMDSTATE *state);
static HRESULT STDMETHODCALLTYPE cmdInvoke(IExplorerCommand *self, IShellItemArray *items, IBindCtx *context);
static HRESULT STDMETHODCALLTYPE cmdFlags(IExplorerCommand *self, EXPCMDFLAGS *flags);
static HRESULT STDMETHODCALLTYPE cmdSubcommands(IExplorerCommand *self, IEnumExplorerCommand **commands);

static IExplorerCommandVtbl commandVtbl = {
    cmdQuery, cmdAddRef, cmdRelease, cmdTitle, cmdIcon, cmdTip, cmdName,
    cmdState, cmdInvoke, cmdFlags, cmdSubcommands
};

static HRESULT STDMETHODCALLTYPE factoryQuery(IClassFactory *self, REFIID iid, void **out);
static ULONG STDMETHODCALLTYPE factoryAddRef(IClassFactory *self);
static ULONG STDMETHODCALLTYPE factoryRelease(IClassFactory *self);
static HRESULT STDMETHODCALLTYPE factoryCreate(IClassFactory *self, IUnknown *outer, REFIID iid, void **out);
static HRESULT STDMETHODCALLTYPE factoryLock(IClassFactory *self, BOOL lock);
static IClassFactoryVtbl factoryVtbl = {factoryQuery, factoryAddRef, factoryRelease, factoryCreate, factoryLock};

static HRESULT STDMETHODCALLTYPE cmdQuery(IExplorerCommand *self, REFIID iid, void **out) {
    if (!out) return E_POINTER;
    *out = NULL;
    if (IsEqualIID(iid, &IID_IUnknown) || IsEqualIID(iid, &IID_IExplorerCommand)) {
        *out = self;
        cmdAddRef(self);
        return S_OK;
    }
    return E_NOINTERFACE;
}
static ULONG STDMETHODCALLTYPE cmdAddRef(IExplorerCommand *self) {
    return (ULONG)InterlockedIncrement(&((RintasCommand *)self)->refs);
}
static ULONG STDMETHODCALLTYPE cmdRelease(IExplorerCommand *self) {
    RintasCommand *object = (RintasCommand *)self;
    ULONG count = (ULONG)InterlockedDecrement(&object->refs);
    if (!count) { HeapFree(GetProcessHeap(), 0, object); InterlockedDecrement(&g_objects); }
    return count;
}
static HRESULT STDMETHODCALLTYPE cmdTitle(IExplorerCommand *self, IShellItemArray *items, LPWSTR *title) {
    const WCHAR label[] = L"Bagikan dengan Rintas";
    LPWSTR copy;
    (void)self; (void)items;
    if (!title) return E_POINTER;
    copy = (LPWSTR)CoTaskMemAlloc(sizeof(label));
    if (!copy) return E_OUTOFMEMORY;
    CopyMemory(copy, label, sizeof(label));
    *title = copy;
    return S_OK;
}
static HRESULT STDMETHODCALLTYPE cmdIcon(IExplorerCommand *self, IShellItemArray *items, LPWSTR *icon) {
    WCHAR modulePath[MAX_PATH];
    DWORD length, i;
    DWORD light = 1, bytes = sizeof(light);
    LPWSTR copy;
    (void)self; (void)items;
    if (!icon) return E_POINTER;
    *icon = NULL;
    length = GetModuleFileNameW(g_module, modulePath, MAX_PATH);
    if (!length || length >= MAX_PATH) return E_FAIL;
    for (i = length; i > 0 && modulePath[i - 1] != L'\\'; --i) {}
    modulePath[i] = 0;
    if (i + 23 >= MAX_PATH) return E_FAIL;
    RegGetValueW(HKEY_CURRENT_USER, L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", L"AppsUseLightTheme", RRF_RT_REG_DWORD, NULL, &light, &bytes);
    lstrcatW(modulePath, light ? L"Assets\\RintasLight.ico" : L"Assets\\RintasDark.ico");
    copy = (LPWSTR)CoTaskMemAlloc((lstrlenW(modulePath) + 1) * sizeof(WCHAR));
    if (!copy) return E_OUTOFMEMORY;
    lstrcpyW(copy, modulePath);
    *icon = copy;
    return S_OK;
}
static HRESULT STDMETHODCALLTYPE cmdTip(IExplorerCommand *self, IShellItemArray *items, LPWSTR *tip) {
    (void)self; (void)items;
    if (tip) *tip = NULL;
    return E_NOTIMPL;
}
static HRESULT STDMETHODCALLTYPE cmdName(IExplorerCommand *self, GUID *name) {
    (void)self;
    if (!name) return E_POINTER;
    *name = CLSID_Rintas;
    return S_OK;
}
static HRESULT STDMETHODCALLTYPE cmdState(IExplorerCommand *self, IShellItemArray *items, BOOL slow, EXPCMDSTATE *state) {
    DWORD count = 0;
    (void)self; (void)slow;
    if (!state) return E_POINTER;
    *state = ECS_ENABLED;
    if (items && SUCCEEDED(IShellItemArray_GetCount(items, &count)) && count != 1) *state = ECS_HIDDEN;
    return S_OK;
}
static HRESULT STDMETHODCALLTYPE cmdInvoke(IExplorerCommand *self, IShellItemArray *items, IBindCtx *context) {
    IShellItem *item = NULL;
    LPWSTR file = NULL;
    WCHAR modulePath[MAX_PATH];
    LPWSTR parameters = NULL;
    DWORD length, i;
    HINSTANCE launched;
    HRESULT result;
    (void)self; (void)context;
    if (!items) return E_INVALIDARG;
    result = IShellItemArray_GetItemAt(items, 0, &item);
    if (FAILED(result)) return result;
    result = IShellItem_GetDisplayName(item, SIGDN_FILESYSPATH, &file);
    IShellItem_Release(item);
    if (FAILED(result)) return result;
    length = GetModuleFileNameW(g_module, modulePath, MAX_PATH);
    if (!length || length >= MAX_PATH) { CoTaskMemFree(file); return E_FAIL; }
    for (i = length; i > 0 && modulePath[i - 1] != L'\\'; --i) {}
    modulePath[i] = 0;
    if (i + 10 >= MAX_PATH) { CoTaskMemFree(file); return E_FAIL; }
    lstrcatW(modulePath, L"Rintas.exe");
    parameters = (LPWSTR)HeapAlloc(GetProcessHeap(), 0, (lstrlenW(file) + 13) * sizeof(WCHAR));
    if (!parameters) { CoTaskMemFree(file); return E_OUTOFMEMORY; }
    lstrcpyW(parameters, L"--share \"");
    lstrcatW(parameters, file);
    lstrcatW(parameters, L"\"");
    launched = ShellExecuteW(NULL, L"open", modulePath, parameters, NULL, SW_SHOWNORMAL);
    HeapFree(GetProcessHeap(), 0, parameters);
    CoTaskMemFree(file);
    return ((INT_PTR)launched > 32) ? S_OK : E_FAIL;
}
static HRESULT STDMETHODCALLTYPE cmdFlags(IExplorerCommand *self, EXPCMDFLAGS *flags) {
    (void)self;
    if (!flags) return E_POINTER;
    *flags = ECF_DEFAULT;
    return S_OK;
}
static HRESULT STDMETHODCALLTYPE cmdSubcommands(IExplorerCommand *self, IEnumExplorerCommand **commands) {
    (void)self;
    if (commands) *commands = NULL;
    return E_NOTIMPL;
}

static HRESULT STDMETHODCALLTYPE factoryQuery(IClassFactory *self, REFIID iid, void **out) {
    if (!out) return E_POINTER;
    *out = NULL;
    if (IsEqualIID(iid, &IID_IUnknown) || IsEqualIID(iid, &IID_IClassFactory)) {
        *out = self;
        factoryAddRef(self);
        return S_OK;
    }
    return E_NOINTERFACE;
}
static ULONG STDMETHODCALLTYPE factoryAddRef(IClassFactory *self) {
    return (ULONG)InterlockedIncrement(&((RintasFactory *)self)->refs);
}
static ULONG STDMETHODCALLTYPE factoryRelease(IClassFactory *self) {
    RintasFactory *object = (RintasFactory *)self;
    ULONG count = (ULONG)InterlockedDecrement(&object->refs);
    if (!count) { HeapFree(GetProcessHeap(), 0, object); InterlockedDecrement(&g_objects); }
    return count;
}
static HRESULT STDMETHODCALLTYPE factoryCreate(IClassFactory *self, IUnknown *outer, REFIID iid, void **out) {
    RintasCommand *object;
    HRESULT result;
    (void)self;
    if (outer) return CLASS_E_NOAGGREGATION;
    object = (RintasCommand *)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(*object));
    if (!object) return E_OUTOFMEMORY;
    object->iface.lpVtbl = &commandVtbl;
    object->refs = 1;
    InterlockedIncrement(&g_objects);
    result = cmdQuery(&object->iface, iid, out);
    cmdRelease(&object->iface);
    return result;
}
static HRESULT STDMETHODCALLTYPE factoryLock(IClassFactory *self, BOOL lock) {
    (void)self;
    if (lock) InterlockedIncrement(&g_objects);
    else InterlockedDecrement(&g_objects);
    return S_OK;
}

BOOL WINAPI DllMain(HINSTANCE module, DWORD reason, LPVOID reserved) {
    (void)reserved;
    if (reason == DLL_PROCESS_ATTACH) { g_module = module; DisableThreadLibraryCalls(module); }
    return TRUE;
}
STDAPI DllCanUnloadNow(void) {
    return g_objects == 0 ? S_OK : S_FALSE;
}
STDAPI DllGetClassObject(REFCLSID clsid, REFIID iid, LPVOID *out) {
    RintasFactory *object;
    HRESULT result;
    if (!IsEqualCLSID(clsid, &CLSID_Rintas)) return CLASS_E_CLASSNOTAVAILABLE;
    object = (RintasFactory *)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(*object));
    if (!object) return E_OUTOFMEMORY;
    object->iface.lpVtbl = &factoryVtbl;
    object->refs = 1;
    InterlockedIncrement(&g_objects);
    result = factoryQuery(&object->iface, iid, out);
    factoryRelease(&object->iface);
    return result;
}

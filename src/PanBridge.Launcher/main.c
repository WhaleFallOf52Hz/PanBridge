#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shellapi.h>
#include <stdint.h>
#include <stdio.h>
#include <wchar.h>
#include "runtime.h"

typedef int32_t (__cdecl *hostfxr_startup_fn)(int, const wchar_t **, const wchar_t *, const wchar_t *, const wchar_t *);

// Run the managed entry point in this process, preserving PanBridge.exe as
// the image used by the tray, single-instance events and Windows startup UI.
int WINAPI wWinMain(HINSTANCE instance, HINSTANCE previous, PWSTR command, int show)
{
    (void)instance; (void)previous; (void)command; (void)show;
    wchar_t image[32768], root[32768], runtime[32768], library[32768], app[32768];
    DWORD length = GetModuleFileNameW(NULL, image, 32768);
    if (!length || length >= 32768) return 1;
    wcscpy(root, image);
    wchar_t *separator = wcsrchr(root, L'\\');
    if (!separator) return 1;
    *separator = L'\0';
    if (swprintf(runtime, 32768, L"%ls\\runtime\\dotnet", root) < 0 ||
        swprintf(library, 32768, L"%ls\\host\\fxr\\%ls\\hostfxr.dll", runtime, PANBRIDGE_RUNTIME_VERSION) < 0 ||
        swprintf(app, 32768, L"%ls\\PanBridge.dll", root) < 0) return 1;
    HMODULE host = LoadLibraryExW(library, NULL, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    hostfxr_startup_fn startup = host ? (hostfxr_startup_fn)(void *)GetProcAddress(host, "hostfxr_main_startupinfo") : NULL;
    if (!startup) {
        MessageBoxW(NULL, L"应用运行文件缺失或损坏，请重新安装交云桥。", L"交云桥 · PanBridge", MB_OK | MB_ICONERROR);
        return 1;
    }
    int argc = 0;
    wchar_t **argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (!argv) return 1;
    int32_t result = startup(argc, (const wchar_t **)argv, image, runtime, app);
    LocalFree(argv);
    FreeLibrary(host);
    if ((uint32_t)result >= 0x80008000u && (uint32_t)result <= 0x8000ffffu)
        MessageBoxW(NULL, L"无法启动应用自带的 .NET 运行时，请重新安装交云桥。", L"交云桥 · PanBridge", MB_OK | MB_ICONERROR);
    return result;
}

#pragma once

#ifdef WATERFLOWNATIVE_EXPORTS
#define HOOK_API __declspec(dllexport)
#else
#define HOOK_API __declspec(dllimport)
#endif

extern "C" {
    // Install
    HOOK_API bool InstallHook(void* hNotifyWnd);

    // Uninstall
    HOOK_API void UninstallHook();
}
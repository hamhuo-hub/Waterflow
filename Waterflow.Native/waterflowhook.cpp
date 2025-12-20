#include "pch.h"
#include "waterflowhook.h"
#include <cmath>
// define gesture
#define GESTURE_NONE 0
#define GESTURE_START 1
#define GESTURE_DRAGGING 2
#define GESTURE_END 3
#define GESTURE_CANCEL 4

// interaction status
// GLOBAL VAR
bool g_isRightDown = false;
POINT g_startPoint = { 0,0 };
bool g_isDragging = false;
HWND g_hTargetWnd = NULL;
const int DRAG_THRESHOLD = 10; // dead area
HHOOK g_hHook = NULL;
HMODULE g_hModule = NULL;


// MESSAGE ID
#define WM_WATERFLOW_GESTURE (WM_USER + 1001)

// main function
LRESULT CALLBACK LowLevelMouseProc(int nCode, WPARAM wParam, LPARAM lParam) {
	if (nCode == HC_ACTION) {
		MSLLHOOKSTRUCT* pMouseStruct = (MSLLHOOKSTRUCT*)lParam;

		switch (wParam) {
			// click right button
		case WM_RBUTTONDOWN:
			g_isRightDown = true;
			// get point position
			g_startPoint = pMouseStruct->pt;
			g_isDragging = false;
			break;
			// WATING FOR FURTHER ACTION

		case WM_MOUSEMOVE:
			if (g_isRightDown) {
				if (g_isDragging) {
					PostMessage(g_hTargetWnd, WM_WATERFLOW_GESTURE, GESTURE_DRAGGING,
						MAKELPARAM(pMouseStruct->pt.x, pMouseStruct->pt.y));
					return 1; // eat!
				}
				else { // caculate distance
					long dx = pMouseStruct->pt.x - g_startPoint.x;
					long dy = pMouseStruct->pt.y - g_startPoint.y;
					if (sqrt(dx * dx + dy * dy) > DRAG_THRESHOLD) {
						g_isDragging = true;
						PostMessage(g_hTargetWnd, WM_WATERFLOW_GESTURE, GESTURE_START, 0);
						return 1; // eat!
					}
				}
			}
			break;

		case WM_RBUTTONUP:
			if (g_isDragging) {
				g_isDragging = false;
				g_isRightDown = false;
				// stop interaction
				PostMessage(g_hTargetWnd, WM_WATERFLOW_GESTURE, GESTURE_END, 0);
				return 1; // eat!
			}
			break;
		}
	}
	return CallNextHookEx(g_hHook, nCode, wParam, lParam);
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD  ul_reason_for_call, LPVOID lpReserved)
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
		g_hModule = hModule; 
		break;
	case DLL_THREAD_ATTACH:
	case DLL_THREAD_DETACH:
	case DLL_PROCESS_DETACH:
		break;
	}
	return TRUE;
}

HOOK_API bool InstallHook(void* hNotifyWnd)
{
	if (g_hHook != NULL) return false;
	g_hTargetWnd = (HWND)hNotifyWnd;
	
	g_hHook = SetWindowsHookEx(WH_MOUSE_LL, LowLevelMouseProc, g_hModule, 0);
	return (g_hHook != NULL);
}

HOOK_API void UninstallHook()
{
	if (g_hHook != NULL)
	{
		UnhookWindowsHookEx(g_hHook);
		g_hHook = NULL;
	}
}
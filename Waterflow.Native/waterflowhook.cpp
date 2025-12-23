#include "pch.h"
#include "waterflowhook.h"
#include <cmath>

// Gesture Definition
#define GESTURE_NONE 0
#define GESTURE_START 1
#define GESTURE_DRAGGING 2
#define GESTURE_END 3
#define GESTURE_CANCEL 4

// Thereshold
const ULONGLONG THROTTLE_MS = 10;

const int DRAG_THRESHOLD = 15;

const ULONGLONG MAX_GESTURE_DELAY = 300;

const int DRAG_THRESHOLD_SQ = DRAG_THRESHOLD * DRAG_THRESHOLD;

#define INJECTED_EVENT_SIGNATURE 0xFF998877

bool g_isRightDown = false;
POINT g_startPoint = { 0,0 };
bool g_isGestureActive = false; 
bool g_isValidTiming = true;   // Time window

HWND g_hTargetWnd = NULL;
HHOOK g_hHook = NULL;
HMODULE g_hModule = NULL;

static ULONGLONG g_lastMoveTime = 0;
static ULONGLONG g_rightDownTime = 0;

#define WM_WATERFLOW_GESTURE (WM_USER + 1001)

#define WM_WATERFLOW_SIMULATE_CLICK (WM_USER + 1002)

LRESULT CALLBACK LowLevelMouseProc(int nCode, WPARAM wParam, LPARAM lParam) {
	if (nCode < 0) {
		return CallNextHookEx(g_hHook, nCode, wParam, lParam);
	}
	
	if (nCode == HC_ACTION) {
		MSLLHOOKSTRUCT* pMouseStruct = (MSLLHOOKSTRUCT*)lParam;

		if (pMouseStruct->dwExtraInfo == INJECTED_EVENT_SIGNATURE) {
			return CallNextHookEx(g_hHook, nCode, wParam, lParam);
		}

		switch (wParam) {
		case WM_RBUTTONDOWN:
			g_isRightDown = true;
			g_startPoint = pMouseStruct->pt;
			g_isGestureActive = false;
			g_isValidTiming = true;
			g_rightDownTime = GetTickCount64(); // start time
			g_lastMoveTime = 0;

			// PASS!!
			return 1;

		case WM_MOUSEMOVE:
			if (!g_isRightDown) break;
			if (!g_isValidTiming) break;

			if (g_isGestureActive) {
				// Gesture is already active, update position with throttling
				ULONGLONG currentTime = GetTickCount64();
				if (g_lastMoveTime != 0 && (currentTime - g_lastMoveTime < THROTTLE_MS)) {
					break;
				}
				g_lastMoveTime = currentTime;
				PostMessage(g_hTargetWnd, WM_WATERFLOW_GESTURE, GESTURE_DRAGGING,
					MAKELPARAM(pMouseStruct->pt.x, pMouseStruct->pt.y));
			}
			else {
				// Check if gesture should start
				if ((GetTickCount64() - g_rightDownTime) > MAX_GESTURE_DELAY) {
					g_isValidTiming = false;
					break;
				}

				long dx = pMouseStruct->pt.x - g_startPoint.x;
				long dy = pMouseStruct->pt.y - g_startPoint.y;

				if ((dx * dx + dy * dy) > DRAG_THRESHOLD_SQ) {
					g_isGestureActive = true;
					g_lastMoveTime = GetTickCount64();

					PostMessage(g_hTargetWnd, WM_WATERFLOW_GESTURE, GESTURE_START,
						MAKELPARAM(g_startPoint.x, g_startPoint.y));

					PostMessage(g_hTargetWnd, WM_WATERFLOW_GESTURE, GESTURE_DRAGGING,
						MAKELPARAM(pMouseStruct->pt.x, pMouseStruct->pt.y));
				}
			}
			break;

		case WM_RBUTTONUP:
			if (g_isRightDown) {
				g_isRightDown = false;

				if (g_isGestureActive) {
					g_isGestureActive = false;
					PostMessage(g_hTargetWnd, WM_WATERFLOW_GESTURE, GESTURE_END,
						MAKELPARAM(pMouseStruct->pt.x, pMouseStruct->pt.y));
					return 1;
				}
				else {
					if (g_isValidTiming) {
						PostMessage(g_hTargetWnd, WM_WATERFLOW_SIMULATE_CLICK,
							pMouseStruct->pt.x, pMouseStruct->pt.y);
					}
					return 1;
				}
			}
			break;
		default:
			break;
		}
		return CallNextHookEx(g_hHook, nCode, wParam, lParam);
	}
	
	// Fallback return (should never reach here, but satisfies compiler)
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
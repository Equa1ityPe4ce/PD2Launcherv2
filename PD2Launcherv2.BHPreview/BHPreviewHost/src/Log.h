#pragma once
// Diagnostics go to stderr (and an optional log file); stdout carries the JSON protocol.
typedef struct _EXCEPTION_POINTERS EXCEPTION_POINTERS;

namespace mock {
void Log(const char* fmt, ...);
void SetLogFile(const wchar_t* path);
// Logs an exception's address (as module+offset), registers and likely return addresses.
void LogException(EXCEPTION_POINTERS* info, const char* what);
}

#pragma once
// Diagnostics go to stderr (and an optional log file); stdout carries the JSON protocol.
namespace mock {
void Log(const char* fmt, ...);
void SetLogFile(const wchar_t* path);
}

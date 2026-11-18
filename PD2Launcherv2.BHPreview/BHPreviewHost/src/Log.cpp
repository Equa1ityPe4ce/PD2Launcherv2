#include "Log.h"

#include <windows.h>
#include <cstdarg>
#include <cstdio>
#include <mutex>

namespace mock {
namespace {
std::mutex g_logLock;
FILE* g_logFile = nullptr;
}

void SetLogFile(const wchar_t* path) {
    std::lock_guard<std::mutex> lock(g_logLock);
    if (g_logFile) fclose(g_logFile);
    g_logFile = _wfopen(path, L"w");
}

void Log(const char* fmt, ...) {
    char buf[2048];
    va_list args;
    va_start(args, fmt);
    vsnprintf(buf, sizeof(buf), fmt, args);
    va_end(args);
    std::lock_guard<std::mutex> lock(g_logLock);
    fprintf(stderr, "[bhpreview %5lu] %s\n", GetCurrentThreadId(), buf);
    fflush(stderr);
    if (g_logFile) { fprintf(g_logFile, "%s\n", buf); fflush(g_logFile); }
}

} // namespace mock

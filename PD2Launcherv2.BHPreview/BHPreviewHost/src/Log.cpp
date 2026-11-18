#include "Log.h"

#include <windows.h>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <string>
#include <mutex>

namespace mock {
namespace {
std::mutex g_logLock;
FILE* g_logFile = nullptr;

// "Module+0xRVA" for an address, so crash reports can be matched against a disassembly.
std::string DescribeAddress(DWORD addr) {
    HMODULE m = nullptr;
    char name[MAX_PATH] = "?";
    if (GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                           (LPCSTR)(uintptr_t)addr, &m) && m) {
        GetModuleFileNameA(m, name, sizeof(name));
        const char* base = strrchr(name, '\\');
        char buf[MAX_PATH + 32];
        snprintf(buf, sizeof(buf), "%s+0x%lX", base ? base + 1 : name, addr - (DWORD)(uintptr_t)m);
        return buf;
    }
    char buf[32];
    snprintf(buf, sizeof(buf), "0x%08lX", addr);
    return buf;
}

bool ReadStackDword(const DWORD* at, DWORD* out) {
    __try { *out = *at; return true; } __except (EXCEPTION_EXECUTE_HANDLER) { return false; }
}
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

void LogException(EXCEPTION_POINTERS* info, const char* what) {
    const CONTEXT* c = info->ContextRecord;
    const EXCEPTION_RECORD* e = info->ExceptionRecord;
    Log("%s thread %lu: exception 0x%08lX at %s (access %s 0x%08lX)", what, GetCurrentThreadId(), e->ExceptionCode,
        DescribeAddress((DWORD)(uintptr_t)e->ExceptionAddress).c_str(),
        e->NumberParameters > 1 ? (e->ExceptionInformation[0] ? "write" : "read") : "?",
        e->NumberParameters > 1 ? (DWORD)e->ExceptionInformation[1] : 0);
    Log("  eax=%08lX ebx=%08lX ecx=%08lX edx=%08lX esi=%08lX edi=%08lX ebp=%08lX esp=%08lX",
        c->Eax, c->Ebx, c->Ecx, c->Edx, c->Esi, c->Edi, c->Ebp, c->Esp);
    // Anything on the stack that points into a loaded module is a likely return address.
    const DWORD* sp = (const DWORD*)(uintptr_t)c->Esp;
    int shown = 0;
    for (int i = 0; i < 512 && shown < 16; i++) {
        DWORD v;
        if (!ReadStackDword(sp + i, &v)) break;
        HMODULE m = nullptr;
        if (v > 0x10000 && GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                                              (LPCSTR)(uintptr_t)v, &m) && m) {
            Log("  [esp+0x%03X] %s", i * 4, DescribeAddress(v).c_str());
            shown++;
        }
    }
}

} // namespace mock

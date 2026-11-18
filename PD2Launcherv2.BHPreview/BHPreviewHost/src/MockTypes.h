#pragma once
// Plain descriptions of what BH expects to find inside the game's modules.
// The tables themselves (kFuncSymbols, ...) are generated from BH's D2Ptrs.h.
#include <cstdint>

namespace mock {

// One fake module per DLL name BH resolves with GetModuleHandle. D2SOUND symbols are
// mapped onto D2CMP by the generator, because that is where BH itself looks them up.
enum class Dll : int {
    D2CLIENT, D2COMMON, D2GFX, D2LANG, D2WIN, D2NET, D2GAME, D2LAUNCH, FOG,
    BNCLIENT, STORM, D2CMP, D2MULTI, D2MCPCLIENT, Count
};

enum class CallConv { Cdecl, Stdcall, Fastcall, Thiscall };
enum class PatchKind { Jump, Call, NOP, Push };

struct FuncSymbol {
    Dll dll;
    const char* name;      // e.g. "D2COMMON_GetUnitStat" (BH's own name for it)
    int offset;            // 1.13c: >0 = RVA in the module, <0 = -ordinal
    CallConv conv;
    int argCount;
    int stackBytes;        // bytes the callee pops (0 for cdecl)
    const char* signature; // BH's declaration, for logs
};

struct VarSymbol {
    Dll dll;
    const char* name;
    int offset;
    const char* type;
};

struct AsmSymbol {
    Dll dll;
    const char* name;
    int offset;
};

struct PatchSite {
    Dll dll;
    int offset;
    int length;
    PatchKind kind;
    const char* target;   // BH function the patch redirects to
    const char* file;     // BH source file declaring it
};

inline const char* DllFileName(Dll d) {
    static const char* names[] = {
        "D2CLIENT.dll", "D2COMMON.dll", "D2GFX.dll", "D2LANG.dll", "D2WIN.dll", "D2NET.dll",
        "D2GAME.dll", "D2LAUNCH.dll", "FOG.dll", "BNCLIENT.dll", "STORM.dll", "D2CMP.dll",
        "D2MULTI.dll", "D2MCPCLIENT.dll" };
    return names[(int)d];
}

// Preferred load address for each fake module. Nothing depends on these values (BH only
// ever adds offsets to whatever GetModuleHandle returns, and the loader may relocate the
// fake modules freely); they are just spaced apart to keep logs easy to read.
inline uint32_t DllPreferredBase(Dll d) {
    return 0x50000000u + (uint32_t)d * 0x00400000u;
}

} // namespace mock

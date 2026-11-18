// --selftest: checks the mock game machinery without BH or any game files. It builds the
// fake modules and verifies that BH's lookups (GetModuleHandle, GetProcAddress by ordinal
// and by name) land on the right addresses, that default stubs pop exactly the argument
// bytes BH's declarations say, and that implemented functions are reachable.
#include "SelfTest.h"
#include "Log.h"
#include "MockGame.h"
#include "../generated/D2Exports.g.h"

#include <cstdio>

namespace mock {
namespace {

int g_failures = 0;

void Check(bool ok, const char* what) {
    std::printf("%s  %s\n", ok ? "PASS" : "FAIL", what);
    if (!ok) g_failures++;
}

// Calls `fn` with `stackArgs` zero DWORDs on the stack (plus ecx/edx = 0) and reports how
// far esp moved: 0 means the callee popped exactly what it should.
__declspec(noinline) int CallAndMeasure(void* fn, int stackArgs, uint32_t* result) {
    int drift = 0;
    uint32_t value = 0;
    __asm {
        push esi
        push edi
        mov esi, esp
        mov edi, stackArgs
    push_loop:
        test edi, edi
        jz pushed
        push 0
        dec edi
        jmp push_loop
    pushed:
        xor ecx, ecx
        xor edx, edx
        call fn
        mov value, eax
        mov eax, esp
        sub eax, esi
        mov drift, eax
        mov esp, esi
        pop edi
        pop esi
    }
    *result = value;
    return drift;
}

const FuncSymbol* FindFunc(const char* name) {
    for (auto& f : kFuncSymbols)
        if (strcmp(f.name, name) == 0) return &f;
    return nullptr;
}

} // namespace

int RunSelfTest(const std::wstring& sandbox) {
    static GameData data; // empty: no game files are needed for these checks
    static World world(data);
    GameState state;
    state.difficulty = 1;
    state.functionReturns["D2COMMON_GetRepairCost"] = 1234;
    world.Build(state);
    Ctx().data = &data;
    Ctx().world = &world;

    std::string error;
    Check(Game().Create(sandbox, L"", error), "fake modules build and load");
    if (!error.empty()) std::printf("      %s\n", error.c_str());
    Game().InitializeVariables(state);

    for (int d = 0; d < (int)Dll::Count; d++) {
        uint8_t* base = Game().Resolve((Dll)d, 0);
        if (!base) continue;
        char what[96];
        snprintf(what, sizeof(what), "GetModuleHandle(\"%s\") finds the fake module", DllFileName((Dll)d));
        Check((uint8_t*)GetModuleHandleA(DllFileName((Dll)d)) == base, what);
    }

    // Every ordinal BH imports must resolve through GetProcAddress, as Patch::GetDllOffset does.
    int ordinals = 0, ordinalMisses = 0;
    for (auto& f : kFuncSymbols) {
        if (f.offset >= 0) continue;
        ordinals++;
        FARPROC p = GetProcAddress(GetModuleHandleA(DllFileName(f.dll)), MAKEINTRESOURCEA(-f.offset));
        if ((uint8_t*)p != Game().Symbol(f.name) && (uint8_t*)p != Game().Resolve(f.dll, f.offset)) ordinalMisses++;
    }
    char what[128];
    snprintf(what, sizeof(what), "all %d ordinal imports resolve by GetProcAddress", ordinals);
    Check(ordinalMisses == 0, what);

    uint32_t value = 0;
    Check(CallAndMeasure(Game().Symbol("D2CLIENT_GetDifficulty"), 0, &value) == 0 && (value & 0xFF) == 1,
          "implemented stdcall function returns the configured difficulty");

    const FuncSymbol* repair = FindFunc("D2COMMON_GetRepairCost");
    if (repair) {
        int drift = CallAndMeasure(Game().Symbol(repair->name), repair->stackBytes / 4, &value);
        Check(drift == 0 && value == 1234, "default stub pops a 6-argument stdcall and returns the configured value");
    }
    const FuncSymbol* shop = FindFunc("D2CLIENT_ShopAction");
    if (shop) {
        int drift = CallAndMeasure(Game().Symbol(shop->name), shop->stackBytes / 4, &value);
        Check(drift == 0 && value == 0, "default stub pops a fastcall's stack arguments and returns 0");
    }

    uint8_t* loopSite = Game().Resolve(Dll::D2CLIENT, 0x44230);
    Check(loopSite && CallAndMeasure(loopSite, 0, &value) == 0, "unpatched game-loop site returns cleanly");

    HMODULE pd2 = GetModuleHandleA("ProjectDiablo.dll");
    FARPROC discovery = pd2 ? GetProcAddress(pd2, "_D2Client_GetItemDiscoveryState@12") : nullptr;
    Check(discovery && CallAndMeasure((void*)discovery, 3, &value) == 0 && value == state.itemDiscoveryState,
          "ProjectDiablo.dll item discovery export");

    std::printf("%d failure(s)\n", g_failures);
    return g_failures == 0 ? 0 : 1;
}

} // namespace mock

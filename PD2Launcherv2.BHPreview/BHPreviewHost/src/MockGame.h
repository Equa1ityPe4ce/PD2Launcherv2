#pragma once
// The fake Diablo II process: builds the shell modules, lays BH's expected functions,
// variables and patch sites out inside them, and routes calls to the implementations in
// D2Impl.cpp (or to a logging default that returns a configurable value).
#include "BHHeaders.h"
#include "GameData.h"
#include "MockTypes.h"
#include "World.h"

#include <map>
#include <mutex>
#include <set>
#include <string>
#include <vector>

#if !defined(_M_IX86)
#error BHPreviewHost must be built for 32-bit x86: BH.dll is a 32-bit DLL.
#endif

namespace mock {

struct ImplEntry {
    const char* name;   // BH's name for the function, or an export name for ProjectDiablo.dll
    void* fn;
    CallConv conv;
    int stackBytes;     // derived from the implementation's own signature
};

// Computes how many argument bytes a callee pops, from the function's type.
template <class... A>
constexpr int StdcallStack() { return (0 + ... + (((int)sizeof(A) + 3) & ~3)); }
template <class... A>
constexpr int FastcallStack() {
    constexpr int sizes[] = { 0, (int)sizeof(A)... };
    int regs = 0, total = 0;
    for (size_t i = 1; i < sizeof(sizes) / sizeof(sizes[0]); i++) {
        if (sizes[i] <= 4 && regs < 2) { regs++; continue; }
        total += (sizes[i] + 3) & ~3;
    }
    return total;
}
template <class R, class... A>
ImplEntry MakeImpl(const char* name, R(__stdcall* fn)(A...)) { return { name, (void*)fn, CallConv::Stdcall, StdcallStack<A...>() }; }
template <class R, class... A>
ImplEntry MakeImpl(const char* name, R(__fastcall* fn)(A...)) { return { name, (void*)fn, CallConv::Fastcall, FastcallStack<A...>() }; }
template <class R, class... A>
ImplEntry MakeImpl(const char* name, R(__cdecl* fn)(A...)) { return { name, (void*)fn, CallConv::Cdecl, 0 }; }

// Implementations live in D2Impl.cpp.
const std::vector<ImplEntry>& GameImplementations();
const std::vector<ImplEntry>& ProjectDiabloImplementations();

// Everything the implementations can reach.
struct Context {
    GameData* data = nullptr;
    World* world = nullptr;
    HWND gameWindow = nullptr;
    std::mutex eventsLock;
    std::vector<std::wstring> printedMessages; // PrintGameString etc. during the current request
    std::vector<int> soundsPlayed;
    bool affixTablesQueried = false;           // set when BH's Item module finishes OnGameJoin
};
Context& Ctx();

class MockGame {
public:
    // Writes the shell modules into `sandbox`, loads them and lays everything out. When
    // `installDir` holds the real game DLLs, each fake module also copies the real one's
    // image size and exported ordinals, so a BH build that uses a symbol the generated
    // table does not know still finds something there (a stub that logs the call).
    bool Create(const std::wstring& sandbox, const std::wstring& installDir, std::string& error);

    // Address BH would compute for (dll, offset): base+offset, or the export for -ordinal.
    uint8_t* Resolve(Dll dll, int offset) const;
    // Address of a function or variable by BH's name ("D2CLIENT_PlayerUnit"); null if unknown.
    uint8_t* Symbol(const std::string& name) const;
    const PatchSite* FindPatchSite(Dll dll, int offset) const;

    // Writes the configured initial value of every variable (GameState::variables) and the
    // ones the mock itself owns (data tables, player pointer).
    void InitializeVariables(const GameState& state);

    // Called from the default stub of every function without an implementation.
    uint64_t OnUnimplemented(int index);

private:
    struct Module {
        HMODULE handle = nullptr;
        uint8_t* base = nullptr;
        uint32_t regionEnd = 0;
        std::map<int, uint32_t> ordinalRva; // ordinal -> RVA of its slot
    };
    bool BuildModules(const std::wstring& sandbox, const std::wstring& installDir, std::string& error);
    bool BuildProjectDiablo(const std::wstring& sandbox, std::string& error);
    void InstallFunctions();
    void InstallPatchScaffolding();
    uint8_t* NewTrampoline(size_t size);

    Module modules_[(int)Dll::Count];
    std::map<std::string, uint8_t*> symbols_;
    uint8_t* trampolines_ = nullptr;
    size_t trampolineUsed_ = 0;
    std::mutex lock_;
    std::set<int> reported_;
    std::map<std::string, uint32_t> returnOverrides_;
    std::vector<std::pair<Dll, int>> unknownOrdinals_; // real exports the generated table lacks
};

MockGame& Game();

} // namespace mock

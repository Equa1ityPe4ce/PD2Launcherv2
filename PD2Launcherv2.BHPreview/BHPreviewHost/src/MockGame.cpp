#include "MockGame.h"
#include "Log.h"
#include "PeShell.h"
#include "../generated/D2Exports.g.h"

#include <algorithm>
#include <filesystem>
#include <fstream>

namespace fs = std::filesystem;

namespace mock {
namespace {

constexpr int kAsmIndexBase = 100000;
constexpr int kUnknownOrdinalIndexBase = 200000;
constexpr uint32_t kFuncSlotSize = 16;
constexpr uint32_t kVarSlotSize = 0x100;

// Bytes popped by the game routines BH reaches through ASMPTR addresses. These are
// called from BH's hand-written stubs, so the convention is read off those stubs.
const std::map<std::string, int> kAsmStackBytes = {
    { "D2CLIENT_GetItemDescription", 4 }, // D2CLIENT_GetItemDescription_STUB pushes pItem only
};

uint32_t AlignUp(uint32_t v, uint32_t a) { return (v + a - 1) & ~(a - 1); }

// Reads the image size and exported ordinals of a real game DLL without loading it.
bool ReadRealModule(const fs::path& path, uint32_t& sizeOfImage, std::set<int>& ordinals) {
    std::ifstream in(path, std::ios::binary);
    if (!in) return false;
    std::vector<uint8_t> f((std::istreambuf_iterator<char>(in)), std::istreambuf_iterator<char>());
    if (f.size() < sizeof(IMAGE_DOS_HEADER)) return false;
    auto* dos = reinterpret_cast<IMAGE_DOS_HEADER*>(f.data());
    if (dos->e_magic != IMAGE_DOS_SIGNATURE || (size_t)dos->e_lfanew + sizeof(IMAGE_NT_HEADERS32) > f.size()) return false;
    auto* nt = reinterpret_cast<IMAGE_NT_HEADERS32*>(f.data() + dos->e_lfanew);
    if (nt->Signature != IMAGE_NT_SIGNATURE || nt->OptionalHeader.Magic != IMAGE_NT_OPTIONAL_HDR32_MAGIC) return false;
    sizeOfImage = nt->OptionalHeader.SizeOfImage;
    const IMAGE_DATA_DIRECTORY& ed = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXPORT];
    if (!ed.VirtualAddress) return true;
    auto* sec = IMAGE_FIRST_SECTION(nt);
    auto rvaToOffset = [&](uint32_t rva) -> int64_t {
        for (int i = 0; i < nt->FileHeader.NumberOfSections; i++) {
            uint32_t start = sec[i].VirtualAddress, size = std::max(sec[i].Misc.VirtualSize, sec[i].SizeOfRawData);
            if (rva >= start && rva < start + size) return (int64_t)sec[i].PointerToRawData + (rva - start);
        }
        return -1;
    };
    int64_t dirOff = rvaToOffset(ed.VirtualAddress);
    if (dirOff < 0 || (size_t)dirOff + sizeof(IMAGE_EXPORT_DIRECTORY) > f.size()) return true;
    auto* dir = reinterpret_cast<IMAGE_EXPORT_DIRECTORY*>(f.data() + dirOff);
    int64_t funcsOff = rvaToOffset(dir->AddressOfFunctions);
    if (funcsOff < 0) return true;
    for (DWORD i = 0; i < dir->NumberOfFunctions; i++) {
        if ((size_t)funcsOff + (i + 1) * 4 > f.size()) break;
        if (*reinterpret_cast<uint32_t*>(f.data() + funcsOff + i * 4)) ordinals.insert((int)(dir->Base + i));
    }
    return true;
}

void WriteJump(uint8_t* at, const void* to) {
    at[0] = 0xE9;
    *reinterpret_cast<int32_t*>(at + 1) = (int32_t)((const uint8_t*)to - (at + 5));
}

const char* ConvName(CallConv c) {
    switch (c) {
    case CallConv::Stdcall: return "stdcall";
    case CallConv::Fastcall: return "fastcall";
    case CallConv::Thiscall: return "thiscall";
    default: return "cdecl";
    }
}

} // namespace

extern "C" uint64_t __stdcall MockOnUnimplemented(int index) {
    return Game().OnUnimplemented(index);
}

// Entered from a per-function trampoline that pushed the symbol index. Returns the
// configured value and pops the caller's arguments the way the real function would.
__declspec(naked) void GenericStub() {
    __asm {
        push dword ptr [esp]      // index, as the argument to MockOnUnimplemented
        call MockOnUnimplemented  // eax = return value, edx = argument bytes to pop
        add esp, 4                // drop the index the trampoline pushed
        pop ecx                   // caller's return address
        add esp, edx              // callee-cleanup of the original arguments
        jmp ecx
    }
}

MockGame& Game() {
    static MockGame game;
    return game;
}

Context& Ctx() {
    static Context ctx;
    return ctx;
}

uint8_t* MockGame::Resolve(Dll dll, int offset) const {
    const Module& m = modules_[(int)dll];
    if (!m.base) return nullptr;
    if (offset >= 0) return m.base + offset;
    auto it = m.ordinalRva.find(-offset);
    return it == m.ordinalRva.end() ? nullptr : m.base + it->second;
}

uint8_t* MockGame::Symbol(const std::string& name) const {
    auto it = symbols_.find(name);
    return it == symbols_.end() ? nullptr : it->second;
}

const PatchSite* MockGame::FindPatchSite(Dll dll, int offset) const {
    for (const PatchSite& p : kPatchSites)
        if (p.dll == dll && p.offset == offset) return &p;
    return nullptr;
}

uint8_t* MockGame::NewTrampoline(size_t size) {
    constexpr size_t kTrampolineArena = 0x100000;
    if (!trampolines_) {
        trampolines_ = (uint8_t*)VirtualAlloc(nullptr, kTrampolineArena, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
    }
    if (!trampolines_ || trampolineUsed_ + size > kTrampolineArena) return nullptr;
    uint8_t* t = trampolines_ + trampolineUsed_;
    trampolineUsed_ += (size + 15) & ~size_t(15);
    return t;
}

bool MockGame::BuildModules(const std::wstring& sandbox, const std::wstring& installDir, std::string& error) {
    struct Need { int maxOffset = 0; std::set<int> funcOrdinals, varOrdinals, realOrdinals; uint32_t realSize = 0; bool used = false; };
    Need need[(int)Dll::Count];
    auto note = [&](Dll d, int off, bool isVar) {
        Need& n = need[(int)d];
        n.used = true;
        if (off > 0) n.maxOffset = std::max(n.maxOffset, off);
        else if (isVar) n.varOrdinals.insert(-off);
        else n.funcOrdinals.insert(-off);
    };
    for (auto& f : kFuncSymbols) note(f.dll, f.offset, false);
    for (auto& v : kVarSymbols) note(v.dll, v.offset, true);
    for (auto& a : kAsmSymbols) note(a.dll, a.offset, false);
    for (auto& p : kPatchSites) note(p.dll, p.offset + p.length + 8, false);

    for (int i = 0; i < (int)Dll::Count; i++) {
        if (!need[i].used) continue;
        if (!installDir.empty() && ReadRealModule(fs::path(installDir) / DllFileName((Dll)i), need[i].realSize, need[i].realOrdinals))
            Log("real %s: image 0x%X bytes, %zu exports", DllFileName((Dll)i), need[i].realSize, need[i].realOrdinals.size());
    }

    for (int i = 0; i < (int)Dll::Count; i++) {
        if (!need[i].used) continue;
        Module& m = modules_[i];
        ShellSpec spec;
        spec.moduleName = DllFileName((Dll)i);
        spec.preferredBase = DllPreferredBase((Dll)i);
        uint32_t end = std::max<uint32_t>(need[i].maxOffset + 0x100, need[i].realSize);
        uint32_t next = AlignUp(std::max<uint32_t>(end, kShellRegionRva), 0x100);
        for (int ord : need[i].funcOrdinals) { m.ordinalRva[ord] = next; next += kFuncSlotSize; }
        for (int ord : need[i].varOrdinals) if (!m.ordinalRva.count(ord)) { m.ordinalRva[ord] = next; next += kVarSlotSize; }
        for (int ord : need[i].realOrdinals) {
            if (m.ordinalRva.count(ord)) continue;
            m.ordinalRva[ord] = next;
            next += kVarSlotSize; // unknown use: room for data, and code reads as a stub below
            unknownOrdinals_.emplace_back((Dll)i, ord);
        }
        spec.ordinalExports = m.ordinalRva;
        spec.regionSize = next - kShellRegionRva;
        m.regionEnd = next;

        fs::path file = fs::path(sandbox) / spec.moduleName;
        if (!WriteShellDll(file.wstring(), spec, error)) return false;
        m.handle = LoadLibraryExW(file.c_str(), nullptr, LOAD_WITH_ALTERED_SEARCH_PATH);
        if (!m.handle) {
            error = "could not load generated module " + spec.moduleName + " (error " + std::to_string(GetLastError()) + ")";
            return false;
        }
        m.base = (uint8_t*)m.handle;
        Log("fake %s at %p (%u KB, %zu ordinals)", spec.moduleName.c_str(), m.base, spec.regionSize / 1024, m.ordinalRva.size());
    }
    return true;
}

bool MockGame::BuildProjectDiablo(const std::wstring& sandbox, std::string& error) {
    // BH looks these up by name in ProjectDiablo.dll (BH::CheckForPD2) and calls them
    // without further checks once found, so the mock provides all of them.
    const auto& impls = ProjectDiabloImplementations();
    ShellSpec spec;
    spec.moduleName = "ProjectDiablo.dll";
    spec.preferredBase = 0x4F000000;
    uint32_t next = kShellRegionRva;
    for (auto& impl : impls) { spec.namedExports[impl.name] = next; next += kFuncSlotSize; }
    spec.regionSize = AlignUp(next - kShellRegionRva, 0x1000);
    fs::path file = fs::path(sandbox) / spec.moduleName;
    if (!WriteShellDll(file.wstring(), spec, error)) return false;
    HMODULE h = LoadLibraryExW(file.c_str(), nullptr, LOAD_WITH_ALTERED_SEARCH_PATH);
    if (!h) { error = "could not load generated ProjectDiablo.dll"; return false; }
    for (auto& impl : impls) {
        uint8_t* slot = (uint8_t*)GetProcAddress(h, impl.name);
        if (slot) WriteJump(slot, impl.fn);
    }
    return true;
}

void MockGame::InstallFunctions() {
    std::map<std::string, const ImplEntry*> impls;
    for (auto& e : GameImplementations()) impls[e.name] = &e;

    // Byte ranges already spoken for, so aliases and neighbours never overwrite each other.
    std::set<uint8_t*> claimed;
    auto isFree = [&](uint8_t* at, int len) {
        for (int i = 0; i < len; i++) if (claimed.count(at + i)) return false;
        return true;
    };
    auto claim = [&](uint8_t* at, int len) { for (int i = 0; i < len; i++) claimed.insert(at + i); };

    for (auto& v : kVarSymbols) {
        uint8_t* at = Resolve(v.dll, v.offset);
        symbols_[v.name] = at;
        claim(at, 4);
    }
    for (auto& p : kPatchSites) claim(Resolve(p.dll, p.offset), p.length + 4); // + continuation code

    int implemented = 0;
    for (int i = 0; i < (int)(sizeof(kFuncSymbols) / sizeof(kFuncSymbols[0])); i++) {
        const FuncSymbol& f = kFuncSymbols[i];
        uint8_t* slot = Resolve(f.dll, f.offset);
        if (!slot) continue;
        if (symbols_.count(f.name) == 0) symbols_[f.name] = slot;
        if (!isFree(slot, 5)) {
            // Same address as an earlier symbol (BH declares a few twice under two names).
            continue;
        }
        claim(slot, 5);
        auto it = impls.find(f.name);
        if (it != impls.end()) {
            const ImplEntry& impl = *it->second;
            if (impl.conv == f.conv && impl.stackBytes == f.stackBytes) {
                WriteJump(slot, impl.fn);
                implemented++;
                continue;
            }
            Log("ERROR: implementation of %s is %s/%d bytes but BH declares %s/%d (%s); using the default stub",
                f.name, ConvName(impl.conv), impl.stackBytes, ConvName(f.conv), f.stackBytes, f.signature);
        }
        uint8_t* t = NewTrampoline(10);
        t[0] = 0x68; // push imm32
        *reinterpret_cast<int32_t*>(t + 1) = i;
        WriteJump(t + 5, (void*)&GenericStub);
        WriteJump(slot, t);
    }
    for (int j = 0; j < (int)(sizeof(kAsmSymbols) / sizeof(kAsmSymbols[0])); j++) {
        const AsmSymbol& a = kAsmSymbols[j];
        uint8_t* at = Resolve(a.dll, a.offset);
        if (!at) continue;
        if (symbols_.count(a.name) == 0) symbols_[a.name] = at;
        if (!isFree(at, 5)) continue;
        claim(at, 5);
        uint8_t* t = NewTrampoline(10);
        t[0] = 0x68;
        *reinterpret_cast<int32_t*>(t + 1) = kAsmIndexBase + j;
        WriteJump(t + 5, (void*)&GenericStub);
        WriteJump(at, t);
    }
    for (int k = 0; k < (int)unknownOrdinals_.size(); k++) {
        uint8_t* at = Resolve(unknownOrdinals_[k].first, -unknownOrdinals_[k].second);
        uint8_t* t = NewTrampoline(10);
        t[0] = 0x68;
        *reinterpret_cast<int32_t*>(t + 1) = kUnknownOrdinalIndexBase + k;
        WriteJump(t + 5, (void*)&GenericStub);
        WriteJump(at, t);
    }
    Log("%d of %zu game functions implemented; the rest return their configured default",
        implemented, sizeof(kFuncSymbols) / sizeof(kFuncSymbols[0]));
}

void MockGame::InstallPatchScaffolding() {
    // Before BH installs a patch the bytes there are the "original game code". The mock's
    // original code is just `ret`, and the instruction after the patched bytes is a `ret`
    // as well, so calling a patch site from the driver runs BH's handler and comes back.
    for (auto& p : kPatchSites) {
        uint8_t* at = Resolve(p.dll, p.offset);
        memset(at, 0xC3, p.length + 1);
    }
    // GameLoop_Interception re-creates the instructions it overwrote (sub esp, 0x20 ...),
    // so the code after the patch has to undo that before returning.
    if (const PatchSite* loop = FindPatchSite(Dll::D2CLIENT, 0x44230)) {
        uint8_t* after = Resolve(loop->dll, loop->offset) + loop->length;
        const uint8_t addEsp20Ret[] = { 0x83, 0xC4, 0x20, 0xC3 }; // add esp, 0x20 ; ret
        memcpy(after, addEsp20Ret, sizeof(addEsp20Ret));
    }
}

void MockGame::InitializeVariables(const GameState& state) {
    returnOverrides_ = state.functionReturns;
    auto set = [&](const char* name, uint32_t value) {
        if (uint8_t* at = Symbol(name)) *reinterpret_cast<uint32_t*>(at) = value;
    };
    // Variables the mock owns.
    set("D2COMMON_sgptDataTable", (uint32_t)Ctx().data->DataTable());
    set("D2CLIENT_PlayerUnit", (uint32_t)Ctx().world->Player());
    // Everything else starts at zero unless the request says otherwise.
    for (auto& [name, value] : state.variables) {
        if (Symbol(name)) set(name.c_str(), value);
        else Log("unknown variable %s in request", name.c_str());
    }
}

uint64_t MockGame::OnUnimplemented(int index) {
    const char* name;
    int stackBytes;
    char unknownName[64];
    if (index >= kUnknownOrdinalIndexBase) {
        auto& u = unknownOrdinals_[index - kUnknownOrdinalIndexBase];
        snprintf(unknownName, sizeof(unknownName), "%s ordinal %d (not in the generated table)", DllFileName(u.first), u.second);
        name = unknownName;
        stackBytes = 0; // unknown: a stdcall function called here will unbalance the stack
    } else if (index >= kAsmIndexBase) {
        const AsmSymbol& a = kAsmSymbols[index - kAsmIndexBase];
        name = a.name;
        auto it = kAsmStackBytes.find(a.name);
        stackBytes = it == kAsmStackBytes.end() ? 0 : it->second;
    } else {
        const FuncSymbol& f = kFuncSymbols[index];
        name = f.name;
        stackBytes = f.stackBytes;
    }
    uint32_t value = 0;
    {
        std::lock_guard<std::mutex> lock(lock_);
        if (reported_.insert(index).second) Log("default stub: %s", name);
        auto it = returnOverrides_.find(name);
        if (it != returnOverrides_.end()) value = it->second;
    }
    return ((uint64_t)(uint32_t)stackBytes << 32) | value;
}

bool MockGame::Create(const std::wstring& sandbox, const std::wstring& installDir, std::string& error) {
    if (!BuildModules(sandbox, installDir, error)) return false;
    if (!BuildProjectDiablo(sandbox, error)) return false;
    InstallPatchScaffolding();
    InstallFunctions();
    return true;
}

} // namespace mock

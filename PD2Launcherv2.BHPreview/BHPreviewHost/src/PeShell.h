#pragma once
// Writes "shell" DLLs: images with no code and no entry point, made of one large
// zero-filled read/write/execute section plus an export table. The mock game loads
// them under the real module names (D2CLIENT.dll, D2COMMON.dll, ...) so that BH's
// GetModuleHandle/GetProcAddress lookups succeed, then fills in code and data at the
// exact offsets BH uses.
#include <cstdint>
#include <map>
#include <string>

namespace mock {

struct ShellSpec {
    std::string moduleName;                     // name recorded in the export directory
    uint32_t preferredBase = 0;
    uint32_t regionSize = 0;                    // bytes available from RVA 0x1000 up
    std::map<int, uint32_t> ordinalExports;     // ordinal -> RVA
    std::map<std::string, uint32_t> namedExports; // name -> RVA
};

// RVA where the writable region starts. Every offset BH uses is >= this.
constexpr uint32_t kShellRegionRva = 0x1000;

bool WriteShellDll(const std::wstring& path, const ShellSpec& spec, std::string& error);

} // namespace mock

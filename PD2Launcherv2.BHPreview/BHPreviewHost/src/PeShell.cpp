#include "PeShell.h"

#include <windows.h>
#include <algorithm>
#include <climits>
#include <filesystem>
#include <fstream>
#include <vector>

namespace mock {
namespace {

constexpr uint32_t kFileAlign = 0x200;
constexpr uint32_t kSectionAlign = 0x1000;
constexpr uint32_t kHeadersSize = 0x400;

uint32_t AlignUp(uint32_t v, uint32_t a) { return (v + a - 1) & ~(a - 1); }

template <typename T>
void Put(std::vector<uint8_t>& buf, size_t at, const T& value) {
    if (buf.size() < at + sizeof(T)) buf.resize(at + sizeof(T));
    memcpy(buf.data() + at, &value, sizeof(T));
}

void PutBytes(std::vector<uint8_t>& buf, size_t at, const void* data, size_t len) {
    if (buf.size() < at + len) buf.resize(at + len);
    memcpy(buf.data() + at, data, len);
}

} // namespace

bool WriteShellDll(const std::wstring& path, const ShellSpec& spec, std::string& error) {
    const uint32_t regionVSize = AlignUp(spec.regionSize, kSectionAlign);
    const uint32_t edataRva = kShellRegionRva + regionVSize;

    // ---- export directory + relocation block, laid out inside the .edata section ----
    // Named exports get ordinals after the highest explicit ordinal.
    int minOrd = INT_MAX, maxOrd = 0;
    for (auto& [ord, rva] : spec.ordinalExports) { minOrd = std::min(minOrd, ord); maxOrd = std::max(maxOrd, ord); }
    if (minOrd == INT_MAX) { minOrd = 1; maxOrd = 0; }
    std::map<int, uint32_t> functions = spec.ordinalExports;
    std::vector<std::pair<std::string, int>> names; // name -> ordinal
    for (auto& [name, rva] : spec.namedExports) {
        int ord = ++maxOrd;
        functions[ord] = rva;
        names.emplace_back(name, ord);
    }
    std::sort(names.begin(), names.end()); // the loader binary-searches AddressOfNames
    const uint32_t numFunctions = functions.empty() ? 0 : (uint32_t)(maxOrd - minOrd + 1);

    std::vector<uint8_t> ed; // section contents; offsets below are relative to edataRva
    size_t off = sizeof(IMAGE_EXPORT_DIRECTORY);
    const size_t funcsOff = off;     off += numFunctions * 4;
    const size_t namesOff = off;     off += names.size() * 4;
    const size_t ordsOff = off;      off += names.size() * 2;
    const size_t modNameOff = off;   off += spec.moduleName.size() + 1;
    std::vector<size_t> nameStrOffs;
    for (auto& n : names) { nameStrOffs.push_back(off); off += n.first.size() + 1; }
    off = AlignUp((uint32_t)off, 4);
    const size_t relocOff = off;     off += 12; // one block: header (8) + one ABSOLUTE entry (2) + pad (2)
    ed.resize(off);

    IMAGE_EXPORT_DIRECTORY dir = {};
    dir.Name = edataRva + (DWORD)modNameOff;
    dir.Base = (DWORD)minOrd;
    dir.NumberOfFunctions = numFunctions;
    dir.NumberOfNames = (DWORD)names.size();
    dir.AddressOfFunctions = edataRva + (DWORD)funcsOff;
    dir.AddressOfNames = edataRva + (DWORD)namesOff;
    dir.AddressOfNameOrdinals = edataRva + (DWORD)ordsOff;
    Put(ed, 0, dir);
    for (auto& [ord, rva] : functions) Put<uint32_t>(ed, funcsOff + (ord - minOrd) * 4, rva);
    for (size_t i = 0; i < names.size(); i++) {
        Put<uint32_t>(ed, namesOff + i * 4, edataRva + (uint32_t)nameStrOffs[i]);
        Put<uint16_t>(ed, ordsOff + i * 2, (uint16_t)(names[i].second - minOrd));
        PutBytes(ed, nameStrOffs[i], names[i].first.c_str(), names[i].first.size() + 1);
    }
    PutBytes(ed, modNameOff, spec.moduleName.c_str(), spec.moduleName.size() + 1);
    // A no-op relocation block keeps the image relocatable if its preferred base is taken.
    Put<uint32_t>(ed, relocOff, kShellRegionRva); // VirtualAddress
    Put<uint32_t>(ed, relocOff + 4, 12);          // SizeOfBlock
    Put<uint16_t>(ed, relocOff + 8, 0);           // IMAGE_REL_BASED_ABSOLUTE (padding entry)
    Put<uint16_t>(ed, relocOff + 10, 0);

    const uint32_t edataRawSize = AlignUp((uint32_t)ed.size(), kFileAlign);
    const uint32_t edataVSize = AlignUp((uint32_t)ed.size(), kSectionAlign);
    const uint32_t sizeOfImage = edataRva + edataVSize;

    // ---- headers ----
    std::vector<uint8_t> file(kHeadersSize, 0);
    IMAGE_DOS_HEADER dos = {};
    dos.e_magic = IMAGE_DOS_SIGNATURE;
    dos.e_lfanew = 0x40;
    Put(file, 0, dos);

    IMAGE_NT_HEADERS32 nt = {};
    nt.Signature = IMAGE_NT_SIGNATURE;
    nt.FileHeader.Machine = IMAGE_FILE_MACHINE_I386;
    nt.FileHeader.NumberOfSections = 2;
    nt.FileHeader.SizeOfOptionalHeader = sizeof(IMAGE_OPTIONAL_HEADER32);
    nt.FileHeader.Characteristics = IMAGE_FILE_EXECUTABLE_IMAGE | IMAGE_FILE_32BIT_MACHINE | IMAGE_FILE_DLL;
    auto& oh = nt.OptionalHeader;
    oh.Magic = IMAGE_NT_OPTIONAL_HDR32_MAGIC;
    oh.MajorLinkerVersion = 14;
    oh.SizeOfInitializedData = edataRawSize;
    oh.SizeOfUninitializedData = regionVSize;
    oh.AddressOfEntryPoint = 0; // no DllMain
    oh.BaseOfData = edataRva;
    oh.ImageBase = spec.preferredBase;
    oh.SectionAlignment = kSectionAlign;
    oh.FileAlignment = kFileAlign;
    oh.MajorOperatingSystemVersion = 6;
    oh.MajorSubsystemVersion = 6;
    oh.SizeOfImage = sizeOfImage;
    oh.SizeOfHeaders = kHeadersSize;
    oh.Subsystem = IMAGE_SUBSYSTEM_WINDOWS_GUI;
    oh.DllCharacteristics = 0; // no ASLR request, no NX: the region holds generated code
    oh.SizeOfStackReserve = 0x100000;
    oh.SizeOfStackCommit = 0x1000;
    oh.SizeOfHeapReserve = 0x100000;
    oh.SizeOfHeapCommit = 0x1000;
    oh.NumberOfRvaAndSizes = IMAGE_NUMBEROF_DIRECTORY_ENTRIES;
    // The export directory's size must cover the whole table: the loader treats any export
    // RVA that falls inside it as a forwarder string.
    oh.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXPORT] = { edataRva, (DWORD)relocOff };
    oh.DataDirectory[IMAGE_DIRECTORY_ENTRY_BASERELOC] = { edataRva + (DWORD)relocOff, 12 };
    Put(file, dos.e_lfanew, nt);

    IMAGE_SECTION_HEADER sections[2] = {};
    memcpy(sections[0].Name, ".game", 5);
    sections[0].Misc.VirtualSize = regionVSize;
    sections[0].VirtualAddress = kShellRegionRva;
    sections[0].SizeOfRawData = 0;
    sections[0].PointerToRawData = 0;
    sections[0].Characteristics = IMAGE_SCN_CNT_UNINITIALIZED_DATA | IMAGE_SCN_MEM_READ |
                                  IMAGE_SCN_MEM_WRITE | IMAGE_SCN_MEM_EXECUTE;
    memcpy(sections[1].Name, ".edata", 6);
    sections[1].Misc.VirtualSize = (DWORD)ed.size();
    sections[1].VirtualAddress = edataRva;
    sections[1].SizeOfRawData = edataRawSize;
    sections[1].PointerToRawData = kHeadersSize;
    sections[1].Characteristics = IMAGE_SCN_CNT_INITIALIZED_DATA | IMAGE_SCN_MEM_READ;
    PutBytes(file, dos.e_lfanew + sizeof(IMAGE_NT_HEADERS32), sections, sizeof(sections));

    ed.resize(edataRawSize, 0);
    file.insert(file.end(), ed.begin(), ed.end());

    std::ofstream out(std::filesystem::path(path), std::ios::binary | std::ios::trunc);
    if (!out) { error = "cannot write shell module"; return false; }
    out.write((const char*)file.data(), (std::streamsize)file.size());
    return out.good();
}

} // namespace mock

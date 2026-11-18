#include "GameFiles.h"
#include "Log.h"

#include <filesystem>
#include <fstream>

namespace fs = std::filesystem;

namespace mock {
namespace {

// Storm.dll 1.13c exports these by ordinal only.
constexpr int kOrdCloseArchive = 252;
constexpr int kOrdCloseFile = 253;
constexpr int kOrdGetFileSize = 265;
constexpr int kOrdOpenArchive = 266;
constexpr int kOrdOpenFileEx = 268;
constexpr int kOrdReadFile = 269;

// Highest priority first: PD2's own data overrides the LoD patch, which overrides the base game.
const wchar_t* kArchives[] = {
    L"pd2data.mpq", L"patch_d2.mpq", L"d2exp.mpq", L"d2data.mpq", L"d2char.mpq",
};

// PD2's Storm.dll imports three version.dll functions through PD2_EXT.dll, a version.dll
// proxy whose DllMain refuses to run outside the game (ERROR_DLL_INIT_FAILED). The sandbox
// copy of Storm imports them from version.dll itself; the two names are the same length.
// Returns false only if the file could not be read or written.
bool CopyStormWithoutPd2Ext(const fs::path& from, const fs::path& to) {
    std::ifstream in(from, std::ios::binary);
    if (!in) return false;
    std::vector<uint8_t> f((std::istreambuf_iterator<char>(in)), std::istreambuf_iterator<char>());
    in.close();
    if (f.size() >= sizeof(IMAGE_DOS_HEADER)) {
        auto* dos = reinterpret_cast<IMAGE_DOS_HEADER*>(f.data());
        if (dos->e_magic == IMAGE_DOS_SIGNATURE && (size_t)dos->e_lfanew + sizeof(IMAGE_NT_HEADERS32) <= f.size()) {
            auto* nt = reinterpret_cast<IMAGE_NT_HEADERS32*>(f.data() + dos->e_lfanew);
            auto* sec = IMAGE_FIRST_SECTION(nt);
            auto rvaToOffset = [&](uint32_t rva) -> int64_t {
                for (int i = 0; i < nt->FileHeader.NumberOfSections; i++) {
                    uint32_t start = sec[i].VirtualAddress, size = sec[i].SizeOfRawData;
                    if (rva >= start && rva < start + size) return (int64_t)sec[i].PointerToRawData + (rva - start);
                }
                return -1;
            };
            const IMAGE_DATA_DIRECTORY& id = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
            int64_t off = id.VirtualAddress ? rvaToOffset(id.VirtualAddress) : -1;
            while (off >= 0 && (size_t)off + sizeof(IMAGE_IMPORT_DESCRIPTOR) <= f.size()) {
                auto* desc = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR*>(f.data() + off);
                if (!desc->Name) break;
                int64_t nameOff = rvaToOffset(desc->Name);
                if (nameOff >= 0 && (size_t)nameOff + 12 <= f.size() &&
                    _stricmp(reinterpret_cast<char*>(f.data() + nameOff), "PD2_EXT.dll") == 0) {
                    memcpy(f.data() + nameOff, "version.dll", 12);
                    Log("Storm.dll: importing version.dll in place of PD2_EXT.dll");
                }
                off += sizeof(IMAGE_IMPORT_DESCRIPTOR);
            }
        }
    }
    std::ofstream out(to, std::ios::binary | std::ios::trunc);
    out.write(reinterpret_cast<const char*>(f.data()), f.size());
    return (bool)out;
}

} // namespace

GameFiles::~GameFiles() {
    if (closeArchive_) {
        for (HANDLE h : archives_) closeArchive_(h);
    }
    // Storm stays loaded: unloading it while BH may still hold strings from it buys nothing.
}

bool GameFiles::Open(const std::wstring& installDir, const std::wstring& dataDir, const std::wstring& sandboxDir,
                     std::string& error) {
    dataDir_ = dataDir;
    if (installDir.empty()) {
        if (dataDir_.empty()) { error = "no install folder or data folder given"; return false; }
        return true;
    }

    // Prefer StormLib.dll, which PD2 ships in the install folder: it has the same stdcall
    // API, exported by name. PD2's own Storm.dll loads (see CopyStormWithoutPd2Ext) but its
    // 1.13c-ordinal file lookups find nothing outside the game.
    DWORD openFlags = 0;
    fs::path stormLib = fs::path(installDir) / L"StormLib.dll";
    if (fs::exists(stormLib) && (storm_ = LoadLibraryExW(stormLib.c_str(), nullptr, LOAD_WITH_ALTERED_SEARCH_PATH))) {
        openArchive_ = (SFileOpenArchive_t)GetProcAddress(storm_, "SFileOpenArchive");
        closeArchive_ = (SFileCloseArchive_t)GetProcAddress(storm_, "SFileCloseArchive");
        openFile_ = (SFileOpenFileEx_t)GetProcAddress(storm_, "SFileOpenFileEx");
        closeFile_ = (SFileCloseFile_t)GetProcAddress(storm_, "SFileCloseFile");
        fileSize_ = (SFileGetFileSize_t)GetProcAddress(storm_, "SFileGetFileSize");
        readFile_ = (SFileReadFile_t)GetProcAddress(storm_, "SFileReadFile");
        openFlags = 0x100; // STREAM_FLAG_READ_ONLY
        Log("reading archives with %s", stormLib.string().c_str());
    } else {
        // The real Storm.dll, from a copy in the sandbox. No fake module uses the name STORM
        // (BH resolves no Storm symbols), so it can be loaded without clashing with the mock.
        fs::path stormPath = fs::path(installDir) / L"Storm.dll";
        if (!fs::exists(stormPath)) stormPath = fs::path(installDir).parent_path() / L"Storm.dll";
        fs::path stormCopy = fs::path(sandboxDir) / L"Storm.dll";
        if (!CopyStormWithoutPd2Ext(stormPath, stormCopy)) {
            error = "could not copy Storm.dll from the install folder";
            return false;
        }
        storm_ = LoadLibraryExW(stormCopy.c_str(), nullptr, LOAD_WITH_ALTERED_SEARCH_PATH);
        if (!storm_) {
            if (!dataDir_.empty()) { Log("Storm.dll not loaded; using the data folder only"); return true; }
            error = "could not load Storm.dll from the install folder (error " + std::to_string(GetLastError()) + ")";
            return false;
        }
        openArchive_ = (SFileOpenArchive_t)GetProcAddress(storm_, MAKEINTRESOURCEA(kOrdOpenArchive));
        closeArchive_ = (SFileCloseArchive_t)GetProcAddress(storm_, MAKEINTRESOURCEA(kOrdCloseArchive));
        openFile_ = (SFileOpenFileEx_t)GetProcAddress(storm_, MAKEINTRESOURCEA(kOrdOpenFileEx));
        closeFile_ = (SFileCloseFile_t)GetProcAddress(storm_, MAKEINTRESOURCEA(kOrdCloseFile));
        fileSize_ = (SFileGetFileSize_t)GetProcAddress(storm_, MAKEINTRESOURCEA(kOrdGetFileSize));
        readFile_ = (SFileReadFile_t)GetProcAddress(storm_, MAKEINTRESOURCEA(kOrdReadFile));
        Log("reading archives with %s", stormPath.string().c_str());
    }
    if (!openArchive_ || !closeArchive_ || !openFile_ || !closeFile_ || !fileSize_ || !readFile_) {
        error = "the MPQ library is missing an expected export";
        return false;
    }

    DWORD priority = 0x2000;
    for (const wchar_t* name : kArchives) {
        for (const fs::path& dir : { fs::path(installDir), fs::path(installDir).parent_path() }) {
            fs::path p = dir / name;
            if (!fs::exists(p)) continue;
            HANDLE h = nullptr;
            std::string narrow = p.string();
            if (openArchive_(narrow.c_str(), priority--, openFlags, &h) && h) {
                archives_.push_back(h);
                archiveNames_.push_back(p.wstring());
                Log("opened archive %s", narrow.c_str());
            } else {
                Log("could not open archive %s", narrow.c_str());
            }
            break;
        }
    }
    if (archives_.empty() && dataDir_.empty()) {
        error = "no game archives found in the install folder";
        return false;
    }
    return true;
}

bool GameFiles::Read(const std::string& path, std::vector<uint8_t>& out) const {
    if (!dataDir_.empty()) {
        std::string rel = path;
        for (char& c : rel) if (c == '\\') c = '/';
        fs::path p = fs::path(dataDir_) / fs::path(rel);
        std::ifstream in(p, std::ios::binary);
        if (in) {
            out.assign(std::istreambuf_iterator<char>(in), std::istreambuf_iterator<char>());
            return true;
        }
    }
    for (HANDLE archive : archives_) {
        HANDLE file = nullptr;
        if (!openFile_(archive, path.c_str(), 0, &file) || !file) continue;
        DWORD size = fileSize_(file, nullptr);
        out.resize(size);
        DWORD read = 0;
        BOOL ok = readFile_(file, out.data(), size, &read, nullptr);
        closeFile_(file);
        if (ok && read == size) return true;
    }
    return false;
}

} // namespace mock

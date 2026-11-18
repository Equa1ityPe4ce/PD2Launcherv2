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

} // namespace

GameFiles::~GameFiles() {
    if (closeArchive_) {
        for (HANDLE h : archives_) closeArchive_(h);
    }
    // Storm stays loaded: unloading it while BH may still hold strings from it buys nothing.
}

bool GameFiles::Open(const std::wstring& installDir, const std::wstring& dataDir, std::string& error) {
    dataDir_ = dataDir;
    if (installDir.empty()) {
        if (dataDir_.empty()) { error = "no install folder or data folder given"; return false; }
        return true;
    }

    // Load the real Storm.dll by full path. No fake module uses the name STORM (BH resolves
    // no Storm symbols), so the real one can be loaded without clashing with the mock.
    fs::path stormPath = fs::path(installDir) / L"Storm.dll";
    if (!fs::exists(stormPath)) stormPath = fs::path(installDir).parent_path() / L"Storm.dll";
    storm_ = LoadLibraryExW(stormPath.c_str(), nullptr, LOAD_WITH_ALTERED_SEARCH_PATH);
    if (!storm_) {
        if (!dataDir_.empty()) { Log("Storm.dll not loaded; using the data folder only"); return true; }
        error = "could not load Storm.dll from the install folder";
        return false;
    }
    openArchive_ = (SFileOpenArchive_t)GetProcAddress(storm_, MAKEINTRESOURCEA(kOrdOpenArchive));
    closeArchive_ = (SFileCloseArchive_t)GetProcAddress(storm_, MAKEINTRESOURCEA(kOrdCloseArchive));
    openFile_ = (SFileOpenFileEx_t)GetProcAddress(storm_, MAKEINTRESOURCEA(kOrdOpenFileEx));
    closeFile_ = (SFileCloseFile_t)GetProcAddress(storm_, MAKEINTRESOURCEA(kOrdCloseFile));
    fileSize_ = (SFileGetFileSize_t)GetProcAddress(storm_, MAKEINTRESOURCEA(kOrdGetFileSize));
    readFile_ = (SFileReadFile_t)GetProcAddress(storm_, MAKEINTRESOURCEA(kOrdReadFile));
    if (!openArchive_ || !closeArchive_ || !openFile_ || !closeFile_ || !fileSize_ || !readFile_) {
        error = "Storm.dll is missing an expected export";
        return false;
    }

    DWORD priority = 0x2000;
    for (const wchar_t* name : kArchives) {
        for (const fs::path& dir : { fs::path(installDir), fs::path(installDir).parent_path() }) {
            fs::path p = dir / name;
            if (!fs::exists(p)) continue;
            HANDLE h = nullptr;
            std::string narrow = p.string();
            if (openArchive_(narrow.c_str(), priority--, 0, &h) && h) {
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

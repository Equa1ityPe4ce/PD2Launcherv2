#pragma once
// Reads game data files (excel .bin tables, .tbl string tables) from the user's own
// install, so the preview always uses the data of the PD2 season that is installed.
//
// Two sources are supported, tried in order:
//   1. A directory of extracted files (dataDir/data/global/excel/weapons.bin, ...),
//      handy for development and for testing without an install.
//   2. The install's MPQ archives, read through the game's own Storm.dll.
#include <windows.h>
#include <cstdint>
#include <string>
#include <vector>

namespace mock {

class GameFiles {
public:
    ~GameFiles();

    // installDir: folder holding Game.exe/Storm.dll/pd2data.mpq (the PD2 install).
    // dataDir: optional folder of extracted files; may be empty.
    bool Open(const std::wstring& installDir, const std::wstring& dataDir, std::string& error);

    // path uses MPQ style, e.g. "data\\global\\excel\\weapons.bin".
    bool Read(const std::string& path, std::vector<uint8_t>& out) const;

    const std::vector<std::wstring>& OpenedArchives() const { return archiveNames_; }

private:
    typedef BOOL(__stdcall* SFileOpenArchive_t)(const char*, DWORD, DWORD, HANDLE*);
    typedef BOOL(__stdcall* SFileCloseArchive_t)(HANDLE);
    typedef BOOL(__stdcall* SFileOpenFileEx_t)(HANDLE, const char*, DWORD, HANDLE*);
    typedef BOOL(__stdcall* SFileCloseFile_t)(HANDLE);
    typedef DWORD(__stdcall* SFileGetFileSize_t)(HANDLE, DWORD*);
    typedef BOOL(__stdcall* SFileReadFile_t)(HANDLE, void*, DWORD, DWORD*, LPOVERLAPPED);

    std::wstring dataDir_;
    HMODULE storm_ = nullptr;
    SFileOpenArchive_t openArchive_ = nullptr;
    SFileCloseArchive_t closeArchive_ = nullptr;
    SFileOpenFileEx_t openFile_ = nullptr;
    SFileCloseFile_t closeFile_ = nullptr;
    SFileGetFileSize_t fileSize_ = nullptr;
    SFileReadFile_t readFile_ = nullptr;
    std::vector<HANDLE> archives_;          // highest priority first
    std::vector<std::wstring> archiveNames_;
};

} // namespace mock

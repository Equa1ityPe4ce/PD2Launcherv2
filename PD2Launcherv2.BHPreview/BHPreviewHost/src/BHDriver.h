#pragma once
// Loads the real BH.dll into the mock game and drives it the way the game would: joins a
// game, runs the game loop, and calls BH's item-name and item-description hooks.
#include "GameData.h"
#include "GameFiles.h"
#include "MockGame.h"
#include "World.h"
#include "../../third_party/nlohmann/json.hpp"

#include <memory>
#include <string>

namespace mock {

struct DriverOptions {
    std::wstring installDir;  // PD2 install: Game.exe, BH.dll, Storm.dll, MPQs, BH.json
    std::wstring bhDll;       // BH.dll to test; default installDir\BH.dll
    std::wstring dataDir;     // optional extracted data files
    std::wstring sandboxDir;  // scratch folder this process may fill (fake modules, filter copy)
    std::wstring filterFile;  // loot filter to preview
    int filterLevel = -1;     // -1 = keep the level from the user's BH.json
    nlohmann::json initialState = nlohmann::json::object();
};

class BHDriver {
public:
    bool Start(const DriverOptions& options, std::string& error);
    // Request: { "state": {...optional GameState changes...}, "items": [ItemSpec, ...] }
    nlohmann::json Evaluate(const nlohmann::json& request);
    nlohmann::json Describe() const; // what the mock is running with (for diagnostics)
    nlohmann::json Catalog();        // every item base in the installed data, for item pickers

private:
    bool PrepareSandbox(const DriverOptions& options, std::string& error);
    bool LoadBH(std::string& error);
    void JoinGame();
    void TickGameLoop();
    std::wstring EvaluateName(UnitAny* item, bool& crashed);
    std::wstring EvaluateDescription(UnitAny* item, bool& crashed);

    DriverOptions options_;
    GameFiles files_;
    GameData data_;
    std::unique_ptr<World> world_;
    HMODULE bh_ = nullptr;
    uint8_t* itemNameSite_ = nullptr;
    uint8_t* gameLoopSite_ = nullptr;
    void(__stdcall* onProperties_)(wchar_t*) = nullptr;
};

std::string ToUtf8(const std::wstring& s);
std::wstring FromUtf8(const std::string& s);

} // namespace mock

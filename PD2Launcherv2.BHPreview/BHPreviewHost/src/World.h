#pragma once
// The mock game world: one player standing in one area, plus whatever items a request
// asks about. Every value BH can read about the world comes from GameState, which has a
// default for everything and can be overridden per request from JSON.
#include "BHHeaders.h"
#include "GameData.h"
#include "../../third_party/nlohmann/json.hpp"

#include <deque>
#include <map>
#include <memory>
#include <mutex>
#include <string>
#include <vector>

namespace mock {

struct GameState {
    // Character
    std::string charName = "Preview";
    int charClass = 1;          // 0 ama, 1 sor, 2 nec, 3 pal, 4 bar, 5 dru, 6 ass
    int charLevel = 90;
    std::map<int, int> playerStats; // extra player stats by id (level is filled from charLevel)
    // Where the player is
    int difficulty = 2;         // 0 normal, 1 nightmare, 2 hell
    int areaId = 109;           // Levels.txt id; 109 = Harrogath
    int act = 4;                // 0-based act; 4 = Act V
    // Quest flags returned by GetQuestFlag/GetQuestFlag2 when not listed in questFlags.
    int defaultQuestFlag = 1;
    std::map<std::string, int> questFlags; // "act:quest" or "quest:flag" -> value
    // UI panels BH may ask about (D2CLIENT_GetUIState); 0 = closed.
    int defaultUiState = 0;
    std::map<int, int> uiStates;
    // PD2-specific hooks (ProjectDiablo.dll)
    DWORD itemDiscoveryState = 0xF; // DISCOVERYSTATE_*: everything discovered and owned
    DWORD itemStorageType = 0;
    // Escape hatches: the value any not-otherwise-implemented game function returns, and
    // the initial DWORD of any game variable, by BH's name for it.
    std::map<std::string, uint32_t> functionReturns;
    std::map<std::string, uint32_t> variables;

    void Apply(const nlohmann::json& j); // fields missing from j keep their current value
    nlohmann::json ToJson() const;
};

// An item described by a request.
struct ItemSpec {
    std::string code;
    DWORD quality = 2;          // 1 inferior .. 9 tempered (BH's ITEM_QUALITY_* order)
    DWORD itemLevel = 85;
    DWORD flags = 0x10;         // ItemData::dwFlags; identified by default
    DWORD fileIndex = 0;        // UniqueItems/SetItems row for unique/set items
    WORD prefixes[3] = {}, suffixes[3] = {};
    WORD rarePrefix = 0, rareSuffix = 0, autoPrefix = 0;
    int sockets = 0;
    std::vector<Stat> stats;    // (stat, layer, value)
    std::vector<Stat> setStats; // full-set bonuses, BH reads them from pStats->pSetStat
    int runeword = -1;          // Runes.txt row, for runeword items
    int price = -1;             // vendor price; -1 = not given (the mock returns 1)
    int levelRequirement = -1;  // -1 = computed from the tables
    std::wstring gameName;      // overrides the name the mock game would build
};

// Per-item values the mock game needs that have no home in the game's own structures.
struct ItemExtras {
    int runeword = -1;
    int price = -1;
    int levelRequirement = -1;
    std::wstring gameName;
};

bool ParseItemSpec(const nlohmann::json& j, GameData& data, ItemSpec& out, std::string& error);

class World {
public:
    explicit World(GameData& data) : data_(data) {}

    void Build(const GameState& state);  // creates the player and the area around them
    void Update(const GameState& state); // changes them in place (pointers BH holds stay valid)
    GameState& State() { return state_; }
    UnitAny* Player() { return playerPresent_ ? player_ : nullptr; }
    void SetPlayerPresent(bool present) { playerPresent_ = present; }

    UnitAny* CreateItem(const ItemSpec& spec);
    void ClearItems();
    UnitAny* FindUnit(DWORD id, DWORD type);

    // Stats as D2COMMON sees them
    int UnitStat(UnitAny* unit, DWORD stat, DWORD layer, bool anyLayer = false);
    QuestInfo* Quests() { return &quests_; }
    const ItemExtras* Extras(UnitAny* item);
    Room1* PlayerRoom() { return &room1_; }
    BnetData* BnData() { return &bnData_; }

private:
    struct OwnedUnit {
        UnitAny unit = {};
        ItemData itemData = {};
        Inventory inventory = {}; // an item's socketed contents (always empty here)
        StatList stats = {};
        std::vector<Stat> statArray;
        std::vector<Stat> setStatArray;
        ItemExtras extras;
    };
    StatList* BuildStatList(OwnedUnit& owner, std::vector<Stat> stats, std::vector<Stat> setStats);

    GameData& data_;
    GameState state_;
    bool playerPresent_ = false;
    std::recursive_mutex lock_;
    std::deque<OwnedUnit> units_; // stable addresses
    UnitAny* player_ = nullptr;
    PlayerData playerData_ = {};
    Inventory inventory_ = {};
    Info info_ = {};
    Path path_ = {};
    Room1 room1_ = {};
    Room2 room2_ = {};
    std::vector<uint8_t> level_;  // Level, padded (only some of it is declared)
    std::vector<uint8_t> act_;    // Act + ActMisc
    QuestInfo quests_ = {};
    std::vector<uint8_t> questBuffer_;
    BnetData bnData_ = {}; // D2LAUNCH_BnData: BH's threads read the game and account names
    DWORD nextUnitId_ = 1; // never reused: BH caches item names by unit id
    size_t firstItemIndex_ = 0;
};

} // namespace mock

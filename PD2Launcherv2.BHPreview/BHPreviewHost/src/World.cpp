#include "World.h"
#include "Log.h"

#include <algorithm>

using nlohmann::json;

namespace mock {
namespace {

constexpr DWORD kItemModeOnGround = 3;

template <typename T>
void Take(const json& j, const char* key, T& field) {
    auto it = j.find(key);
    if (it != j.end() && !it->is_null()) field = it->get<T>();
}

std::map<int, int> IntKeyMap(const json& j) {
    std::map<int, int> out;
    for (auto& [k, v] : j.items()) out[std::stoi(k)] = v.get<int>();
    return out;
}

json IntKeyJson(const std::map<int, int>& m) {
    json out = json::object();
    for (auto& [k, v] : m) out[std::to_string(k)] = v;
    return out;
}

const std::map<std::string, DWORD> kQualities = {
    { "inferior", 1 }, { "normal", 2 }, { "superior", 3 }, { "magic", 4 }, { "set", 5 },
    { "rare", 6 }, { "unique", 7 }, { "crafted", 8 }, { "tempered", 9 },
};

const std::map<std::string, DWORD> kFlags = {
    { "identified", ITEM_IDENTIFIED }, { "socketed", ITEM_HASSOCKETS }, { "new", ITEM_NEW },
    { "ethereal", ITEM_ETHEREAL }, { "personalized", ITEM_PERSONALIZED }, { "runeword", ITEM_RUNEWORD },
    { "allocated", 0x10000000 /* ITEMFLAG_ALLOCED (some BH versions define it with a stray ';') */ },
};

bool ParseStats(const json& arr, std::vector<Stat>& out, std::string& error) {
    for (auto& s : arr) {
        Stat st = {};
        if (!s.contains("stat")) { error = "stat entry without \"stat\""; return false; }
        st.wStatIndex = (WORD)s["stat"].get<int>();
        st.wSubIndex = (WORD)s.value("layer", 0);
        st.dwStatValue = (DWORD)s.value("value", 0);
        out.push_back(st);
    }
    return true;
}

} // namespace

void GameState::Apply(const json& j) {
    if (auto c = j.find("character"); c != j.end()) {
        Take(*c, "name", charName);
        Take(*c, "class", charClass);
        Take(*c, "level", charLevel);
        if (c->contains("stats")) playerStats = IntKeyMap((*c)["stats"]);
    }
    Take(j, "difficulty", difficulty);
    Take(j, "area", areaId);
    Take(j, "act", act);
    Take(j, "defaultQuestFlag", defaultQuestFlag);
    if (j.contains("questFlags")) questFlags = j["questFlags"].get<std::map<std::string, int>>();
    Take(j, "defaultUiState", defaultUiState);
    if (j.contains("uiStates")) uiStates = IntKeyMap(j["uiStates"]);
    Take(j, "itemDiscoveryState", itemDiscoveryState);
    Take(j, "itemStorageType", itemStorageType);
    if (j.contains("functionReturns")) functionReturns = j["functionReturns"].get<std::map<std::string, uint32_t>>();
    if (j.contains("variables")) variables = j["variables"].get<std::map<std::string, uint32_t>>();
}

json GameState::ToJson() const {
    return json{
        { "character", { { "name", charName }, { "class", charClass }, { "level", charLevel }, { "stats", IntKeyJson(playerStats) } } },
        { "difficulty", difficulty }, { "area", areaId }, { "act", act },
        { "defaultQuestFlag", defaultQuestFlag }, { "questFlags", questFlags },
        { "defaultUiState", defaultUiState }, { "uiStates", IntKeyJson(uiStates) },
        { "itemDiscoveryState", itemDiscoveryState }, { "itemStorageType", itemStorageType },
        { "functionReturns", functionReturns }, { "variables", variables },
    };
}

bool ParseItemSpec(const json& j, GameData& data, ItemSpec& out, std::string& error) {
    out = ItemSpec();
    out.code = j.value("code", "");
    if (data.ItemClassFromCode(out.code) < 0) { error = "unknown item code '" + out.code + "'"; return false; }
    if (auto q = j.find("quality"); q != j.end()) {
        if (q->is_string()) {
            auto it = kQualities.find(q->get<std::string>());
            if (it == kQualities.end()) { error = "unknown quality " + q->get<std::string>(); return false; }
            out.quality = it->second;
        } else {
            out.quality = q->get<DWORD>();
        }
    }
    Take(j, "itemLevel", out.itemLevel);
    Take(j, "fileIndex", out.fileIndex);
    Take(j, "sockets", out.sockets);
    if (auto f = j.find("flags"); f != j.end()) {
        if (f->is_number()) {
            out.flags = f->get<DWORD>();
        } else {
            out.flags = 0;
            for (auto& name : *f) {
                auto it = kFlags.find(name.get<std::string>());
                if (it == kFlags.end()) { error = "unknown flag " + name.get<std::string>(); return false; }
                out.flags |= it->second;
            }
        }
    }
    if (out.sockets > 0) out.flags |= ITEM_HASSOCKETS;
    if (auto p = j.find("prefixes"); p != j.end())
        for (size_t i = 0; i < 3 && i < p->size(); i++) out.prefixes[i] = (*p)[i].get<WORD>();
    if (auto s = j.find("suffixes"); s != j.end())
        for (size_t i = 0; i < 3 && i < s->size(); i++) out.suffixes[i] = (*s)[i].get<WORD>();
    Take(j, "rarePrefix", out.rarePrefix);
    Take(j, "rareSuffix", out.rareSuffix);
    Take(j, "autoPrefix", out.autoPrefix);
    Take(j, "runeword", out.runeword);
    Take(j, "price", out.price);
    Take(j, "levelRequirement", out.levelRequirement);
    if (j.contains("gameName")) {
        std::string n = j["gameName"].get<std::string>();
        int len = MultiByteToWideChar(CP_UTF8, 0, n.c_str(), (int)n.size(), nullptr, 0);
        out.gameName.resize(len);
        MultiByteToWideChar(CP_UTF8, 0, n.c_str(), (int)n.size(), out.gameName.data(), len);
    }
    if (out.runeword >= 0) out.flags |= ITEM_RUNEWORD;
    if (j.contains("stats") && !ParseStats(j["stats"], out.stats, error)) return false;
    if (j.contains("setStats") && !ParseStats(j["setStats"], out.setStats, error)) return false;
    if (out.sockets > 0) {
        bool hasSocketStat = std::any_of(out.stats.begin(), out.stats.end(),
                                         [](const Stat& s) { return s.wStatIndex == STAT_SOCKETS; });
        if (!hasSocketStat) out.stats.push_back(Stat{ 0, (WORD)STAT_SOCKETS, (DWORD)out.sockets });
    }
    return true;
}

StatList* World::BuildStatList(OwnedUnit& owner, std::vector<Stat> stats, std::vector<Stat> setStats) {
    // The game keeps stat lists sorted by (stat id, layer).
    auto byId = [](const Stat& a, const Stat& b) {
        return a.wStatIndex != b.wStatIndex ? a.wStatIndex < b.wStatIndex : a.wSubIndex < b.wSubIndex;
    };
    std::sort(stats.begin(), stats.end(), byId);
    std::sort(setStats.begin(), setStats.end(), byId);
    owner.statArray = std::move(stats);
    owner.setStatArray = std::move(setStats);
    StatList& list = owner.stats;
    list = {};
    list.pUnit = &owner.unit;
    list.dwUnitType = owner.unit.dwType;
    list.dwUnitId = owner.unit.dwUnitId;
    list.pStat = owner.statArray.empty() ? nullptr : owner.statArray.data();
    list.wStatCount1 = (WORD)owner.statArray.size();
    list.wnSize = (WORD)owner.statArray.size();
    list.pSetStat = owner.setStatArray.empty() ? nullptr : owner.setStatArray.data();
    list.wSetStatCount = (WORD)owner.setStatArray.size();
    return &list;
}

void World::Build(const GameState& state) {
    std::lock_guard<std::recursive_mutex> lock(lock_);
    units_.clear();

    level_.assign(sizeof(Level) + 0x400, 0);
    act_.assign(sizeof(Act) + sizeof(ActMisc) + 0x100, 0);
    Level* level = reinterpret_cast<Level*>(level_.data());
    Act* act = reinterpret_cast<Act*>(act_.data());
    ActMisc* misc = reinterpret_cast<ActMisc*>(act_.data() + sizeof(Act));
    level->pMisc = misc;
    level->pRoom2First = &room2_;
    act->pRoom1 = &room1_;
    act->pMisc = misc;
    misc->pAct = act;
    misc->pLevelFirst = level;
    room2_ = {};
    room2_.pLevel = level;
    room1_ = {};
    room1_.pRoom2 = &room2_;
    room1_.dwXSize = room1_.dwYSize = 32;
    path_ = {};
    path_.pRoom1 = &room1_;
    path_.xPos = 5000;
    path_.yPos = 5000;

    questBuffer_.assign(0x200, 0);
    quests_.pBuffer = questBuffer_.data();

    units_.emplace_back();
    OwnedUnit& p = units_.back();
    player_ = &p.unit;
    playerData_ = {};
    playerData_.pNormalQuest = playerData_.pNightmareQuest = playerData_.pHellQuest = &quests_;
    inventory_ = {};
    inventory_.dwSignature = 0x01020304;
    inventory_.pOwner = player_;
    info_ = {};
    p.unit.dwType = UNIT_PLAYER;
    p.unit.dwUnitId = nextUnitId_++;
    p.unit.dwMode = 1; // neutral
    p.unit.pPlayerData = &playerData_;
    p.unit.pAct = act;
    p.unit.pPath = &path_;
    p.unit.pInventory = &inventory_;
    p.unit.pInfo = &info_;
    p.unit.wX = 5000;
    p.unit.wY = 5000;
    path_.pUnit = player_;
    inventory_.dwOwnerId = p.unit.dwUnitId;
    firstItemIndex_ = units_.size();
    Update(state);
}

void World::Update(const GameState& state) {
    std::lock_guard<std::recursive_mutex> lock(lock_);
    state_ = state;
    Level* level = reinterpret_cast<Level*>(level_.data());
    Act* act = reinterpret_cast<Act*>(act_.data());
    level->dwLevelNo = (DWORD)state.areaId;
    act->dwAct = (DWORD)state.act;
    strncpy_s(playerData_.szName, state.charName.c_str(), _TRUNCATE);
    OwnedUnit& p = units_.front();
    p.unit.dwTxtFileNo = (DWORD)state.charClass;
    p.unit.dwAct = (DWORD)state.act;
    std::vector<Stat> stats;
    stats.push_back(Stat{ 0, (WORD)STAT_LEVEL, (DWORD)state.charLevel });
    for (auto& [id, value] : state.playerStats)
        if (id != STAT_LEVEL) stats.push_back(Stat{ 0, (WORD)id, (DWORD)value });
    p.unit.pStats = BuildStatList(p, stats, {});
}

UnitAny* World::CreateItem(const ItemSpec& spec) {
    std::lock_guard<std::recursive_mutex> lock(lock_);
    units_.emplace_back();
    OwnedUnit& o = units_.back();
    o.unit.dwType = UNIT_ITEM;
    o.unit.dwTxtFileNo = (DWORD)data_.ItemClassFromCode(spec.code);
    o.unit.dwUnitId = nextUnitId_++;
    o.unit.dwMode = kItemModeOnGround;
    o.unit.pItemData = &o.itemData;
    o.unit.dwAct = (DWORD)state_.act;
    o.unit.pAct = player_ ? player_->pAct : nullptr;
    o.unit.wX = 5001;
    o.unit.wY = 5001;
    ItemData& d = o.itemData;
    d.dwQuality = spec.quality;
    d.dwItemFlags = 0xFFFFFFFF; // not owned by anyone: lying on the ground
    d.dwFlags = spec.flags;
    d.dwFileIndex = spec.fileIndex;
    d.dwItemLevel = spec.itemLevel;
    d.wRarePrefix = spec.rarePrefix;
    d.wRareSuffix = spec.rareSuffix;
    d.wAutoPrefix = spec.autoPrefix;
    for (int i = 0; i < 3; i++) { d.wPrefix[i] = spec.prefixes[i]; d.wSuffix[i] = spec.suffixes[i]; }
    d.BodyLocation = 0xFF;
    d.ItemLocation = 0xFF;
    o.unit.pStats = BuildStatList(o, spec.stats, spec.setStats);
    o.extras.runeword = spec.runeword;
    o.extras.price = spec.price;
    o.extras.levelRequirement = spec.levelRequirement;
    o.extras.gameName = spec.gameName;
    return &o.unit;
}

void World::ClearItems() {
    std::lock_guard<std::recursive_mutex> lock(lock_);
    // Items are only appended after the player, so trimming the tail drops them all.
    while (units_.size() > firstItemIndex_) units_.pop_back();
}

UnitAny* World::FindUnit(DWORD id, DWORD type) {
    std::lock_guard<std::recursive_mutex> lock(lock_);
    for (auto& o : units_)
        if (o.unit.dwUnitId == id && o.unit.dwType == type) return &o.unit;
    return nullptr;
}

const ItemExtras* World::Extras(UnitAny* item) {
    std::lock_guard<std::recursive_mutex> lock(lock_);
    for (auto& o : units_)
        if (&o.unit == item) return &o.extras;
    return nullptr;
}

int World::UnitStat(UnitAny* unit, DWORD stat, DWORD layer, bool anyLayer) {
    if (!unit || !unit->pStats) return 0;
    StatList* list = unit->pStats;
    int total = 0;
    for (WORD i = 0; i < list->wStatCount1; i++) {
        const Stat& s = list->pStat[i];
        if (s.wStatIndex == stat && (anyLayer || s.wSubIndex == layer)) total += (int)s.dwStatValue;
    }
    return total;
}

} // namespace mock

#include "ModCatalog.h"
#include "BHDriver.h"

using nlohmann::json;

namespace mock {
namespace {

constexpr int kNoClass = 7;
constexpr WORD kNoStat = 0xFFFF;
constexpr int kResistStats[] = { 39, 41, 43, 45 }; // fire, lightning, cold, poison

struct ModStat {
    int stat, layer, min, max;
};

std::string Range(int min, int max) {
    return min == max ? std::to_string(min) : std::to_string(min) + "-" + std::to_string(max);
}

// Game strings with their ÿc colour codes removed.
std::string Text(const wchar_t* s) {
    std::wstring out;
    for (size_t i = 0; s && s[i]; i++) {
        if (s[i] == L'\xFF' && s[i + 1] == L'c') { i += s[i + 2] ? 2 : 1; continue; }
        out.push_back(s[i]);
    }
    return ToUtf8(out);
}

// Puts `value` where the game string expects its number (%d, %+d), or in front as "+value"
// when it has none ("to Amazon Skill Levels").
std::string Fill(std::string s, const std::string& value) {
    bool filled = false;
    for (const char* code : { "%+d", "%d" }) {
        size_t at = s.find(code);
        if (at != std::string::npos) {
            s.replace(at, strlen(code), (code[1] == '+' ? "+" : "") + value);
            filled = true;
            break;
        }
    }
    if (!filled) s = "+" + value + " " + s;
    for (size_t at; (at = s.find("%%")) != std::string::npos;) s.replace(at, 2, "%");
    return s;
}

ItemStatCostTxt* StatCost(GameData& d, int stat) {
    sgptDataTable* dt = d.DataTable();
    return stat >= 0 && (DWORD)stat < dt->dwItemStatCostRecs ? dt->pItemStatCostTxt + stat : nullptr;
}

CharStatsTxt* CharStats(GameData& d, int cls) {
    sgptDataTable* dt = d.DataTable();
    return cls >= 0 && (DWORD)cls < dt->dwCharsStatsRecs ? dt->pCharStatsTxt + cls : nullptr;
}

std::string SkillName(GameData& d, int skill) {
    sgptDataTable* dt = d.DataTable();
    if (skill < 0 || (DWORD)skill >= dt->dwSkillsRecs) return "skill " + std::to_string(skill);
    DWORD desc = dt->pSkillsTxt[skill].wSkillDesc;
    if (desc >= dt->dwSkillDescRecs) return "skill " + std::to_string(skill);
    return Text(d.LocaleText(dt->pSkillDescTxt[desc].wStrName));
}

// The stats one property sets, for the property functions automods and staff mods use
// (Properties.txt func column).
std::vector<ModStat> Expand(GameData& d, int prop, int param, int min, int max) {
    sgptDataTable* dt = d.DataTable();
    std::vector<ModStat> out;
    if (prop < 0 || (DWORD)prop >= dt->dwProportiesRecs) return out;
    const PropertiesTxt& p = dt->pPropertiesTxt[prop];
    for (int i = 0; i < 7 && p.nFunc[i]; i++) {
        int stat = p.wStat[i];
        switch (p.nFunc[i]) {
        case 3: // same value as the previous stat (res-all, all-stats)
            if (!out.empty() && stat != kNoStat) out.push_back({ stat, 0, out.back().min, out.back().max });
            break;
        case 5: for (int s : { 21, 23, 159 }) out.push_back({ s, 0, min, max }); break; // min damage
        case 6: for (int s : { 22, 24, 160 }) out.push_back({ s, 0, min, max }); break; // max damage
        case 7: for (int s : { 17, 18 }) out.push_back({ s, 0, min, max }); break;      // enhanced damage
        case 10: out.push_back({ 188, (param / 3) * 8 + param % 3, min, max }); break;  // skill tab
        case 17: if (stat != kNoStat) out.push_back({ stat, 0, param, param }); break;  // per level
        case 21: if (stat != kNoStat) out.push_back({ stat, p.wVal[i], min, max }); break; // class skills
        case 22: if (stat != kNoStat) out.push_back({ stat, param, min, max }); break;  // one skill
        default: if (stat != kNoStat) out.push_back({ stat, 0, min, max }); break;
        }
    }
    return out;
}

// How the game would describe the stats of one mod, close enough to recognise it.
std::string Describe(GameData& d, const std::vector<ModStat>& stats) {
    if (stats.empty()) return "";
    const ModStat& s = stats.front();
    std::string value = Range(s.min, s.max);
    bool allRes = std::all_of(std::begin(kResistStats), std::end(kResistStats), [&](int r) {
        return std::any_of(stats.begin(), stats.end(), [&](const ModStat& m) { return m.stat == r; });
    });
    if (allRes) {
        const wchar_t* t = d.LocaleTextByKey("strModAllResistances");
        return t ? Fill(Text(t), value) : "All Resistances +" + value + "%";
    }
    if (s.stat == 188) {
        CharStatsTxt* cs = CharStats(d, s.layer / 8);
        int tab = s.layer % 8;
        if (cs && tab < 3) {
            WORD strs[] = { cs->wStrSkillTab1, cs->wStrSkillTab2, cs->wStrSkillTab3 };
            return Fill(Text(d.LocaleText(strs[tab])), value);
        }
    }
    if (s.stat == 83) {
        if (CharStatsTxt* cs = CharStats(d, s.layer)) return Fill(Text(d.LocaleText(cs->wStrAllSkills)), value);
    }
    if (s.stat == 107 || s.stat == 97) return "+" + value + " to " + SkillName(d, s.layer);
    if (s.stat == 17 || s.stat == 18) return "+" + value + "% Enhanced Damage";
    ItemStatCostTxt* isc = StatCost(d, s.stat);
    std::string desc = isc && isc->wDescStrPos ? Text(d.LocaleText(isc->wDescStrPos)) : "stat " + std::to_string(s.stat);
    if (desc.find('%') != std::string::npos) return Fill(desc, value);
    bool percent = isc && (isc->bDescFunc == 2 || isc->bDescFunc == 4 || isc->bDescFunc == 5);
    std::string number = "+" + value + (percent ? "%" : "");
    if (isc && isc->bDescVal == 2) return desc + " " + number;
    return number + " " + desc;
}

json StatsJson(GameData& d, const std::vector<ModStat>& stats) {
    json out = json::array();
    for (const ModStat& s : stats) {
        ItemStatCostTxt* isc = StatCost(d, s.stat);
        out.push_back({ { "stat", s.stat }, { "layer", s.layer }, { "min", s.min }, { "max", s.max },
                        { "shift", isc ? isc->bValShift : 0 } });
    }
    return out;
}

} // namespace

json BuildModCatalog(GameData& data) {
    D2MagicAffixDataTbl* affixes = data.MagicAffixTables();
    json autoMods = json::array();
    int first = (int)(affixes->pAutoMagic - affixes->pMagicAffixTxt);
    for (int i = first; i < affixes->nMagicAffixTxtRecordCount; i++) {
        AutoMagicTxt& a = affixes->pMagicAffixTxt[i];
        json mods = json::array();
        std::vector<std::string> texts;
        for (const ItemsTxtStat& m : a.hMods) {
            if ((int)m.dwProp < 0) continue;
            auto stats = Expand(data, (int)m.dwProp, (int)m.dwPar, m.dwMin, m.dwMax);
            if (stats.empty()) continue;
            std::string text = Describe(data, stats);
            texts.push_back(text);
            mods.push_back({ { "text", text }, { "stats", StatsJson(data, stats) } });
        }
        if (mods.empty()) continue;
        std::string text;
        for (auto& t : texts) text += (text.empty() ? "" : ", ") + t;
        autoMods.push_back({ { "index", i }, { "group", a.dwGroup }, { "level", a.dwLevel }, { "text", text }, { "mods", mods } });
    }

    json classes = json::array();
    for (int c = 0; c < kNoClass; c++) {
        CharStatsTxt* cs = CharStats(data, c);
        if (!cs) break;
        json tabs = json::array();
        WORD strs[] = { cs->wStrSkillTab1, cs->wStrSkillTab2, cs->wStrSkillTab3 };
        for (int t = 0; t < 3; t++)
            tabs.push_back({ { "layer", c * 8 + t }, { "text", Fill(Text(data.LocaleText(strs[t])), "N") } });
        classes.push_back({ { "id", c }, { "name", std::string(cs->szClass, strnlen(cs->szClass, sizeof(cs->szClass))) },
                            { "allSkills", Fill(Text(data.LocaleText(cs->wStrAllSkills)), "N") }, { "tabs", tabs } });
    }

    json skills = json::array();
    sgptDataTable* dt = data.DataTable();
    for (DWORD s = 0; s < dt->dwSkillsRecs; s++) {
        const SkillsTxt& sk = dt->pSkillsTxt[s];
        if (sk.bClassId >= (DWORD)kNoClass || sk.wSkillDesc == 0 || sk.wSkillDesc >= dt->dwSkillDescRecs) continue;
        if (dt->pSkillDescTxt[sk.wSkillDesc].bSkillPage == 0) continue; // not in a skill tree
        skills.push_back({ { "id", s }, { "class", sk.bClassId }, { "name", SkillName(data, (int)s) },
                           { "level", sk.wReqLevel } });
    }
    return json{ { "autoMods", autoMods }, { "classes", classes }, { "skills", skills } };
}

json BaseModInfo(GameData& data, int classId) {
    ItemsTxt* t = data.Item((DWORD)classId);
    json autoMods = json::array();
    int staffClass = -1;
    if (!t) return json{ { "autoMods", autoMods }, { "staffModClass", staffClass } };
    if (D2ItemTypesTxt* type = data.ItemType(t->nType); type && type->nStaffMods < kNoClass) staffClass = type->nStaffMods;
    // A base spawns with an automagic row of its "auto prefix" group whose item types include
    // it and whose excluded types do not.
    if (t->wautoprefix) {
        D2MagicAffixDataTbl* affixes = data.MagicAffixTables();
        int first = (int)(affixes->pAutoMagic - affixes->pMagicAffixTxt);
        auto isType = [&](int type) { return type > 0 && (data.TypeMatches(t->nType, type) || data.TypeMatches(t->wtype2, type)); };
        for (int i = first; i < affixes->nMagicAffixTxtRecordCount; i++) {
            AutoMagicTxt& a = affixes->pMagicAffixTxt[i];
            if (a.dwGroup != t->wautoprefix) continue;
            if (!std::any_of(std::begin(a.wIType), std::end(a.wIType), isType)) continue;
            if (std::any_of(std::begin(a.wEType), std::end(a.wEType), isType)) continue;
            autoMods.push_back(i);
        }
    }
    return json{ { "autoMods", autoMods }, { "staffModClass", staffClass } };
}

} // namespace mock

#include "GameData.h"
#include "Log.h"

#include <algorithm>
#include <cstring>

namespace mock {
namespace {

std::string CodeString(const char code[4]) {
    std::string s(code, 4);
    while (!s.empty() && (s.back() == ' ' || s.back() == '\0')) s.pop_back();
    return s;
}

#pragma pack(push, 1)
struct TblHeader {
    WORD crc;
    WORD numElements;
    DWORD hashTableSize;
    BYTE version;
    DWORD indexStart;
    DWORD maxTries;
    DWORD fileSize;
};
struct TblHashNode {
    BYTE used;
    WORD index;
    DWORD hashValue;
    DWORD keyOffset;
    DWORD stringOffset;
    WORD stringLength;
};
#pragma pack(pop)

} // namespace

bool GameData::LoadTable(const GameFiles& files, const char* file, size_t bhStride, Table& out,
                         std::string& error, bool required) {
    std::string path = std::string("data\\global\\excel\\") + file;
    std::vector<uint8_t> raw;
    if (!files.Read(path, raw) || raw.size() < 4) {
        if (required) error = "missing game table " + path;
        else Log("optional table %s not found", path.c_str());
        return !required;
    }
    DWORD count = *reinterpret_cast<DWORD*>(raw.data());
    size_t fileStride = count ? (raw.size() - 4) / count : bhStride;
    out.count = (int)count;
    out.stride = bhStride;
    out.records.assign(count * bhStride + bhStride /* one zeroed spare record */, 0);
    if (fileStride == bhStride) {
        memcpy(out.records.data(), raw.data() + 4, count * bhStride);
    } else {
        // The file's records and BH's struct differ in size. Re-stride so BH's array indexing
        // still lands on record starts. A larger BH struct is expected for some tables (BH's
        // ItemStatCostTxt carries trailing PD2 fields the .bin does not have; they stay zero).
        // A smaller BH struct means the data has fields BH does not know about.
        Log("%s%s records are %zu bytes, BH's struct is %zu; re-striding",
            fileStride > bhStride ? "WARNING: " : "", file, fileStride, bhStride);
        size_t copy = std::min(fileStride, bhStride);
        for (DWORD i = 0; i < count; i++)
            memcpy(out.records.data() + i * bhStride, raw.data() + 4 + i * fileStride, copy);
    }
    Log("loaded %s: %u records", file, count);
    return true;
}

bool GameData::LoadTbl(const GameFiles& files, const char* file, int indexBase, std::vector<std::wstring>& out) {
    std::vector<uint8_t> raw;
    if (!files.Read(std::string("data\\local\\lng\\eng\\") + file, raw) || raw.size() < sizeof(TblHeader)) {
        Log("string table %s not found", file);
        return false;
    }
    const TblHeader* h = reinterpret_cast<const TblHeader*>(raw.data());
    const WORD* indices = reinterpret_cast<const WORD*>(raw.data() + sizeof(TblHeader));
    const TblHashNode* nodes = reinterpret_cast<const TblHashNode*>(indices + h->numElements);
    size_t nodesEnd = (const uint8_t*)(nodes + h->hashTableSize) - raw.data();
    if (nodesEnd > raw.size()) { Log("string table %s is truncated", file); return false; }
    out.assign(h->numElements, std::wstring());
    for (WORD i = 0; i < h->numElements; i++) {
        WORD n = indices[i];
        if (n >= h->hashTableSize || !nodes[n].used) continue;
        DWORD off = nodes[n].stringOffset;
        DWORD len = nodes[n].stringLength;
        if (off >= raw.size()) continue;
        len = std::min<DWORD>(len, (DWORD)(raw.size() - off));
        DWORD n8 = 0;
        while (n8 < len && raw[off + n8]) n8++;
        // The game decodes table strings as UTF-8 (PD2's tables store the 'ÿ' colour-code
        // marker as C3 BF). A string that is not valid UTF-8 is widened byte by byte, which
        // keeps a raw 0xFF marker intact.
        const char* src = reinterpret_cast<const char*>(raw.data() + off);
        std::wstring s;
        int wide = n8 ? MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, src, (int)n8, nullptr, 0) : 0;
        if (wide > 0) {
            s.resize(wide);
            MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, src, (int)n8, s.data(), wide);
        } else {
            for (DWORD k = 0; k < n8; k++) s.push_back((wchar_t)(uint8_t)src[k]);
        }
        DWORD keyOff = nodes[n].keyOffset;
        if (keyOff < raw.size()) {
            const char* key = reinterpret_cast<const char*>(raw.data() + keyOff);
            size_t keyLen = strnlen(key, raw.size() - keyOff);
            std::string k(key, keyLen);
            byKey_[k] = s; // tables load lowest priority first, so later tables win
            keyIndex_[k] = indexBase + i;
        }
        out[i] = std::move(s);
    }
    Log("loaded %s: %u strings", file, h->numElements);
    return true;
}

bool GameData::Load(const GameFiles& files, std::string& error) {
    Table weapons, armor, misc;
    if (!LoadTable(files, "weapons.bin", sizeof(ItemsTxt), weapons, error) ||
        !LoadTable(files, "armor.bin", sizeof(ItemsTxt), armor, error) ||
        !LoadTable(files, "misc.bin", sizeof(ItemsTxt), misc, error))
        return false;

    struct Spec { const char* file; size_t stride; bool required; };
    const Spec specs[] = {
        { "itemtypes.bin", sizeof(D2ItemTypesTxt), true },
        { "itemstatcost.bin", sizeof(ItemStatCostTxt), true },
        { "charstats.bin", sizeof(CharStatsTxt), true },
        { "properties.bin", sizeof(PropertiesTxt), true },
        { "skills.bin", sizeof(SkillsTxt), true },
        { "skilldesc.bin", sizeof(SkillDescTxt), true },
        { "uniqueitems.bin", sizeof(UniqueItemsTxt), true },
        { "setitems.bin", sizeof(SetItemsTxt), true },
        { "sets.bin", sizeof(SetsTxt), true },
        { "levels.bin", sizeof(LevelsTxt), true },
        { "runes.bin", sizeof(RunesTxt), true },
        { "gems.bin", sizeof(D2GemsTxt), true },
        { "magicsuffix.bin", sizeof(AutoMagicTxt), true },
        { "magicprefix.bin", sizeof(AutoMagicTxt), true },
        { "automagic.bin", sizeof(AutoMagicTxt), true },
        { "raresuffix.bin", sizeof(RareAffixTxt), true },
        { "rareprefix.bin", sizeof(RareAffixTxt), true },
    };
    for (const Spec& s : specs) {
        if (!LoadTable(files, s.file, s.stride, tables_[s.file], error, s.required)) return false;
    }

    // Items: the game keeps weapons, armor and misc in one array, in that order; an item's
    // class id (UnitAny::dwTxtFileNo) indexes the combined array.
    const int total = weapons.count + armor.count + misc.count;
    items_.assign((size_t)(total + 1) * sizeof(ItemsTxt), 0);
    size_t at = 0;
    for (Table* t : { &weapons, &armor, &misc }) {
        memcpy(items_.data() + at, t->records.data(), (size_t)t->count * sizeof(ItemsTxt));
        at += (size_t)t->count * sizeof(ItemsTxt);
    }
    ItemsTxt* all = reinterpret_cast<ItemsTxt*>(items_.data());
    itemTables_.nItemsTxtRecordCount = total;
    itemTables_.pItemsTxt = all;
    itemTables_.pWeapons = all;
    itemTables_.nWeaponsTxtRecordCount = weapons.count;
    itemTables_.pArmor = all + weapons.count;
    itemTables_.nArmorTxtRecordCount = armor.count;
    itemTables_.pMisc = all + weapons.count + armor.count;
    itemTables_.nMiscTxtRecordCount = misc.count;
    for (int i = 0; i < total; i++) itemByCode_.emplace(CodeString(all[i].szCode), i);

    // Magic affixes: suffixes, then prefixes, then automagic, in one array (ItemData's
    // wPrefix/wSuffix values index it). Rare affixes: suffixes then prefixes.
    {
        Table& suf = tables_["magicsuffix.bin"]; Table& pre = tables_["magicprefix.bin"]; Table& aut = tables_["automagic.bin"];
        const int n = suf.count + pre.count + aut.count;
        magicAffixRecords_.assign((size_t)(n + 1) * sizeof(AutoMagicTxt), 0);
        size_t o = 0;
        for (Table* t : { &suf, &pre, &aut }) {
            memcpy(magicAffixRecords_.data() + o, t->records.data(), (size_t)t->count * sizeof(AutoMagicTxt));
            o += (size_t)t->count * sizeof(AutoMagicTxt);
        }
        AutoMagicTxt* base = reinterpret_cast<AutoMagicTxt*>(magicAffixRecords_.data());
        magicAffix_.nMagicAffixTxtRecordCount = n;
        magicAffix_.pMagicAffixTxt = base;
        magicAffix_.pMagicSuffix = base;
        magicAffix_.pMagicPrefix = base + suf.count;
        magicAffix_.pAutoMagic = base + suf.count + pre.count;
    }
    {
        Table& suf = tables_["raresuffix.bin"]; Table& pre = tables_["rareprefix.bin"];
        const int n = suf.count + pre.count;
        rareAffixRecords_.assign((size_t)(n + 1) * sizeof(RareAffixTxt), 0);
        memcpy(rareAffixRecords_.data(), suf.records.data(), (size_t)suf.count * sizeof(RareAffixTxt));
        memcpy(rareAffixRecords_.data() + (size_t)suf.count * sizeof(RareAffixTxt), pre.records.data(),
               (size_t)pre.count * sizeof(RareAffixTxt));
        RareAffixTxt* base = reinterpret_cast<RareAffixTxt*>(rareAffixRecords_.data());
        rareAffix_.nRareAffixTxtRecordCount = n;
        rareAffix_.pRareAffixTxt = base;
        rareAffix_.pRareSuffix = base;
        rareAffix_.pRarePrefix = base + suf.count;
    }

    // sgptDataTable: allocate generously past BH's declared size (the real one is larger).
    dataTable_.assign(sizeof(sgptDataTable) + 0x1000, 0);
    sgptDataTable* dt = DataTable();
    auto recs = [&](const char* f) { return tables_[f].records.data(); };
    auto count = [&](const char* f) { return (DWORD)tables_[f].count; };
    dt->pItemsTypeTxt = (D2ItemTypesTxt*)recs("itemtypes.bin");      dt->dwItemsTypeRecs = count("itemtypes.bin");
    dt->pItemStatCostTxt = (ItemStatCostTxt*)recs("itemstatcost.bin"); dt->dwItemStatCostRecs = count("itemstatcost.bin");
    dt->pCharStatsTxt = (CharStatsTxt*)recs("charstats.bin");         dt->dwCharsStatsRecs = count("charstats.bin");
    dt->pPropertiesTxt = (PropertiesTxt*)recs("properties.bin");      dt->dwProportiesRecs = count("properties.bin");
    dt->pSkillsTxt = (SkillsTxt*)recs("skills.bin");                  dt->dwSkillsRecs = count("skills.bin");
    dt->pSkillDescTxt = (SkillDescTxt*)recs("skilldesc.bin");         dt->dwSkillDescRecs = count("skilldesc.bin");
    dt->pUniqueItemsTxt = (UniqueItemsTxt*)recs("uniqueitems.bin");   dt->dwUniqItemsRecs = count("uniqueitems.bin");
    dt->pSetItemsTxt = (SetItemsTxt*)recs("setitems.bin");            dt->dwSetItemsRecs = count("setitems.bin");
    dt->pSetsTxt = (SetsTxt*)recs("sets.bin");                        dt->dwSetsRecs = count("sets.bin");
    dt->pLevelsTxt = (LevelsTxt*)recs("levels.bin");                  dt->dwLevelsRecs = count("levels.bin");
    dt->pRunes = recs("runes.bin");
    runeCount_ = tables_["runes.bin"].count;
    gemCount_ = tables_["gems.bin"].count;

    D2ItemTypesTxt* types = dt->pItemsTypeTxt;
    for (DWORD i = 0; i < dt->dwItemsTypeRecs; i++) typeByCode_.emplace(CodeString(types[i].szCode), (int)i);

    // Lowest key-lookup priority first: later tables overwrite earlier keys.
    LoadTbl(files, "string.tbl", 0, strings_);
    LoadTbl(files, "expansionstring.tbl", 20000, expansionStrings_);
    LoadTbl(files, "patchstring.tbl", 10000, patchStrings_);
    ResolveNameIndices();
    return true;
}

WORD GameData::TblIndex(const char* key, size_t maxLen) const {
    auto it = keyIndex_.find(std::string(key, strnlen(key, maxLen)));
    return it == keyIndex_.end() ? 0 : (WORD)it->second;
}

// The compiled .bin files leave some string indices empty; D2COMMON fills them in from the
// record's name key when it loads the tables, and BH reads the filled-in values.
void GameData::ResolveNameIndices() {
    sgptDataTable* dt = DataTable();
    for (int i = 0; i < tables_["uniqueitems.bin"].count; i++) {
        UniqueItemsTxt& u = dt->pUniqueItemsTxt[i];
        if (!u.wTblIndex) u.wTblIndex = TblIndex(u.szName, sizeof(u.szName));
    }
    for (int i = 0; i < tables_["setitems.bin"].count; i++) {
        SetItemsTxt& si = dt->pSetItemsTxt[i];
        if (!si.dwTblIndex) si.dwTblIndex = TblIndex(si.szName, sizeof(si.szName));
    }
    for (int i = 0; i < magicAffix_.nMagicAffixTxtRecordCount; i++) {
        AutoMagicTxt& a = magicAffix_.pMagicAffixTxt[i];
        if (!a.wIndex) a.wIndex = TblIndex(a.szName, sizeof(a.szName));
    }
    for (int i = 0; i < rareAffix_.nRareAffixTxtRecordCount; i++) {
        RareAffixTxt& r = rareAffix_.pRareAffixTxt[i];
        if (!r.wStringId) r.wStringId = TblIndex(r.szName, sizeof(r.szName));
    }
}

ItemsTxt* GameData::Item(DWORD classId) {
    if ((int)classId < 0 || (int)classId >= itemTables_.nItemsTxtRecordCount) return nullptr;
    return itemTables_.pItemsTxt + classId;
}

int GameData::ItemClassFromCode(const std::string& code) const {
    auto it = itemByCode_.find(code);
    return it == itemByCode_.end() ? -1 : it->second;
}

int GameData::ItemTypeCount() const { return (int)tables_.at("itemtypes.bin").count; }

D2ItemTypesTxt* GameData::ItemType(int type) {
    if (type < 0 || type >= ItemTypeCount()) return nullptr;
    return DataTable()->pItemsTypeTxt + type;
}

int GameData::ItemTypeFromCode(const std::string& code) const {
    auto it = typeByCode_.find(code);
    return it == typeByCode_.end() ? -1 : it->second;
}

bool GameData::TypeMatches(int type, int ancestor) {
    // Same walk as ITEMS_CheckItemTypes: a type matches itself and everything it is
    // equivalent to through Equiv1/Equiv2, recursively. Type 0 is "none".
    if (type <= 0 || ancestor <= 0) return false;
    std::vector<int> stack{ type };
    std::vector<bool> seen(ItemTypeCount() + 1, false);
    while (!stack.empty()) {
        int t = stack.back();
        stack.pop_back();
        if (t <= 0 || t >= ItemTypeCount() || seen[t]) continue;
        if (t == ancestor) return true;
        seen[t] = true;
        D2ItemTypesTxt* rec = ItemType(t);
        stack.push_back(rec->nEquiv1);
        stack.push_back(rec->nEquiv2);
    }
    return false;
}

RunesTxt* GameData::Rune(int index) {
    if (index < 0 || index >= runeCount_) return nullptr;
    return reinterpret_cast<RunesTxt*>(tables_["runes.bin"].records.data()) + index;
}

D2GemsTxt* GameData::Gem(int index) {
    if (index < 0 || index >= gemCount_) return nullptr;
    return reinterpret_cast<D2GemsTxt*>(tables_["gems.bin"].records.data()) + index;
}

LevelsTxt* GameData::LevelRecord(int levelId) {
    if (levelId < 0 || levelId >= tables_["levels.bin"].count) return nullptr;
    return DataTable()->pLevelsTxt + levelId;
}

AutoMagicTxt* GameData::MagicAffix(int index) {
    if (index <= 0 || index >= magicAffix_.nMagicAffixTxtRecordCount) return nullptr;
    return magicAffix_.pMagicAffixTxt + index;
}

RareAffixTxt* GameData::RareAffix(int index) {
    if (index <= 0 || index >= rareAffix_.nRareAffixTxtRecordCount) return nullptr;
    return rareAffix_.pRareAffixTxt + index;
}

UniqueItemsTxt* GameData::Unique(int index) {
    if (index < 0 || index >= tables_["uniqueitems.bin"].count) return nullptr;
    return DataTable()->pUniqueItemsTxt + index;
}

SetItemsTxt* GameData::SetItem(int index) {
    if (index < 0 || index >= tables_["setitems.bin"].count) return nullptr;
    return DataTable()->pSetItemsTxt + index;
}

const wchar_t* GameData::LocaleTextByKey(const std::string& key) const {
    auto it = byKey_.find(key);
    return it == byKey_.end() ? nullptr : it->second.c_str();
}

const wchar_t* GameData::LocaleText(int index) {
    static const wchar_t empty[] = L"";
    const std::vector<std::wstring>* table = &strings_;
    if (index >= 20000) { table = &expansionStrings_; index -= 20000; }
    else if (index >= 10000) { table = &patchStrings_; index -= 10000; }
    if (index < 0 || index >= (int)table->size()) return empty;
    return (*table)[index].c_str();
}

} // namespace mock

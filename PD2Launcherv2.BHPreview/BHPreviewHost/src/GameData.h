#pragma once
// The game's data tables, loaded from the install's own .bin/.tbl files and arranged
// the way D2COMMON keeps them in memory (sgptDataTable and friends).
#include "BHHeaders.h"
#include "GameFiles.h"

#include <map>
#include <memory>
#include <string>
#include <vector>

namespace mock {

class GameData {
public:
    bool Load(const GameFiles& files, std::string& error);

    sgptDataTable* DataTable() { return reinterpret_cast<sgptDataTable*>(dataTable_.data()); }
    D2ItemDataTbl* ItemTables() { return &itemTables_; }
    D2MagicAffixDataTbl* MagicAffixTables() { return &magicAffix_; }
    RareAffixDataTbl* RareAffixTables() { return &rareAffix_; }

    ItemsTxt* Item(DWORD classId);
    int ItemCount() const { return itemTables_.nItemsTxtRecordCount; }
    // Looks up an item by its 3/4 letter code ("cap", "r01", ...); returns -1 if unknown.
    int ItemClassFromCode(const std::string& code) const;
    D2ItemTypesTxt* ItemType(int type);
    int ItemTypeCount() const;
    // True if `type` is `ancestor` or inherits from it through Equiv1/Equiv2.
    bool TypeMatches(int type, int ancestor);
    int ItemTypeFromCode(const std::string& code) const;

    RunesTxt* Rune(int index);
    int RuneCount() const { return runeCount_; }
    D2GemsTxt* Gem(int index);
    int GemCount() const { return gemCount_; }
    LevelsTxt* LevelRecord(int levelId);
    AutoMagicTxt* MagicAffix(int index);
    RareAffixTxt* RareAffix(int index);
    UniqueItemsTxt* Unique(int index);
    SetItemsTxt* SetItem(int index);

    // D2LANG_GetLocaleText semantics: <10000 string.tbl, <20000 patchstring.tbl, else expansionstring.tbl.
    const wchar_t* LocaleText(int index);
    // Lookup by string key ("Runeword1", "Hiquality"); patchstring wins over expansionstring,
    // which wins over string, as in the game. Returns null when the key is unknown.
    const wchar_t* LocaleTextByKey(const std::string& key) const;

private:
    struct Table {
        std::vector<uint8_t> records; // contiguous, laid out with BH's struct size
        int count = 0;
        size_t stride = 0;
    };
    bool LoadTable(const GameFiles& files, const char* file, size_t bhStride, Table& out, std::string& error, bool required = true);
    bool LoadTbl(const GameFiles& files, const char* file, int indexBase, std::vector<std::wstring>& out);
    // Index (with the 0/10000/20000 table offset) of a string key, as D2LANG resolves it; 0 if unknown.
    WORD TblIndex(const char* key, size_t maxLen) const;
    void ResolveNameIndices();
    std::map<std::string, std::wstring> byKey_;
    std::map<std::string, int> keyIndex_;

    std::map<std::string, Table> tables_;
    std::vector<uint8_t> dataTable_;
    D2ItemDataTbl itemTables_ = {};
    D2MagicAffixDataTbl magicAffix_ = {};
    RareAffixDataTbl rareAffix_ = {};
    std::vector<uint8_t> items_, magicAffixRecords_, rareAffixRecords_;
    int runeCount_ = 0, gemCount_ = 0;
    std::map<std::string, int> itemByCode_, typeByCode_;
    std::vector<std::wstring> strings_, patchStrings_, expansionStrings_;
};

} // namespace mock

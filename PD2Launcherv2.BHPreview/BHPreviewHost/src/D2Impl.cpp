// Implementations of the game functions BH's item code calls. Each one reproduces what
// the real 1.13c function does, using the mock World and the game's own data tables.
// Anything not listed here falls back to MockGame's default stub, which logs the call and
// returns the value configured for it (GameState::functionReturns), 0 by default.
//
// Every signature must match BH's declaration in D2Ptrs.h (calling convention and argument
// bytes); MockGame checks this when it installs them and refuses mismatches.
#include "MockGame.h"
#include "Log.h"

#include <algorithm>
#include <string>

namespace mock {
namespace {

GameData& Data() { return *Ctx().data; }
World& Wld() { return *Ctx().world; }

bool IsItem(UnitAny* u) { return u && u->dwType == UNIT_ITEM && u->pItemData; }

ItemsTxt* TxtOf(UnitAny* item) { return IsItem(item) ? Data().Item(item->dwTxtFileNo) : nullptr; }

bool ItemIsOfType(UnitAny* item, int type) {
    ItemsTxt* t = TxtOf(item);
    if (!t) return false;
    return Data().TypeMatches(t->nType, type) || Data().TypeMatches(t->wtype2, type);
}

std::wstring Widen(const char* s, size_t max) {
    std::wstring w;
    for (size_t i = 0; i < max && s[i]; i++) w.push_back((wchar_t)(unsigned char)s[i]);
    return w;
}

// The name the game itself would give an item before BH rewrites it (what D2CLIENT's
// GetItemName produces, and what BH's ItemNamePatch receives). Multi-line names are
// stored bottom line first: the game draws the last line at the top.
std::wstring GameItemName(UnitAny* item) {
    if (const ItemExtras* x = Wld().Extras(item); x && !x->gameName.empty()) return x->gameName;
    ItemsTxt* txt = TxtOf(item);
    if (!txt) return L"";
    ItemData* d = item->pItemData;
    std::wstring base = Data().LocaleText(txt->wnamestr);

    if (txt->szCode[0] == 'g' && txt->szCode[1] == 'l' && txt->szCode[2] == 'd')
        return std::to_wstring(Wld().UnitStat(item, STAT_GOLD, 0)) + L" " + base;

    const bool identified = (d->dwFlags & ITEM_IDENTIFIED) != 0;
    if (const ItemExtras* x = Wld().Extras(item); x && x->runeword >= 0 && (d->dwFlags & ITEM_RUNEWORD)) {
        if (RunesTxt* rw = Data().Rune(x->runeword)) {
            const wchar_t* name = Data().LocaleTextByKey(std::string(rw->szName, strnlen(rw->szName, sizeof(rw->szName))));
            return base + L"\n" + (name ? std::wstring(name) : Widen(rw->szRuneName, sizeof(rw->szRuneName)));
        }
    }
    switch (d->dwQuality) {
    case 1: { // inferior: LowQualityItems.txt order
        static const char* keys[] = { "Crude", "Cracked", "Damaged", "Low Quality" };
        const wchar_t* prefix = d->dwFileIndex < 4 ? Data().LocaleTextByKey(keys[d->dwFileIndex]) : nullptr;
        return prefix ? std::wstring(prefix) + L" " + base : base;
    }
    case 3: { // superior
        const wchar_t* prefix = Data().LocaleTextByKey("Hiquality");
        return prefix ? std::wstring(prefix) + L" " + base : base;
    }
    case 4: { // magic: prefix base suffix
        if (!identified) return base;
        std::wstring name = base;
        if (AutoMagicTxt* p = Data().MagicAffix(d->wPrefix[0])) name = std::wstring(Data().LocaleText(p->wIndex)) + L" " + name;
        if (AutoMagicTxt* s = Data().MagicAffix(d->wSuffix[0])) name = name + L" " + Data().LocaleText(s->wIndex);
        return name;
    }
    case 5: { // set
        if (!identified) return base;
        SetItemsTxt* s = Data().SetItem(d->dwFileIndex);
        return s ? base + L"\n" + Data().LocaleText((int)s->dwTblIndex) : base;
    }
    case 6: case 8: { // rare, crafted
        if (!identified) return base;
        std::wstring name;
        if (RareAffixTxt* p = Data().RareAffix(d->wRarePrefix)) name = Data().LocaleText(p->wStringId);
        if (RareAffixTxt* s = Data().RareAffix(d->wRareSuffix)) name += (name.empty() ? L"" : L" ") + std::wstring(Data().LocaleText(s->wStringId));
        return name.empty() ? base : base + L"\n" + name;
    }
    case 7: { // unique
        if (!identified) return base;
        UniqueItemsTxt* u = Data().Unique(d->dwFileIndex);
        return u ? base + L"\n" + Data().LocaleText(u->wTblIndex) : base;
    }
    default:
        return base;
    }
}

int LevelRequirement(UnitAny* item) {
    if (const ItemExtras* x = Wld().Extras(item); x && x->levelRequirement >= 0) return x->levelRequirement;
    ItemsTxt* txt = TxtOf(item);
    if (!txt) return 0;
    ItemData* d = item->pItemData;
    int req = txt->blevelreq;
    if (d->dwQuality == 7) { if (UniqueItemsTxt* u = Data().Unique(d->dwFileIndex)) req = std::max<int>(req, u->wLvlReq); }
    else if (d->dwQuality == 5) { if (SetItemsTxt* s = Data().SetItem(d->dwFileIndex)) req = std::max<int>(req, s->wLvlReq); }
    for (int i = 0; i < 3; i++) {
        if (AutoMagicTxt* p = Data().MagicAffix(d->wPrefix[i])) req = std::max<int>(req, p->nLevelReq);
        if (AutoMagicTxt* s = Data().MagicAffix(d->wSuffix[i])) req = std::max<int>(req, s->nLevelReq);
    }
    if (AutoMagicTxt* a = Data().MagicAffix(d->wAutoPrefix)) req = std::max<int>(req, a->nLevelReq);
    return req;
}

bool IsTown(DWORD levelId) {
    return levelId == 1 || levelId == 40 || levelId == 75 || levelId == 103 || levelId == 109;
}

// ---------------------------------------------------------------- D2CLIENT
UnitAny* __stdcall Client_GetPlayerUnit() { return Wld().Player(); }
BYTE __stdcall Client_GetDifficulty() { return (BYTE)Wld().State().difficulty; }
void* __stdcall Client_GetQuestInfo() { return Wld().Quests(); }
UnitAny* __fastcall Client_FindServerSideUnit(DWORD id, DWORD type) { return Wld().FindUnit(id, type); }
UnitAny* __fastcall Client_FindClientSideUnit(DWORD id, DWORD type) { return Wld().FindUnit(id, type); }
UnitAny* __fastcall Client_GetCurrentInteractingNPC() { return nullptr; }
UnitAny* __stdcall Client_GetSelectedUnit() { return nullptr; }
UnitAny* __fastcall Client_GetCursorItem() { return nullptr; }
UnitAny* __fastcall Client_GetMercUnit() { return nullptr; }
// BH reaches this through D2CLIENT_GetUIVar_STUB, which leaves the argument in ecx too.
DWORD __fastcall Client_GetUiVar(DWORD varNo) {
    auto& s = Wld().State();
    auto it = s.uiStates.find((int)varNo);
    return (DWORD)(it == s.uiStates.end() ? s.defaultUiState : it->second);
}
DWORD __fastcall Client_SetUIVar(DWORD, DWORD, DWORD) { return 0; }
int __fastcall Client_GetUnitX(UnitAny* u) { return u ? u->wX : 0; }
int __fastcall Client_GetUnitY(UnitAny* u) { return u ? u->wY : 0; }
BOOL __stdcall Client_GetItemName(UnitAny* item, wchar_t* buffer, DWORD size) {
    if (!buffer || !size) return FALSE;
    std::wstring name = GameItemName(item);
    wcsncpy_s(buffer, size, name.c_str(), _TRUNCATE);
    return TRUE;
}
void Record(const wchar_t* message) {
    if (!message) return;
    std::lock_guard<std::mutex> lock(Ctx().eventsLock);
    Ctx().printedMessages.push_back(message);
}
void __stdcall Client_PrintGameString(wchar_t* message, int) { Record(message); }
void __stdcall Client_PrintPartyString(wchar_t* message, int) { Record(message); }

// ---------------------------------------------------------------- D2COMMON
ItemsTxt* __stdcall Common_GetItemText(DWORD classId) { return Data().Item(classId); }
ItemsTxt* __stdcall Common_GetItemTextFromItemCode(DWORD code, int* classId) {
    char c[5] = {};
    memcpy(c, &code, 4);
    std::string s(c);
    while (!s.empty() && s.back() == ' ') s.pop_back();
    int id = Data().ItemClassFromCode(s);
    if (classId) *classId = id;
    return id >= 0 ? Data().Item((DWORD)id) : nullptr;
}
LevelText* __stdcall Common_GetLevelText(DWORD levelNo) { return reinterpret_cast<LevelText*>(Data().LevelRecord((int)levelNo)); }
StatList* __stdcall Common_GetStatList(UnitAny* unit, DWORD, DWORD) { return unit ? unit->pStats : nullptr; }
DWORD __stdcall Common_CopyStatList(StatList* list, Stat* out, DWORD max) {
    if (!list || !out) return 0;
    DWORD n = std::min<DWORD>(list->wStatCount1, max);
    for (DWORD i = 0; i < n; i++) out[i] = list->pStat[i];
    return n;
}
int __stdcall Common_GetBaseStatSigned(UnitAny* unit, int stat, int layer) { return Wld().UnitStat(unit, (DWORD)stat, (DWORD)layer); }
DWORD __stdcall Common_GetUnitStat(UnitAny* unit, DWORD stat, DWORD layer) { return (DWORD)Wld().UnitStat(unit, stat, layer); }
int __stdcall Common_GetUnitState(UnitAny*, DWORD) { return 0; }
StatList* __stdcall Common_GetStateStatList(UnitAny*, DWORD) { return nullptr; }
int __stdcall Common_GetStatValueFromStatList(StatList* list, int stat, WORD layer) {
    if (!list) return 0;
    int total = 0;
    for (WORD i = 0; i < list->wStatCount1; i++)
        if (list->pStat[i].wStatIndex == stat && list->pStat[i].wSubIndex == layer) total += (int)list->pStat[i].dwStatValue;
    return total;
}
D2ItemDataTbl* __stdcall Common_GetItemDataTables() { return Data().ItemTables(); }
D2MagicAffixDataTbl* __stdcall Common_GetMagicAffixDataTables() {
    // The last thing BH's Item::OnGameJoin asks for before it marks itself initialized.
    Ctx().affixTablesQueried = true;
    return Data().MagicAffixTables();
}
RareAffixTxt* __stdcall Common_GetRareAffixTxtRecord(int id) { return Data().RareAffix(id); }
AutoMagicTxt* __stdcall Common_GetItemMagicalMods(int id) { return Data().MagicAffix(id); }
BOOL __stdcall Common_IsMatchingType(UnitAny* item, int type) { return ItemIsOfType(item, type); }
BOOL __stdcall Common_CheckItemTypes(int type, int ancestor) { return Data().TypeMatches(type, ancestor); }
int __stdcall Common_GetItemType(UnitAny* item) { ItemsTxt* t = TxtOf(item); return t ? t->nType : 0; }
Room1* __stdcall Common_GetRoomFromUnit(UnitAny*) { return Wld().PlayerRoom(); }
int __stdcall Common_GetLevelIdFromRoom(Room1*) { return Wld().State().areaId; }
BOOL __stdcall Common_IsTownByLevelNo(DWORD levelNo) { return IsTown(levelNo); }
BOOL __stdcall Common_IsTownByRoom(Room1*) { return IsTown((DWORD)Wld().State().areaId); }
int __stdcall Common_GetSkillLevel(UnitAny*, Skill* skill, BOOL) { return skill ? (int)skill->skillLevel : 0; }
DWORD __stdcall Common_GetItemLevelRequirement(UnitAny* item, UnitAny*) { return (DWORD)LevelRequirement(item); }
int __stdcall Common_11015_GetItemLevelRequirement(UnitAny* item, UnitAny*) { return LevelRequirement(item); }
DWORD __stdcall Common_GetItemPrice(UnitAny*, UnitAny* item, DWORD, DWORD, DWORD, DWORD) {
    const ItemExtras* x = Wld().Extras(item);
    return (DWORD)(x && x->price >= 0 ? x->price : 1);
}
BYTE __stdcall Common_GetMaxSockets(UnitAny* item) {
    ItemsTxt* t = TxtOf(item);
    if (!t) return 0;
    D2ItemTypesTxt* type = Data().ItemType(t->nType);
    if (!type) return t->bgemsockets;
    DWORD ilvl = item->pItemData->dwItemLevel;
    BYTE byLevel = ilvl < 25 ? type->nMaxSock1 : ilvl < 40 ? type->nMaxSock25 : type->nMaxSock40;
    return std::min<BYTE>(t->bgemsockets, byLevel);
}
UnitAny* __stdcall Common_GetItemFromInventory(Inventory*) { return nullptr; }
UnitAny* __stdcall Common_GetNextItemFromInventory(UnitAny*) { return nullptr; }
RunesTxt* __stdcall Common_GetRunesTxt(int record) { return Data().Rune(record); }
int* __stdcall Common_GetRunesTxtRecords() { static int count; count = Data().RuneCount(); return &count; }
RunesTxt* __stdcall Common_GetRunesTxtRecordFromItem(UnitAny* item) {
    const ItemExtras* x = Wld().Extras(item);
    return x && x->runeword >= 0 && IsItem(item) && (item->pItemData->dwFlags & ITEM_RUNEWORD) ? Data().Rune(x->runeword) : nullptr;
}
int __stdcall Common_10244_CheckRequirements(UnitAny*, UnitAny*, BOOL, BOOL* str, BOOL* dex, BOOL* lvl) {
    if (str) *str = TRUE;
    if (dex) *dex = TRUE;
    if (lvl) *lvl = TRUE;
    return TRUE;
}
int __stdcall Common_10326_GetItemTxtFileNo(UnitAny* item) { return item ? (int)item->dwTxtFileNo : 0; }
int __stdcall Common_10711_ItemCheckIfThrowable(UnitAny* item) {
    ItemsTxt* t = TxtOf(item);
    D2ItemTypesTxt* type = t ? Data().ItemType(t->nType) : nullptr;
    return type ? type->nThrowable : 0;
}
int __stdcall Common_10757_CheckIfSocketable(UnitAny* item) { ItemsTxt* t = TxtOf(item); return t && t->bgemsockets > 0; }
int __stdcall Common_10822_GetItemRequiredClass(UnitAny* item) {
    ItemsTxt* t = TxtOf(item);
    if (!t) return 7;
    // ItemTypes' Class column, inherited through Equiv1/Equiv2; 7 means "any class".
    for (int type : { (int)t->nType, (int)t->wtype2 }) {
        for (int i = 0; i < Data().ItemTypeCount(); i++) {
            D2ItemTypesTxt* rec = Data().ItemType(i);
            if (rec && rec->nClass < 7 && Data().TypeMatches(type, i)) return rec->nClass;
        }
    }
    return 7;
}
D2GemsTxt* __stdcall Common_10806_GetGemsTxtRecord(int id) { return Data().Gem(id); }
int __stdcall Common_10865_HasDurability(UnitAny* item) { ItemsTxt* t = TxtOf(item); return t && !t->bnodurability; }
int __stdcall Common_11116_GetMaxDurability(UnitAny* item) { return Wld().UnitStat(item, STAT_MAXDURABILITY, 0); }
int __stdcall Common_11144_GetQuiverType(UnitAny* item) {
    ItemsTxt* t = TxtOf(item);
    D2ItemTypesTxt* type = t ? Data().ItemType(t->nType) : nullptr;
    return type ? type->wQuiver : 0;
}
UnitAny* __stdcall Common_11147_UnitIsItem(UnitAny* unit) { return IsItem(unit) ? unit : nullptr; }
int __stdcall Common_10957_GetStaffMods(UnitAny* item) {
    ItemsTxt* t = TxtOf(item);
    D2ItemTypesTxt* type = t ? Data().ItemType(t->nType) : nullptr;
    return type ? type->nStaffMods : 0;
}
UnitAny* __stdcall Common_NoItem(Inventory*) { return nullptr; }
UnitAny* __stdcall Common_NoItemAtLoc(Inventory*, int) { return nullptr; }
BOOL __stdcall Common_UnitCanDualWield(UnitAny*) { return FALSE; }

// ---------------------------------------------------------------- D2LANG / D2GFX
wchar_t* __fastcall Lang_GetLocaleText(WORD index) { return const_cast<wchar_t*>(Data().LocaleText(index)); }
HWND __stdcall Gfx_GetHwnd() { return Ctx().gameWindow; }

// ---------------------------------------------------------------- ProjectDiablo.dll
DWORD __stdcall Pd2_GetItemDiscoveryState(DWORD, DWORD, DWORD) { return Wld().State().itemDiscoveryState; }
DWORD __stdcall Pd2_GetItemStorageType(UnitAny*) { return Wld().State().itemStorageType; }
BOOL __stdcall Pd2_PlaySound(UnitAny*, int sound, int, int, BOOL) {
    std::lock_guard<std::mutex> lock(Ctx().eventsLock);
    Ctx().soundsPlayed.push_back(sound);
    return TRUE;
}

} // namespace

const std::vector<ImplEntry>& GameImplementations() {
    static const std::vector<ImplEntry> impls = {
        MakeImpl("D2CLIENT_GetPlayerUnit", &Client_GetPlayerUnit),
        MakeImpl("D2CLIENT_GetDifficulty", &Client_GetDifficulty),
        MakeImpl("D2CLIENT_GetQuestInfo", &Client_GetQuestInfo),
        MakeImpl("D2CLIENT_FindServerSideUnit", &Client_FindServerSideUnit),
        MakeImpl("D2CLIENT_FindClientSideUnit", &Client_FindClientSideUnit),
        MakeImpl("D2CLIENT_GetCurrentInteractingNPC", &Client_GetCurrentInteractingNPC),
        MakeImpl("D2CLIENT_GetSelectedUnit", &Client_GetSelectedUnit),
        MakeImpl("D2CLIENT_GetCursorItem", &Client_GetCursorItem),
        MakeImpl("D2CLIENT_GetMercUnit", &Client_GetMercUnit),
        MakeImpl("D2CLIENT_GetUiVar_I", &Client_GetUiVar),
        MakeImpl("D2CLIENT_SetUIVar", &Client_SetUIVar),
        MakeImpl("D2CLIENT_GetUnitX", &Client_GetUnitX),
        MakeImpl("D2CLIENT_GetUnitY", &Client_GetUnitY),
        MakeImpl("D2CLIENT_GetItemName", &Client_GetItemName),
        MakeImpl("D2CLIENT_PrintGameString", &Client_PrintGameString),
        MakeImpl("D2CLIENT_PrintPartyString", &Client_PrintPartyString),

        MakeImpl("D2COMMON_GetItemText", &Common_GetItemText),
        MakeImpl("D2COMMON_GetItemTextFromItemCode", &Common_GetItemTextFromItemCode),
        MakeImpl("D2COMMON_GetLevelText", &Common_GetLevelText),
        MakeImpl("D2COMMON_GetStatList", &Common_GetStatList),
        MakeImpl("D2COMMON_CopyStatList", &Common_CopyStatList),
        MakeImpl("D2COMMON_GetBaseStatSigned", &Common_GetBaseStatSigned),
        MakeImpl("D2COMMON_GetUnitStat", &Common_GetUnitStat),
        MakeImpl("D2COMMON_GetUnitState", &Common_GetUnitState),
        MakeImpl("D2COMMON_GetStateStatList", &Common_GetStateStatList),
        MakeImpl("D2COMMON_GetStatValueFromStatList", &Common_GetStatValueFromStatList),
        MakeImpl("D2COMMON_10535_DATATBLS_GetItemDataTables", &Common_GetItemDataTables),
        MakeImpl("D2COMMON_10492_DATATBLS_GetMagicAffixDataTables", &Common_GetMagicAffixDataTables),
        MakeImpl("D2COMMON_10365_DATATBLS_GetRareAffixTxtRecord", &Common_GetRareAffixTxtRecord),
        MakeImpl("D2COMMON_GetItemMagicalMods", &Common_GetItemMagicalMods),
        MakeImpl("D2COMMON_IsMatchingType", &Common_IsMatchingType),
        MakeImpl("D2COMMON_10744_ITEMS_IsMatchingType", &Common_IsMatchingType),
        MakeImpl("D2COMMON_11034_ITEMS_CheckItemTypes", &Common_CheckItemTypes),
        MakeImpl("D2COMMON_GetItemType", &Common_GetItemType),
        MakeImpl("D2COMMON_GetRoomFromUnit", &Common_GetRoomFromUnit),
        MakeImpl("D2COMMON_GetLevelIdFromRoom", &Common_GetLevelIdFromRoom),
        MakeImpl("D2COMMON_IsTownByLevelNo", &Common_IsTownByLevelNo),
        MakeImpl("D2COMMON_IsTownByRoom", &Common_IsTownByRoom),
        MakeImpl("D2COMMON_GetSkillLevel", &Common_GetSkillLevel),
        MakeImpl("D2COMMON_GetItemLevelRequirement", &Common_GetItemLevelRequirement),
        MakeImpl("D2COMMON_11015_GetItemLevelRequirement", &Common_11015_GetItemLevelRequirement),
        MakeImpl("D2COMMON_GetItemPrice", &Common_GetItemPrice),
        MakeImpl("D2COMMON_GetMaxSockets", &Common_GetMaxSockets),
        MakeImpl("D2COMMON_GetItemFromInventory", &Common_GetItemFromInventory),
        MakeImpl("D2COMMON_GetNextItemFromInventory", &Common_GetNextItemFromInventory),
        MakeImpl("D2COMMON_GetRunesTxt", &Common_GetRunesTxt),
        MakeImpl("D2COMMON_GetRunesTxtRecords", &Common_GetRunesTxtRecords),
        MakeImpl("D2COMMON_ITEMS_GetRunesTxtRecordFromItem", &Common_GetRunesTxtRecordFromItem),
        MakeImpl("D2COMMON_10244_CheckRequirements", &Common_10244_CheckRequirements),
        MakeImpl("D2COMMON_10326_GetItemTxtFileNo", &Common_10326_GetItemTxtFileNo),
        MakeImpl("D2COMMON_10711_ItemCheckIfThrowable", &Common_10711_ItemCheckIfThrowable),
        MakeImpl("D2COMMON_10757_CheckIfSocketable", &Common_10757_CheckIfSocketable),
        MakeImpl("D2COMMON_10822_GetItemRequiredClass", &Common_10822_GetItemRequiredClass),
        MakeImpl("D2COMMON_10806_DATATBLS_GetGemsTxtRecord", &Common_10806_GetGemsTxtRecord),
        MakeImpl("D2COMMON_10865_HasDurability", &Common_10865_HasDurability),
        MakeImpl("D2COMMON_11116_GetMaxDurabilityFromUnit", &Common_11116_GetMaxDurability),
        MakeImpl("D2COMMON_11144_GetQuiverType", &Common_11144_GetQuiverType),
        MakeImpl("D2COMMON_11147_UnitIsItem", &Common_11147_UnitIsItem),
        MakeImpl("D2COMMON_10957_GetStaffMods", &Common_10957_GetStaffMods),
        MakeImpl("D2COMMON_GetCursorItem", &Common_NoItem),
        MakeImpl("D2COMMON_GetLeftHandWeapon", &Common_NoItem),
        MakeImpl("D2COMMON_10061_INVENTORY_GetLeftHandWeapon", &Common_NoItem),
        MakeImpl("D2COMMON_GetItemByBodyLoc", &Common_NoItemAtLoc),
        MakeImpl("D2COMMON_11139_INVENTORY_GetItemFromBodyLoc", &Common_NoItemAtLoc),
        MakeImpl("D2COMMON_UnitCanDualWield", &Common_UnitCanDualWield),

        MakeImpl("D2LANG_GetLocaleText", &Lang_GetLocaleText),
        MakeImpl("D2GFX_GetHwnd", &Gfx_GetHwnd),
    };
    return impls;
}

const std::vector<ImplEntry>& ProjectDiabloImplementations() {
    static const std::vector<ImplEntry> impls = {
        MakeImpl("_D2Client_GetItemDiscoveryState@12", &Pd2_GetItemDiscoveryState),
        MakeImpl("_D2Client_GetItemStorageType@4", &Pd2_GetItemStorageType),
        MakeImpl("_D2Client_PlaySoundWithCustomVolumeOrPriority@20", &Pd2_PlaySound),
    };
    return impls;
}

} // namespace mock

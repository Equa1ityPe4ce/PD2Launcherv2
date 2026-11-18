#include "BHDriver.h"
#include "Log.h"
#include "ModCatalog.h"

#include <chrono>
#include <filesystem>
#include <fstream>
#include <thread>

namespace fs = std::filesystem;
using nlohmann::json;

namespace mock {
namespace {

// Offsets of the two D2CLIENT patch sites the driver calls (1.13c).
constexpr int kItemNameSite = 0x92366;  // Item.cpp: itemNamePatch
constexpr int kGameLoopSite = 0x44230;  // BH.cpp: GameLoop_Interception
constexpr int kNameSizeGroundLabel = 128; // anything but 127 (gamble) and 191 (NPC popup)
constexpr int kNameBufferChars = 0x400;
constexpr int kDescriptionChars = 1024;   // ITEM_TEXT_SIZE_LIMIT + 1

#pragma warning(disable : 4731) // ebp is deliberately repointed (and restored) below

// Stands in for the game's stack frame: ItemName_Interception reads a byte at [ebp+0x12a].
uint8_t g_fakeFrame[0x400];

// Calls the patched ItemName site with the registers and stack the game sets up there:
// edi = name buffer, ebx = item, and the name size 0x1bec bytes above the stack pointer
// that BH's handler sees (it is entered through two calls: ours and the patch's).
__declspec(noinline) void CallItemNameSite(void* site, wchar_t* name, UnitAny* item, int nameSize) {
    __asm {
        push ebx
        push esi
        push edi
        push ebp
        mov eax, nameSize
        mov edi, name
        mov ebx, item
        mov ecx, site
        lea ebp, g_fakeFrame
        sub esp, 0x1C00
        mov [esp + 0x1BE4], eax
        call ecx
        add esp, 0x1C00
        pop ebp
        pop edi
        pop esi
        pop ebx
    }
}

__declspec(noinline) void CallGameLoopSite(void* site) {
    __asm {
        mov eax, site
        pushad
        call eax
        popad
    }
}

// SEH wrappers: a crash inside BH becomes an error in the response instead of killing the
// process. (No C++ objects with destructors may live in these functions.)
#if defined(__MINGW32__)
// Development builds with clang/mingw (used to run the host under Wine) have no x86 SEH.
#define SEH_TRY if (true)
#define SEH_EXCEPT(filter) else
#else
#define SEH_TRY __try
#define SEH_EXCEPT(filter) __except (filter)
#endif
int LogAndHandle(EXCEPTION_POINTERS* info, const char* what) {
    LogException(info, what);
    return EXCEPTION_EXECUTE_HANDLER;
}
bool SafeItemName(void* site, wchar_t* name, UnitAny* item, int nameSize) {
    SEH_TRY { CallItemNameSite(site, name, item, nameSize); return true; }
    SEH_EXCEPT(LogAndHandle(GetExceptionInformation(), "item name")) { return false; }
}
bool SafeGameLoop(void* site) {
    SEH_TRY { CallGameLoopSite(site); return true; }
    SEH_EXCEPT(LogAndHandle(GetExceptionInformation(), "game loop")) { return false; }
}
bool SafeOnProperties(void(__stdcall* fn)(wchar_t*), wchar_t* text) {
    SEH_TRY { fn(text); return true; }
    SEH_EXCEPT(LogAndHandle(GetExceptionInformation(), "properties")) { return false; }
}
HMODULE SafeLoadLibrary(const wchar_t* path, DWORD* code) {
    SEH_TRY { return LoadLibraryW(path); }
    SEH_EXCEPT((*code = GetExceptionCode(), EXCEPTION_EXECUTE_HANDLER)) { return nullptr; }
}

// D2CLIENT_GetItemName as the mock game implements it.
void GameItemName(UnitAny* item, wchar_t* buffer, DWORD size) {
    using GetItemName_t = BOOL(__stdcall*)(UnitAny*, wchar_t*, DWORD);
    ((GetItemName_t)Game().Symbol("D2CLIENT_GetItemName"))(item, buffer, size);
}

template <typename Pred>
bool WaitFor(Pred pred, int timeoutMs) {
    auto end = std::chrono::steady_clock::now() + std::chrono::milliseconds(timeoutMs);
    while (!pred()) {
        if (std::chrono::steady_clock::now() > end) return false;
        std::this_thread::sleep_for(std::chrono::milliseconds(10));
    }
    return true;
}

FARPROC FindExport(HMODULE m, const char* plain, const char* decorated) {
    FARPROC p = GetProcAddress(m, plain);
    return p ? p : GetProcAddress(m, decorated);
}

HWND CreateHiddenGameWindow() {
    WNDCLASSW wc = {};
    wc.lpfnWndProc = DefWindowProcW;
    wc.hInstance = GetModuleHandleW(nullptr);
    wc.lpszClassName = L"Diablo II"; // the game's window class name
    RegisterClassW(&wc);
    return CreateWindowExW(0, wc.lpszClassName, L"Diablo II", WS_OVERLAPPEDWINDOW, 0, 0, 800, 600,
                           nullptr, nullptr, wc.hInstance, nullptr);
}

// The colour the game puts in front of an item's name after BH's hook has run.
int QualityColor(UnitAny* item, GameData& data) {
    ItemData* d = item->pItemData;
    if (d->dwFlags & ITEM_RUNEWORD) return 4;
    switch (d->dwQuality) {
    case 4: return 3;  // magic: blue
    case 5: return 2;  // set: green
    case 6: return 9;  // rare: yellow
    case 7: return 4;  // unique: gold
    case 8: return 8;  // crafted: orange
    default: break;
    }
    ItemsTxt* t = data.Item(item->dwTxtFileNo);
    if (t && t->bquest) return 4;
    if (d->dwFlags & (ITEM_ETHEREAL | ITEM_HASSOCKETS)) return 5; // grey
    return 0; // white
}

} // namespace

std::string ToUtf8(const std::wstring& s) {
    if (s.empty()) return {};
    int n = WideCharToMultiByte(CP_UTF8, 0, s.c_str(), (int)s.size(), nullptr, 0, nullptr, nullptr);
    std::string out(n, '\0');
    WideCharToMultiByte(CP_UTF8, 0, s.c_str(), (int)s.size(), out.data(), n, nullptr, nullptr);
    return out;
}

std::wstring FromUtf8(const std::string& s) {
    if (s.empty()) return {};
    int n = MultiByteToWideChar(CP_UTF8, 0, s.c_str(), (int)s.size(), nullptr, 0);
    std::wstring out(n, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, s.c_str(), (int)s.size(), out.data(), n);
    return out;
}

bool BHDriver::PrepareSandbox(const DriverOptions& o, std::string& error) {
    std::error_code ec;
    fs::path sandbox(o.sandboxDir);
    fs::create_directories(sandbox / "AppData", ec);
    fs::create_directories(sandbox / "filters" / "local", ec);

    fs::path install(o.installDir);
    fs::path bh = o.bhDll.empty() ? install / "BH.dll" : fs::path(o.bhDll);
    if (!fs::copy_file(bh, sandbox / "BH.dll", fs::copy_options::overwrite_existing, ec)) {
        error = "could not copy BH.dll from " + bh.string();
        return false;
    }
    // BH reads the game version from Game.exe in the working directory; it is only read.
    if (!fs::copy_file(install / "Game.exe", sandbox / "Game.exe", fs::copy_options::overwrite_existing, ec)) {
        error = "could not copy Game.exe (BH reads the game version from it)";
        return false;
    }
    if (!fs::copy_file(o.filterFile, sandbox / "filters" / "local" / "preview.filter", fs::copy_options::overwrite_existing, ec)) {
        error = "could not copy the filter file";
        return false;
    }
    // Point BH at the filter the same way the launcher does for a local filter.
    json launcher = { { "SelectedAuthorAndFilter", {
        { "selectedAuthor", { { "author", "Local Filter" } } },
        { "selectedFilter", { { "name", "preview.filter" } } } } } };
    std::ofstream(sandbox / "AppData" / "launcherSettings.json") << launcher.dump();

    // Start from the user's own BH settings so the preview matches what they see in game.
    json settings = json::object();
    std::ifstream userSettings(install / "BH.json");
    if (userSettings) settings = json::parse(userSettings, nullptr, false);
    if (settings.is_discarded() || !settings.is_object()) settings = json::object();
    settings["lootfilter"]["advanced_item_display"] = true;
    if (o.filterLevel >= 0) settings["lootfilter"]["filter_level"] = o.filterLevel;
    std::ofstream(sandbox / "BH.json") << settings.dump(2);
    return true;
}

bool BHDriver::LoadBH(std::string& error) {
    SetCurrentDirectoryW(options_.sandboxDir.c_str()); // BH.json, AppData, Game.exe are relative
    DWORD code = 0;
    std::wstring path = (fs::path(options_.sandboxDir) / "BH.dll").wstring();
    bh_ = SafeLoadLibrary(path.c_str(), &code);
    if (!bh_) {
        char buf[160];
        snprintf(buf, sizeof(buf), "BH.dll failed to load (error %lu, exception 0x%08lX)", GetLastError(), code);
        error = buf;
        return false;
    }
    auto isReady = (int(__cdecl*)())FindExport(bh_, "BHIsReady", "_BHIsReady");
    if (!isReady || !WaitFor([&] { return isReady() != 0; }, 10000)) {
        error = "BH.dll did not finish initializing";
        return false;
    }
    onProperties_ = (void(__stdcall*)(wchar_t*))FindExport(bh_, "BHOnProperties", "_BHOnProperties@4");
    return true;
}

void BHDriver::JoinGame() {
    // BH's GameThread notices the player unit and raises OnGameJoin; its Item module then
    // reads the data tables, the last of which is the magic affix table.
    world_->SetPlayerPresent(true);
    if (uint8_t* var = Game().Symbol("D2CLIENT_PlayerUnit")) *reinterpret_cast<UnitAny**>(var) = world_->Player();
    if (!WaitFor([] { return Ctx().affixTablesQueried; }, 10000)) Log("BH never finished joining the game");
    std::this_thread::sleep_for(std::chrono::milliseconds(100));
    TickGameLoop();
}

void BHDriver::TickGameLoop() {
    if (!SafeGameLoop(gameLoopSite_)) Log("BH crashed during the game loop");
}

bool BHDriver::Start(const DriverOptions& options, std::string& error) {
    options_ = options;
    if (!PrepareSandbox(options, error)) return false;
    if (!files_.Open(options.installDir, options.dataDir, options.sandboxDir, error)) return false;
    if (!data_.Load(files_, error)) return false;

    GameState state;
    state.Apply(options.initialState);
    world_ = std::make_unique<World>(data_);
    world_->Build(state);
    Ctx().data = &data_;
    Ctx().world = world_.get();
    Ctx().gameWindow = CreateHiddenGameWindow();

    if (!Game().Create(options.sandboxDir, options.installDir, error)) return false;
    Game().InitializeVariables(state);
    itemNameSite_ = Game().Resolve(Dll::D2CLIENT, kItemNameSite);
    gameLoopSite_ = Game().Resolve(Dll::D2CLIENT, kGameLoopSite);

    if (!LoadBH(error)) return false;
    if (itemNameSite_[0] != 0xE8) {
        error = "BH did not install its item name hook where the mock expects it (D2CLIENT+0x92366)";
        return false;
    }
    JoinGame();
    return true;
}

std::wstring BHDriver::EvaluateName(UnitAny* item, bool& crashed) {
    // The game fills the buffer with its own name for the item before calling the hook.
    std::vector<wchar_t> buffer(kNameBufferChars, L'\0');
    GameItemName(item, buffer.data(), kNameBufferChars);
    crashed = !SafeItemName(itemNameSite_, buffer.data(), item, kNameSizeGroundLabel);
    return buffer.data();
}

std::wstring BHDriver::EvaluateDescription(UnitAny* item, bool& crashed) {
    crashed = false;
    if (!onProperties_) return L"";
    if (uint8_t* var = Game().Symbol("D2CLIENT_SelectedInvItem")) *reinterpret_cast<UnitAny**>(var) = item;
    std::vector<wchar_t> text(kDescriptionChars, L'\0');
    crashed = !SafeOnProperties(onProperties_, text.data());
    if (uint8_t* var = Game().Symbol("D2CLIENT_SelectedInvItem")) *reinterpret_cast<UnitAny**>(var) = nullptr;
    return text.data();
}

json BHDriver::Evaluate(const json& request) {
    if (request.contains("state")) {
        GameState state = world_->State();
        state.Apply(request["state"]);
        world_->Update(state);
        Game().InitializeVariables(state);
    }
    {
        std::lock_guard<std::mutex> lock(Ctx().eventsLock);
        Ctx().printedMessages.clear();
        Ctx().soundsPlayed.clear();
    }
    TickGameLoop(); // also (re)builds BH's rules if it has not yet

    json results = json::array();
    for (const json& spec : request.value("items", json::array())) {
        json r = json::object();
        ItemSpec itemSpec;
        std::string error;
        if (!ParseItemSpec(spec, data_, itemSpec, error)) {
            r["error"] = error;
            results.push_back(r);
            continue;
        }
        // Items are never freed: BH keeps pointers to recently seen items between calls.
        UnitAny* item = world_->CreateItem(itemSpec);
        std::vector<wchar_t> gameName(kNameBufferChars, L'\0');
        GameItemName(item, gameName.data(), kNameBufferChars);
        bool nameCrashed = false, descCrashed = false;
        std::wstring name = EvaluateName(item, nameCrashed);
        std::wstring description = EvaluateDescription(item, descCrashed);
        r["gameName"] = ToUtf8(gameName.data());
        r["name"] = ToUtf8(name);
        r["hidden"] = name.empty();
        r["description"] = ToUtf8(description);
        r["qualityColor"] = QualityColor(item, data_);
        if (nameCrashed || descCrashed) r["error"] = nameCrashed ? "BH crashed computing the name" : "BH crashed computing the description";
        results.push_back(r);
    }

    json messages = json::array();
    json sounds = json::array();
    {
        std::lock_guard<std::mutex> lock(Ctx().eventsLock);
        for (auto& m : Ctx().printedMessages) messages.push_back(ToUtf8(m));
        for (int s : Ctx().soundsPlayed) sounds.push_back(s);
    }
    return json{ { "items", results }, { "messages", messages }, { "sounds", sounds } };
}

json BHDriver::Catalog() {
    json items = json::array();
    D2ItemDataTbl* tables = data_.ItemTables();
    for (int i = 0; i < tables->nItemsTxtRecordCount; i++) {
        ItemsTxt* t = tables->pItemsTxt + i;
        std::string code(t->szCode, strnlen(t->szCode, 4));
        while (!code.empty() && code.back() == ' ') code.pop_back();
        if (code.empty()) continue;
        D2ItemTypesTxt* type = data_.ItemType(t->nType);
        std::string typeCode = type ? std::string(type->szCode, strnlen(type->szCode, 4)) : "";
        while (!typeCode.empty() && typeCode.back() == ' ') typeCode.pop_back();
        const char* group = i < tables->nWeaponsTxtRecordCount ? "weapon"
                          : i < tables->nWeaponsTxtRecordCount + tables->nArmorTxtRecordCount ? "armor" : "misc";
        json item = { { "code", code }, { "name", ToUtf8(data_.LocaleText(t->wnamestr)) },
                      { "type", typeCode }, { "group", group }, { "level", t->blevel },
                      { "levelRequirement", t->blevelreq }, { "maxSockets", t->bgemsockets } };
        item.update(BaseModInfo(data_, i));
        items.push_back(item);
    }
    // Unique and set rows, so callers can preview named items by name.
    auto codeOf = [](const char* c) {
        std::string code(c, strnlen(c, 4));
        while (!code.empty() && code.back() == ' ') code.pop_back();
        return code;
    };
    json uniques = json::array();
    for (int i = 0; UniqueItemsTxt* u = data_.Unique(i); i++)
        uniques.push_back({ { "index", i }, { "code", codeOf(u->szCode) }, { "name", ToUtf8(data_.LocaleText(u->wTblIndex)) } });
    json sets = json::array();
    for (int i = 0; SetItemsTxt* si = data_.SetItem(i); i++)
        sets.push_back({ { "index", i }, { "code", codeOf(si->szCode) }, { "name", ToUtf8(data_.LocaleText((int)si->dwTblIndex)) } });
    json runewords = json::array();
    for (int i = 0; RunesTxt* r = data_.Rune(i); i++) {
        const wchar_t* name = data_.LocaleTextByKey(std::string(r->szName, strnlen(r->szName, sizeof(r->szName))));
        runewords.push_back({ { "index", i }, { "name", name ? ToUtf8(name) : std::string(r->szRuneName, strnlen(r->szRuneName, sizeof(r->szRuneName))) } });
    }
    json catalog = { { "items", items }, { "uniques", uniques }, { "sets", sets }, { "runewords", runewords } };
    catalog.update(BuildModCatalog(data_));
    return catalog;
}

json BHDriver::Describe() const {
    json archives = json::array();
    for (auto& a : files_.OpenedArchives()) archives.push_back(ToUtf8(a));
    return json{ { "archives", archives }, { "state", world_ ? world_->State().ToJson() : json() },
                 { "items", data_.ItemCount() } };
}

} // namespace mock

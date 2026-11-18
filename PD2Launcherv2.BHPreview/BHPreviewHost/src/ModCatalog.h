#pragma once
// What the launcher's item builder can put on an item, read from the installed tables:
// automagic rows (the "auto mods" a base can spawn with, e.g. all resistances on paladin
// shields), the class skills and skill tabs staff mods use, and per-base links to both.
#include "GameData.h"
#include "../../third_party/nlohmann/json.hpp"

namespace mock {

// { "autoMods": [...], "classes": [...], "skills": [...] }
nlohmann::json BuildModCatalog(GameData& data);

// Extra fields for one base in the catalog: "autoMods" (indices of the automagic rows it can
// spawn with) and "staffModClass" (class whose skills its staff mods use, or -1).
nlohmann::json BaseModInfo(GameData& data, int classId);

} // namespace mock

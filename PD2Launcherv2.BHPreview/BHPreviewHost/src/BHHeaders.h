#pragma once
// Game structure definitions come straight from the BH checkout being previewed
// (see BHSourceDir in the project file). Using BH's own headers means the mock lays
// out every structure exactly the way the BH.dll under test reads it.
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include "D2Structs.h"
#include "Constants.h"

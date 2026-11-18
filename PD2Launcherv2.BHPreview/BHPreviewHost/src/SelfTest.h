#pragma once
#include <string>
namespace mock {
// Returns 0 when every check passes. Prints one PASS/FAIL line per check to stdout.
int RunSelfTest(const std::wstring& sandbox);
}

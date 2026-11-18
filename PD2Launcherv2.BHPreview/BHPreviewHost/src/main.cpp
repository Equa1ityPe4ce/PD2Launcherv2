// BHPreviewHost: runs the user's real BH.dll inside a mock Diablo II and answers
// "what would BH show for this item?" for the launcher's loot filter preview.
//
// Usage:
//   BHPreviewHost.exe --selftest <scratch folder>   (checks the mock machinery only)
//   BHPreviewHost.exe --install <PD2 folder> --sandbox <scratch folder> --filter <file>
//                     [--bh <BH.dll>] [--data <extracted data folder>]
//                     [--filter-level N] [--state <json file>] [--log <file>]
//
// Protocol: once started it prints one JSON line {"ready":true,...} (or {"ready":false,
// "error":...} and exits). It then reads one JSON request per line on stdin and answers
// each with one JSON line on stdout, echoing the request's "id". Requests:
//   {"id":1,"op":"evaluate","state":{...},"items":[{"code":"cap","quality":"unique",...}]}
//   {"id":2,"op":"describe"}
//   {"id":3,"op":"catalog"}   every item base (code, name, type, ...) in the installed data
// Diagnostics go to stderr.
#include "BHDriver.h"
#include "Log.h"
#include "SelfTest.h"

#include <fcntl.h>
#include <io.h>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <string>

using nlohmann::json;

namespace {

// A crash on one of BH's own threads cannot be caught by the driver's SEH wrappers; log
// where it happened before the process dies.
LONG WINAPI LogCrash(EXCEPTION_POINTERS* info) {
    mock::LogException(info, "CRASH");
    return EXCEPTION_CONTINUE_SEARCH;
}

void WriteLine(const json& j) {
    std::string line = j.dump(-1, ' ', false, json::error_handler_t::replace);
    fwrite(line.data(), 1, line.size(), stdout);
    fputc('\n', stdout);
    fflush(stdout);
}

} // namespace

int wmain(int argc, wchar_t** argv) {
    _setmode(_fileno(stdout), _O_BINARY);
    _setmode(_fileno(stdin), _O_BINARY);
    SetUnhandledExceptionFilter(LogCrash);

    if (argc == 3 && std::wstring(argv[1]) == L"--selftest") return mock::RunSelfTest(argv[2]);

    mock::DriverOptions options;
    for (int i = 1; i + 1 < argc; i += 2) {
        std::wstring key = argv[i], value = argv[i + 1];
        if (key == L"--install") options.installDir = value;
        else if (key == L"--sandbox") options.sandboxDir = value;
        else if (key == L"--filter") options.filterFile = value;
        else if (key == L"--bh") options.bhDll = value;
        else if (key == L"--data") options.dataDir = value;
        else if (key == L"--filter-level") options.filterLevel = _wtoi(value.c_str());
        else if (key == L"--log") mock::SetLogFile(value.c_str());
        else if (key == L"--state") {
            std::ifstream in{ std::filesystem::path(value) };
            options.initialState = json::parse(in, nullptr, false);
            if (options.initialState.is_discarded()) options.initialState = json::object();
        }
    }
    if (options.installDir.empty() || options.sandboxDir.empty() || options.filterFile.empty()) {
        WriteLine({ { "ready", false }, { "error", "usage: --install <dir> --sandbox <dir> --filter <file>" } });
        return 2;
    }

    mock::BHDriver driver;
    std::string error;
    if (!driver.Start(options, error)) {
        mock::Log("start failed: %s", error.c_str());
        WriteLine({ { "ready", false }, { "error", error } });
        return 1;
    }
    WriteLine({ { "ready", true }, { "mock", driver.Describe() } });

    std::string line;
    while (std::getline(std::cin, line)) {
        if (line.empty() || line == "\r") continue;
        json request = json::parse(line, nullptr, false);
        if (request.is_discarded()) {
            WriteLine({ { "error", "request is not valid JSON" } });
            continue;
        }
        json response;
        try {
            std::string op = request.value("op", "evaluate");
            if (op == "evaluate") response = driver.Evaluate(request);
            else if (op == "describe") response = driver.Describe();
            else if (op == "catalog") response = driver.Catalog();
            else response = { { "error", "unknown op " + op } };
        } catch (const std::exception& e) {
            response = { { "error", e.what() } };
        }
        if (request.contains("id")) response["id"] = request["id"];
        WriteLine(response);
    }
    // BH has threads running inside the mock that never expect the game to end; skip
    // static destructors and DLL detach handlers entirely.
    TerminateProcess(GetCurrentProcess(), 0);
    return 0;
}

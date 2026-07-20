#pragma once

#include <string>

namespace mfg {

struct InstallResult {
    bool ok = false;
    std::string message;
};

InstallResult install_forwarder(
    const std::string& nsp_path,
    const std::string& expected_title_id);

} // namespace mfg

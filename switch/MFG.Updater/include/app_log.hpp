#pragma once

#include <string>

namespace mfg::log {

inline constexpr const char* Path = "sdmc:/switch/MFG/logs/mfg-updater.log";

void initialize();
void shutdown();
void info(const std::string& message);
void warning(const std::string& message);
void error(const std::string& message);
void result(const std::string& operation, unsigned int value);

} // namespace mfg::log

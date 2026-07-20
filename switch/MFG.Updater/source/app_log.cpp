#include "app_log.hpp"

#include <switch.h>

#include <cstdio>
#include <ctime>
#include <sys/stat.h>

namespace {

constexpr const char* LogDirectory = "sdmc:/switch/MFG/logs";
constexpr const char* PreviousPath = "sdmc:/switch/MFG/logs/mfg-updater.previous.log";
constexpr long MaximumLogSize = 512 * 1024;
FILE* log_file = nullptr;

void rotate_if_needed() {
    FILE* existing = std::fopen(mfg::log::Path, "rb");
    if (!existing)
        return;
    std::fseek(existing, 0, SEEK_END);
    const long size = std::ftell(existing);
    std::fclose(existing);
    if (size < MaximumLogSize)
        return;
    std::remove(PreviousPath);
    std::rename(mfg::log::Path, PreviousPath);
}

void write(const char* level, const std::string& message) {
    if (!log_file)
        return;
    std::time_t now = std::time(nullptr);
    std::tm local{};
    localtime_r(&now, &local);
    char timestamp[32]{};
    std::strftime(timestamp, sizeof(timestamp), "%Y-%m-%d %H:%M:%S", &local);
    std::fprintf(log_file, "%s [%s] %s\n", timestamp, level, message.c_str());
    std::fflush(log_file);
}

} // namespace

namespace mfg::log {

void initialize() {
    mkdir("sdmc:/switch/MFG", 0777);
    mkdir(LogDirectory, 0777);
    rotate_if_needed();
    log_file = std::fopen(Path, "ab");
    write("INFO", "MFG Updater 0.5.0 iniciado.");
    const u32 firmware_version = hosversionGet();
    char firmware[48]{};
    std::snprintf(firmware, sizeof(firmware), "Firmware detectado: %u.%u.%u.",
        HOSVER_MAJOR(firmware_version), HOSVER_MINOR(firmware_version), HOSVER_MICRO(firmware_version));
    write("INFO", firmware);
}

void shutdown() {
    write("INFO", "MFG Updater encerrado.");
    if (log_file) {
        std::fclose(log_file);
        log_file = nullptr;
    }
}

void info(const std::string& message) { write("INFO", message); }
void warning(const std::string& message) { write("WARN", message); }
void error(const std::string& message) { write("ERROR", message); }

void result(const std::string& operation, unsigned int value) {
    char message[256]{};
    std::snprintf(message, sizeof(message), "%s: 0x%08X.", operation.c_str(), value);
    write(value == 0 ? "INFO" : "ERROR", message);
}

} // namespace mfg::log

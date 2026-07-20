#pragma once

#include <string>
#include <vector>

namespace mfg {

struct Forwarder {
    std::string title_id;
    std::string name;
    std::string nsp_file_name;
    std::string sha256;
    bool file_available = false;
    bool hash_valid = false;
};

struct Manifest {
    int schema_version = 0;
    std::string updated_at;
    std::vector<Forwarder> games;
    std::vector<std::string> removed_title_ids;
};

struct ManifestResult {
    bool ok = false;
    std::string error;
    Manifest manifest;
};

ManifestResult load_manifest(const std::string& path, const std::string& forwarder_directory);

} // namespace mfg

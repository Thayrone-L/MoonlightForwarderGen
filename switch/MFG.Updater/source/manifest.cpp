#include "manifest.hpp"
#include "app_log.hpp"

#include <json-c/json.h>
#include <switch.h>

#include <array>
#include <cstdio>
#include <cctype>
#include <iomanip>
#include <memory>
#include <sstream>
#include <string>

namespace {

using JsonPtr = std::unique_ptr<json_object, decltype(&json_object_put)>;

bool read_text_file(const std::string& path, std::string& output) {
    std::unique_ptr<FILE, decltype(&std::fclose)> file(std::fopen(path.c_str(), "rb"), &std::fclose);
    if (!file)
        return false;
    std::fseek(file.get(), 0, SEEK_END);
    const long size = std::ftell(file.get());
    std::rewind(file.get());
    if (size < 0)
        return false;
    output.resize(static_cast<std::size_t>(size));
    return size == 0 || std::fread(output.data(), 1, output.size(), file.get()) == output.size();
}

bool file_exists(const std::string& path) {
    std::unique_ptr<FILE, decltype(&std::fclose)> file(std::fopen(path.c_str(), "rb"), &std::fclose);
    return file != nullptr;
}

bool verify_sha256(const std::string& path, const std::string& expected_hash) {
    std::unique_ptr<FILE, decltype(&std::fclose)> file(std::fopen(path.c_str(), "rb"), &std::fclose);
    if (!file)
        return false;

    Sha256Context context;
    sha256ContextCreate(&context);
    std::array<unsigned char, 128 * 1024> buffer{};
    while (true) {
        const std::size_t count = std::fread(buffer.data(), 1, buffer.size(), file.get());
        if (count > 0)
            sha256ContextUpdate(&context, buffer.data(), count);
        if (count < buffer.size()) {
            if (std::ferror(file.get()))
                return false;
            break;
        }
    }

    std::array<unsigned char, SHA256_HASH_SIZE> hash{};
    sha256ContextGetHash(&context, hash.data());
    std::ostringstream hexadecimal;
    hexadecimal << std::hex << std::setfill('0');
    for (const unsigned char byte : hash)
        hexadecimal << std::setw(2) << static_cast<unsigned int>(byte);

    std::string normalized_expected = expected_hash;
    for (char& character : normalized_expected)
        character = static_cast<char>(std::tolower(static_cast<unsigned char>(character)));
    return hexadecimal.str() == normalized_expected;
}

bool get_required_string(json_object* object, const char* key, std::string& output) {
    json_object* value = nullptr;
    if (!json_object_object_get_ex(object, key, &value) ||
        !json_object_is_type(value, json_type_string)) {
        return false;
    }
    const char* text = json_object_get_string(value);
    if (!text || text[0] == '\0')
        return false;
    output = text;
    return true;
}

bool is_safe_file_name(const std::string& value) {
    return !value.empty() && value.find('/') == std::string::npos &&
           value.find('\\') == std::string::npos && value != "." && value != "..";
}

bool is_hex(const std::string& value, std::size_t expected_length) {
    if (value.size() != expected_length)
        return false;
    for (const unsigned char character : value) {
        if (!std::isxdigit(character))
            return false;
    }
    return true;
}

} // namespace

namespace mfg {

ManifestResult load_manifest(const std::string& path, const std::string& forwarder_directory) {
    ManifestResult result;
    log::info("Carregando manifesto: " + path);
    std::string json_text;
    if (!read_text_file(path, json_text)) {
        result.error = "Nao foi possivel ler SD:/switch/MFG/manifest.json.";
        log::error(result.error);
        return result;
    }

    json_tokener* tokener = json_tokener_new();
    json_object* parsed = json_tokener_parse_ex(tokener, json_text.data(), json_text.size());
    const json_tokener_error parse_error = json_tokener_get_error(tokener);
    json_tokener_free(tokener);
    JsonPtr root(parsed, &json_object_put);
    if (!root || parse_error != json_tokener_success ||
        !json_object_is_type(root.get(), json_type_object)) {
        result.error = "O manifesto nao contem JSON valido.";
        log::error(result.error);
        return result;
    }

    json_object* schema = nullptr;
    if (!json_object_object_get_ex(root.get(), "schemaVersion", &schema) ||
        !json_object_is_type(schema, json_type_int) || json_object_get_int(schema) != 1) {
        result.error = "Versao de manifesto nao suportada. Sincronize novamente pelo MFG.";
        log::error(result.error);
        return result;
    }
    result.manifest.schema_version = json_object_get_int(schema);

    json_object* updated_at = nullptr;
    if (json_object_object_get_ex(root.get(), "updatedAt", &updated_at) &&
        json_object_is_type(updated_at, json_type_string))
        result.manifest.updated_at = json_object_get_string(updated_at);

    json_object* games = nullptr;
    if (!json_object_object_get_ex(root.get(), "games", &games) ||
        !json_object_is_type(games, json_type_array)) {
        result.error = "A lista de forwarders esta ausente no manifesto.";
        return result;
    }

    const std::size_t game_count = json_object_array_length(games);
    for (std::size_t index = 0; index < game_count; ++index) {
        json_object* item = json_object_array_get_idx(games, index);
        Forwarder forwarder;
        if (!json_object_is_type(item, json_type_object) ||
            !get_required_string(item, "titleId", forwarder.title_id) ||
            !get_required_string(item, "name", forwarder.name) ||
            !get_required_string(item, "nspFileName", forwarder.nsp_file_name) ||
            !get_required_string(item, "sha256", forwarder.sha256) ||
            !is_hex(forwarder.title_id, 16) || !is_hex(forwarder.sha256, 64) ||
            !is_safe_file_name(forwarder.nsp_file_name)) {
            result.error = "O manifesto contem uma entrada ausente ou insegura.";
            return result;
        }
        const std::string nsp_path = forwarder_directory + "/" + forwarder.nsp_file_name;
        forwarder.file_available = file_exists(nsp_path);
        forwarder.hash_valid = forwarder.file_available && verify_sha256(nsp_path, forwarder.sha256);
        if (!forwarder.file_available)
            log::warning("NSP ausente: " + forwarder.nsp_file_name);
        else if (!forwarder.hash_valid)
            log::warning("SHA-256 divergente: " + forwarder.nsp_file_name);
        else
            log::info("NSP validado: " + forwarder.nsp_file_name);
        result.manifest.games.push_back(std::move(forwarder));
    }

    json_object* removed = nullptr;
    if (json_object_object_get_ex(root.get(), "removedTitleIds", &removed) &&
        !json_object_is_type(removed, json_type_array)) {
        result.error = "A lista de remocoes do manifesto e invalida.";
        return result;
    }
    if (removed) {
        const std::size_t removed_count = json_object_array_length(removed);
        for (std::size_t index = 0; index < removed_count; ++index) {
            json_object* item = json_object_array_get_idx(removed, index);
            if (!json_object_is_type(item, json_type_string) ||
                !is_hex(json_object_get_string(item), 16)) {
                result.error = "O manifesto contem um Title ID de remocao invalido.";
                return result;
            }
            result.manifest.removed_title_ids.emplace_back(json_object_get_string(item));
        }
    }

    result.ok = true;
    log::info("Manifesto carregado com " + std::to_string(result.manifest.games.size()) + " jogo(s).");
    return result;
}

} // namespace mfg

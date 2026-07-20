#include "installer.hpp"
#include "app_log.hpp"

#include <switch.h>

#include <algorithm>
#include <array>
#include <cctype>
#include <cstdio>
#include <cstring>
#include <memory>
#include <string>
#include <vector>

namespace {

constexpr u32 Pfs0Magic = 0x30534650;
constexpr std::size_t CopyBufferSize = 1024 * 1024;

struct Pfs0Header {
    u32 magic;
    u32 file_count;
    u32 string_table_size;
    u32 reserved;
} NX_PACKED;

struct Pfs0RawEntry {
    u64 data_offset;
    u64 file_size;
    u32 string_offset;
    u32 reserved;
} NX_PACKED;

struct Pfs0Entry {
    u64 offset;
    u64 size;
    std::string name;
};

struct PackagedContentMetaHeader {
    u64 title_id;
    u32 version;
    u8 type;
    u8 reserved_0d;
    u16 extended_header_size;
    u16 content_count;
    u16 content_meta_count;
    u8 attributes;
    u8 storage_id;
    u8 install_type;
    u8 committed;
    u32 required_system_version;
    u32 reserved_1c;
} NX_PACKED;

static_assert(sizeof(Pfs0Header) == 0x10);
static_assert(sizeof(Pfs0RawEntry) == 0x18);
static_assert(sizeof(PackagedContentMetaHeader) == 0x20);

struct ParsedNsp {
    u64 data_offset = 0;
    std::vector<Pfs0Entry> entries;
};

struct NsContentStorageRecord {
    NcmContentMetaKey meta_record;
    u64 storage_id;
};

using FilePtr = std::unique_ptr<FILE, decltype(&std::fclose)>;

std::string result_text(const char* operation, Result result) {
    char buffer[160];
    std::snprintf(buffer, sizeof(buffer), "%s (erro 0x%08X).", operation, result);
    return buffer;
}

bool add_overflows(u64 left, u64 right) {
    return right > UINT64_MAX - left;
}

bool read_exact(FILE* file, u64 offset, void* output, std::size_t size) {
    if (offset > static_cast<u64>(INT64_MAX) ||
        fseeko(file, static_cast<off_t>(offset), SEEK_SET) != 0)
        return false;
    return size == 0 || std::fread(output, 1, size, file) == size;
}

bool parse_pfs0(FILE* file, ParsedNsp& output, std::string& error) {
    Pfs0Header header{};
    if (!read_exact(file, 0, &header, sizeof(header)) || header.magic != Pfs0Magic) {
        error = "O arquivo nao possui um cabecalho PFS0 valido.";
        return false;
    }
    if (header.file_count == 0 || header.file_count > 32 ||
        header.string_table_size == 0 || header.string_table_size > 64 * 1024) {
        error = "A tabela PFS0 excede os limites aceitos pelo MFG.";
        return false;
    }

    const u64 entries_size = static_cast<u64>(header.file_count) * sizeof(Pfs0RawEntry);
    const u64 string_offset = sizeof(Pfs0Header) + entries_size;
    output.data_offset = string_offset + header.string_table_size;
    std::vector<Pfs0RawEntry> entries(header.file_count);
    std::vector<char> strings(header.string_table_size);
    if (!read_exact(file, sizeof(Pfs0Header), entries.data(), entries_size) ||
        !read_exact(file, string_offset, strings.data(), strings.size())) {
        error = "Nao foi possivel ler as tabelas do NSP.";
        return false;
    }

    if (fseeko(file, 0, SEEK_END) != 0) {
        error = "Nao foi possivel medir o NSP.";
        return false;
    }
    const off_t measured_size = ftello(file);
    if (measured_size < 0) {
        error = "O tamanho do NSP e invalido.";
        return false;
    }
    const u64 file_size = static_cast<u64>(measured_size);

    for (const auto& entry : entries) {
        if (entry.string_offset >= strings.size() ||
            std::find(strings.begin() + entry.string_offset, strings.end(), '\0') == strings.end() ||
            add_overflows(output.data_offset, entry.data_offset) ||
            add_overflows(output.data_offset + entry.data_offset, entry.file_size) ||
            output.data_offset + entry.data_offset + entry.file_size > file_size) {
            error = "O NSP contem uma entrada PFS0 fora dos limites.";
            return false;
        }
        const std::string name(strings.data() + entry.string_offset);
        if (name.empty() || name.find('/') != std::string::npos || name.find('\\') != std::string::npos) {
            error = "O NSP contem um nome de arquivo inseguro.";
            return false;
        }
        output.entries.push_back({output.data_offset + entry.data_offset, entry.file_size, name});
    }
    return true;
}

bool ends_with(const std::string& value, const std::string& suffix) {
    return value.size() >= suffix.size() &&
           value.compare(value.size() - suffix.size(), suffix.size(), suffix) == 0;
}

bool parse_content_id(const std::string& name, NcmContentId& output) {
    const std::size_t separator = name.find('.');
    if (separator != 32)
        return false;
    for (std::size_t index = 0; index < 16; ++index) {
        const auto hex_value = [](char character) -> int {
            if (character >= '0' && character <= '9') return character - '0';
            character = static_cast<char>(std::tolower(static_cast<unsigned char>(character)));
            if (character >= 'a' && character <= 'f') return 10 + character - 'a';
            return -1;
        };
        const int high = hex_value(name[index * 2]);
        const int low = hex_value(name[index * 2 + 1]);
        if (high < 0 || low < 0)
            return false;
        output.c[index] = static_cast<u8>((high << 4) | low);
    }
    return true;
}

const Pfs0Entry* find_nca(const ParsedNsp& nsp, const NcmContentId& id) {
    static constexpr char Hex[] = "0123456789abcdef";
    std::string prefix(32, '0');
    for (std::size_t index = 0; index < 16; ++index) {
        prefix[index * 2] = Hex[id.c[index] >> 4];
        prefix[index * 2 + 1] = Hex[id.c[index] & 0xF];
    }
    for (const auto& entry : nsp.entries) {
        std::string lowered = entry.name;
        std::transform(lowered.begin(), lowered.end(), lowered.begin(),
            [](unsigned char value) { return static_cast<char>(std::tolower(value)); });
        if (lowered == prefix + ".nca" || lowered == prefix + ".cnmt.nca")
            return &entry;
    }
    return nullptr;
}

Result install_nca(FILE* nsp_file, const Pfs0Entry& entry, const NcmContentId& id,
                   NcmContentStorage& storage) {
    bool present = false;
    Result result = ncmContentStorageHas(&storage, &present, &id);
    if (R_FAILED(result) || present)
        return result;

    NcmPlaceHolderId placeholder{};
    result = ncmContentStorageGeneratePlaceHolderId(&storage, &placeholder);
    if (R_FAILED(result))
        return result;
    result = ncmContentStorageCreatePlaceHolder(&storage, &id, &placeholder, entry.size);
    if (R_FAILED(result))
        return result;

    auto buffer = std::make_unique<unsigned char[]>(CopyBufferSize);
    u64 copied = 0;
    while (copied < entry.size) {
        const std::size_t count = static_cast<std::size_t>(
            std::min<u64>(CopyBufferSize, entry.size - copied));
        if (!read_exact(nsp_file, entry.offset + copied, buffer.get(), count)) {
            ncmContentStorageDeletePlaceHolder(&storage, &placeholder);
            return MAKERESULT(Module_Libnx, LibnxError_IoError);
        }
        result = ncmContentStorageWritePlaceHolder(&storage, &placeholder, copied, buffer.get(), count);
        if (R_FAILED(result)) {
            ncmContentStorageDeletePlaceHolder(&storage, &placeholder);
            return result;
        }
        copied += count;
    }

    result = ncmContentStorageFlushPlaceHolder(&storage);
    if (R_SUCCEEDED(result))
        result = ncmContentStorageRegister(&storage, &id, &placeholder);
    if (R_FAILED(result))
        ncmContentStorageDeletePlaceHolder(&storage, &placeholder);
    return result;
}

bool read_cnmt(const char* nca_path, std::vector<unsigned char>& output, std::string& error) {
    FsFileSystem file_system{};
    Result result = fsOpenFileSystemWithId(
        &file_system, 0, FsFileSystemType_ContentMeta, nca_path, FsContentAttributes_All);
    if (R_FAILED(result)) {
        error = result_text("Nao foi possivel abrir o CNMT", result);
        return false;
    }

    FsDir directory{};
    result = fsFsOpenDirectory(&file_system, "/", FsDirOpenMode_ReadFiles, &directory);
    if (R_FAILED(result)) {
        fsFsClose(&file_system);
        error = result_text("Nao foi possivel listar o CNMT", result);
        return false;
    }
    std::array<FsDirectoryEntry, 8> entries{};
    s64 count = 0;
    result = fsDirRead(&directory, &count, entries.size(), entries.data());
    fsDirClose(&directory);
    if (R_FAILED(result)) {
        fsFsClose(&file_system);
        error = result_text("Nao foi possivel localizar o CNMT", result);
        return false;
    }

    std::string cnmt_name;
    for (s64 index = 0; index < count; ++index) {
        if (ends_with(entries[index].name, ".cnmt")) {
            cnmt_name = std::string("/") + entries[index].name;
            break;
        }
    }
    if (cnmt_name.empty()) {
        fsFsClose(&file_system);
        error = "O NCA meta nao contem um arquivo CNMT.";
        return false;
    }

    FsFile file{};
    result = fsFsOpenFile(&file_system, cnmt_name.c_str(), FsOpenMode_Read, &file);
    if (R_FAILED(result)) {
        fsFsClose(&file_system);
        error = result_text("Nao foi possivel abrir o arquivo CNMT", result);
        return false;
    }
    s64 size = 0;
    result = fsFileGetSize(&file, &size);
    if (R_SUCCEEDED(result) && size >= static_cast<s64>(sizeof(PackagedContentMetaHeader)) &&
        size <= 1024 * 1024) {
        output.resize(static_cast<std::size_t>(size));
        u64 read = 0;
        result = fsFileRead(&file, 0, output.data(), output.size(), FsReadOption_None, &read);
        if (R_SUCCEEDED(result) && read != output.size())
            result = MAKERESULT(Module_Libnx, LibnxError_IoError);
    } else if (R_SUCCEEDED(result)) {
        result = MAKERESULT(Module_Libnx, LibnxError_BadInput);
    }
    fsFileClose(&file);
    fsFsClose(&file_system);
    if (R_FAILED(result)) {
        error = result_text("Nao foi possivel ler o arquivo CNMT", result);
        return false;
    }
    return true;
}

bool parse_title_id(const std::string& text, u64& output) {
    if (text.size() != 16)
        return false;
    output = 0;
    for (char character : text) {
        int value = -1;
        if (character >= '0' && character <= '9') value = character - '0';
        else {
            character = static_cast<char>(std::tolower(static_cast<unsigned char>(character)));
            if (character >= 'a' && character <= 'f') value = 10 + character - 'a';
        }
        if (value < 0)
            return false;
        output = (output << 4) | static_cast<u64>(value);
    }
    return true;
}

Result push_application_record(Service& manager, u64 title_id,
                               const NcmContentMetaKey& key) {
    struct {
        u8 event;
        u8 padding[7];
        u64 application_id;
    } input{3, {}, title_id};
    NsContentStorageRecord record{key, NcmStorageId_SdCard};
    return serviceDispatchIn(&manager, 16, input,
        .buffer_attrs = {SfBufferAttr_HipcMapAlias | SfBufferAttr_In},
        .buffers = {{&record, sizeof(record)}});
}

} // namespace

namespace mfg {

InstallResult install_forwarder(const std::string& nsp_path,
                                const std::string& expected_title_id) {
    InstallResult output;
    log::info("Instalacao iniciada. Title ID: " + expected_title_id + ", arquivo: " + nsp_path);
    FilePtr file(std::fopen(nsp_path.c_str(), "rb"), &std::fclose);
    if (!file) {
        output.message = "Nao foi possivel abrir o NSP.";
        return output;
    }

    ParsedNsp nsp;
    if (!parse_pfs0(file.get(), nsp, output.message))
    {
        log::error(output.message);
        return output;
    }
    log::info("PFS0 validado com " + std::to_string(nsp.entries.size()) + " arquivo(s).");
    const Pfs0Entry* meta_entry = nullptr;
    NcmContentId meta_id{};
    for (const auto& entry : nsp.entries) {
        if (ends_with(entry.name, ".cnmt.nca")) {
            if (meta_entry || !parse_content_id(entry.name, meta_id)) {
                output.message = "O NSP deve conter exatamente um CNMT NCA valido.";
                return output;
            }
            meta_entry = &entry;
        } else if (!ends_with(entry.name, ".nca")) {
            output.message = "O NSP contem arquivos nao aceitos pelo instalador MFG.";
            return output;
        }
    }
    if (!meta_entry) {
        output.message = "O NSP nao contem um CNMT NCA.";
        return output;
    }

    Result result = ncmInitialize();
    log::result("ncmInitialize", result);
    if (R_FAILED(result)) {
        output.message = result_text("O servico NCM nao esta disponivel", result);
        return output;
    }

    NcmContentStorage storage{};
    NcmContentMetaDatabase database{};
    Service manager{};
    bool storage_open = false;
    bool database_open = false;
    bool manager_open = false;
    bool ns_initialized = false;

    result = ncmOpenContentStorage(&storage, NcmStorageId_SdCard);
    log::result("ncmOpenContentStorage(SD)", result);
    storage_open = R_SUCCEEDED(result);
    if (R_SUCCEEDED(result))
        result = install_nca(file.get(), *meta_entry, meta_id, storage);
    log::result("Instalacao do CNMT NCA", result);

    char meta_path[FS_MAX_PATH]{};
    if (R_SUCCEEDED(result))
        result = ncmContentStorageGetPath(&storage, meta_path, sizeof(meta_path), &meta_id);

    std::vector<unsigned char> cnmt;
    if (R_SUCCEEDED(result) && !read_cnmt(meta_path, cnmt, output.message))
        result = MAKERESULT(Module_Libnx, LibnxError_BadInput);

    u64 expected_id = 0;
    PackagedContentMetaHeader* header = nullptr;
    if (R_SUCCEEDED(result)) {
        header = reinterpret_cast<PackagedContentMetaHeader*>(cnmt.data());
        if (!parse_title_id(expected_title_id, expected_id) ||
            header->title_id != expected_id ||
            header->type != NcmContentMetaType_Application ||
            header->content_count == 0 || header->content_count > 16 ||
            header->content_meta_count != 0 ||
            sizeof(*header) + header->extended_header_size +
                static_cast<u64>(header->content_count) * sizeof(NcmPackagedContentInfo) > cnmt.size()) {
            output.message = "O CNMT nao corresponde ao forwarder MFG selecionado.";
            result = MAKERESULT(Module_Libnx, LibnxError_BadInput);
        }
    }

    std::vector<NcmContentInfo> content_infos;
    if (R_SUCCEEDED(result)) {
        auto* packaged = reinterpret_cast<NcmPackagedContentInfo*>(
            cnmt.data() + sizeof(*header) + header->extended_header_size);
        for (u16 index = 0; index < header->content_count; ++index) {
            if (packaged[index].info.content_type > NcmContentType_LegalInformation ||
                !find_nca(nsp, packaged[index].info.content_id)) {
                output.message = "O NSP nao contem todos os NCAs declarados no CNMT.";
                result = MAKERESULT(Module_Libnx, LibnxError_BadInput);
                break;
            }
            content_infos.push_back(packaged[index].info);
        }
    }

    if (R_SUCCEEDED(result)) {
        for (const auto& info : content_infos) {
            const Pfs0Entry* entry = find_nca(nsp, info.content_id);
            result = install_nca(file.get(), *entry, info.content_id, storage);
            log::result("Instalacao de content NCA", result);
            if (R_FAILED(result)) {
                output.message = result_text("Falha ao instalar um NCA", result);
                break;
            }
        }
    }

    NcmContentMetaKey key{};
    std::vector<unsigned char> install_meta;
    if (R_SUCCEEDED(result)) {
        key.id = header->title_id;
        key.version = header->version;
        key.type = header->type;
        key.install_type = NcmContentInstallType_Full;

        NcmContentMetaHeader install_header{};
        install_header.extended_header_size = header->extended_header_size;
        install_header.content_count = static_cast<u16>(content_infos.size() + 1);
        install_header.content_meta_count = 0;
        install_header.attributes = header->attributes;
        install_header.storage_id = NcmStorageId_None;
        install_meta.resize(sizeof(install_header) + header->extended_header_size +
                            install_header.content_count * sizeof(NcmContentInfo));
        std::memcpy(install_meta.data(), &install_header, sizeof(install_header));
        std::memcpy(install_meta.data() + sizeof(install_header),
                    cnmt.data() + sizeof(*header), header->extended_header_size);

        NcmContentInfo meta_info{};
        meta_info.content_id = meta_id;
        ncmU64ToContentInfoSize(meta_entry->size, &meta_info);
        meta_info.content_type = NcmContentType_Meta;
        auto* destination = reinterpret_cast<NcmContentInfo*>(
            install_meta.data() + sizeof(install_header) + header->extended_header_size);
        destination[0] = meta_info;
        std::copy(content_infos.begin(), content_infos.end(), destination + 1);

        result = ncmOpenContentMetaDatabase(&database, NcmStorageId_SdCard);
        log::result("ncmOpenContentMetaDatabase(SD)", result);
        database_open = R_SUCCEEDED(result);
        if (R_SUCCEEDED(result))
            result = ncmContentMetaDatabaseSet(&database, &key, install_meta.data(), install_meta.size());
        if (R_SUCCEEDED(result))
            result = ncmContentMetaDatabaseCommit(&database);
        log::result("Commit do ContentMetaDatabase", result);
        if (R_FAILED(result))
            output.message = result_text("Falha ao registrar o conteudo", result);
    }

    if (R_SUCCEEDED(result)) {
        result = nsInitialize();
        log::result("nsInitialize", result);
        ns_initialized = R_SUCCEEDED(result);
        if (R_SUCCEEDED(result) && hosversionAtLeast(3, 0, 0)) {
            result = nsGetApplicationManagerInterface(&manager);
            manager_open = R_SUCCEEDED(result);
        } else if (R_SUCCEEDED(result)) {
            manager = *nsGetServiceSession_ApplicationManagerInterface();
        }
        if (R_SUCCEEDED(result))
            result = push_application_record(manager, expected_id, key);
        log::result("Publicacao no menu HOME", result);
        if (R_FAILED(result))
            output.message = result_text("Falha ao publicar o forwarder no menu HOME", result);
    }

    if (manager_open)
        serviceClose(&manager);
    if (ns_initialized)
        nsExit();
    if (database_open)
        ncmContentMetaDatabaseClose(&database);
    if (storage_open)
        ncmContentStorageClose(&storage);
    ncmExit();

    if (R_SUCCEEDED(result)) {
        output.ok = true;
        output.message = "Forwarder instalado no cartao SD. Volte ao menu HOME para usa-lo.";
    } else if (output.message.empty()) {
        output.message = result_text("A instalacao falhou", result);
    }
    if (output.ok)
        log::info("Instalacao concluida. Title ID: " + expected_title_id);
    else
        log::error("Instalacao encerrada com falha: " + output.message);
    return output;
}

} // namespace mfg

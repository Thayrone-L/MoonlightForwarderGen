#include "installer.hpp"
#include "manifest.hpp"
#include "app_log.hpp"

#include <borealis.hpp>

#include <cstdlib>
#include <string>

namespace {

constexpr const char* ManifestPath = "sdmc:/switch/MFG/manifest.json";
constexpr const char* ForwarderDirectory = "sdmc:/switch/MFG";
constexpr float LibraryContentWidth = 694.0f;
constexpr float DetachedContentOffset = 48.0f;

const NVGcolor Background = nvgRGB(16, 19, 26);
const NVGcolor Surface = nvgRGB(25, 30, 40);
const NVGcolor SurfaceRaised = nvgRGB(36, 43, 56);
const NVGcolor Text = nvgRGB(244, 247, 251);
const NVGcolor Muted = nvgRGB(156, 168, 186);
const NVGcolor Purple = nvgRGB(124, 92, 252);
const NVGcolor Green = nvgRGB(85, 214, 160);
const NVGcolor Red = nvgRGB(212, 134, 140);

brls::Label* make_label(const std::string& text, float size, NVGcolor color) {
    auto* label = new brls::Label();
    label->setText(text);
    label->setFontSize(size);
    label->setTextColor(color);
    label->setHorizontalAlign(brls::HorizontalAlign::LEFT);
    return label;
}

std::string cover_path(const mfg::Forwarder& game) {
    return game.cover_file_name.empty()
        ? std::string()
        : std::string(ForwarderDirectory) + "/" + game.cover_file_name;
}

void load_cover(brls::Image* image, const mfg::Forwarder& game) {
    if (game.cover_file_name.empty())
        image->setImageFromRes("img/mfg-default.jpg");
    else
        image->setImageFromFile(cover_path(game));
}

class GameRow final : public brls::Box {
  public:
    std::function<void()> focused;

    explicit GameRow(const mfg::Forwarder& game) : brls::Box(brls::Axis::ROW) {
        setFocusable(true);
        setWidth(LibraryContentWidth - DetachedContentOffset);
        setHeight(66);
        setPadding(7, 14, 7, 8);
        setMarginBottom(8);
        setAlignItems(brls::AlignItems::CENTER);
        setBackgroundColor(SurfaceRaised);
        setCornerRadius(11);

        auto* art = new brls::Image();
        art->setWidth(52);
        art->setHeight(52);
        art->setShrink(0);
        art->setScalingType(brls::ImageScalingType::CROP);
        load_cover(art, game);
        addView(art);

        auto* copy = new brls::Box(brls::Axis::COLUMN);
        copy->setGrow(1);
        copy->setMarginLeft(13);
        copy->setJustifyContent(brls::JustifyContent::CENTER);
        auto* name = make_label(game.name, 19, Text);
        name->setSingleLine(true);
        copy->addView(name);
        auto* state = make_label(game.hash_valid ? "Pronto" : "Sincronize pelo computador",
                                 14, game.hash_valid ? Green : Red);
        state->setMarginTop(2);
        copy->addView(state);
        addView(copy);
    }

    void onFocusGained() override {
        brls::Box::onFocusGained();
        setBackgroundColor(nvgRGB(48, 40, 79));
        if (focused)
            focused();
    }

    void onFocusLost() override {
        brls::Box::onFocusLost();
        setBackgroundColor(SurfaceRaised);
    }
};

class MainActivity final : public brls::Activity {
  public:
    MainActivity() : result(mfg::load_manifest(ManifestPath, ForwarderDirectory)) {}

    brls::View* createContentView() override {
        auto* root = new brls::Box(brls::Axis::COLUMN);
        root->setBackgroundColor(Background);

        auto* header = new brls::Box(brls::Axis::ROW);
        header->setHeight(76);
        header->setPadding(0, 46, 0, 46);
        header->setAlignItems(brls::AlignItems::CENTER);
        header->setBackgroundColor(nvgRGB(12, 15, 21));
        header->addView(make_label("Moonlight Forwarder", 27, Text));
        auto* header_spacer = new brls::Padding();
        header->addView(header_spacer);
        auto* ready = make_label(result.ok ? "Biblioteca pronta  ·  v0.5.4" : "Sincronize pelo computador  ·  v0.5.4",
                                 16, result.ok ? Green : Red);
        ready->setHorizontalAlign(brls::HorizontalAlign::RIGHT);
        header->addView(ready);
        root->addView(header);

        auto* page = new brls::Box(brls::Axis::ROW);
        page->setGrow(1);
        page->setBackgroundColor(Background);
        page->setPadding(22, 48, 22, 48);

        auto* library = new brls::Box(brls::Axis::COLUMN);
        library->setGrow(1);
        library->setMarginRight(22);
        library->setBackgroundColor(Surface);
        library->setCornerRadius(18);
        library->setPadding(24);
        library->addView(make_label("Seus jogos", 29, Text));
        auto* subtitle = make_label("Escolha um jogo. O resto acontece sozinho.", 17, Muted);
        subtitle->setMarginTop(5);
        subtitle->setMarginBottom(18);
        library->addView(subtitle);

        if (!result.ok) {
            auto* empty = new brls::Box(brls::Axis::COLUMN);
            empty->setBackgroundColor(Surface);
            empty->setCornerRadius(16);
            empty->setPadding(32);
            empty->addView(make_label("Biblioteca ainda nao encontrada", 25, Text));
            auto* explanation = make_label(
                "Conecte o cartao SD ao computador e abra o MFG. Seus jogos serao copiados automaticamente.",
                18, Muted);
            explanation->setMarginTop(14);
            empty->addView(explanation);
            library->addView(empty);
        } else if (result.manifest.games.empty()) {
            auto* empty = new brls::Box(brls::Axis::COLUMN);
            empty->setBackgroundColor(Surface);
            empty->setCornerRadius(16);
            empty->setPadding(32);
            empty->addView(make_label("Sua biblioteca esta vazia", 25, Text));
            auto* explanation = make_label("Escolha um jogo no aplicativo do computador para comecar.", 18, Muted);
            explanation->setMarginTop(14);
            empty->addView(explanation);
            library->addView(empty);
        } else {
            auto* scroller = new brls::ScrollingFrame();
            scroller->setGrow(1);
            scroller->setWidth(LibraryContentWidth);
            scroller->setMarginTop(12);
            scroller->setScrollingBehavior(brls::ScrollingBehavior::NATURAL);
            auto* rows = new brls::Box(brls::Axis::COLUMN);
            rows->setWidth(LibraryContentWidth - DetachedContentOffset);
            rows->setTranslationX(DetachedContentOffset);
            rows->setAlignItems(brls::AlignItems::STRETCH);
            rows->setPaddingTop(18);
            rows->setHeight(static_cast<float>(result.manifest.games.size() * 74 + 18));
            for (std::size_t index = 0; index < result.manifest.games.size(); ++index) {
                const auto& game = result.manifest.games[index];
                auto* row = new GameRow(game);
                row->focused = [this, index]() { select_game(index); };
                row->registerClickAction([this, index](brls::View*) {
                    select_game(index);
                    return true;
                });
                rows->addView(row);
            }
            scroller->setContentView(rows);
            library->addView(scroller);
        }

        page->addView(library);
        page->addView(build_detail_panel());
        root->addView(page);

        auto* footer = new brls::Box(brls::Axis::ROW);
        footer->setHeight(54);
        footer->setPadding(0, 46, 0, 46);
        footer->setAlignItems(brls::AlignItems::CENTER);
        footer->setBackgroundColor(nvgRGB(12, 15, 21));
        footer->addView(make_label("A  Selecionar", 15, Muted));
        auto* footer_spacer = new brls::Padding();
        footer->addView(footer_spacer);
        footer->addView(make_label("X  Atualizar     +  Sair", 15, Muted));
        root->addView(footer);
        return root;
    }

    void onContentAvailable() override {
        registerAction("Atualizar", brls::BUTTON_X, [](brls::View*) {
            brls::Application::pushActivity(new MainActivity(), brls::TransitionAnimation::FADE);
            return true;
        }, true);
        registerAction("Sair", brls::BUTTON_START, [](brls::View*) {
            brls::Application::quit();
            return true;
        }, true);
    }

  private:
    mfg::ManifestResult result;
    std::size_t selected_index = 0;
    bool awaiting_confirmation = false;
    brls::Label* selected_title = nullptr;
    brls::Label* selected_status = nullptr;
    brls::Image* selected_cover = nullptr;
    brls::Button* install_button = nullptr;

    brls::View* build_detail_panel() {
        auto* detail = new brls::Box(brls::Axis::COLUMN);
        detail->setWidth(420);
        detail->setBackgroundColor(Surface);
        detail->setCornerRadius(18);
        detail->setPadding(34);
        detail->setAlignItems(brls::AlignItems::CENTER);
        detail->setJustifyContent(brls::JustifyContent::CENTER);

        const bool has_game = result.ok && !result.manifest.games.empty();
        selected_cover = new brls::Image();
        selected_cover->setWidth(180);
        selected_cover->setHeight(180);
        selected_cover->setShrink(0);
        selected_cover->setScalingType(brls::ImageScalingType::CROP);
        if (has_game)
            load_cover(selected_cover, result.manifest.games[0]);
        else
            selected_cover->setImageFromRes("img/mfg-default.jpg");
        detail->addView(selected_cover);

        selected_title = make_label(has_game ? result.manifest.games[0].name : "Tudo pronto por aqui", 29, Text);
        selected_title->setWidth(340);
        selected_title->setMarginTop(18);
        selected_title->setHorizontalAlign(brls::HorizontalAlign::CENTER);
        detail->addView(selected_title);

        selected_status = make_label(has_game ? status_for(result.manifest.games[0]) :
            "Prepare seus jogos no computador e volte ao Switch.", 18, Muted);
        selected_status->setWidth(340);
        selected_status->setMarginTop(12);
        selected_status->setMarginBottom(26);
        selected_status->setHorizontalAlign(brls::HorizontalAlign::CENTER);
        detail->addView(selected_status);

        install_button = new brls::Button();
        install_button->setText("Adicionar ao Switch");
        install_button->setStyle(&brls::BUTTONSTYLE_PRIMARY);
        install_button->setWidth(340);
        install_button->setHeight(58);
        install_button->setState(has_game && result.manifest.games[0].hash_valid
            ? brls::ButtonState::ENABLED : brls::ButtonState::DISABLED);
        install_button->registerClickAction([this](brls::View*) { return install_selected(); });
        detail->addView(install_button);
        return detail;
    }

    std::string status_for(const mfg::Forwarder& game) const {
        return game.hash_valid
            ? "Pronto para virar um atalho no seu Switch."
            : "Sincronize este jogo novamente no computador.";
    }

    void select_game(std::size_t index) {
        if (!result.ok || index >= result.manifest.games.size())
            return;
        selected_index = index;
        awaiting_confirmation = false;
        const auto& game = result.manifest.games[index];
        load_cover(selected_cover, game);
        selected_title->setText(game.name);
        selected_status->setText(status_for(game));
        selected_status->setTextColor(game.hash_valid ? Muted : Red);
        install_button->setText("Adicionar ao Switch");
        install_button->setState(game.hash_valid ? brls::ButtonState::ENABLED : brls::ButtonState::DISABLED);
    }

    bool install_selected() {
        if (!result.ok || selected_index >= result.manifest.games.size())
            return false;
        const auto& game = result.manifest.games[selected_index];
        mfg::log::info("Jogo selecionado: " + game.name + " (" + game.title_id + ").");
        if (!game.hash_valid) {
            brls::Application::notify("Sincronize este jogo novamente no computador.");
            return true;
        }
        if (!awaiting_confirmation) {
            awaiting_confirmation = true;
            selected_status->setText("Pressione A novamente para confirmar a instalacao.");
            selected_status->setTextColor(Purple);
            install_button->setText("Confirmar instalacao");
            return true;
        }

        awaiting_confirmation = false;
        selected_status->setText("Instalando... nao remova o cartao SD.");
        selected_status->setTextColor(Muted);
        install_button->setState(brls::ButtonState::DISABLED);
        const auto install_result = mfg::install_forwarder(
            std::string(ForwarderDirectory) + "/" + game.nsp_file_name, game.title_id);
        selected_status->setText(install_result.message);
        selected_status->setTextColor(install_result.ok ? Green : Red);
        install_button->setText(install_result.ok ? "Instalado" : "Tentar novamente");
        install_button->setState(install_result.ok ? brls::ButtonState::DISABLED : brls::ButtonState::ENABLED);
        brls::Application::notify(install_result.message);
        return true;
    }
};

} // namespace

int main(int, char**) {
    mfg::log::initialize();
    brls::Logger::setLogLevel(brls::LogLevel::INFO);
    if (!brls::Application::init()) {
        mfg::log::error("Falha ao inicializar Borealis.");
        mfg::log::shutdown();
        return EXIT_FAILURE;
    }

    brls::Application::createWindow("Moonlight Forwarder");
    brls::Application::setGlobalQuit(false);

    for (auto theme : { brls::getLightTheme(), brls::getDarkTheme() }) {
        theme.addColor("brls/background", Background);
        theme.addColor("brls/text", Text);
        theme.addColor("brls/highlight/background", SurfaceRaised);
        theme.addColor("brls/highlight/color1", Purple);
        theme.addColor("brls/highlight/color2", nvgRGB(146, 120, 255));
        theme.addColor("brls/button/default_enabled_background", SurfaceRaised);
        theme.addColor("brls/button/default_disabled_background", SurfaceRaised);
        theme.addColor("brls/button/default_enabled_text", Text);
        theme.addColor("brls/button/default_disabled_text", Muted);
        theme.addColor("brls/button/primary_enabled_background", Purple);
        theme.addColor("brls/button/primary_disabled_background", SurfaceRaised);
        theme.addColor("brls/button/primary_enabled_text", Text);
        theme.addColor("brls/button/primary_disabled_text", Muted);
    }
    brls::Application::pushActivity(new MainActivity());

    while (brls::Application::mainLoop()) {}
    mfg::log::shutdown();
    return EXIT_SUCCESS;
}

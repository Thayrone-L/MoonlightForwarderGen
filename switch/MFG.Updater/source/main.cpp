#include "installer.hpp"
#include "manifest.hpp"
#include "app_log.hpp"

#include <borealis.hpp>

#include <cstdlib>
#include <string>

namespace {

constexpr const char* ManifestPath = "sdmc:/switch/MFG/manifest.json";
constexpr const char* ForwarderDirectory = "sdmc:/switch/MFG";

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

class GameButton final : public brls::Button {
  public:
    std::function<void()> focused;

    void onFocusGained() override {
        brls::Button::onFocusGained();
        if (focused)
            focused();
    }
};

class MainActivity final : public brls::Activity {
  public:
    MainActivity() : result(mfg::load_manifest(ManifestPath, ForwarderDirectory)) {}

    brls::View* createContentView() override {
        auto* frame = new brls::AppletFrame();
        frame->setTitle("Moonlight Forwarder");

        auto* page = new brls::Box(brls::Axis::ROW);
        page->setBackgroundColor(Background);
        page->setPadding(28, 42, 30, 42);

        auto* library = new brls::Box(brls::Axis::COLUMN);
        library->setGrow(1.25f);
        library->setMarginRight(24);
        library->addView(make_label("Seus jogos", 34, Text));
        auto* subtitle = make_label("Escolha um jogo para adicionar ao menu HOME.", 18, Muted);
        subtitle->setMarginTop(5);
        subtitle->setMarginBottom(22);
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
            scroller->setScrollingBehavior(brls::ScrollingBehavior::CENTERED);
            auto* rows = new brls::Box(brls::Axis::COLUMN);
            for (std::size_t index = 0; index < result.manifest.games.size(); ++index) {
                const auto& game = result.manifest.games[index];
                auto* row = new GameButton();
                row->setText(std::string(game.hash_valid ? "  Pronto     " : "  Sincronize  ") + game.name);
                row->setStyle(&brls::BUTTONSTYLE_DEFAULT);
                row->setHeight(62);
                row->setMarginBottom(8);
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
        frame->setContentView(page);
        return frame;
    }

    void onContentAvailable() override {
        registerAction("Atualizar", brls::BUTTON_X, [](brls::View*) {
            brls::Application::pushActivity(new MainActivity(), brls::TransitionAnimation::FADE);
            return true;
        });
        registerExitAction();
    }

  private:
    mfg::ManifestResult result;
    std::size_t selected_index = 0;
    bool awaiting_confirmation = false;
    brls::Label* selected_title = nullptr;
    brls::Label* selected_status = nullptr;
    brls::Button* install_button = nullptr;

    brls::View* build_detail_panel() {
        auto* detail = new brls::Box(brls::Axis::COLUMN);
        detail->setWidth(430);
        detail->setBackgroundColor(Surface);
        detail->setCornerRadius(18);
        detail->setPadding(34);
        detail->setJustifyContent(brls::JustifyContent::CENTER);

        auto* badge = new brls::Box(brls::Axis::COLUMN);
        badge->setWidth(126);
        badge->setHeight(126);
        badge->setBackgroundColor(SurfaceRaised);
        badge->setCornerRadius(24);
        badge->setAlignItems(brls::AlignItems::CENTER);
        badge->setJustifyContent(brls::JustifyContent::CENTER);
        auto* moon = make_label("MFG", 28, Purple);
        moon->setHorizontalAlign(brls::HorizontalAlign::CENTER);
        badge->addView(moon);
        detail->addView(badge);

        const bool has_game = result.ok && !result.manifest.games.empty();
        selected_title = make_label(has_game ? result.manifest.games[0].name : "Tudo pronto por aqui", 29, Text);
        selected_title->setMarginTop(26);
        selected_title->setHorizontalAlign(brls::HorizontalAlign::CENTER);
        detail->addView(selected_title);

        selected_status = make_label(has_game ? status_for(result.manifest.games[0]) :
            "Prepare seus jogos no computador e volte ao Switch.", 18, Muted);
        selected_status->setMarginTop(12);
        selected_status->setMarginBottom(26);
        selected_status->setHorizontalAlign(brls::HorizontalAlign::CENTER);
        detail->addView(selected_status);

        install_button = new brls::Button();
        install_button->setText("Adicionar ao Switch");
        install_button->setStyle(&brls::BUTTONSTYLE_PRIMARY);
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
    brls::Application::pushActivity(new MainActivity());

    while (brls::Application::mainLoop()) {}
    mfg::log::shutdown();
    return EXIT_SUCCESS;
}

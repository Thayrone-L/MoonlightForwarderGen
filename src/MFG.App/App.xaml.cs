using System.Windows;
using MFG.App.Data;
using MFG.App.Services;

namespace MFG.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppLog.Initialize();
        DispatcherUnhandledException += (_, exception) => AppLog.Error("Erro nao tratado na interface.", exception.Exception);
        TaskScheduler.UnobservedTaskException += (_, exception) =>
        {
            AppLog.Error("Erro nao observado em tarefa assincrona.", exception.Exception);
            exception.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, exception) =>
        {
            if (exception.ExceptionObject is Exception error)
                AppLog.Error("Erro nao tratado no aplicativo.", error);
        };
        try
        {
            await MfgDbContext.InitializeAsync();
            AppLog.Info("Banco de dados pronto.");
        }
        catch (Exception exception)
        {
            AppLog.Error("Falha ao iniciar o banco de dados.", exception);
            throw;
        }
    }
}

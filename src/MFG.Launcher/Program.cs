using MFG.Common.Communication;
using MFG.Common.Contracts;

if (args.Length != 2 || !string.Equals(args[0], "launch-game", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(args[1]))
{
    Console.Error.WriteLine("Uso: MFG.Launcher launch-game <GameId>");
    return 64;
}

try
{
    var client = new AgentClient();
    var response = await client.SendAsync(new AgentCommand("launch-game", args[1].Trim()));

    if (!response.Success)
    {
        Console.Error.WriteLine(response.Message);
        return 2;
    }

    Console.WriteLine(response.Message);
    return 0;
}
catch (TimeoutException)
{
    Console.Error.WriteLine("Não foi possível conectar ao MFG.Agent. Inicie o Agent e tente novamente.");
    return 3;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Falha ao comunicar com o MFG.Agent: {exception.Message}");
    return 1;
}

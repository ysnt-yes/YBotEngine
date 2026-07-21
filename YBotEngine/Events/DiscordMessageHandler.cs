using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace YBotEngine.Events;

public class DiscordMessageHandler : IMessageCreateGatewayHandler, IMessageDeleteGatewayHandler, IMessageUpdateGatewayHandler
{
    ValueTask IMessageCreateGatewayHandler.HandleAsync(Message arg)
    {
        throw new NotImplementedException();
    }

    public ValueTask HandleAsync(MessageDeleteEventArgs arg)
    {
        throw new NotImplementedException();
    }

    ValueTask IMessageUpdateGatewayHandler.HandleAsync(Message arg)
    {
        throw new NotImplementedException();
    }
}
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace YBotEngine.Services.Events;

public interface IEventBus
{
    void Publish<T>(T eventMessage) where T : notnull;
    void Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : notnull;
}

public class EventBus : IEventBus
{
    private readonly Channel<object> _busChannel = Channel.CreateUnbounded<object>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly ConcurrentDictionary<Type, List<Func<object, CancellationToken, Task>>> _subscribers = new();
    private readonly CancellationTokenSource _cts = new();

    public EventBus()
    {
        Task.Run(() => ProcessEventLoopAsync(_cts.Token));
    }

    public void Publish<T>(T eventMessage) where T : notnull
    {
        _busChannel.Writer.TryWrite(eventMessage);
    }

    public void Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : notnull
    {
        var type = typeof(T);
        var handlersList = _subscribers.GetOrAdd(type, _ => []);

        lock (handlersList)
        {
            handlersList.Add((msg, token) => handler((T)msg, token));
        }
    }

    private async Task ProcessEventLoopAsync(CancellationToken token)
    {
        await foreach (var message in _busChannel.Reader.ReadAllAsync(token))
        {
            var messageType = message.GetType();
            if (!_subscribers.TryGetValue(messageType, out var handlersList)) continue;

            List<Func<object, CancellationToken, Task>> targets;
            lock (handlersList)
            {
                targets = [.. handlersList];
            }

            foreach (var handler in targets)
            {
                try
                {
                    _ = Task.Run(() => handler(message, token), token);
                }
                catch
                {
                    // Intentionally left unhandled for lightweight processing
                }
            }
        }
    }
}

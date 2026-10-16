using System.Threading.Channels;
using Store.Application;
using Store.Domain;

namespace Store.Infrastructure.ErrorLogging;

public sealed class ErrorLogQueue : IErrorLogWriter
{
    private readonly Channel<ErrorLog> _channel;

    public ErrorLogQueue(ErrorLogOptions options)
    {
        _channel = Channel.CreateBounded<ErrorLog>(new BoundedChannelOptions(options.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ChannelReader<ErrorLog> Reader => _channel.Reader;

    public bool TryWrite(ErrorLog errorLog) => _channel.Writer.TryWrite(errorLog);
}

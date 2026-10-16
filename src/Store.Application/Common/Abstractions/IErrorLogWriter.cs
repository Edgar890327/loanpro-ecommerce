using Store.Domain;

namespace Store.Application;

public interface IErrorLogWriter
{
    bool TryWrite(ErrorLog errorLog);
}

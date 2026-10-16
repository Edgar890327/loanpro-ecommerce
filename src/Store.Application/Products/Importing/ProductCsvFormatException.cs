namespace Store.Application;

public sealed class ProductCsvFormatException(string message, Exception? innerException = null)
    : Exception(message, innerException);

namespace Store.Application;

public sealed class ProductCsvLimitException(string message) : Exception(message);

namespace CryptoTrader.Exchanges.Okx;

public sealed class OkxApiException : InvalidOperationException
{
    public OkxApiException(string message, string? code = null, int? httpStatus = null)
        : base(message)
    {
        Code = code;
        HttpStatus = httpStatus;
    }

    public OkxApiException(string message, Exception innerException)
        : base(message, innerException)
    {
        Code = null;
        HttpStatus = null;
    }

    public string? Code { get; }

    public int? HttpStatus { get; }
}

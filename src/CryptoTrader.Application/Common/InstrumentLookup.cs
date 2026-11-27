using CryptoTrader.Application.Abstractions.Persistence;
using CryptoTrader.Domain.Common;
using CryptoTrader.Domain.Instruments;

namespace CryptoTrader.Application.Common;

internal static class InstrumentLookup
{
    public static async Task<Instrument> RequireAsync(
        IInstrumentRepository repository,
        string symbolOrId,
        ExchangeId? exchange,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var key = GuardKey(symbolOrId);
        var listed = await repository.ListAsync(exchange, cancellationToken);
        var match = listed.FirstOrDefault(instrument => SameInstrument(instrument, key));
        if (match is not null)
        {
            return match;
        }

        if (exchange is ExchangeId constrained)
        {
            var bySymbol = await repository.FindByExchangeSymbolAsync(constrained, key, cancellationToken);
            if (bySymbol is null && !string.Equals(key, key.ToUpperInvariant(), StringComparison.Ordinal))
            {
                bySymbol = await repository.FindByExchangeSymbolAsync(constrained, key.ToUpperInvariant(), cancellationToken);
            }

            if (bySymbol is not null)
            {
                return bySymbol;
            }
        }

        try
        {
            var byId = await repository.FindAsync(new InstrumentId(key), cancellationToken);
            if (byId is not null && (exchange is null || byId.Exchange == exchange.Value))
            {
                return byId;
            }
        }
        catch (DomainException)
        {
        }

        var exchangeText = exchange is ExchangeId id ? $" on {id.Value}" : string.Empty;
        throw new NotFoundException($"Instrument '{key}'{exchangeText} was not found.");
    }

    private static bool SameInstrument(Instrument instrument, string key) =>
        string.Equals(instrument.ExchangeSymbol, key, StringComparison.OrdinalIgnoreCase)
        || string.Equals(instrument.Id.Value, key, StringComparison.Ordinal);

    private static string GuardKey(string symbolOrId)
    {
        if (string.IsNullOrWhiteSpace(symbolOrId))
        {
            throw new ArgumentException("Instrument is required.", nameof(symbolOrId));
        }

        return symbolOrId.Trim();
    }
}

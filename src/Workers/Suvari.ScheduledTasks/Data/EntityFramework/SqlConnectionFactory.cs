using Microsoft.Extensions.Options;
using Suvari.ScheduledTasks.Options;

namespace Suvari.ScheduledTasks.Data.EntityFramework;

public class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly SqlConnectionsOptions _options;

    public SqlConnectionFactory(IOptions<SqlConnectionsOptions> options)
    {
        _options = options.Value;
        Console.WriteLine($"[SqlFactory] Portal={Mask(_options.Portal)}, Nebim={Mask(_options.Nebim)}, EBA={Mask(_options.EBA)}");
    }

    public Kata GetConnection(SqlConnectionName name)
    {
        var (connStr, label) = name switch
        {
            SqlConnectionName.Portal       => (_options.Portal,       "Portal"),
            SqlConnectionName.SuvariPortal => (_options.SuvariPortal, "SuvariPortal"),
            SqlConnectionName.Nebim        => (_options.Nebim,        "Nebim"),
            SqlConnectionName.eBA          => (_options.EBA,          "eBA"),
            SqlConnectionName.External     => (_options.External,     "External"),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };

        if (string.IsNullOrWhiteSpace(connStr))
            throw new InvalidOperationException(
                $"'{label}' SQL bağlantı dizesi yapılandırılmamış. " +
                $"MongoDB Settings koleksiyonundan okunamadı. " +
                $"Konsol [DI:SQL] çıktısını kontrol edin.");

        return new Kata(connStr);
    }

    private static string Mask(string s) =>
        string.IsNullOrWhiteSpace(s) ? "NULL/BOŞŞ" : (s.Length > 20 ? s[..20] + "..." : s);
}

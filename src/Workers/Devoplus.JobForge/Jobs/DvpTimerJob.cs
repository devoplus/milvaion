using Milvasoft.Milvaion.Sdk.Worker.Abstractions;

namespace Devoplus.JobForge.Jobs;

/// <summary>
/// 30 saniye boyunca her saniye log basan zamanlayıcı job.
/// </summary>
public class DvpTimer : IAsyncJob
{
    private const int _totalSeconds = 30;

    public async Task ExecuteAsync(IJobContext context)
    {
        context.LogInformation($"⏱️ DvpTimer başladı. Toplam süre: {_totalSeconds} saniye.");

        for (int second = 1; second <= _totalSeconds; second++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            context.LogInformation($"🕐 {second}. saniye");

            await Task.Delay(1000, context.CancellationToken);
        }

        context.LogInformation("✅ DvpTimer tamamlandı. 30 saniyelik işlem başarıyla bitti.");
    }
}

using chainshop_b.Blockchain;
using chainshop_b.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace chainshop_b.Services
{
    // Cron backend: tiap N menit mencari order yang timer off-chain-nya sudah habis, lalu wallet operator
    // mengirim release / refund ke smart contract (lihat EscrowAutomationRunner).
    public class EscrowAutomationService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly BlockchainOptions _options;
        private readonly ILogger<EscrowAutomationService> _logger;

        // order yang aksinya gagal tidak dicoba lagi selama 30 menit (supaya tidak memblokir antrean)
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, DateTime> _failedAt = new();
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan UnpaidExpiry = TimeSpan.FromHours(24);

        public EscrowAutomationService(IServiceScopeFactory scopeFactory, IOptions<BlockchainOptions> options, ILogger<EscrowAutomationService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.IsConfigured)
            {
                _logger.LogWarning("Escrow automation tidak aktif: Blockchain belum dikonfigurasi");
                return;
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, _options.AutomationIntervalMinutes));
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnce(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Escrow automation run failed");
                }

                await Task.Delay(interval, stoppingToken);
            }
        }

        public async Task RunOnce(CancellationToken stoppingToken)
        {
            var now = DateTime.UtcNow;
            List<Guid> dueIds;
            List<Guid> unpaidIds;

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();

                dueIds = await context.TrOrders.AsNoTracking()
                    .Where(x => x.OnchainOrderId != null && (
                        ((x.Status == EscrowSyncService.StatusPaid || x.Status == EscrowSyncService.StatusProcessing) &&
                            x.SellerDeadlineAt != null && x.SellerDeadlineAt < now) ||
                        (x.Status == EscrowSyncService.StatusCancelled && x.SellerDeadlineAt != null) ||
                        (x.Status == EscrowSyncService.StatusDelivered && x.BuyerDeadlineAt != null && x.BuyerDeadlineAt < now) ||
                        ((x.Status == EscrowSyncService.StatusReturnPending || x.Status == EscrowSyncService.StatusReturnDelivered) &&
                            x.ReturnDeadlineAt != null && x.ReturnDeadlineAt < now)))
                    .OrderBy(x => x.SellerDeadlineAt ?? x.BuyerDeadlineAt ?? x.ReturnDeadlineAt)
                    .Select(x => x.Id)
                    .Take(200)
                    .ToListAsync(stoppingToken);

                // order batal di database tanpa catatan refund: cek apakah deposit ternyata masuk onchain
                var since = now.AddDays(-7);
                dueIds.AddRange(await context.TrOrders.AsNoTracking()
                    .Where(x => x.OnchainOrderId != null && x.Status == EscrowSyncService.StatusCancelled &&
                                x.SellerDeadlineAt == null && x.CreatedAt > since &&
                                !context.TrEscrowTransactions.Any(e => e.OrderId == x.Id))
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => x.Id)
                    .Take(20)
                    .ToListAsync(stoppingToken));

                // sapuan berkala: order yang barangnya sudah dikirim disinkronkan ulang (tanpa transaksi), supaya
                // release / refund / claimStale yang tidak sempat dicatat backend tetap terbaca
                var staleBefore = now.AddHours(-1);
                dueIds.AddRange(await context.TrOrders.AsNoTracking()
                    .Where(x => x.OnchainOrderId != null && EscrowSyncService.ShippedStatuses.Contains(x.Status) &&
                                x.UpdatedAt < staleBefore)
                    .OrderBy(x => x.UpdatedAt)
                    .Select(x => x.Id)
                    .Take(30)
                    .ToListAsync(stoppingToken));
                dueIds = dueIds.Distinct().ToList();

                var unpaidBefore = now - UnpaidExpiry;
                unpaidIds = await context.TrOrders.AsNoTracking()
                    .Where(x => x.Status == EscrowSyncService.StatusPendingPayment && x.CreatedAt < unpaidBefore)
                    .OrderBy(x => x.CreatedAt)
                    .Select(x => x.Id)
                    .Take(50)
                    .ToListAsync(stoppingToken);
            }

            foreach (var id in dueIds)
            {
                if (stoppingToken.IsCancellationRequested) break;
                if (_failedAt.TryGetValue(id, out var failed) && DateTime.UtcNow - failed < RetryDelay) continue;

                // satu scope (DbContext baru) per order supaya data selalu segar dan error tidak menular
                using var scope = _scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<EscrowAutomationRunner>();
                try
                {
                    var result = await runner.Run(id);
                    if (result.ok) _failedAt.TryRemove(id, out _);
                    else _failedAt[id] = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _failedAt[id] = DateTime.UtcNow;
                    _logger.LogError(ex, "Escrow automation gagal untuk order {Id}", id);
                }
            }

            foreach (var id in unpaidIds)
            {
                if (stoppingToken.IsCancellationRequested) break;
                using var scope = _scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<EscrowAutomationRunner>();
                try
                {
                    await runner.ExpireUnpaid(id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Gagal membatalkan order belum dibayar {Id}", id);
                }
            }
        }
    }
}

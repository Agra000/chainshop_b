using chainshop_b.Blockchain;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace chainshop_b.Services
{
    // Satu-satunya pintu backend ke smart contract escrow (Sepolia):
    // baca state order, verifikasi tx yang dikirim frontend, dan kirim tx release/refund untuk cron (OPERATOR_ROLE).
    public class BlockchainService
    {
        private readonly BlockchainOptions _options;
        private readonly ILogger<BlockchainService> _logger;
        private readonly Web3? _readWeb3;
        private readonly Web3? _operatorWeb3;
        private readonly string? _operatorAddress;

        private static readonly decimal UnitFactor = 1_000_000m; // mUSDT 6 desimal

        public BlockchainService(IOptions<BlockchainOptions> options, ILogger<BlockchainService> logger)
        {
            _options = options.Value;
            _logger = logger;

            if (!_options.IsConfigured) return;

            _readWeb3 = new Web3(_options.RpcUrl);
            if (!string.IsNullOrWhiteSpace(_options.OperatorPrivateKey))
            {
                var account = new Account(_options.OperatorPrivateKey, new BigInteger(_options.ChainId));
                _operatorWeb3 = new Web3(account, _options.RpcUrl);
                _operatorAddress = account.Address;
            }
        }

        public bool IsEnabled => _readWeb3 != null;
        public bool CanSendAutoTx => _operatorWeb3 != null;
        public string? OperatorAddress => _operatorAddress;
        public string EscrowAddress => _options.EscrowAddress;
        public string ExplorerUrl => _options.ExplorerUrl.TrimEnd('/');
        public double ShippingFee => _options.ShippingFee;
        public BlockchainOptions Options => _options;

        // ===== timer off-chain =====
        public TimeSpan BuyerConfirmWindow => TimeSpan.FromMinutes(Math.Max(1, _options.BuyerConfirmMinutes));
        public TimeSpan ReturnShipWindow => TimeSpan.FromMinutes(Math.Max(1, _options.ReturnShipMinutes));
        public TimeSpan ReturnConfirmWindow => TimeSpan.FromMinutes(Math.Max(1, _options.ReturnConfirmMinutes));
        public TimeSpan NotDeliveredWindow => TimeSpan.FromMinutes(Math.Max(1, _options.NotDeliveredMinutes));
        public int MaxRefundRequests => Math.Max(1, _options.MaxRefundRequests);

        public string NotConfiguredMessage =>
            "Blockchain belum dikonfigurasi di backend (Blockchain:EscrowAddress / Blockchain:RpcUrl)";

        // ===== helper konversi =====

        // order id DB (Guid) -> bytes32 onchain = keccak256("<guid lowercase>")
        public static string ToOnchainOrderId(Guid orderId)
        {
            return Sha3Keccack.Current.CalculateHash(orderId.ToString("D").ToLower()).EnsureHexPrefix();
        }

        public static byte[] OrderIdBytes(string onchainOrderId)
        {
            return onchainOrderId.HexToByteArray();
        }

        // nilai mUSDT (mis. 12.5) -> unit 6 desimal (12500000)
        public static BigInteger ToUnits(double value)
        {
            var units = decimal.Round((decimal)value * UnitFactor, 0, MidpointRounding.AwayFromZero);
            return new BigInteger(units);
        }

        public static double FromUnits(BigInteger units)
        {
            return (double)((decimal)units / UnitFactor);
        }

        public static DateTime? FromUnix(ulong seconds)
        {
            return seconds == 0 ? null : DateTimeOffset.FromUnixTimeSeconds((long)seconds).UtcDateTime;
        }

        public static bool IsValidTxHash(string? txHash)
        {
            return !string.IsNullOrWhiteSpace(txHash) && Regex.IsMatch(txHash, "^0x[0-9a-fA-F]{64}$");
        }

        public static bool SameAddress(string? a, string? b)
        {
            return !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
                   string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public string TxUrl(string? txHash)
        {
            return string.IsNullOrWhiteSpace(txHash) ? "" : $"{ExplorerUrl}/tx/{txHash}";
        }

        // ===== view =====

        public async Task<OnchainOrderDto?> GetOrder(string onchainOrderId)
        {
            if (_readWeb3 == null) return null;
            var handler = _readWeb3.Eth.GetContractQueryHandler<OrdersFunction>();
            return await handler.QueryDeserializingToObjectAsync<OnchainOrderDto>(
                new OrdersFunction { OrderId = OrderIdBytes(onchainOrderId) }, _options.EscrowAddress);
        }

        // estimasi gas (dalam unit mUSDT) untuk order dengan itemCount baris barang
        public async Task<BigInteger> QuoteGasFeeUnits(int itemCount)
        {
            if (_readWeb3 == null) throw new InvalidOperationException(NotConfiguredMessage);
            var handler = _readWeb3.Eth.GetContractQueryHandler<QuoteGasFeeFunction>();
            return await handler.QueryAsync<BigInteger>(_options.EscrowAddress,
                new QuoteGasFeeFunction { ItemCount = (uint)Math.Max(1, itemCount) });
        }

        // apakah wallet ini punya OPERATOR_ROLE / ADMIN_ROLE di contract
        public async Task<bool> IsOperatorOrAdmin(string? address)
        {
            if (_readWeb3 == null || string.IsNullOrWhiteSpace(address)) return false;
            var handler = _readWeb3.Eth.GetContractQueryHandler<IsOperatorOrAdminFunction>();
            return await handler.QueryAsync<bool>(_options.EscrowAddress, new IsOperatorOrAdminFunction { Account = address });
        }

        public static bool IsValidSignatureHex(string? signature)
        {
            return !string.IsNullOrWhiteSpace(signature) &&
                   System.Text.RegularExpressions.Regex.IsMatch(signature, "^0x[0-9a-fA-F]{130,}$");
        }

        // tanda tangan "Acceptance" buyer valid untuk order ini (diverifikasi contract, termasuk domain & jumlah)
        public async Task<bool> IsValidAcceptance(string onchainOrderId, string signature)
        {
            if (_readWeb3 == null || !IsValidSignatureHex(signature)) return false;
            var handler = _readWeb3.Eth.GetContractQueryHandler<IsValidAcceptanceFunction>();
            return await handler.QueryAsync<bool>(_options.EscrowAddress, new IsValidAcceptanceFunction
            {
                OrderId = OrderIdBytes(onchainOrderId),
                Signature = signature.HexToByteArray()
            });
        }

        // ===== verifikasi tx dari frontend =====

        public async Task<TransactionReceipt?> WaitForReceipt(string txHash, int timeoutSeconds = 90)
        {
            if (_readWeb3 == null) return null;
            var until = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (DateTime.UtcNow < until)
            {
                var receipt = await _readWeb3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
                if (receipt != null) return receipt;
                await Task.Delay(3000);
            }
            return null;
        }

        /// <summary>
        /// Cek tx: sukses, dikirim ke contract escrow, (opsional) dari wallet tertentu,
        /// dan berisi minimal satu event dari daftar <paramref name="eventSignatures"/> untuk order ini.
        /// </summary>
        public async Task<(bool ok, string message, TransactionReceipt? receipt)> VerifyEscrowTx(
            string? txHash, string onchainOrderId, string? expectedFrom, params string[] eventSignatures)
        {
            if (_readWeb3 == null) return (false, NotConfiguredMessage, null);
            if (!IsValidTxHash(txHash)) return (false, "Invalid transaction hash", null);

            var receipt = await WaitForReceipt(txHash!);
            if (receipt == null) return (false, "Transaction is not mined yet, please try again in a moment", null);
            if (receipt.Status == null || receipt.Status.Value != 1) return (false, "Transaction failed on-chain", receipt);
            if (!SameAddress(receipt.To, _options.EscrowAddress)) return (false, "Transaction is not sent to the escrow contract", receipt);
            if (expectedFrom != null && !SameAddress(receipt.From, expectedFrom))
            {
                return (false, "Transaction is sent from a different wallet", receipt);
            }

            if (eventSignatures.Length > 0 && !eventSignatures.Any(sig => ReceiptHasEvent(receipt, sig, onchainOrderId)))
            {
                return (false, "Transaction does not contain the expected escrow event for this order", receipt);
            }

            return (true, "OK", receipt);
        }

        public bool ReceiptHasEvent(TransactionReceipt receipt, string eventSignature, string onchainOrderId)
        {
            var topic0 = Sha3Keccack.Current.CalculateHash(eventSignature).EnsureHexPrefix().ToLower();
            var orderTopic = onchainOrderId.EnsureHexPrefix().ToLower();

            JArray logs = receipt.Logs ?? new JArray();
            foreach (var log in logs)
            {
                if (!SameAddress(log["address"]?.ToString(), _options.EscrowAddress)) continue;
                var topics = log["topics"] as JArray;
                if (topics == null || topics.Count < 2) continue;
                if (topics[0]?.ToString().ToLower() == topic0 && topics[1]?.ToString().ToLower() == orderTopic)
                {
                    return true;
                }
            }
            return false;
        }

        // isi invoice dari event InvoiceRecorded di receipt (null jika tidak ada / gagal decode)
        public InvoiceRecordedEventDto? DecodeInvoice(TransactionReceipt receipt, string onchainOrderId)
        {
            try
            {
                var orderHex = onchainOrderId.EnsureHexPrefix().ToLower();
                return (receipt.Logs ?? new JArray())
                    .DecodeAllEvents<InvoiceRecordedEventDto>()
                    .Select(x => x.Event)
                    .FirstOrDefault(x => x.OrderId.ToHex(true).ToLower() == orderHex);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gagal decode event InvoiceRecorded");
                return null;
            }
        }

        // ===== tx operator (cron) =====

        private void EnsureOperator()
        {
            if (_operatorWeb3 == null)
            {
                throw new InvalidOperationException("Blockchain:OperatorPrivateKey belum diisi, cron tidak bisa mengirim transaksi");
            }
        }

        /// <summary>release: dana ke seller (order harus Shipped di contract)</summary>
        public async Task<TransactionReceipt> SendRelease(string onchainOrderId)
        {
            EnsureOperator();
            return await _operatorWeb3!.Eth.GetContractTransactionHandler<ReleaseFunction>()
                .SendRequestAndWaitForReceiptAsync(_options.EscrowAddress,
                    new ReleaseFunction { OrderId = OrderIdBytes(onchainOrderId) }, new CancellationTokenSource(TimeSpan.FromMinutes(3)).Token);
        }

        /// <summary>refund: Funded -> semua dana ke buyer, Shipped -> barang+ongkir ke buyer & estimasi gas ke seller</summary>
        public async Task<TransactionReceipt> SendRefund(string onchainOrderId)
        {
            EnsureOperator();
            return await _operatorWeb3!.Eth.GetContractTransactionHandler<RefundFunction>()
                .SendRequestAndWaitForReceiptAsync(_options.EscrowAddress,
                    new RefundFunction { OrderId = OrderIdBytes(onchainOrderId) }, new CancellationTokenSource(TimeSpan.FromMinutes(3)).Token);
        }
    }
}

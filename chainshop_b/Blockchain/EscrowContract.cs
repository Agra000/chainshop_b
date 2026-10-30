using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace chainshop_b.Blockchain
{
    // Konfigurasi appsettings "Blockchain" (alamat dari chainshop-contracts/deployments, private key via user-secrets)
    public class BlockchainOptions
    {
        public bool Enabled { get; set; } = true;
        public string RpcUrl { get; set; } = "https://ethereum-sepolia-rpc.publicnode.com";
        public long ChainId { get; set; } = 11155111;
        public string EscrowAddress { get; set; } = "";
        public string MockUsdtAddress { get; set; } = "";
        // wallet backend untuk cron (OPERATOR_ROLE di contract), bukan wallet admin. Isi Sepolia ETH dari faucet.
        public string? OperatorPrivateKey { get; set; }
        public int AutomationIntervalMinutes { get; set; } = 5;
        public string ExplorerUrl { get; set; } = "https://sepolia.etherscan.io";
        // ongkir flat per order (mUSDT)
        public double ShippingFee { get; set; } = 1.0;

        // ===== timer off-chain (dijalankan backend). Persingkat untuk demo, mis. 5 =====
        // barang sampai -> buyer terima / ajukan refund
        public int BuyerConfirmMinutes { get; set; } = 3 * 24 * 60;
        // refund disetujui -> buyer wajib kirim retur
        public int ReturnShipMinutes { get; set; } = 3 * 24 * 60;
        // retur sampai -> seller wajib konfirmasi
        public int ReturnConfirmMinutes { get; set; } = 3 * 24 * 60;
        // dikirim -> buyer boleh refund jika barang belum sampai
        public int NotDeliveredMinutes { get; set; } = 7 * 24 * 60;
        public int MaxRefundRequests { get; set; } = 3;

        public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(RpcUrl) && !string.IsNullOrWhiteSpace(EscrowAddress);
    }

    // status order di smart contract (urutan HARUS sama dengan enum Status di ChainShopEscrow.sol)
    public enum OnchainStatus : byte
    {
        None = 0,
        Funded = 1,
        Shipped = 2,
        Completed = 3,
        Refunded = 4,
        Cancelled = 5
    }

    // signature event untuk mencocokkan log di receipt (topic[0] = keccak256(signature), topic[1] = orderId)
    public static class EscrowEvents
    {
        public const string Deposited = "Deposited(bytes32,address,address,uint256,uint256,uint256,uint256)";
        public const string OrderCancelled = "OrderCancelled(bytes32,uint256,address)";
        public const string Shipped = "Shipped(bytes32,uint256)";
        public const string InvoiceRecorded = "InvoiceRecorded(bytes32,string,address,address,(string,uint256,uint256)[],uint256,uint256,uint256,uint256,string,string,uint64,uint64)";
        public const string Released = "Released(bytes32,uint256,address)";
        public const string Refunded = "Refunded(bytes32,uint256,uint256,address)";
    }

    // ===== event InvoiceRecorded (di-decode untuk verifikasi isi invoice) =====

    public class InvoiceItemDto
    {
        [Parameter("string", "name", 1)]
        public string Name { get; set; } = "";

        [Parameter("uint256", "qty", 2)]
        public BigInteger Qty { get; set; }

        [Parameter("uint256", "unitPrice", 3)]
        public BigInteger UnitPrice { get; set; }
    }

    [Event("InvoiceRecorded")]
    public class InvoiceRecordedEventDto : IEventDTO
    {
        [Parameter("bytes32", "orderId", 1, true)]
        public byte[] OrderId { get; set; } = Array.Empty<byte>();

        [Parameter("string", "invoiceId", 2, false)]
        public string InvoiceId { get; set; } = "";

        [Parameter("address", "seller", 3, true)]
        public string Seller { get; set; } = "";

        [Parameter("address", "buyer", 4, true)]
        public string Buyer { get; set; } = "";

        [Parameter("tuple[]", "items", 5, false)]
        public List<InvoiceItemDto> Items { get; set; } = new();

        [Parameter("uint256", "itemsTotal", 6, false)]
        public BigInteger ItemsTotal { get; set; }

        [Parameter("uint256", "shippingFee", 7, false)]
        public BigInteger ShippingFee { get; set; }

        [Parameter("uint256", "gasFeeEstimate", 8, false)]
        public BigInteger GasFeeEstimate { get; set; }

        [Parameter("uint256", "totalPaid", 9, false)]
        public BigInteger TotalPaid { get; set; }

        [Parameter("string", "courier", 10, false)]
        public string Courier { get; set; } = "";

        [Parameter("string", "trackingNumber", 11, false)]
        public string TrackingNumber { get; set; } = "";

        [Parameter("uint64", "purchasedAt", 12, false)]
        public ulong PurchasedAt { get; set; }

        [Parameter("uint64", "shippedAt", 13, false)]
        public ulong ShippedAt { get; set; }
    }

    // ===== view functions =====

    // getter mapping public `orders(bytes32)` -> semua field struct Order
    [Function("orders", typeof(OnchainOrderDto))]
    public class OrdersFunction : FunctionMessage
    {
        [Parameter("bytes32", "", 1)]
        public byte[] OrderId { get; set; } = Array.Empty<byte>();
    }

    [FunctionOutput]
    public class OnchainOrderDto : IFunctionOutputDTO
    {
        [Parameter("address", "buyer", 1)]
        public string Buyer { get; set; } = "";

        [Parameter("address", "seller", 2)]
        public string Seller { get; set; } = "";

        [Parameter("uint256", "itemsTotal", 3)]
        public BigInteger ItemsTotal { get; set; }

        [Parameter("uint256", "shippingFee", 4)]
        public BigInteger ShippingFee { get; set; }

        [Parameter("uint256", "gasFee", 5)]
        public BigInteger GasFee { get; set; }

        [Parameter("uint32", "itemCount", 6)]
        public uint ItemCount { get; set; }

        [Parameter("uint64", "fundedAt", 7)]
        public ulong FundedAt { get; set; }

        [Parameter("uint64", "shipDeadline", 8)]
        public ulong ShipDeadline { get; set; }

        [Parameter("uint64", "shippedAt", 9)]
        public ulong ShippedAt { get; set; }

        [Parameter("uint8", "status", 10)]
        public byte StatusCode { get; set; }

        public OnchainStatus Status => (OnchainStatus)StatusCode;
    }

    [Function("quoteGasFee", "uint256")]
    public class QuoteGasFeeFunction : FunctionMessage
    {
        [Parameter("uint32", "itemCount", 1)]
        public uint ItemCount { get; set; }
    }

    [Function("isOperatorOrAdmin", "bool")]
    public class IsOperatorOrAdminFunction : FunctionMessage
    {
        [Parameter("address", "account", 1)]
        public string Account { get; set; } = "";
    }

    // v3: cek tanda tangan EIP-712 "Acceptance" buyer (gratis, tanpa gas) sebelum disimpan backend
    [Function("isValidAcceptance", "bool")]
    public class IsValidAcceptanceFunction : FunctionMessage
    {
        [Parameter("bytes32", "orderId", 1)]
        public byte[] OrderId { get; set; } = Array.Empty<byte>();

        [Parameter("bytes", "signature", 2)]
        public byte[] Signature { get; set; } = Array.Empty<byte>();
    }

    // ===== tx yang dikirim wallet operator (cron) =====

    [Function("release")]
    public class ReleaseFunction : FunctionMessage
    {
        [Parameter("bytes32", "orderId", 1)]
        public byte[] OrderId { get; set; } = Array.Empty<byte>();
    }

    [Function("refund")]
    public class RefundFunction : FunctionMessage
    {
        [Parameter("bytes32", "orderId", 1)]
        public byte[] OrderId { get; set; } = Array.Empty<byte>();
    }
}

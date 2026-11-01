namespace Contracts;

// Published by Order service when a customer checks out
public record OrderCreated(Guid OrderId, string Sku, int Quantity, decimal Amount);

// Published by Inventory service
public record StockReserved(Guid OrderId, string Sku, int Quantity);
public record StockRejected(Guid OrderId, string Sku, string Reason);

// Compensation: Order saga asks Inventory to give stock back (e.g. payment failed)
public record ReleaseStockRequested(Guid OrderId, string Sku);
public record StockReleased(Guid OrderId, string Sku, int Quantity);

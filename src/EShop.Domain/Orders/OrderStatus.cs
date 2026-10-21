namespace EShop.Domain.Orders;

public enum OrderStatus { PendingPayment = 1, Paid, Shipped, Delivered, Cancelled, Expired }
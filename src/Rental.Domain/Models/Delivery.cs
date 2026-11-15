namespace Rental.Domain.Models;

public sealed record Delivery(
    string ItemCode,
    int Added);

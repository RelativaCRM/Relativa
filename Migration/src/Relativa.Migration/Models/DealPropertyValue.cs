using System;
namespace Relativa.Migration.Models;
public class DealPropertyValue {
    public int Id { get; set; }
    public decimal? Value { get; set; }
    public int? OwnerId { get; set; }
    public int ClientId { get; set; }
    public DateTime? ExpectedClose { get; set; }
    public decimal? ClosureScore { get; set; }
    public DateTime CreatedAt { get; set; }
    public User? Owner { get; set; }
    public Entity Client { get; set; } = null!;
}

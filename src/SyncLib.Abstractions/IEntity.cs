// SyncAbstractions/IEntity.cs
namespace SyncLibrary.Abstractions;

public interface IEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

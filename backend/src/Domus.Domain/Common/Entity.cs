namespace Domus.Domain.Common;

public abstract class Entity
{
    protected Entity(Guid id) => Id = id;

    protected Entity() { }

    public Guid Id { get; protected set; }

    public static Guid NewId() => Guid.CreateVersion7();

    public override bool Equals(object? obj) =>
        obj is Entity other && other.GetType() == GetType() && other.Id == Id && Id != Guid.Empty;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

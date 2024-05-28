namespace Fidelity.Core.Abstractions;

public interface IComponent
{
  public void Update(float dt);
  public void Notify(GameObject parent);
  public ComponentType GetComponentType();
}
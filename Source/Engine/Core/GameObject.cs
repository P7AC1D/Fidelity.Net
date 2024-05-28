using Fidelity.Core.Abstractions;

namespace Fidelity.Core;

public class GameObject
{
  public GameObject? Parent { get; protected set; } = null;
  public List<GameObject> Children { get; } = [];
  public List<IComponent> Components {get; } = [];
  public Transform Local { get; private set; } = new Transform();
  public Transform Global { get; private set; } = new Transform();

  public GameObject AddComponent(IComponent component)
  {
    Components.Add(component);
    return this;
  }

  public GameObject AddNode(GameObject childNode)
  {
    childNode.Parent = this;
    Children.Add(childNode);
    return this;
  }

  public void Update(float dt)
  {
    if (Local.IsDirty)
    {
      Local.Update();
      if (Parent == null)
      {
        Global = Local;
      }
      else
      {
        Global = Parent.Global * Local;
      }
      UpdateChildNodeTransforms();
    }

    if (Global.IsDirty)
    {
      Global.Update();
      NotifyComponents();
    }

    foreach (var component in Components)
    {
      component.Update(dt);
    }
  }

  private void UpdateChildNodeTransforms()
  {
    foreach (var child in Children)
    {
      Transform newGlobal = Local * child.Local;
      child.Global
        .SetPosition(newGlobal.Position)
        .SetRotation(newGlobal.Rotation)
        .SetScale(newGlobal.Scale);
    }
  }

  private void NotifyComponents()
  {
    foreach (var component in Components)
    {
      component.Notify(this);
    }
  }
}

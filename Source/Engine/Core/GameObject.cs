using Engine.SceneManagement;

namespace Engine;

public class GameObject
{
  public GameObject? Parent { get; protected set; } = null;
  public List<GameObject> Children { get; } = [];
  public Transform Local { get; private set; } = new Transform();
  public Transform Global { get; private set; } = new Transform();

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
      UpdateChileNodeTransforms();
    }

    if (Global.IsDirty)
    {
      Global.Update();
      notifyComponents();
    }
  }

  private void UpdateChileNodeTransforms()
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
}

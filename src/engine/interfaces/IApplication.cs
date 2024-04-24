using Silk.NET.Input;
using Silk.NET.Maths;

namespace Fidelity;

public interface IApplication
{
  public void Run();
  public void Load();
  public void Render(double dt);
  public void Update(double dt);
  public void ResizeFramebuffer(Vector2D<int> newSize);
  public void KeyDown(IKeyboard arg1, Key arg2, int arg3);
}
using System.Drawing;
using Fidelity.Rendering.Resources;

namespace Fidelity.Rendering;

public class Material
{
  public Image? DiffuseTexture { get; private set; }
  public Image? NormalTexture { get; private set; }
  public Image? SpecularTexture { get; private set; }

  public Color AmbientColor { get; private set; } = Color.White;
  public Color DiffuseColor { get; private set; } = Color.White;
  public Color SpecularColor { get; private set; } = Color.White;

  public Material SetDiffuseTexture(Image image)
  {
    DiffuseTexture = image;
    return this;
  }

  public Material SetNormalTexture(Image image)
  {
    NormalTexture = image;
    return this;
  }

  public Material SetSpecularTexture(Image image)
  {
    SpecularTexture = image;
    return this;
  }

  public Material SetAmbientColor(Color color)
  {
    AmbientColor = color;
    return this;
  }

  public Material SetSpecularColor(Color color)
  {
    SpecularColor = color;
    return this;
  }
}
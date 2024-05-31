﻿using Fidelity.Core.Abstractions;
using Fidelity.Rendering;

namespace Fidelity.Core.Components;

public class Model : IComponent
{
  public Mesh Mesh { get; private set; }
  public Material Material {get; private set; }

  private bool isDirty = false;

  public void Notify(GameObject parent)
  {
  }

  public void Update(float dt)
  {    
    if (isDirty)
    {
      Mesh.Upload(Renderer.RenderDevices.Device, Renderer.RenderDevices.PhysicalDevice, Renderer.RenderDevices.CommandPool, Renderer.RenderDevices.GraphicsQueue);
    }
    isDirty = false;
  }

  public Model SetMesh(Mesh mesh)
  {
    Mesh = mesh;
    isDirty = true;
    return this;
  }

  public Model SetMaterial(Material material)
  {
    Material = material;
    isDirty = true;
    return this;
  }

  public ComponentType GetComponentType()
  {
    return ComponentType.Model;
  }
}

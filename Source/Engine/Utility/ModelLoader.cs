using System.Numerics;
using System.Reflection;
using Fidelity.Core;
using Fidelity.Core.Components;
using Silk.NET.Assimp;
using Material = Fidelity.Rendering.Material;

namespace Fidelity.Utility;

public static class ModelLoader
{
  public static unsafe GameObject FromFile(string fileName, bool reconstructWorldTransforms)
  {
    try
    {
      using var assimp = Assimp.GetApi();
      var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      var filePath = $"{assemblyPath}/{fileName}";

      var aiScene = assimp.ImportFile(
        filePath,
        (uint)(PostProcessPreset.TargetRealTimeMaximumQuality | PostProcessSteps.Triangulate | PostProcessSteps.GenerateNormals | PostProcessSteps.GenerateUVCoords));

      if (aiScene == null || aiScene->MFlags == Assimp.SceneFlagsIncomplete || aiScene->MRootNode == null)
      {
        var error = assimp.GetErrorStringS();
        throw new Exception(error);
      }

      assimp.ReleaseImport(aiScene);
      return BuildModel(aiScene, assemblyPath!, reconstructWorldTransforms);
    }
    catch (Exception ex)
    {
      throw new Exception($"Failed to load model {fileName}.", ex);
    }
  }

  private static unsafe GameObject BuildModel(Scene* aiScene, string rootPath, bool reconstructWorldTransforms)
  {
    GameObject root = new();
    Material[] materials = new Material[aiScene->MNumMaterials];
    for (int i = 0; i < aiScene->MNumMaterials; i++)
    {
      materials[i] = BuildMaterial(rootPath, aiScene->MMaterials[i]);
    }

    for (int i = 0; i < aiScene->MNumMeshes; i++)
    {
      var aiMesh = aiScene->MMeshes[i];
      
      Model model = new Model()
        .SetMaterial(materials[aiMesh->MMaterialIndex])
        .SetMesh(BuildMesh(rootPath, aiMesh, reconstructWorldTransforms, out Vector3 offset)!);

      GameObject gameObject = new GameObject()
        .AddComponent(model);

      //currentObject.transform().setPosition(offset);
    }
    return root;
  }

  private static unsafe Material BuildMaterial(string rootPath, Silk.NET.Assimp.Material* aiMaterial)
  {
    return new Material();
  }

  private static unsafe Rendering.Mesh? BuildMesh(string rootPath, Mesh* aiMesh, bool reconstructWorldTransforms, out Vector3 offset)
  {
    offset = Vector3.Zero;
    if (aiMesh->MVertices != null || aiMesh->MNormals != null)
    {
      return null;
    }


    Vector3 centroid = CalculateCentroid(aiMesh);
    var mesh = new Rendering.Mesh();

    BuildVertexData(aiMesh->MVertices, aiMesh->MNumVertices, out Vector3[] vertices);
    if (reconstructWorldTransforms)
    {
      offset = centroid;
      OffsetVertices(vertices, centroid);
    }
    mesh.SetPositions(vertices);

    if (aiMesh->MNormals != null)
    {
      mesh.SetNormals(BuildNormalData(aiMesh->MNormals, aiMesh->MNumVertices));
    }
    else
    {
      mesh.GenerateNormals();
    }

    // Assume that mesh contains a single set of texture coordinate data.
    if (aiMesh->MTextureCoords[0] != null)
    {
      BuildTexCoordData(aiMesh->MTextureCoords[0], aiMesh->MNumVertices, out Vector2[] texCoords);
      mesh.SetUvs(texCoords);
    }

    if (aiMesh->MFaces != null)
    {
      BuildIndexData(aiMesh->MFaces, aiMesh->MNumFaces, out uint[] indices);
      mesh.SetIndices(indices);
    }

    mesh.GenerateTangents();
    return mesh;
  }

  private static unsafe Vector3 CalculateCentroid(Mesh* mesh)
  {
    float areaSum = 0.0f;
    Vector3 centroid = Vector3.Zero;
    for (int i = 0; i < mesh->MNumFaces; i++)
    {
      var face = mesh->MFaces[i];
      var p0 = new Vector3(mesh->MVertices[face.MIndices[0]].X, mesh->MVertices[face.MIndices[0]].Y, mesh->MVertices[face.MIndices[0]].Z);
      var p1 = new Vector3(mesh->MVertices[face.MIndices[1]].X, mesh->MVertices[face.MIndices[1]].Y, mesh->MVertices[face.MIndices[1]].Z);
      var p2 = new Vector3(mesh->MVertices[face.MIndices[2]].X, mesh->MVertices[face.MIndices[2]].Y, mesh->MVertices[face.MIndices[2]].Z);

      Vector3 center = (p0 + p1 + p2) / 3.0f;
      float area = 0.5f * Vector3.Cross(p1 - p0, p2 - p0).Length();
      centroid += area * center;
      areaSum += area;
    }
    return centroid / areaSum;
  }

  private static unsafe void BuildTexCoordData(Vector3* texCoords, uint texCoordCount, out Vector2[] texCoordsOut)
  {
    texCoordsOut = new Vector2[texCoordCount];
    for (int i = 0; i < texCoordCount; i++)
    {
      Vector2 texCoord = new(texCoords[i].X, texCoords[i].Y);
      texCoordsOut[i] = texCoord;
    }
  }

  private static unsafe void BuildIndexData(Face* faces, uint faceCount, out uint[] indicesOut)
  {
    var indices = new List<uint>
    {
      Capacity = (int)(faceCount * 3)
    };
    for (int i = 0; i < faceCount; i++)
    {
      var face = faces + i;
      if (face->MNumIndices != 3)
      {
        throw new Exception("Non-triangle face read.");
      }
      indices.Add(face->MIndices[0]);
      indices.Add(face->MIndices[1]);
      indices.Add(face->MIndices[2]);
    }
    indicesOut = [.. indices];
  }

  private static unsafe Vector3 BuildVertexData(Vector3* vertices, uint vertexCount, out Vector3[] verticesOut)
  {
    Vector3 avg = Vector3.Zero;

    verticesOut = new Vector3[vertexCount];
    for (int i = 0; i < vertexCount; i++)
    {
      Vector3 vertex = new(vertices[i].X, vertices[i].Y, vertices[i].Z);

      avg.X = (avg.X + vertices[i].X) / 2.0f;
      avg.Y = (avg.Y + vertices[i].Y) / 2.0f;
      avg.Z = (avg.Z + vertices[i].Z) / 2.0f;

      verticesOut[i] = vertex;
    }
    return avg;
  }

  private static unsafe Vector3[] BuildNormalData(Vector3* normals, uint normalCount)
  {
    Vector3[] normalsOut = new Vector3[normalCount];
    for (int i = 0; i < normalCount; i++)
    {
      Vector3 normal = new(normals[i].X, normals[i].Y, normals[i].Z);
      normalsOut[i] = normal;
    }
    return normalsOut;
  }

  private static void OffsetVertices(Vector3[] vertices, Vector3 midPoint)
  {
    for (int i = 0; i < vertices.Length; i++)
    {
      vertices[i] = vertices[i] - midPoint;
    }
  }
}

using System.Numerics;
using System;
using Fidelity.Rendering.Resources;
using Silk.NET.Vulkan;
using Fidelity.Rendering.Enums;
using System.Runtime.CompilerServices;

namespace Fidelity.Rendering;

public class Mesh
{
  private GpuBuffer vertexBuffer, indexBuffer;
  private Vector3[] positionData = [];
  private Vector3[] normalData = [];
  private Vector2[] uvData = [];
  private Vector3[] tangentData = [];
  private Vector3[] bitangentData = [];
  private uint[] indexData = [];
  private VertexDataFormat vertexDataFormat;

  public uint VertexCount { get; private set; }
  public uint IndexCount { get; private set; }
  public uint Stride { get; private set; }
  public bool Indexed { get; private set; } = false;

  // Temp
  public GpuBuffer VertexBuffer => vertexBuffer;
  public GpuBuffer IndexBuffer => indexBuffer;

  [Flags]
  private enum VertexDataFormat
  {
    Position = 1 << 0,
    Normal = 1 << 1,
    Uv = 1 << 2,
    Tangent = 1 << 3,
    Bitanget = 1 << 4
  };

  public Mesh SetPositions(Vector3[] positions)
  {
    if (positions?.Length == 0)
      throw new ArgumentException("Positions must not be empty", nameof(positions));

    uint vertexCount = (uint)positions!.Length;
    VertexCount = VertexCount >= vertexCount || VertexCount == 0 ? vertexCount : VertexCount;

    positionData = positions!;
    vertexDataFormat |= VertexDataFormat.Position;
    return this;
  }

  public Mesh SetNormals(Vector3[] normals)
  {
    if (normals?.Length == 0)
      throw new ArgumentException("Normals must not be empty", nameof(normals));

    uint vertexCount = (uint)normals!.Length;
    VertexCount = VertexCount >= vertexCount || VertexCount == 0 ? vertexCount : VertexCount;

    normalData = normals!;
    vertexDataFormat |= VertexDataFormat.Normal;
    return this;
  }

  public Mesh SetUvs(Vector2[] uvs)
  {
    if (uvs?.Length == 0)
      throw new ArgumentException("UVs must not be empty", nameof(uvs));

    uint vertexCount = (uint)uvs!.Length;
    VertexCount = VertexCount >= vertexCount || VertexCount == 0 ? vertexCount : VertexCount;

    uvData = uvs!;
    vertexDataFormat |= VertexDataFormat.Uv;
    return this;
  }

  public Mesh SetTangents(Vector3[] tangents)
  {
    if (tangents?.Length == 0)
      throw new ArgumentException("Tangents must not be empty", nameof(tangents));

    uint vertexCount = (uint)tangents!.Length;
    VertexCount = VertexCount >= vertexCount || VertexCount == 0 ? vertexCount : VertexCount;

    tangentData = tangents!;
    vertexDataFormat |= VertexDataFormat.Tangent;
    return this;
  }

  public Mesh SetBitangents(Vector3[] bitangents)
  {
    if (bitangents?.Length == 0)
      throw new ArgumentException("Bitangents must not be empty", nameof(bitangents));

    uint vertexCount = (uint)bitangents!.Length;
    VertexCount = VertexCount >= vertexCount || VertexCount == 0 ? vertexCount : VertexCount;

    bitangentData = bitangents!;
    vertexDataFormat |= VertexDataFormat.Bitanget;
    return this;
  }

  public Mesh SetIndices(uint[] indices)
  {
    if (indices?.Length == 0)
      throw new ArgumentException("Indices must not be empty", nameof(indices));

    indexData = indices!;
    Indexed = true;
    return this;
  }

  public Mesh GenerateTangents()
  {
    if (!positionData.Any() && !uvData.Any())
    {
      return this;
    }

    if (Indexed)
    {
      Vector3[] tangents = new Vector3[positionData.Length];
      Vector3[] bitangents = new Vector3[positionData.Length];

      // TODO: improve this using vectors instead of floats
      for (uint i = 0; i < indexData.Length; i += 3)
      {
        Vector3 p0 = positionData[indexData[i]];
        Vector3 p1 = positionData[indexData[i + 1]];
        Vector3 p2 = positionData[indexData[i + 2]];

        Vector2 uv0 = uvData[indexData[i]];
        Vector2 uv1 = uvData[indexData[i + 1]];
        Vector2 uv2 = uvData[indexData[i + 2]];

        Vector3 q0 = p1 - p0;
        Vector3 q1 = p2 - p0;

        Vector3 s = new(uv1 - uv0, 0);
        Vector3 t = new(uv2 - uv0, 0);

        Vector3 tangent = new(0, 0, 0);
        Vector3 bitangent = new(0, 0, 0);
        float demon = (s[0] * t[1] - t[0] * s[1]);
        if (Math.Abs(demon) >= 0e8f)
        {
          float f = 1.0f / demon;
          s *= f;
          t *= f;

          tangent = t[1] * q0 - s[1] * q1;
          bitangent = s[0] * q1 - t[0] * q0;
        }

        tangents[indexData[i]] += tangent;
        tangents[indexData[i + 1]] += tangent;
        tangents[indexData[i + 2]] += tangent;

        bitangents[indexData[i]] += bitangent;
        bitangents[indexData[i + 1]] += bitangent;
        bitangents[indexData[i + 2]] += bitangent;
      }

      for (uint i = 0; i < positionData.Length; i++)
      {
        tangents[i] = Vector3.Normalize(tangents[i]);
        bitangents[i] = Vector3.Normalize(bitangents[i]);
      }

      SetTangents(tangents);
      SetBitangents(bitangents);
    }
    return this;
  }

  public Mesh GenerateNormals()
  {
    if (!positionData.Any())
    {
      return this;
    }

    Vector3[] normals = new Vector3[positionData.Length];
    if (Indexed)
    {
      for (int i = 0; i < indexData.Length; i += 3)
      {
        Vector3 vecA = positionData[indexData[i]];
        Vector3 vecB = positionData[indexData[i + 1]];
        Vector3 vecC = positionData[indexData[i + 2]];
        Vector3 vecAB = vecB - vecA;
        Vector3 vecAC = vecC - vecA;
        Vector3 normal = Vector3.Cross(vecAB, vecAC);
        normals[indexData[i]] += normal;
        normals[indexData[i + 1]] += normal;
        normals[indexData[i + 2]] += normal;
      }

      for (int i = 0; i < normals.Length; i++)
      {
        normals[i] = Vector3.Normalize(normals[i]);
      }
    }
    else
    {
      for (int i = 0; i < positionData.Length; i += 3)
      {
        Vector3 vecAB = positionData[i + 1] - positionData[i];
        Vector3 vecAC = positionData[i + 2] - positionData[i];
        Vector3 normal = Vector3.Normalize(Vector3.Cross(vecAB, vecAC));
        normals[i] = normal;
        normals[i + 1] = normal;
        normals[i + 2] = normal;
      }
    }
    SetNormals(normals);
    return this;
  }

  public Mesh Upload(Device device, PhysicalDevice physicalDevice, Resources.CommandPool commandPool, GraphicsQueue graphicsQueue)
  {
    UploadVertexData(device, physicalDevice, commandPool, graphicsQueue);
    UploadIndexData(device, physicalDevice, commandPool, graphicsQueue);

    return this;
  }

  
  private unsafe void UploadVertexData(Device device, PhysicalDevice physicalDevice, Resources.CommandPool commandPool, GraphicsQueue graphicsQueue)
  {
    var dataToUpload = CreateRestructuredVertexDataArray(out uint stride);

    ulong bufferSize = (ulong)(Unsafe.SizeOf<float>() * dataToUpload!.Length);

    using GpuBuffer staging = new GpuBuffer(device, physicalDevice)
      .Allocate(BufferType.Staging, bufferSize)
      .WriteDataArray(dataToUpload);

    vertexBuffer = new GpuBuffer(device, physicalDevice)
      .Allocate(BufferType.Vertex, bufferSize);

    staging.CopyData(vertexBuffer, bufferSize, commandPool, graphicsQueue);
  }

  private unsafe void UploadIndexData(Device device, PhysicalDevice physicalDevice, Resources.CommandPool commandPool, GraphicsQueue graphicsQueue)
  {
    ulong bufferSize = (ulong)(Unsafe.SizeOf<uint>() * indexData!.Length);

    using GpuBuffer staging = new(device, physicalDevice);
    staging.Allocate(BufferType.Staging, bufferSize)
      .WriteDataArray(indexData);

    indexBuffer = new GpuBuffer(device, physicalDevice)
      .Allocate(BufferType.Index, bufferSize);
    staging.CopyData(indexBuffer, bufferSize, commandPool, graphicsQueue);
  }

  private float[] CreateRestructuredVertexDataArray(out uint stride)
  {
    stride = 0;
    IList<float> restructredData = CreateVertexDataArray();
    if (vertexDataFormat.HasFlag(VertexDataFormat.Position))
    {
      stride += 3 * sizeof(float);
    }

    if (vertexDataFormat.HasFlag(VertexDataFormat.Normal))
    {
      stride += 3 * sizeof(float);
    }

    if (vertexDataFormat.HasFlag(VertexDataFormat.Uv))
    {
      stride += 2 * sizeof(float);
    }

    if (vertexDataFormat.HasFlag(VertexDataFormat.Tangent))
    {
      stride += 3 * sizeof(float);
    }

    if (vertexDataFormat.HasFlag(VertexDataFormat.Bitanget))
    {
      stride += 3 * sizeof(float);
    }

    for (int i = 0; i < VertexCount; i++)
    {
      if (vertexDataFormat.HasFlag(VertexDataFormat.Position))
      {
        restructredData.Add(positionData[i].X);
        restructredData.Add(positionData[i].Y);
        restructredData.Add(positionData[i].Z);
      }

      if (vertexDataFormat.HasFlag(VertexDataFormat.Normal))
      {
        restructredData.Add(normalData[i].X);
        restructredData.Add(normalData[i].Y);
        restructredData.Add(normalData[i].Z);
      }

      if (vertexDataFormat.HasFlag(VertexDataFormat.Uv))
      {
        restructredData.Add(uvData[i].X);
        restructredData.Add(uvData[i].Y);
      }

      if (vertexDataFormat.HasFlag(VertexDataFormat.Tangent))
      {
        restructredData.Add(tangentData[i].X);
        restructredData.Add(tangentData[i].Y);
        restructredData.Add(tangentData[i].Z);
      }

      if (vertexDataFormat.HasFlag(VertexDataFormat.Bitanget))
      {
        restructredData.Add(bitangentData[i].X);
        restructredData.Add(bitangentData[i].Y);
        restructredData.Add(bitangentData[i].Z);
      }
    }
    return restructredData.ToArray();
  }

  private IList<float> CreateVertexDataArray()
  {
    uint count = 0;
    if (vertexDataFormat.HasFlag(VertexDataFormat.Position))
    {
      count += VertexCount * 3;
    }

    if (vertexDataFormat.HasFlag(VertexDataFormat.Normal))
    {
      count += VertexCount * 3;
    }

    if (vertexDataFormat.HasFlag(VertexDataFormat.Uv))
    {
      count += VertexCount * 2;
    }

    if (vertexDataFormat.HasFlag(VertexDataFormat.Tangent))
    {
      count += VertexCount * 3;
    }

    if (vertexDataFormat.HasFlag(VertexDataFormat.Bitanget))
    {
      count += VertexCount * 3;
    }

    return new List<float>((int)count);
  }
}

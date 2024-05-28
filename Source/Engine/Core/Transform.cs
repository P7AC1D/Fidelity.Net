using System.Numerics;

namespace Engine.SceneManagement;

public class Transform
{
  public bool IsDirty { get; private set;} = true;
  public Vector3 Position { get; private set; } = Vector3.Zero;
  public Vector3 Scale { get; private set; } = Vector3.One;
  public Quaternion Rotation { get; private set; } = Quaternion.Identity;
  public Matrix4x4 Matrix { get; private set; } = Matrix4x4.Identity;
  public Vector3 Forward => Vector3.Normalize(new Vector3(Matrix.M13, Matrix.M23, Matrix.M33));
  public Vector3 Up => Vector3.Normalize(new Vector3(Matrix.M12, Matrix.M22, Matrix.M32));
  public Vector3 Right => Vector3.Normalize(new Vector3(Matrix.M11, Matrix.M21, Matrix.M31));

  public Transform()
  {
    UpdateTransform();
  }

  public Transform(Matrix4x4 matrix)
  {
    Matrix4x4.Decompose(matrix, out Vector3 scale, out Quaternion rotation, out Vector3 position);
    Position = position;
    Scale = scale;
    Rotation = rotation;
    UpdateTransform();
  }

  public Transform Translate(Vector3 translation)
  {
    Position += translation;
    IsDirty = true;
    return this;
  }

  public Transform Rotate(Quaternion rotation)
  {
    Rotation *= rotation;
    IsDirty = true;
    return this;
  }

  public Transform ScaleBy(Vector3 scale)
  {
    Scale *= scale;
    Vector3.Clamp(Scale, Vector3.One * 0.1f, Vector3.One * 100.0f);
    IsDirty = true;
    return this;
  }

  public Transform SetPosition(Vector3 position)
  {
    Position = position;
    IsDirty = true;
    return this;
  }

  public Transform SetScale(Vector3 scale)
  {
    Scale = scale;
    Vector3.Clamp(Scale, Vector3.One * 0.1f, Vector3.One * 100.0f);
    IsDirty = true;
    return this;
  }

  public Transform SetRotation(Quaternion rotation)
  {
    Rotation = rotation;
    IsDirty = true;
    return this;
  }

  public Transform LookAt(Vector3 eye, Vector3 target, Vector3 up)
  {
    Matrix = Matrix4x4.CreateLookAt(eye, target, up);
    Matrix4x4.Decompose(Matrix, out Vector3 scale, out Quaternion rotation, out Vector3 position);
    Position = position;
    Scale = scale;
    Rotation = rotation;
    return this;
  }

  public void Update()
  {
    if (IsDirty)
    {
      UpdateTransform();
      IsDirty = false;
    }
  }

  public static Transform operator *(Transform a, Transform b)
  {
    return new Transform(a.Matrix * b.Matrix);
  }

  private void UpdateTransform()
  {
    Matrix = Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateFromQuaternion(Rotation) * Matrix4x4.CreateTranslation(Position);
  }
}

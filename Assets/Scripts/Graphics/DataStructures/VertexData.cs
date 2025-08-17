using UnityEngine;

public class VertexData
{
    public Vector3[] Positions { get; private set; }
    public Vector3[] Normals { get; private set; }
    public Vector2[] UVs { get; private set; }

    public VertexData(int vertexCount)
    {
        Positions = new Vector3[vertexCount];
        Normals = new Vector3[vertexCount];
        UVs = new Vector2[vertexCount];
    }

    public void SetPosition(int index, Vector3 position)
    {
        if (index < 0 || index >= Positions.Length)
            throw new System.IndexOutOfRangeException("Index out of range for vertex positions.");
        Positions[index] = position;
    }

    public void SetNormal(int index, Vector3 normal)
    {
        if (index < 0 || index >= Normals.Length)
            throw new System.IndexOutOfRangeException("Index out of range for vertex normals.");
        Normals[index] = normal;
    }

    public void SetUV(int index, Vector2 uv)
    {
        if (index < 0 || index >= UVs.Length)
            throw new System.IndexOutOfRangeException("Index out of range for vertex UVs.");
        UVs[index] = uv;
    }
}
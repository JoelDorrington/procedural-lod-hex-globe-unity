using System.Collections.Generic;
using UnityEngine;

public class GlobeGrid : MonoBehaviour
{
    public int gridWidth;
    public int gridHeight;
    public float cellSize;

    private Cell[,] cells;

    void Start()
    {
        GenerateGrid();
    }

    void GenerateGrid()
    {
        cells = new Cell[gridWidth, gridHeight];

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                cells[x, y] = new Cell();
            }
        }

    }


    public Cell GetCell(int x, int y)
    {
        if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
        {
            return cells[x, y];
        }
        return null;
    }
}
using System.Collections.Generic;

namespace HexGlobeProject.HexMap
{
    /// <summary>
    /// Represents an individual cell on the globe with a latitude and longitude position.
    /// </summary>
    public class CellNode
    {
        public int Id { get; private set; }
        public float Latitude { get; private set; }
        public float Longitude { get; private set; }
        public List<CellNode> Neighbors { get; private set; }

        public CellNode(int id, float latitude, float longitude)
        {
            Id = id;
            Latitude = latitude;
            Longitude = longitude;
            Neighbors = new List<CellNode>();
        }

        /// <summary>
        /// Adds a neighboring cell if it isn't already present.
        /// </summary>
        /// <param name="neighbor">The neighboring CellNode.</param>
        public void AddNeighbor(CellNode neighbor)
        {
            if (neighbor != null && !Neighbors.Contains(neighbor))
            {
                Neighbors.Add(neighbor);
            }
        }
    }

    /// <summary>
    /// A simple graph structure to manage cells and their neighbor relationships.
    /// </summary>
    public class CellGraph
    {
        private readonly Dictionary<int, CellNode> nodeDict = new Dictionary<int, CellNode>();

        /// <summary>
        /// Adds a new cell to the graph with the specified ID and geographic position.
        /// </summary>
        /// <param name="id">Unique identifier for the cell.</param>
        /// <param name="latitude">Latitude in degrees.</param>
        /// <param name="longitude">Longitude in degrees.</param>
        public void AddCell(int id, float latitude, float longitude)
        {
            if (!nodeDict.ContainsKey(id))
            {
                nodeDict.Add(id, new CellNode(id, latitude, longitude));
            }
        }

        /// <summary>
        /// Connects two cells by their IDs, adding them as neighbors in both directions.
        /// </summary>
        /// <param name="id1">First cell ID.</param>
        /// <param name="id2">Second cell ID.</param>
        public void ConnectCells(int id1, int id2)
        {
            if (nodeDict.TryGetValue(id1, out CellNode cell1) && nodeDict.TryGetValue(id2, out CellNode cell2))
            {
                cell1.AddNeighbor(cell2);
                cell2.AddNeighbor(cell1);
            }
        }

        /// <summary>
        /// Retrieves all cells in the graph.
        /// </summary>
        /// <returns>An enumerable collection of CellNode objects.</returns>
        public IEnumerable<CellNode> GetAllCells()
        {
            return nodeDict.Values;
        }

        /// <summary>
        /// Retrieves a cell by its unique identifier.
        /// </summary>
        /// <param name="id">The cell's ID.</param>
        /// <returns>The CellNode if found; otherwise, null.</returns>
        public CellNode GetCellById(int id)
        {
            if (nodeDict.TryGetValue(id, out CellNode cell))
            {
                return cell;
            }
            return null;
        }
    }
}

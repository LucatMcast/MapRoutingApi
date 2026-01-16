using System.Collections.Concurrent;

namespace MapRoutingApi.Models
{
    public class Edge
    {
        public string Target { get; set; } = string.Empty;
        public int Distance { get; set; }
    }

    public class Node
    {
        public string Name { get; set; } = string.Empty;
        public List<Edge> Edges { get; set; } = new();
    }
}

namespace MapRoutingApi.Services
{
    using MapRoutingApi.Models;

    public interface IMapService
    {
        void SetMap(List<Node> nodes);
        List<Node> GetMap();
        (List<string> Path, int Distance) GetShortestPath(string from, string to);
    }

    public class MapService : IMapService
    {
        // Thread-safe container not strictly necessary for this assignment but Im used to it
        private List<Node> _nodes = new();
        private readonly object _lock = new();

        public void SetMap(List<Node> nodes)
        {
            lock (_lock)
            {
                _nodes = nodes;
            }
        }

        public List<Node> GetMap()
        {
            lock (_lock)
            {
                // Return deep copy or list copy? For this assignment, just returning the list is fine.
                // To be safe against modification, we return a new list.
                return new List<Node>(_nodes);
            }
        }

        public (List<string> Path, int Distance) GetShortestPath(string startName, string endName)
        {
            Dictionary<string, Node> nodeMap;
            lock (_lock)
            {
                nodeMap = _nodes.ToDictionary(n => n.Name, n => n);
            }

            if (!nodeMap.ContainsKey(startName) || !nodeMap.ContainsKey(endName))
            {
                return (new List<string>(), -1); // Nodes not found
            }

            var distances = new Dictionary<string, int>();
            var previous = new Dictionary<string, string>();
            var priorityQueue = new PriorityQueue<string, int>();

            foreach (var node in nodeMap.Keys)
            {
                distances[node] = int.MaxValue;
            }

            distances[startName] = 0;
            priorityQueue.Enqueue(startName, 0);

            while (priorityQueue.Count > 0)
            {
                string currentName = priorityQueue.Dequeue();
                
                if (currentName == endName)
                    break;

                // If we found a shorter path before, skip (standard PQ optimization though strictly not needed with this impl)
                
                var currentNode = nodeMap[currentName];

                foreach (var edge in currentNode.Edges)
                {
                    if (!nodeMap.ContainsKey(edge.Target)) continue; // Safety check

                    int newDist = distances[currentName] + edge.Distance;
                    if (newDist < distances[edge.Target])
                    {
                        distances[edge.Target] = newDist;
                        previous[edge.Target] = currentName;
                        priorityQueue.Enqueue(edge.Target, newDist);
                    }
                }
            }

            if (distances[endName] == int.MaxValue)
            {
                return (new List<string>(), -1); // No path found
            }

            // Reconstruct path
            var path = new List<string>();
            string? step = endName;

            while (step != null)
            {
                path.Insert(0, step);
                if (step == startName) break;
                previous.TryGetValue(step, out step);
            }

            return (path, distances[endName]);
        }
    }
}

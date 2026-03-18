using Milvaion.Application.Features.Workflows.CreateWorkflow;

namespace Milvaion.Application.Features.Workflows;

/// <summary>
/// Extension methods for workflow step collections.
/// </summary>
public static class WorkflowStepExtensions
{
    /// <summary>
    /// Validates that the step graph contains no cycles using Kahn's topological sort.
    /// Returns <see langword="true"/> if the steps form a valid DAG.
    /// </summary>
    public static bool ValidateDAG(this List<CreateWorkflowStepDto> steps)
    {
        if (steps == null || steps.Count == 0)
            return true;

        var adjacency = new Dictionary<string, List<string>>();
        var inDegree = new Dictionary<string, int>();

        foreach (var step in steps)
        {
            var id = step.TempId ?? step.GetHashCode().ToString();
            adjacency.TryAdd(id, []);
            inDegree.TryAdd(id, 0);
        }

        foreach (var step in steps)
        {
            var id = step.TempId ?? step.GetHashCode().ToString();

            if (!string.IsNullOrWhiteSpace(step.DependsOnTempIds))
            {
                var deps = step.DependsOnTempIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var dep in deps)
                {
                    if (adjacency.TryGetValue(dep, out var neighbors))
                    {
                        neighbors.Add(id);
                        inDegree[id] = inDegree.GetValueOrDefault(id) + 1;
                    }
                }
            }
        }

        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var visited = 0;

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            visited++;

            foreach (var neighbor in adjacency.GetValueOrDefault(node, []))
            {
                inDegree[neighbor]--;

                if (inDegree[neighbor] == 0)
                    queue.Enqueue(neighbor);
            }
        }

        return visited == steps.Count;
    }
}

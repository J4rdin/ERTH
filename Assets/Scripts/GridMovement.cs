using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CombatTransitionController))]
[RequireComponent(typeof(LineRenderer))]
public class GridMovement : MonoBehaviour
{
    private CombatTransitionController transitionController;
    private GridManager gridManager;
    private Animator animator;
    private LineRenderer pathLine;
    private CombatantStats stats;

    [Header("Movement Settings")]
    public float moveSpeed = 6f;
    public int defaultMovementRange = 5;
    public bool allowDiagonalSteps = false;
    public float lineHeightOffset = 0.12f;

    [Header("Preview Colors")]
    public Color validPathColor = new Color(1f, 0.85f, 0.1f, 0.95f);
    public Color blockedPathColor = new Color(1f, 0.2f, 0.2f, 0.95f);

    private bool isMoving = false;
    private bool canSelectMoveTile = false;
    private bool isPathValid = false;
    private Vector2Int lastHoveredCell = new Vector2Int(int.MinValue, int.MinValue);
    private List<Vector2Int> currentPreviewPath = new List<Vector2Int>();

    private class AStarNode
    {
        public Vector2Int position;
        public AStarNode parent;
        public int gCost;
        public int hCost;
        public int fCost => gCost + hCost;

        public AStarNode(Vector2Int pos)
        {
            position = pos;
        }
    }

    void Start()
    {
        transitionController = GetComponent<CombatTransitionController>();
        gridManager = FindFirstObjectByType<GridManager>();
        animator = GetComponent<Animator>();
        pathLine = GetComponent<LineRenderer>();
        stats = GetComponent<CombatantStats>();

        ConfigureLineRenderer();
        ClearPathPreview();
    }

    private int GetMoveRange()
    {
        return stats != null ? Mathf.Max(0, stats.movementRange) : defaultMovementRange;
    }

    public void SetMoveSelectionActive(bool active)
    {
        canSelectMoveTile = active;
        ClearPathPreview();

        if (gridManager == null)
            return;

        if (active)
        {
            Vector2Int startCell = gridManager.GetGridPosition(transform.position);
            List<Vector2Int> reachable = GetReachableCells(startCell, GetMoveRange());
            gridManager.HighlightReachableTiles(reachable);
        }
        else
        {
            gridManager.ClearTileHighlights();
        }
    }

    private void ConfigureLineRenderer()
    {
        pathLine.useWorldSpace = true;
        pathLine.startWidth = 0.14f;
        pathLine.endWidth = 0.14f;
        pathLine.numCapVertices = 4;
        pathLine.numCornerVertices = 4;
        pathLine.startColor = validPathColor;
        pathLine.endColor = validPathColor;
        pathLine.positionCount = 0;

        if (pathLine.sharedMaterial == null)
        {
            Shader unlitShader = Shader.Find("Sprites/Default");
            if (unlitShader != null)
            {
                Material mat = new Material(unlitShader);
                mat.color = validPathColor;
                pathLine.material = mat;
            }
        }
    }

    void Update()
    {
        if (transitionController == null || transitionController.currentMode != GameMode.GridCombat || !canSelectMoveTile || isMoving)
        {
            ClearPathPreview();
            return;
        }

        if (Mouse.current == null || Camera.main == null || gridManager == null)
        {
            ClearPathPreview();
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            ClearPathPreview();
            return;
        }

        bool hitBlockedLayer = ((1 << hit.collider.gameObject.layer) & gridManager.blockedLayers.value) != 0;
        Vector2Int startCell = gridManager.GetGridPosition(transform.position);
        Vector2Int targetCell = gridManager.GetGridPosition(hit.point);

        if (startCell == targetCell)
        {
            ClearPathPreview();
            return;
        }

        if (targetCell != lastHoveredCell)
        {
            lastHoveredCell = targetCell;

            bool cellBlocked = hitBlockedLayer || !IsWithinGridBounds(targetCell) || gridManager.IsCellBlocked(targetCell);
            if (cellBlocked)
            {
                isPathValid = false;
                currentPreviewPath.Clear();
                List<Vector2Int> invalidLine = BuildDirectPreviewLine(startCell, targetCell);
                DrawPathPreview(startCell, invalidLine, isValid: false);
            }
            else
            {
                List<Vector2Int> foundPath = FindPathAStar(startCell, targetCell);
                int maxSteps = GetMoveRange();

                if (foundPath.Count > 0 && foundPath.Count <= maxSteps)
                {
                    isPathValid = true;
                    currentPreviewPath = foundPath;
                    DrawPathPreview(startCell, currentPreviewPath, isValid: true);
                }
                else
                {
                    isPathValid = false;
                    currentPreviewPath.Clear();
                    List<Vector2Int> previewLine = foundPath.Count > 0
                        ? foundPath
                        : BuildDirectPreviewLine(startCell, targetCell);
                    DrawPathPreview(startCell, previewLine, isValid: false);
                }
            }
        }

        if (Mouse.current.leftButton.wasPressedThisFrame && isPathValid && currentPreviewPath.Count > 0)
        {
            List<Vector2Int> pathToFollow = new List<Vector2Int>(currentPreviewPath);
            canSelectMoveTile = false;
            ClearPathPreview();
            gridManager.ClearTileHighlights();
            StartCoroutine(FollowTilePath(pathToFollow));
        }
    }

    //SQUARE MAP: Phazed out for test
    //private List<Vector2Int> GetReachableCells(Vector2Int start, int maxSteps)
    //{
    //    List<Vector2Int> reachable = new List<Vector2Int>();
    //    if (maxSteps <= 0 || gridManager == null)
    //        return reachable;

    //    Queue<(Vector2Int cell, int steps)> queue = new Queue<(Vector2Int, int)>();
    //    HashSet<Vector2Int> visited = new HashSet<Vector2Int> { start };
    //    queue.Enqueue((start, 0));

    //    while (queue.Count > 0)
    //    {
    //        var (current, steps) = queue.Dequeue();
    //        if (steps >= maxSteps)
    //            continue;

    //        foreach (Vector2Int neighbor in GetNeighbors(current))
    //        {
    //            if (visited.Contains(neighbor))
    //                continue;

    //            if (gridManager.IsCellBlocked(neighbor))
    //            {
    //                visited.Add(neighbor);
    //                continue;
    //            }

    //            visited.Add(neighbor);
    //            int nextSteps = steps + 1;
    //            reachable.Add(neighbor);
    //            queue.Enqueue((neighbor, nextSteps));
    //        }
    //    }

    //    return reachable;
    //}

    public List<Vector2Int> GetReachableCells(Vector2Int startCell, int maxRange)
    {
        List<Vector2Int> reachable = new List<Vector2Int>();
        if (gridManager == null || maxRange <= 0) return reachable;

        Queue<(Vector2Int cell, int cost)> queue = new Queue<(Vector2Int, int)>();
        Dictionary<Vector2Int, int> bestCost = new Dictionary<Vector2Int, int>();

        queue.Enqueue((startCell, 0));
        bestCost[startCell] = 0;

        while (queue.Count > 0)
        {
            var (current, costSoFar) = queue.Dequeue();

            if (current != startCell)
            {
                reachable.Add(current);
            }

            if (costSoFar >= maxRange) continue;

            foreach (Vector2Int next in GetNeighbors(current))
            {
                if (gridManager.IsCellBlocked(next)) continue;

                int nextCost = costSoFar + 1;
                if (!bestCost.TryGetValue(next, out int existingCost) || nextCost < existingCost)
                {
                    bestCost[next] = nextCost;
                    queue.Enqueue((next, nextCost));
                }
            }
        }

        return reachable;
    }

    private List<Vector2Int> BuildDirectPreviewLine(Vector2Int start, Vector2Int target)
    {
        List<Vector2Int> line = new List<Vector2Int>();
        Vector2Int current = start;
        int safetyCap = 100;

        while (current != target && safetyCap-- > 0)
        {
            int dx = target.x - current.x;
            int dz = target.y - current.y;
            int stepX = dx == 0 ? 0 : (int)Mathf.Sign(dx);
            int stepZ = dz == 0 ? 0 : (int)Mathf.Sign(dz);

            if (allowDiagonalSteps)
            {
                current = new Vector2Int(current.x + stepX, current.y + stepZ);
            }
            else if (Mathf.Abs(dx) >= Mathf.Abs(dz) && stepX != 0)
            {
                current = new Vector2Int(current.x + stepX, current.y);
            }
            else if (stepZ != 0)
            {
                current = new Vector2Int(current.x, current.y + stepZ);
            }
            else
            {
                break;
            }

            line.Add(current);
        }

        return line;
    }

    private void DrawPathPreview(Vector2Int startCell, List<Vector2Int> path, bool isValid)
    {
        if (path == null || path.Count == 0)
        {
            pathLine.positionCount = 0;
            return;
        }

        Color activeColor = isValid ? validPathColor : blockedPathColor;
        pathLine.startColor = activeColor;
        pathLine.endColor = activeColor;
        if (pathLine.material != null)
        {
            pathLine.material.color = activeColor;
        }

        pathLine.positionCount = path.Count + 1;

        Vector3 startPos = gridManager.GetCellCenterWorld(startCell);
        startPos.y += lineHeightOffset;
        pathLine.SetPosition(0, startPos);

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 cellPos = gridManager.GetCellCenterWorld(path[i]);
            cellPos.y += lineHeightOffset;
            pathLine.SetPosition(i + 1, cellPos);
        }
    }

    private void ClearPathPreview()
    {
        lastHoveredCell = new Vector2Int(int.MinValue, int.MinValue);
        isPathValid = false;
        currentPreviewPath.Clear();
        if (pathLine != null && pathLine.positionCount != 0)
        {
            pathLine.positionCount = 0;
        }
    }

    private bool IsWithinGridBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < gridManager.width &&
               cell.y >= 0 && cell.y < gridManager.height;
    }

    private List<Vector2Int> FindPathAStar(Vector2Int start, Vector2Int target)
    {
        List<AStarNode> openSet = new List<AStarNode>();
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, AStarNode> allNodes = new Dictionary<Vector2Int, AStarNode>();

        AStarNode startNode = new AStarNode(start)
        {
            gCost = 0,
            hCost = GetHeuristic(start, target)
        };

        openSet.Add(startNode);
        allNodes[start] = startNode;

        while (openSet.Count > 0)
        {
            AStarNode currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].fCost < currentNode.fCost ||
                    (openSet[i].fCost == currentNode.fCost && openSet[i].hCost < currentNode.hCost))
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode.position);

            if (currentNode.position == target)
            {
                return ReconstructPath(currentNode);
            }

            foreach (Vector2Int neighborPos in GetNeighbors(currentNode.position))
            {
                if (closedSet.Contains(neighborPos))
                    continue;

                if (gridManager.IsCellBlocked(neighborPos))
                {
                    closedSet.Add(neighborPos);
                    continue;
                }

                int stepCost = GetMoveCost(currentNode.position, neighborPos);
                int tentativeGCost = currentNode.gCost + stepCost;

                if (!allNodes.TryGetValue(neighborPos, out AStarNode neighborNode))
                {
                    neighborNode = new AStarNode(neighborPos);
                    allNodes[neighborPos] = neighborNode;
                }

                if (!openSet.Contains(neighborNode) || tentativeGCost < neighborNode.gCost)
                {
                    neighborNode.parent = currentNode;
                    neighborNode.gCost = tentativeGCost;
                    neighborNode.hCost = GetHeuristic(neighborPos, target);

                    if (!openSet.Contains(neighborNode))
                    {
                        openSet.Add(neighborNode);
                    }
                }
            }
        }

        return new List<Vector2Int>();
    }

    private List<Vector2Int> ReconstructPath(AStarNode endNode)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        AStarNode curr = endNode;

        while (curr != null && curr.parent != null)
        {
            path.Add(curr.position);
            curr = curr.parent;
        }

        path.Reverse();
        return path;
    }

    //SQUARE MAP: Phazed out for test
    //private IEnumerable<Vector2Int> GetNeighbors(Vector2Int pos)
    //{
    //    Vector2Int[] cardinals =
    //    {
    //        new Vector2Int(pos.x + 1, pos.y),
    //        new Vector2Int(pos.x - 1, pos.y),
    //        new Vector2Int(pos.x, pos.y + 1),
    //        new Vector2Int(pos.x, pos.y - 1)
    //    };

    //    foreach (Vector2Int c in cardinals)
    //    {
    //        if (IsWithinGridBounds(c))
    //            yield return c;
    //    }

    //    if (allowDiagonalSteps)
    //    {
    //        Vector2Int[] diagonals =
    //        {
    //            new Vector2Int(pos.x + 1, pos.y + 1),
    //            new Vector2Int(pos.x - 1, pos.y + 1),
    //            new Vector2Int(pos.x + 1, pos.y - 1),
    //            new Vector2Int(pos.x - 1, pos.y - 1)
    //        };

    //        foreach (Vector2Int d in diagonals)
    //        {
    //            if (!IsWithinGridBounds(d))
    //                continue;

    //            Vector2Int sideA = new Vector2Int(d.x, pos.y);
    //            Vector2Int sideB = new Vector2Int(pos.x, d.y);
    //            if (gridManager.IsCellBlocked(sideA) && gridManager.IsCellBlocked(sideB))
    //                continue;

    //            yield return d;
    //        }
    //    }
    //}

    private IEnumerable<Vector2Int> GetNeighbors(Vector2Int pos)
    {
        bool isOddRow = (pos.y % 2) == 1;

        Vector2Int[] offsets = isOddRow
            ? new Vector2Int[]
              {
              new Vector2Int(pos.x + 1, pos.y),     // Right
              new Vector2Int(pos.x - 1, pos.y),     // Left
              new Vector2Int(pos.x,     pos.y + 1), // Top-Left
              new Vector2Int(pos.x + 1, pos.y + 1), // Top-Right
              new Vector2Int(pos.x,     pos.y - 1), // Bottom-Left
              new Vector2Int(pos.x + 1, pos.y - 1)  // Bottom-Right
              }
            : new Vector2Int[]
              {
              new Vector2Int(pos.x + 1, pos.y),     // Right
              new Vector2Int(pos.x - 1, pos.y),     // Left
              new Vector2Int(pos.x - 1, pos.y + 1), // Top-Left
              new Vector2Int(pos.x,     pos.y + 1), // Top-Right
              new Vector2Int(pos.x - 1, pos.y - 1), // Bottom-Left
              new Vector2Int(pos.x,     pos.y - 1)  // Bottom-Right
              };

        foreach (Vector2Int n in offsets)
        {
            if (n.x >= 0 && n.x < gridManager.width && n.y >= 0 && n.y < gridManager.height)
            {
                yield return n;
            }
        }
    }

    //SQUARE MAP: Phazed out for test
    //private int GetMoveCost(Vector2Int from, Vector2Int to)
    //{
    //    int dx = Mathf.Abs(to.x - from.x);
    //    int dz = Mathf.Abs(to.y - from.y);
    //    return (dx + dz == 2) ? 14 : 10;
    //}

    //private int GetHeuristic(Vector2Int a, Vector2Int b)
    //{
    //    int dx = Mathf.Abs(a.x - b.x);
    //    int dz = Mathf.Abs(a.y - b.y);

    //    if (allowDiagonalSteps)
    //    {
    //        int diag = Mathf.Min(dx, dz);
    //        int straight = Mathf.Abs(dx - dz);
    //        return 14 * diag + 10 * straight;
    //    }

    //    return 10 * (dx + dz);
    //}

    private int GetMoveCost(Vector2Int from, Vector2Int to)
    {
        return 10;
    }

    private int GetHeuristic(Vector2Int a, Vector2Int b)
    {
        // Manhattan distance in offset rows/cols works as an admissible A* heuristic
        return 10 * (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y));
    }

    private IEnumerator FollowTilePath(List<Vector2Int> path)
    {
        isMoving = true;

        if (animator != null)
            animator.SetFloat("Speed", moveSpeed);

        foreach (Vector2Int cell in path)
        {
            Vector3 targetWorldPos = gridManager.GetCellCenterWorld(cell);
            targetWorldPos.y = transform.position.y;

            Vector3 startPosition = transform.position;
            float distance = Vector3.Distance(startPosition, targetWorldPos);
            float duration = distance > 0.001f ? distance / moveSpeed : 0f;

            Vector3 lookDirection = targetWorldPos - startPosition;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }

            float elapsedTime = 0f;
            while (elapsedTime < duration)
            {
                transform.position = Vector3.Lerp(startPosition, targetWorldPos, elapsedTime / duration);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            transform.position = targetWorldPos;
        }

        if (animator != null)
            animator.SetFloat("Speed", 0f);

        isMoving = false;
        Debug.Log($"[Grid Move] Completed A* path of {path.Count} tiles.");
    }
}
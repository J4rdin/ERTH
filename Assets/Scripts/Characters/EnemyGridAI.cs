using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyGridAI : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5.5f;
    public int defaultMovementRange = 4;
    public bool allowDiagonalSteps = false;

    private GridManager gridManager;
    private CombatantStats stats;
    private Animator animator;
    private bool isTakingTurn = false;

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

    void Awake()
    {
        gridManager = FindFirstObjectByType<GridManager>();
        stats = GetComponent<CombatantStats>();
        animator = GetComponent<Animator>();
    }

    public int GetMoveRange()
    {
        return stats != null ? Mathf.Max(0, stats.movementRange) : defaultMovementRange;
    }

    public void ExecuteTurn(Transform playerTransform, Action onTurnComplete = null)
    {
        if (isTakingTurn) return;
        StartCoroutine(TurnRoutine(playerTransform, onTurnComplete));
    }

    private IEnumerator TurnRoutine(Transform playerTransform, Action onTurnComplete)
    {
        isTakingTurn = true;

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        int maxSteps = GetMoveRange();
        if (gridManager != null && playerTransform != null && maxSteps > 0)
        {
            Vector2Int startCell = gridManager.GetGridPosition(transform.position);
            Vector2Int playerCell = gridManager.GetGridPosition(playerTransform.position);

            List<Vector2Int> movePath = ComputePathTowardPlayer(startCell, playerCell, maxSteps);
            if (movePath.Count > 0)
            {
                yield return StartCoroutine(FollowTilePath(movePath));
            }
        }

        isTakingTurn = false;
        onTurnComplete?.Invoke();
    }

    private List<Vector2Int> ComputePathTowardPlayer(Vector2Int startCell, Vector2Int playerCell, int maxSteps)
    {
        List<Vector2Int> candidateGoals = new List<Vector2Int>();
        foreach (Vector2Int neighbor in GetNeighbors(playerCell))
        {
            if (!gridManager.IsCellBlocked(neighbor) && neighbor != playerCell)
            {
                candidateGoals.Add(neighbor);
            }
        }

        candidateGoals.Sort((a, b) => GetHeuristic(startCell, a).CompareTo(GetHeuristic(startCell, b)));

        List<Vector2Int> bestPath = null;
        foreach (Vector2Int goal in candidateGoals)
        {
            List<Vector2Int> path = FindPathAStar(startCell, goal);
            if (path.Count > 0)
            {
                bestPath = path;
                break;
            }
        }

        if (bestPath == null)
        {
            bestPath = FindClosestStepToward(startCell, playerCell);
        }

        if (bestPath == null || bestPath.Count == 0)
            return new List<Vector2Int>();

        int stepsToTake = Mathf.Min(bestPath.Count, maxSteps);
        List<Vector2Int> trimmedPath = new List<Vector2Int>(stepsToTake);
        for (int i = 0; i < stepsToTake; i++)
        {
            trimmedPath.Add(bestPath[i]);
        }

        return trimmedPath;
    }

    private List<Vector2Int> FindClosestStepToward(Vector2Int start, Vector2Int playerCell)
    {
        Vector2Int bestCell = start;
        int bestDist = GetHeuristic(start, playerCell);

        for (int x = 0; x < gridManager.width; x++)
        {
            for (int y = 0; y < gridManager.height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (cell == start || cell == playerCell || gridManager.IsCellBlocked(cell))
                    continue;

                int distToPlayer = GetHeuristic(cell, playerCell);
                if (distToPlayer < bestDist)
                {
                    List<Vector2Int> candidatePath = FindPathAStar(start, cell);
                    if (candidatePath.Count > 0)
                    {
                        bestDist = distToPlayer;
                        bestCell = cell;
                    }
                }
            }
        }

        return bestCell == start ? new List<Vector2Int>() : FindPathAStar(start, bestCell);
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

    private IEnumerable<Vector2Int> GetNeighbors(Vector2Int pos)
    {
        Vector2Int[] cardinals =
        {
            new Vector2Int(pos.x + 1, pos.y),
            new Vector2Int(pos.x - 1, pos.y),
            new Vector2Int(pos.x, pos.y + 1),
            new Vector2Int(pos.x, pos.y - 1)
        };

        foreach (Vector2Int c in cardinals)
        {
            if (IsWithinGridBounds(c))
                yield return c;
        }

        if (allowDiagonalSteps)
        {
            Vector2Int[] diagonals =
            {
                new Vector2Int(pos.x + 1, pos.y + 1),
                new Vector2Int(pos.x - 1, pos.y + 1),
                new Vector2Int(pos.x + 1, pos.y - 1),
                new Vector2Int(pos.x - 1, pos.y - 1)
            };

            foreach (Vector2Int d in diagonals)
            {
                if (!IsWithinGridBounds(d))
                    continue;

                Vector2Int sideA = new Vector2Int(d.x, pos.y);
                Vector2Int sideB = new Vector2Int(pos.x, d.y);
                if (gridManager.IsCellBlocked(sideA) && gridManager.IsCellBlocked(sideB))
                    continue;

                yield return d;
            }
        }
    }

    private bool IsWithinGridBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < gridManager.width &&
               cell.y >= 0 && cell.y < gridManager.height;
    }

    private int GetMoveCost(Vector2Int from, Vector2Int to)
    {
        int dx = Mathf.Abs(to.x - from.x);
        int dz = Mathf.Abs(to.y - from.y);
        return (dx + dz == 2) ? 14 : 10;
    }

    private int GetHeuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dz = Mathf.Abs(a.y - b.y);

        if (allowDiagonalSteps)
        {
            int diag = Mathf.Min(dx, dz);
            int straight = Mathf.Abs(dx - dz);
            return 14 * diag + 10 * straight;
        }

        return 10 * (dx + dz);
    }

    private IEnumerator FollowTilePath(List<Vector2Int> path)
    {
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

        Debug.Log($"[Enemy AI] {name} moved {path.Count} tiles toward player.");
    }
}
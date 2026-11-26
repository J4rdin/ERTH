using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid Dimensions")]
    public int width = 12;
    public int height = 12;
    public float cellSize = 1f;
    public Vector3 gridOrigin = Vector3.zero;

    [Header("Layers & Walkability")]
    public LayerMask groundLayer;
    public LayerMask blockedLayers;
    public float groundRayHeight = 10f;
    public float groundRayDistance = 25f;
    public float tileHeightOffset = 0.08f;

    [Header("Tile Visuals & Range Highlighting")]
    [Range(0.1f, 0.99f)]
    public float tileScaleRatio = 0.9f;
    public Color tileColor = new Color(1f, 1f, 1f, 0.45f);
    public Color reachableTileColor = new Color(0.15f, 0.95f, 0.45f, 0.75f);

    private readonly List<GameObject> spawnedTiles = new List<GameObject>();
    private readonly Dictionary<Vector2Int, GameObject> tileLookup = new Dictionary<Vector2Int, GameObject>();
    private readonly Dictionary<Vector2Int, Renderer> tileRenderers = new Dictionary<Vector2Int, Renderer>();
    private readonly HashSet<Vector2Int> blockedCells = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, float> cellGroundHeights = new Dictionary<Vector2Int, float>();

    private Material sharedTileMaterial;

    void Reset()
    {
        tileHeightOffset = 0.08f;
        tileScaleRatio = 0.9f;
        tileColor = new Color(1f, 1f, 1f, 0.45f);
        reachableTileColor = new Color(0.15f, 0.95f, 0.45f, 0.75f);
    }

    void Start()
    {
        GenerateGrid();
        SetGridVisibility(false);
    }

    private Material GetOrCreateTileMaterial()
    {
        if (sharedTileMaterial != null)
            return sharedTileMaterial;

        Shader shader = Shader.Find("Sprites/Default")
            ?? Shader.Find("Unlit/Transparent")
            ?? Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            Debug.LogError("[GridManager] No valid tile shader found.");
            return null;
        }

        sharedTileMaterial = new Material(shader);
        sharedTileMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (sharedTileMaterial.HasProperty("_SrcBlend"))
            sharedTileMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (sharedTileMaterial.HasProperty("_DstBlend"))
            sharedTileMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (sharedTileMaterial.HasProperty("_ZWrite"))
            sharedTileMaterial.SetInt("_ZWrite", 0);

        ApplyColorToMaterial(sharedTileMaterial, tileColor);
        return sharedTileMaterial;
    }

    private void ApplyColorToRenderer(Renderer r, Color col)
    {
        if (r == null) return;
        ApplyColorToMaterial(r.material, col);
    }

    private void ApplyColorToMaterial(Material mat, Color col)
    {
        if (mat == null) return;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", col);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", col);

        mat.color = col;
    }

    public void GenerateGrid()
    {
        ClearGrid();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                Vector3 samplePos = new Vector3(
                    gridOrigin.x + (x + 0.5f) * cellSize,
                    gridOrigin.y + groundRayHeight,
                    gridOrigin.z + (y + 0.5f) * cellSize
                );

                if (!Physics.Raycast(samplePos, Vector3.down, out RaycastHit groundHit, groundRayDistance, groundLayer))
                    continue;

                float groundY = groundHit.point.y;
                cellGroundHeights[cell] = groundY;

                Vector3 checkCenter = new Vector3(
                    gridOrigin.x + (x + 0.5f) * cellSize,
                    groundY + (cellSize * 0.5f),
                    gridOrigin.z + (y + 0.5f) * cellSize
                );
                Vector3 halfExtents = Vector3.one * (cellSize * 0.45f);

                if (Physics.CheckBox(checkCenter, halfExtents, Quaternion.identity, blockedLayers))
                {
                    blockedCells.Add(cell);
                }

                CreateTileVisual(cell, groundY);
            }
        }

        Debug.Log($"[GridManager] Generated {spawnedTiles.Count} tiles ({blockedCells.Count} blocked).");
    }

    //SQUARE MAP: Phazed out for test
    //private void CreateTileVisual(Vector2Int cell, float groundY)
    //{
    //    Vector3 tilePos = new Vector3(
    //        gridOrigin.x + (cell.x + 0.5f) * cellSize,
    //        groundY + tileHeightOffset,
    //        gridOrigin.z + (cell.y + 0.5f) * cellSize
    //    );

    //    GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
    //    tile.name = $"Tile_{cell.x}_{cell.y}";
    //    tile.transform.SetParent(transform);
    //    tile.transform.position = tilePos;
    //    tile.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
    //    tile.transform.localScale = Vector3.one * (cellSize * tileScaleRatio);

    //    Collider col = tile.GetComponent<Collider>();
    //    if (col != null)
    //    {
    //        Destroy(col);
    //    }

    //    Renderer r = tile.GetComponent<Renderer>();
    //    if (r != null)
    //    {
    //        Material baseMat = GetOrCreateTileMaterial();
    //        if (baseMat != null)
    //        {
    //            r.material = new Material(baseMat);
    //        }
    //        ApplyColorToRenderer(r, tileColor);
    //        tileRenderers[cell] = r;
    //    }

    //    spawnedTiles.Add(tile);
    //    tileLookup[cell] = tile;
    //}

    public void ClearGrid()
    {
        foreach (GameObject tile in spawnedTiles)
        {
            if (tile != null)
                Destroy(tile);
        }

        spawnedTiles.Clear();
        tileLookup.Clear();
        tileRenderers.Clear();
        blockedCells.Clear();
        cellGroundHeights.Clear();
    }

    public void SetGridVisibility(bool visible)
    {
        if (visible && spawnedTiles.Count == 0)
        {
            GenerateGrid();
        }

        foreach (GameObject tile in spawnedTiles)
        {
            if (tile != null)
            {
                tile.SetActive(visible);
            }
        }
    }

    //SQUARE MAP: Phazed out for test
    //public Vector2Int GetGridPosition(Vector3 worldPos)
    //{
    //    int x = Mathf.FloorToInt((worldPos.x - gridOrigin.x) / cellSize);
    //    int y = Mathf.FloorToInt((worldPos.z - gridOrigin.z) / cellSize);
    //    return new Vector2Int(x, y);
    //}

    //SQUARE MAP: Phazed out for test
    //public Vector3 GetCellCenterWorld(Vector2Int cell)
    //{
    //    float y = cellGroundHeights.TryGetValue(cell, out float recordedY)
    //        ? recordedY
    //        : gridOrigin.y;

    //    return new Vector3(
    //        gridOrigin.x + (cell.x + 0.5f) * cellSize,
    //        y,
    //        gridOrigin.z + (cell.y + 0.5f) * cellSize
    //    );
    //}

    //HEX TEST START
    private float GetHexRadius()
    {
        return cellSize * 0.5f * tileScaleRatio;
    }

    private float GetHexWidth(float radius)
    {
        return Mathf.Sqrt(3f) * radius;
    }

    private float GetRowStep(float radius)
    {
        return 1.5f * radius;
    }

    public Vector3 GetCellCenterWorld(Vector2Int cell)
    {
        float radius = GetHexRadius();
        float hexWidth = GetHexWidth(radius);
        float rowStep = GetRowStep(radius);
        float rowOffset = (cell.y % 2 == 1) ? hexWidth * 0.5f : 0f;

        float y = cellGroundHeights.TryGetValue(cell, out float recordedY)
            ? recordedY
            : gridOrigin.y;

        return new Vector3(
            gridOrigin.x + cell.x * hexWidth + rowOffset + hexWidth * 0.5f,
            y,
            gridOrigin.z + cell.y * rowStep + radius
        );
    }

    public Vector2Int GetGridPosition(Vector3 worldPos)
    {
        float radius = GetHexRadius();
        float hexWidth = GetHexWidth(radius);
        float rowStep = GetRowStep(radius);

        int approxRow = Mathf.Clamp(
            Mathf.RoundToInt((worldPos.z - gridOrigin.z - radius) / rowStep),
            0,
            height - 1
        );

        float rowOffset = (approxRow % 2 == 1) ? hexWidth * 0.5f : 0f;
        int approxCol = Mathf.Clamp(
            Mathf.RoundToInt((worldPos.x - gridOrigin.x - rowOffset - hexWidth * 0.5f) / hexWidth),
            0,
            width - 1
        );

        // Check candidate cells around the approximate hit and pick the closest hex center on XZ
        Vector2Int bestCell = new Vector2Int(approxCol, approxRow);
        float bestDistSq = float.MaxValue;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                Vector2Int candidate = new Vector2Int(approxCol + dx, approxRow + dy);
                if (candidate.x < 0 || candidate.x >= width || candidate.y < 0 || candidate.y >= height)
                    continue;

                Vector3 center = GetCellCenterWorld(candidate);
                float distSq = (worldPos.x - center.x) * (worldPos.x - center.x) +
                               (worldPos.z - center.z) * (worldPos.z - center.z);

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestCell = candidate;
                }
            }
        }

        return bestCell;
    }

    private void CreateTileVisual(Vector2Int cell, float groundY)
    {
        float radius = GetHexRadius();
        Vector3 center = GetCellCenterWorld(cell);
        Vector3 tilePos = new Vector3(center.x, groundY + tileHeightOffset, center.z);

        GameObject tile = new GameObject($"HexTile_{cell.x}_{cell.y}");
        tile.transform.SetParent(transform);
        tile.transform.position = tilePos;
        tile.transform.rotation = Quaternion.identity;

        MeshFilter mf = tile.AddComponent<MeshFilter>();
        mf.sharedMesh = CreatePointyHexMesh(radius * 0.94f); // Slight gap between hexes

        MeshRenderer mr = tile.AddComponent<MeshRenderer>();
        Material baseMat = GetOrCreateTileMaterial();
        if (baseMat != null)
        {
            mr.material = new Material(baseMat);
        }

        ApplyColorToRenderer(mr, tileColor);
        tileRenderers[cell] = mr;

        spawnedTiles.Add(tile);
        tileLookup[cell] = tile;
    }

    private Mesh CreatePointyHexMesh(float radius)
    {
        Mesh mesh = new Mesh { name = "PointyHexMesh" };

        Vector3[] vertices = new Vector3[7];
        vertices[0] = Vector3.zero;

        // Pointy-top angles start at 30 degrees and step by 60 degrees
        for (int i = 0; i < 6; i++)
        {
            float angleRad = Mathf.Deg2Rad * (30f + 60f * i);
            vertices[i + 1] = new Vector3(
                radius * Mathf.Cos(angleRad),
                0f,
                radius * Mathf.Sin(angleRad)
            );
        }

        int[] triangles = new int[18];
        for (int i = 0; i < 6; i++)
        {
            int next = (i == 5) ? 1 : i + 2;
            triangles[i * 3 + 0] = 0;
            triangles[i * 3 + 1] = next;
            triangles[i * 3 + 2] = i + 1;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }
    //HEX TEST END
    public bool IsCellBlocked(Vector2Int cell)
    {
        if (cell.x < 0 || cell.x >= width || cell.y < 0 || cell.y >= height)
            return true;

        if (!cellGroundHeights.ContainsKey(cell))
            return true;

        return blockedCells.Contains(cell);
    }

    public void HighlightReachableTiles(IEnumerable<Vector2Int> cells)
    {
        ClearTileHighlights();
        if (cells == null) return;

        foreach (Vector2Int cell in cells)
        {
            if (tileLookup.TryGetValue(cell, out GameObject tile) && tile != null && tile.activeSelf)
            {
                if (tileRenderers.TryGetValue(cell, out Renderer r) && r != null)
                {
                    ApplyColorToRenderer(r, reachableTileColor);
                }
            }
        }
    }

    public void ClearTileHighlights()
    {
        foreach (var pair in tileRenderers)
        {
            if (pair.Value != null)
            {
                ApplyColorToRenderer(pair.Value, tileColor);
            }
        }
    }
}
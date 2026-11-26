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

    private void CreateTileVisual(Vector2Int cell, float groundY)
    {
        Vector3 tilePos = new Vector3(
            gridOrigin.x + (cell.x + 0.5f) * cellSize,
            groundY + tileHeightOffset,
            gridOrigin.z + (cell.y + 0.5f) * cellSize
        );

        GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
        tile.name = $"Tile_{cell.x}_{cell.y}";
        tile.transform.SetParent(transform);
        tile.transform.position = tilePos;
        tile.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        tile.transform.localScale = Vector3.one * (cellSize * tileScaleRatio);

        Collider col = tile.GetComponent<Collider>();
        if (col != null)
        {
            Destroy(col);
        }

        Renderer r = tile.GetComponent<Renderer>();
        if (r != null)
        {
            Material baseMat = GetOrCreateTileMaterial();
            if (baseMat != null)
            {
                r.material = new Material(baseMat);
            }
            ApplyColorToRenderer(r, tileColor);
            tileRenderers[cell] = r;
        }

        spawnedTiles.Add(tile);
        tileLookup[cell] = tile;
    }

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

    public Vector2Int GetGridPosition(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt((worldPos.x - gridOrigin.x) / cellSize);
        int y = Mathf.FloorToInt((worldPos.z - gridOrigin.z) / cellSize);
        return new Vector2Int(x, y);
    }

    public Vector3 GetCellCenterWorld(Vector2Int cell)
    {
        float y = cellGroundHeights.TryGetValue(cell, out float recordedY)
            ? recordedY
            : gridOrigin.y;

        return new Vector3(
            gridOrigin.x + (cell.x + 0.5f) * cellSize,
            y,
            gridOrigin.z + (cell.y + 0.5f) * cellSize
        );
    }

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
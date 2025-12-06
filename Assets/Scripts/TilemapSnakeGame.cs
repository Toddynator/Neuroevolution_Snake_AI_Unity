using System.Collections.Generic;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
using static UnityEngine.Rendering.HableCurve;

/*
Encapsulates logic for updating a tilemap on a GameObject based on a Snake Game.
Previously had one MonoBehaviour script that handled both the snake game and tilemap logic, but I wanted to abstract them to support parallelisation, and not have duplicate code which would result
in me having to update 2 practically identical scripts when I want to update the underlying snake logic.

I tried to be as efficient as possible with my tilemap by only updating tiles that have been modified in the Snake Grid. This means the logic for this script is a little bit more 
complicated than just simply looping through each tile in the tilemap and setting the tile based on the underlying Snake Grid.
This is probably pretty unnecessary honestly since it's snake, but I suppose if I later set the grid sizes to enormous values then it may pay off.
 */

public class TilemapSnakeGame : MonoBehaviour
{
    public Color AppleColor = new Color(1.0f, 0.0f, 0.0f);
    public Color WallColor = new Color(1.0f, 1.0f, 1.0f);
    private Color headColor;
    private Color tailColor;
    private bool useSnakeColourGradient = false;
    private List<Color> segmentColours = new List<Color>(); // Will remain a size of 1 unless gradient is enabled in the Game Manager.

    private SnakeGame snakeGame = new SnakeGame();
    private List<Vector2Int> segments; // Should only be a reference
    private TileType[,] grid; // Should only be a reference
    private Vector2Int previousApplePosition;
    private Vector2Int previousSnakeTailPosition;
    private int previousSnakeLength = 0;

    public Tilemap tilemap; // What it renders tiles to
    private Tile baseTile; // Can just colour this tile

    public void Initialize(DNA newDNA, GameManager gameManager)
    {
        snakeGame.Initialize(newDNA, gameManager);

        grid = snakeGame.GetGrid();
        segments = snakeGame.GetSegments();
        previousSnakeTailPosition = segments[0];

        /// Game Manager Settings

        useSnakeColourGradient = gameManager.SnakeColourGradient;
        headColor = gameManager.headColor;
        tailColor = gameManager.tailColor;
        segmentColours.Add(headColor);

        /// Create a tile resource

        baseTile = ScriptableObject.CreateInstance<Tile>();
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        baseTile.sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        baseTile.flags = TileFlags.None; // By default tiles are set to lock the colour.

        /// Place Wall Tiles

        Vector2Int gridSize = snakeGame.GetGridSize();
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                if (grid[x, y] == TileType.Wall)
                {
                    updateTilemapTile(new Vector2Int(x, y), WallColor);
                }
                else if (grid[x, y] == TileType.Apple)
                {
                    updateTilemapTile(new Vector2Int(x, y), AppleColor);
                }
            }
        }
    }
    
    public void Restart(DNA newDNA)
    {
        removeTilemapTile(snakeGame.GetApplePosition());
        for (int i = 0; i < segments.Count; i++)
        {
            removeTilemapTile(segments[i]);
        }
        Vector2Int gridSize = snakeGame.GetGridSize();
        // If snake head collided with the wall, replace the tile (incase it was removed)
        if (segments[0].x == 0 || segments[0].x == gridSize.x - 1 || segments[0].y == 0 || segments[0].y == gridSize.y - 1)
        {
            updateTilemapTile(segments[0], WallColor);
        }

        snakeGame.Restart(newDNA);
        segments = snakeGame.GetSegments(); // Need to update reference because segments are replaced.
        updateTilemapTile(snakeGame.GetApplePosition(), AppleColor);
    }

    public void UpdateSnake()
    {
        /// UPDATE GAME

        snakeGame.Update();
        if (!snakeGame.alive) { return; }

        //// UPDATE TILEMAP

        /// Apple

        Vector2Int newApplePosition = snakeGame.GetApplePosition();
        if (previousApplePosition != newApplePosition)
        {
            updateTilemapTile(newApplePosition, AppleColor);
        }

        /// Snake

        // If it has grown
        if (previousSnakeLength < segments.Count - 1)
        {
            /// UPDATE COLOUR GRADIENT (If enabled)

            if (useSnakeColourGradient)
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    // Gradient from head to tail
                    Color color = Color.Lerp(headColor, tailColor, (float)i / (segments.Count - 1));
                    if (i > segmentColours.Count - 1) { segmentColours.Add(color); }
                    else { segmentColours[i] = color; }
                }
            }
        }
        removeTilemapTile(previousSnakeTailPosition); // only need to remove the tail since that is the only tile that can be empty.
        for (int i = 1; i < segments.Count; i++)
        {
            if (useSnakeColourGradient) { updateTilemapTile(segments[i], segmentColours[i]); }
            else { updateTilemapTile(segments[i], segmentColours[0]); }
        }
        if (snakeGame.alive) { updateTilemapTile(segments[0], segmentColours[0]); } // Purely aesthetic, I want the snake when it dies to NOT have the head overlap what it collided with.

        /// SENSES VISUALIZATIONS

        Vector2Int gridSize = snakeGame.GetGridSize();
        Vector2Int direction = snakeGame.GetDirection();
        float rayLength = Mathf.Max(gridSize.x, gridSize.y);
        Vector2 position = new Vector2(this.transform.position.x, this.transform.position.y) + segments[0];
        Vector3 directionVector3 = new Vector3(direction.x, direction.y, 0.0f);
        Debug.DrawRay(position + new Vector2(0.5f, 0.5f), directionVector3 * rayLength, Color.blue); // Draws a line in Scene View (You can see it in the game if gizmo's are enabled).

        /// MAINTAIN PREVIOUS STATS FOR TILEMAP UPDATING

        previousApplePosition = snakeGame.GetApplePosition();
        previousSnakeTailPosition = segments[segments.Count - 1];
        previousSnakeLength = segments.Count - 1;
    }

    private void updateTilemapTile(Vector2Int gridPosition, Color color)
    {
        Vector3Int tilePosition = new Vector3Int(gridPosition.x, gridPosition.y, 0);
        tilemap.SetTile(tilePosition, baseTile);
        tilemap.SetColor(tilePosition, color);
    }

    private void removeTilemapTile(Vector2Int gridPosition)
    {
        Vector3Int tilePosition = new Vector3Int(gridPosition.x, gridPosition.y, 0);
        tilemap.SetTile(tilePosition, null);
    }

    public SnakeGame GetSnakeGame() { return snakeGame; }
}

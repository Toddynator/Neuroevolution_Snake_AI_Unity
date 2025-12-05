using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class SnakeBehaviour : MonoBehaviour
{
    private GameManager GameManager; // For any game settings that the snake may need to check. Should probably remove this and pass in variables on Init!

    /// CORE

    public Tilemap tilemap; // What it renders tiles to
    private Tile baseTile; // Can just colour this tile
    private TileType[,] grid; // X & Y, Represents every tile in the game of Snake.
    private Vector2Int gridSize; // For convenience
    private Vector2Int applePosition;
    private Vector2Int snakeHeadStartPosition;
    private List<Vector2Int> segments;
    private List<Color> segmentColours = new List<Color>(); // Will remain a size of 1 unless gradient is enabled in the Game Manager.
    public DNA dna; // How the Snake chooses its actions.
    public Color AppleColor = new Color(1.0f, 0.0f, 0.0f);
    public Color WallColor = new Color(1.0f, 1.0f, 1.0f);

    /// MOVEMENT

    private Vector2Int direction = Vector2Int.right;
    private Vector2Int prevDirection = Vector2Int.right; // For stopping the snake from moving back into itself. Only important if I don't use turnDirection for whatever reason. (E.g. playing snake myself)
    private int turnDirection = 0; // -1 to 1, 0 to move forward

    /// STATS

    public int distanceToObstacleInFront;
    public float distanceToApple; // Since I want to consider distance even if it isn't in line of sight, this is a float (To account for diagonals)
    public bool seeApple;
    public bool alive = true;
    private bool diedToCollision = false;
    private int numMovesSinceLastApple = 0;
    public int numberOfApplesConsumed = 0;
    public int numOfMoves = 0;
    public int averageMovesPerApple = 0;
    public int numOfMovesWhenGreatestLengthReached = 0;

    public void Initialize(DNA newDNA, GameManager gameManager)
    {
        dna = newDNA;
        GameManager = gameManager;
        segmentColours.Add(gameManager.headColor);

        baseTile = ScriptableObject.CreateInstance<Tile>();
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        baseTile.sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        baseTile.flags = TileFlags.None; // By default tiles are set to lock the colour.

        /// SETUP THE WALLS 

        gridSize = new Vector2Int((int)gameManager.SceneSize.x, (int)gameManager.SceneSize.y);
        grid = new TileType[gridSize.x, gridSize.y];
        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                // Create walls at the edges of the arena.
                if (x == 0 || x == gridSize.x - 1 || y == 0 || y == gridSize.y - 1)
                {
                    grid[x, y] = TileType.Wall;
                }
            }
        }
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

        snakeHeadStartPosition = new Vector2Int((int)(gridSize.x / 2), (int)(gridSize.y / 2)); // Start at the midpoint.
        segments = new List<Vector2Int>();
        segments.Add(snakeHeadStartPosition);
        updateTilemapTile(snakeHeadStartPosition, segmentColours[0]);
        spawnApple();
    }

    public void Restart(DNA newDNA)
    {
        dna = newDNA;

        // Revert Stats
        direction = Vector2Int.right;
        prevDirection = Vector2Int.right;
        turnDirection = 0;
        alive = true;
        diedToCollision = false;
        numMovesSinceLastApple = 0;
        numberOfApplesConsumed = 0;
        numOfMoves = 0;
        averageMovesPerApple = 0;
        numOfMovesWhenGreatestLengthReached = 0;
        distanceToApple = 0;
        distanceToObstacleInFront = 0;
        seeApple = false;

        // EDGE CASE: Remove apple first incase it was overlapped by snake
        grid[applePosition.x, applePosition.y] = TileType.Empty;
        removeTilemapTile(applePosition);
        // Remove segments
        for (int i = 0; i < segments.Count; i++) 
        {
            grid[segments[i].x, segments[i].y] = TileType.Empty;
            removeTilemapTile(segments[i]);
        }
        // Replace wall tile if the snake was over a wall position.
        if (segments[0].x == 0 || segments[0].x == gridSize.x - 1 || segments[0].y == 0 || segments[0].y == gridSize.y - 1)
        {
            grid[segments[0].x, segments[0].y] = TileType.Wall;
            updateTilemapTile(segments[0], WallColor);
        }
        segments[0] = snakeHeadStartPosition;
        segments = new List<Vector2Int>();
        segments.Add(snakeHeadStartPosition);
        if (segmentColours.Count <= 0) { segmentColours.Add(GameManager.headColor); }
        updateTilemapTile(segments[0], segmentColours[0]);

        // Regenerate Apple
        spawnApple();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Ignore directly opposite movement inputs so that snake doesn't go back into itself.
        // Manual controls for an actual person to play snake if I add a way to disable the AI.

        //if (Input.GetKeyDown(KeyCode.W) && prevDirection != Vector2Int.down)
        //{
        //    direction = Vector2Int.up;
        //}
        //else if (Input.GetKeyDown(KeyCode.S) && prevDirection != Vector2Int.up)
        //{
        //    direction = Vector2Int.down;
        //}
        //else if (Input.GetKey(KeyCode.A) && prevDirection != Vector2Int.right)
        //{
        //    direction = Vector2Int.left;
        //}
        //else if (Input.GetKeyDown(KeyCode.D) && prevDirection != Vector2Int.left)
        //{
        //    direction = Vector2Int.right;
        //}
    }

    // Fixed update is ideal for Classic Snake, can control the rate of the game by changing the TimeStep of the project.
    private void FixedUpdate()
    {
        if(!alive) { return; }

        //// MOVE SNAKE

        updateDirection();

        /// Update Segments
        // Only need to remove the last segment of the snake.
        grid[segments[segments.Count - 1].x, segments[segments.Count - 1].y] = TileType.Empty; 
        removeTilemapTile(segments[segments.Count - 1]); 
        for (int i = segments.Count - 1; i > 0; i--)
        {
            segments[i] = segments[i - 1];
            grid[segments[i].x, segments[i].y] = TileType.Snake;         
            if(GameManager.SnakeColourGradient) { updateTilemapTile(segments[i], segmentColours[i]); }
            else { updateTilemapTile(segments[i], segmentColours[0]); }
        }
        /// Update Head
        segments[0] += direction;

        prevDirection = direction;
        numOfMoves++;
        numMovesSinceLastApple++;

        // Check for collisions ~ Only need to check for collisions in the head.
        if (grid[segments[0].x, segments[0].y] == TileType.Wall || grid[segments[0].x, segments[0].y] == TileType.Snake)
        {
            diedToCollision = true;
            gameOver();
        }
        else if (grid[segments[0].x, segments[0].y] == TileType.Apple)
        {
            grow();
            spawnApple();

            numberOfApplesConsumed++;
            numOfMovesWhenGreatestLengthReached = numOfMoves;
            numMovesSinceLastApple = 0;
        }
        if (alive) // This is more a personal choice for visuals, I don't want the snake head to overlap the object it collided with on death.
        {
            grid[segments[0].x, segments[0].y] = TileType.Snake;
            updateTilemapTile(segments[0], segmentColours[0]);
        }

        //// SNAKE SENSES ~ What it sees

        Vector2 differenceAppleAndSnakeVector = applePosition - segments[0];
        distanceToApple = differenceAppleAndSnakeVector.magnitude;

        float rayLength = Mathf.Max(GameManager.SceneSize.x, GameManager.SceneSize.y);
        Vector2 position = new Vector2(this.transform.position.x, this.transform.position.y) + segments[0];
        Vector3 directionVector3 = new Vector3(direction.x, direction.y, 0.0f);
        Debug.DrawRay(position+ new Vector2(0.5f,0.5f), directionVector3 * rayLength, Color.blue); // Draws a line in Scene View (You can see it in the game if gizmo's are enabled).

        seeApple = false;
        Vector2Int currentScanPosition = segments[0];
        if (currentScanPosition.x >= gridSize.x - 1 || currentScanPosition.x <= 0 || currentScanPosition.y <= 0 || currentScanPosition.y >= gridSize.y - 1)
        {
            distanceToObstacleInFront = 0;
        }
        else
        {
            for (int i = 0; i < Mathf.Max(gridSize.x, gridSize.y); i++)
            {
                currentScanPosition += direction;
                TileType scannedTile = grid[currentScanPosition.x, currentScanPosition.y];
                if (scannedTile == TileType.Apple)
                {
                    seeApple = true;
                }
                else if (scannedTile != TileType.Empty)
                {
                    Vector2Int difference = currentScanPosition - segments[0];
                    distanceToObstacleInFront = (int)difference.magnitude;
                    break;
                }
            }
        }

        //// TERMINATE EARLY ~ e.g. snake takes too long

        if (numMovesSinceLastApple >= GameManager.SceneSize.x * GameManager.SceneSize.y)
        {
            gameOver();
        }
        // NOTE: This should be removed if I change dna to instead be used as weightings and therefore have a 'reactive' AI.
        if (numMovesSinceLastApple > dna.genes.Length)
        {
            gameOver();
        }
    }

    private void grow()
    {
        for (int i = 0; i < GameManager.GrowthPerApple; i++)
        {
            Vector2Int segment = segments[segments.Count - 1];
            segments.Add(segment);
        }

        /// UPDATE COLOUR GRADIENT (If enabled)

        if (GameManager.SnakeColourGradient)
        {
            for (int i = 0; i < segments.Count; i++)
            {            
                // Gradient from head to tail
                Color color = Color.Lerp(GameManager.headColor, GameManager.tailColor, (float)i / (segments.Count - 1));
                if (i > segmentColours.Count - 1) { segmentColours.Add(color); }
                else { segmentColours[i] = color; }                 
            }
        }

        // EDGE CASE: Snake has outgrown the level.
        if (segments.Count >= (grid.GetLength(0) - 1) * (grid.GetLength(1) - 1)) { gameOver(); return; }
    }

    private void spawnApple()
    {
        /// TODO, add a way to use the same seed every time, should use apples consumed to ensure apples spawn in a different location each time as well.
        /// Also should improve this so that it keeps track of positions it has already tried.   

        if (GameManager.fixedRNGSeed) { Random.InitState(GameManager.randomGenerationSeed + numberOfApplesConsumed); } // ensure consistent results.
        bool emptyPositionFound = false;
        while (!emptyPositionFound)
        {
            applePosition = Vector2Int.zero;
            // Account for walls on the edge tiles.
            applePosition.x = UnityEngine.Random.Range(1, gridSize.x - 1);
            applePosition.y = UnityEngine.Random.Range(1, gridSize.y - 1);
            // Check if its already occupied by the snake
            if (grid[applePosition.x, applePosition.y] == TileType.Empty)
            {
                emptyPositionFound = true;
                grid[applePosition.x, applePosition.y] = TileType.Apple;
            }        
        }
        updateTilemapTile(new Vector2Int(applePosition.x, applePosition.y), AppleColor);
    }

    private void gameOver()
    {
        alive = false;
        if (numberOfApplesConsumed > 0) { averageMovesPerApple = numOfMovesWhenGreatestLengthReached / numberOfApplesConsumed; }
    }

    private void updateDirection()
    {
        // Turn snake ~ Currently use Genes as a list of inputs.
        turnDirection = dna.genes[numOfMoves % dna.genes.Length];
        if (turnDirection == -1) // Turn Left
        {
            if (direction == Vector2Int.up)
            {
                direction = Vector2Int.left;
            }
            else if (direction == Vector2Int.left)
            {
                direction = Vector2Int.down;
            }
            else if (direction == Vector2Int.down)
            {
                direction = Vector2Int.right;
            }
            else
            {
                direction = Vector2Int.up;
            }
        }
        else if (turnDirection == 1) // Turn Right
        {
            if (direction == Vector2.up)
            {
                direction = Vector2Int.right;
            }
            else if (direction == Vector2.right)
            {
                direction = Vector2Int.down;
            }
            else if (direction == Vector2.down)
            {
                direction = Vector2Int.left;
            }
            else
            {
                direction = Vector2Int.up;
            }
        }
    }

    public float CalculateFitness()
    {
        float score = 0.0f;

        const float SCORE_PER_APPLE = 10.0f;
        const float SCORE_MOVES_MULTIPLIER = 2.0f; // I want to reward optimal routes
        const float SCORE_COLLISION_PENALTY_MULTIPLIER = 0.9f;
        const float SCORE_DECAY_RATE = 0.01f; // Should improve this to be based on maximum number of moves possible in a scene.
        float maxPossibleDistanceToApple = gridSize.magnitude;

        score += numberOfApplesConsumed * SCORE_PER_APPLE; // Primarily reward based on number of apples gained
        score += SCORE_PER_APPLE * (1.0f - distanceToApple / maxPossibleDistanceToApple); // Reward getting closer to the apple with each generation
        if (diedToCollision) { score *= SCORE_COLLISION_PENALTY_MULTIPLIER; } // Penalise the snake killing itself so that the generations don't get trapped on DNA that involves moving into a wall
        score = score * Mathf.Exp(numOfMovesWhenGreatestLengthReached * -(SCORE_DECAY_RATE)) * SCORE_MOVES_MULTIPLIER; // Should reward / penalise for taking too many moves to get each apple.

        dna.fitness = score;

        return score;
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
}

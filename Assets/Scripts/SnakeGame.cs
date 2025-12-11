using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using Unity.Burst.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

public class SnakeGame
{
    /// CORE

    private TileType[,] grid; // X & Y, Represents every tile in the game of Snake.
    private Vector2Int gridSize; // For convenience
    private Vector2Int applePosition;
    private Vector2Int snakeHeadStartPosition;
    private List<Vector2Int> segments;
    public DNA dna; // The weightings used in the neural network.
    private NeuralNetwork neuralNetwork; // How the Snake chooses its actions.

    private System.Random random;
    private int randomGenerationSeed;
    private bool useFixedRNGSeed = false;
    private int growthPerApple = 1;
    private int numHiddenLayers = 0;
    private int numHiddenLayerNeurons = 0;

    /// MOVEMENT

    private Vector2Int direction = Vector2Int.right;
    private Vector2Int prevDirection = Vector2Int.right; // For stopping the snake from moving back into itself. Only important if I don't use turnDirection for whatever reason. (E.g. playing snake myself)
    private int turnDirection = 0; // -1 to 1, 0 to move forward

    /// STATS

    public int distanceToObstacleInFront;
    public int distanceToLeftObstacle;
    public int distanceToRightObstacle;
    public float distanceToApple; // Since I want to consider distance even if it isn't in line of sight, this is a float (To account for diagonals)
    public Vector2 directionToApple;
    public bool seeApple;
    public bool alive = true;
    private bool diedToCollision = false;
    public int numMovesSinceLastApple = 0;
    public int numberOfApplesConsumed = 0;
    public int numOfMoves = 0;
    public int averageMovesPerApple = 0;
    public int numOfMovesWhenGreatestLengthReached = 0;
    public float totalMoveEfficiency = 0; // 0 to 1, 1 when snake takes minimum moves needed to get to an apple.
    public float minimumMovesToApple = 0;
    public float averageMoveEfficiency = 1.0f;
    public int distanceToWallBehind = 0;

    public void Initialize(DNA newDNA, GameManager gameManager)
    {
        dna = newDNA;
        randomGenerationSeed = gameManager.randomGenerationSeed;
        useFixedRNGSeed = gameManager.fixedRNGSeed;
        growthPerApple = gameManager.GrowthPerApple;
        if (useFixedRNGSeed)
        {
            random = new System.Random(randomGenerationSeed);
        }
        else
        {
            random = new System.Random();
        }

        /// NEURAL NETWORK

        numHiddenLayers = gameManager.numberOfHiddenLayers;
        numHiddenLayerNeurons = gameManager.numberOfHiddenLayerNeurons;
        neuralNetwork = new NeuralNetwork(newDNA, GameManager.numberOfInputNeurons, GameManager.numberOfOutputNeurons, numHiddenLayers, numHiddenLayerNeurons);

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

        snakeHeadStartPosition = new Vector2Int((int)(gridSize.x / 2), (int)(gridSize.y / 2)); // Start at the midpoint.
        segments = new List<Vector2Int>();
        segments.Add(snakeHeadStartPosition);
        spawnApple();
    }
    public void Restart(DNA newDNA, GameManager gameManager)
    {
        useFixedRNGSeed = gameManager.fixedRNGSeed;
        if (useFixedRNGSeed)
        {
            random = new System.Random(randomGenerationSeed);
        }
        else
        {
            random = new System.Random();
        }
        dna = newDNA;
        numHiddenLayers = gameManager.numberOfHiddenLayers;
        numHiddenLayerNeurons = gameManager.numberOfHiddenLayerNeurons;
        neuralNetwork = new NeuralNetwork(newDNA, GameManager.numberOfInputNeurons, GameManager.numberOfOutputNeurons, numHiddenLayers, numHiddenLayerNeurons);

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
        averageMoveEfficiency = 0;
        totalMoveEfficiency = 0;

        // EDGE CASE: Remove apple first incase it was overlapped by snake
        grid[applePosition.x, applePosition.y] = TileType.Empty;
        // Remove segments
        for (int i = 0; i < segments.Count; i++)
        {
            grid[segments[i].x, segments[i].y] = TileType.Empty;
        }
        // Replace wall tile if the snake was over a wall position.
        if (segments[0].x == 0 || segments[0].x == gridSize.x - 1 || segments[0].y == 0 || segments[0].y == gridSize.y - 1)
        {
            grid[segments[0].x, segments[0].y] = TileType.Wall;
        }
        segments[0] = snakeHeadStartPosition;
        segments = new List<Vector2Int>();
        segments.Add(snakeHeadStartPosition);

        // Regenerate Apple
        spawnApple();
    }

    public void Update()
    {
        if (!alive) { return; }

        //// MOVE SNAKE

        determineTurnDirection();
        direction = determineDirection(turnDirection);

        /// Update Segments
        // Only need to remove the last segment of the snake.
        grid[segments[segments.Count - 1].x, segments[segments.Count - 1].y] = TileType.Empty;
        for (int i = segments.Count - 1; i > 0; i--)
        {
            segments[i] = segments[i - 1];
            grid[segments[i].x, segments[i].y] = TileType.Snake;
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
        }

        //// SNAKE SENSES ~ What it sees

        directionToApple = applePosition - segments[0];
        distanceToApple = directionToApple.magnitude;

        /// SCAN FOR OBSTACLES

        seeApple = false;
        Vector2Int currentScanPosition = segments[0];
        Vector2Int currentLeftScanPosition = segments[0];
        Vector2Int currentRightScanPosition = segments[0];
        Vector2Int leftScanDirection = determineDirection(-1);
        Vector2Int rightScanDirection = determineDirection(1);
        bool frontHit = false;
        bool leftHit = false;
        bool rightHit = false;
        if (currentScanPosition.x >= gridSize.x - 1 || currentScanPosition.x <= 0 || currentScanPosition.y <= 0 || currentScanPosition.y >= gridSize.y - 1)
        {
            distanceToObstacleInFront = 0;
        }
        else
        {
            // Will scan for furthest possible distance snake can be from a tile
            for (int i = 0; i < Mathf.Max(gridSize.x, gridSize.y); i++)
            {
                // Exit prematurely if all scans are complete
                if (frontHit && leftHit && rightHit) { break; }

                /// FORWARD SCAN
                if (!frontHit)
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
                        frontHit = true;
                    }
                }

                /// LEFT SCAN
                if (!leftHit)
                {
                    currentLeftScanPosition += leftScanDirection;
                    TileType scannedLeftTile = grid[currentLeftScanPosition.x, currentLeftScanPosition.y];
                    if (scannedLeftTile != TileType.Empty && scannedLeftTile != TileType.Apple)
                    {
                        Vector2Int difference = currentLeftScanPosition - segments[0];
                        distanceToLeftObstacle = (int)difference.magnitude;
                        leftHit = true;
                    }
                }

                /// RIGHT SCAN
                if (!rightHit)
                {
                    currentRightScanPosition += rightScanDirection;
                    TileType scannedRightTile = grid[currentRightScanPosition.x, currentRightScanPosition.y];
                    if (scannedRightTile != TileType.Empty && scannedRightTile != TileType.Apple)
                    {
                        Vector2Int difference = currentRightScanPosition - segments[0];
                        distanceToRightObstacle = (int)difference.magnitude;
                        rightHit = true;
                    }
                }
            }
        }
        
        /// Compute the distance to the wall behind the snake
        // Don't check for snake segments as if the snake is any length greater than 1 then we know there is an obstacle directly behind.

        if (direction.x == 1)
        {
            distanceToWallBehind = segments[0].x;
        }
        else if (direction.x == -1)
        {
            distanceToWallBehind = (gridSize.x - 1) - segments[0].x;
        }
        else if (direction.y == 1)
        {
            distanceToWallBehind = segments[0].y;
        }
        else
        {
            distanceToWallBehind = (gridSize.y - 1) - segments[0].y;
        }

        //// TERMINATE EARLY ~ e.g. snake takes too long

        if (numMovesSinceLastApple >= gridSize.x * gridSize.y)
        {
            gameOver();
        }
    }

    private void grow()
    {
        for (int i = 0; i < growthPerApple; i++)
        {
            Vector2Int segment = segments[segments.Count - 1];
            segments.Add(segment);
        }

        // Efficiency Calculation
        if (minimumMovesToApple == 0 || numMovesSinceLastApple == 0) { totalMoveEfficiency += 1.0f; }
        else { totalMoveEfficiency += minimumMovesToApple / numMovesSinceLastApple; }
        

        // EDGE CASE: Snake has outgrown the level.
        if (segments.Count >= (grid.GetLength(0) - 1) * (grid.GetLength(1) - 1)) { gameOver(); return; }
    }
    private void spawnApple()
    {
        /// TODO, add a way to use the same seed every time, should use apples consumed to ensure apples spawn in a different location each time as well.
        /// Also should improve this so that it keeps track of positions it has already tried.   

        bool emptyPositionFound = false;
        while (!emptyPositionFound)
        {
            applePosition = Vector2Int.zero;
            // Account for walls on the edge tiles.
            applePosition.x = random.Next(1, gridSize.x - 1);
            applePosition.y = random.Next(1, gridSize.y - 1);
            // Check if its already occupied by the snake
            if (grid[applePosition.x, applePosition.y] == TileType.Empty)
            {
                emptyPositionFound = true;
                grid[applePosition.x, applePosition.y] = TileType.Apple;
            }
        }

        minimumMovesToApple = Mathf.Abs(applePosition.x - segments[0].x + applePosition.y - segments[0].y); // horizontal + vertical
    }
    private void gameOver()
    {
        alive = false;
        if (numberOfApplesConsumed > 0) { averageMovesPerApple = numOfMovesWhenGreatestLengthReached / numberOfApplesConsumed; }
    }

    private void determineTurnDirection()
    {
        //// NEURAL NETWORK
        // SHOULD MATCH THE NUMBER OF INPUT NEURONS SET IN THE NEURAL NETWORK
        // If input neurons don't match, update GameManager constants. (Not meant to be modifiable during runtime).
        /*
         IDEAS:
         * Passing grid size could be useful, but I'll need a maximumm grid size so that I can convert it into a suitable range for the neural network.
         */

        /// CONVERT INPUTS INTO RANGE 0.0f to 1.0f
        
        // Use max possible distance to convert any distance variables into range
        float maxDistance = Mathf.Max(gridSize.x, gridSize.y);
        float numberOfTiles = gridSize.x * gridSize.y;

        float seesAppleFloat = seeApple ? 1.0f : 0.0f;
        Vector2 normalizedAppleDirection = directionToApple.normalized;
        float inputAppleDirectionX = (normalizedAppleDirection.x + 1.0f) / 2.0f;
        float inputAppleDirectionY = (normalizedAppleDirection.y + 1.0f) / 2.0f;
        float distanceToAppleInput = distanceToApple / maxDistance;
        float distanceToFrontObstacleInput = (float)distanceToObstacleInFront / maxDistance;
        float distanceToLeftObstacleInput = (float)distanceToLeftObstacle / maxDistance;
        float distanceToRightObstacleInput = (float)distanceToRightObstacle / maxDistance;
        float inputDirectionX = (direction.x + 1.0f) / 2.0f;
        float inputDirectionY = (direction.y + 1.0f) / 2.0f;
        float inputHeadPositionX = ((float)segments[0].x / (float)gridSize.x);
        float inputHeadPositionY = ((float)segments[0].y / (float)gridSize.y);
        float inputSnakeLength = segments.Count / numberOfTiles;
        float inputDistanceToWallBehind = direction.x != 0 ? distanceToWallBehind / (gridSize.x - 1) : distanceToWallBehind / (gridSize.y - 1);

        /// DEBUG

        //UnityEngine.Debug.Log(
        //$"seeAppleFloat: {seesAppleFloat}\n" +
        //$"inputAppleDirectionX: {inputAppleDirectionX}\n" +
        //$"inputAppleDirectionY: {inputAppleDirectionY}\n" +
        //$"distanceToAppleInput: {distanceToAppleInput}\n" +
        //$"distanceToFrontObstacleInput: {distanceToFrontObstacleInput}\n" +
        //$"distanceToLeftObstacleInput: {distanceToLeftObstacleInput}\n" +
        //$"distanceToRightObstacleInput: {distanceToRightObstacleInput}\n" +
        //$"inputDirectionX: {inputDirectionX}\n" +
        //$"inputDirectionY: {inputDirectionY}\n" +
        //$"inputHeadPositionX: {inputHeadPositionX}\n" +
        //$"inputHeadPositionY: {inputHeadPositionY}\n" +
        //$"distanceToWallBehind: {distanceToWallBehind}\n" +
        //$"inputDistanceToWallBehind: {inputDistanceToWallBehind}"
        //);

        /// SET NEURAL NETWORK INPUTS

        float[] snakeInputs = new float[]
        {
        distanceToAppleInput,
        seesAppleFloat,
        inputAppleDirectionX,
        inputAppleDirectionY,
        distanceToFrontObstacleInput,
        distanceToLeftObstacleInput,
        distanceToRightObstacleInput,
        inputDistanceToWallBehind,
        inputDirectionX,
        inputDirectionY,
        inputHeadPositionX,
        inputHeadPositionY,
        inputSnakeLength
        }; 

        neuralNetwork.SetInputs(snakeInputs);
        neuralNetwork.CalculateOutputs();
        float[] outputs = neuralNetwork.GetOutputs();

        //Debug.Log("NEURAL NETWORK OUTPUTS: " + outputs[0] + " " + outputs[1] + " " + outputs[2]);

        // Need to compare probabilities calculated for the 3 possible directions, take the most likely option.
        if (outputs[0] > outputs[1] && outputs[0] > outputs[2])
        {
            // TURN LEFT
            turnDirection = -1;
        }
        else if (outputs[1] > outputs[2])
        {
            // TURN FORWARD
            turnDirection = 0;
        }
        else
        {
            // TURN RIGHT
            turnDirection = 1;
        }
    }
    private Vector2Int determineDirection(int turnInput)
    {
        Vector2Int newDirection = Vector2Int.zero;
        if (turnInput == -1) // Turn Left
        {
            if (direction == Vector2Int.up)
            {
                newDirection = Vector2Int.left;
            }
            else if (direction == Vector2Int.left)
            {
                newDirection = Vector2Int.down;
            }
            else if (direction == Vector2Int.down)
            {
                newDirection = Vector2Int.right;
            }
            else
            {
                newDirection = Vector2Int.up;
            }
        }
        else if (turnInput == 1) // Turn Right
        {
            if (direction == Vector2Int.up)
            {
                newDirection = Vector2Int.right;
            }
            else if (direction == Vector2Int.right)
            {
                newDirection = Vector2Int.down;
            }
            else if (direction == Vector2Int.down)
            {
                newDirection = Vector2Int.left;
            }
            else
            {
                newDirection = Vector2Int.up;
            }
        }
        else
        {
            return direction;
        }
        return newDirection;
    }

    public float CalculateFitness(float SCORE_PER_APPLE, float SCORE_MOVES_MULTIPLIER, float SCORE_PROGRESS_TO_NEXT_APPLE_MULTIPLIER, float SCORE_DECAY_RATE)
    {
        float score = 0.0f;
        float maxPossibleDistanceToApple = gridSize.magnitude;
        averageMoveEfficiency = 1.0f;
        if (numberOfApplesConsumed > 0) { 
            averageMovesPerApple = numOfMovesWhenGreatestLengthReached / numberOfApplesConsumed;
            averageMoveEfficiency = (totalMoveEfficiency / numberOfApplesConsumed);
        }
        if (numberOfApplesConsumed > dna.mostApplesEaten) { dna.mostApplesEaten = numberOfApplesConsumed; } 

        //score += numberOfApplesConsumed * SCORE_PER_APPLE; // Primarily reward based on number of apples gained
        score += numberOfApplesConsumed * SCORE_PER_APPLE;
        score += SCORE_PER_APPLE * SCORE_MOVES_MULTIPLIER * averageMoveEfficiency;
        score += SCORE_PROGRESS_TO_NEXT_APPLE_MULTIPLIER * SCORE_PER_APPLE * (1.0f - distanceToApple / maxPossibleDistanceToApple); // Reward getting closer to the apple with each generation
        //score = score * Mathf.Exp(numOfMovesWhenGreatestLengthReached * -(SCORE_DECAY_RATE)) * SCORE_MOVES_MULTIPLIER; // Should reward / penalise for taking too many moves to get each apple.

        dna.fitness = score;

        return score;
    }

    public ref TileType[,] GetGrid() { return ref grid; }
    public ref List<Vector2Int> GetSegments() { return ref segments; }
    public Vector2Int GetApplePosition() { return applePosition; }
    public Vector2Int GetGridSize() { return gridSize; }
    public Vector2Int GetDirection() { return direction; }
}

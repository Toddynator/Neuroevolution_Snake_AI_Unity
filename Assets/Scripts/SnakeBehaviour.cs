using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SnakeBehaviour : MonoBehaviour
{
    private GameManager GameManager;
    private Transform SnakeSegment;
    private List<Transform> segments;

    private Vector2 direction = Vector2.right;
    private Vector2 prevDirection = Vector2.right; // For stopping the snake from moving back into itself
    private int turnDirection = 0; // -1 to 1, 0 to move forward

    public int distanceToObstacleInFront;
    public float distanceToApple; // Since I want to consider distance even if it isn't in line of sight, this is a float (To account for diagonals)
    public bool seeApple;
    public bool alive = true;
    private int numMovesSinceLastApple = 0;

    public DNA dna;

    public int numberOfApplesConsumed = 0;
    public int numOfMoves = 0;
    public int averageMovesPerApple = 0;
    public int numOfMovesWhenGreatestLengthReached = 0;

    public void Initialize(DNA newDNA, Transform snakeSegment, GameManager gameManager)
    {
        dna = newDNA;
        SnakeSegment = snakeSegment;
        GameManager = gameManager;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        segments = new List<Transform>();
        segments.Add(this.transform);
    }

    // Update is called once per frame
    void Update()
    {
        // Ignore directly opposite movement inputs so that snake doesn't go back into itself.

        //if (Input.GetKeyDown(KeyCode.W) && prevDirection != Vector2.down)
        //{
        //    direction = Vector2.up;
        //}
        //else if (Input.GetKeyDown(KeyCode.S) && prevDirection != Vector2.up)
        //{
        //    direction = Vector2.down;
        //}
        //else if (Input.GetKey(KeyCode.A) && prevDirection != Vector2.right)
        //{
        //    direction = Vector2.left;
        //}
        //else if (Input.GetKeyDown(KeyCode.D) && prevDirection != Vector2.left)
        //{
        //    direction = Vector2.right;
        //}

        ///// DEBUG
        //if (Input.GetKeyDown(KeyCode.W))
        //{
        //    turnDirection = 0;
        //}
        //else if (Input.GetKey(KeyCode.A))
        //{
        //    turnDirection = -1;
        //}
        //else if (Input.GetKeyDown(KeyCode.D))
        //{
        //    turnDirection = 1;
        //}
        ///// DEBUG    
    }

    private void FixedUpdate()
    {
        /// MOVE SNAKE

        // Turn snake ~ Currently use Genes as a list of inputs.
        turnDirection = dna.genes[numOfMoves % dna.genes.Length];
        if (turnDirection == -1) // Turn Left
        {
            if (direction == Vector2.up)
            {
                direction = Vector2.left;
            }
            else if (direction == Vector2.left)
            {
                direction = Vector2.down;
            }
            else if (direction == Vector2.down)
            {
                direction = Vector2.right;
            }
            else
            {
                direction = Vector2.up;
            }
        }
        else if (turnDirection == 1) // Turn Right
        {
            if (direction == Vector2.up)
            {
                direction = Vector2.right;
            }
            else if (direction == Vector2.right)
            {
                direction = Vector2.down;
            }
            else if (direction == Vector2.down)
            {
                direction = Vector2.left;
            }
            else
            {
                direction = Vector2.up;
            }
        }

        // Update segments
        for (int i = segments.Count - 1; i > 0; i--)
        {
            segments[i].position = segments[i - 1].position;
        }
        this.transform.position = new Vector3(Mathf.Round(this.transform.position.x) + direction.x, Mathf.Round(this.transform.position.y) + direction.y, 0.0f);
        prevDirection = direction;
        numOfMoves++;
        numMovesSinceLastApple++;

        /// SNAKE SENSES ~ What it sees

        float rayLength = Mathf.Max(GameManager.SceneSize.x, GameManager.SceneSize.y);
        Vector2 position = this.transform.position;
        Debug.DrawRay(position, direction * rayLength, Color.blue); // Draws a line in Scene View (You can see it in the game if gizmo's are enabled).
        RaycastHit2D hit;
        hit = Physics2D.Linecast(position, position + (direction * rayLength), LayerMask.GetMask("Wall", "Snake")); // Pretty much guaranteed to hit something
        if (hit.collider != null)
        {
            distanceToObstacleInFront = (int)hit.distance;
        }
        hit = Physics2D.Linecast(position, position + (direction * rayLength), LayerMask.GetMask("Food"));
        if (hit.collider != null)
        {
            //distanceToApple = (int)hit.distance;
            seeApple = true;
        }
        else { seeApple = false; }
        if (GameManager.GetApple() != null)
        {
            Vector2 difference = GameManager.GetApple().transform.position - transform.position;
            distanceToApple = difference.magnitude;
        }

        /// TERMINATE EARLY ~ e.g. snake takes too long

        if (numMovesSinceLastApple >= GameManager.SceneSize.x * GameManager.SceneSize.y)
        {
            GameOver();
        }

        // NOTE: This should be removed if I change dna to instead be used as weightings and therefore have a 'reactive' AI.
        if (numMovesSinceLastApple > dna.genes.Length)
        {
            GameOver();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Food"))
        {
            Destroy(other.gameObject);
            Grow();
            numberOfApplesConsumed++;
            numOfMovesWhenGreatestLengthReached = numOfMoves;
            numMovesSinceLastApple = 0;
        }
        if (other.CompareTag("Wall"))
        {
            GameOver();
        }
        // NOTE: Could modify this so that it verifies it is one of the stored segment tiles, so that don't have to rely on unity collisions
        if (other.CompareTag("Player"))
        {
            GameOver();
        }
    }

    private void Grow()
    {
        for (int i = 0; i < GameManager.GrowthPerApple; i++)
        {
            Transform segment = Instantiate(SnakeSegment);
            segment.position = segments[segments.Count - 1].position;
            segments.Add(segment);
        }

        /// UPDATE COLOUR GRADIENT (If enabled)

        if (GameManager.SnakeColourGradient)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                // Gradient from head to tail
                Color color = Color.Lerp(GameManager.headColor, GameManager.tailColor, (float)i / (segments.Count - 1));
                SpriteRenderer sprite = segments[i].GetComponent<SpriteRenderer>();
                if (sprite != null) { sprite.color = color; }
            }
        }
    }

    private void GameOver()
    {
        //Debug.Log("Snake GameOver()");
        //SceneManager.LoadScene(SceneManager.GetActiveScene().name); // Restart

        alive = false;
        for (int i = 0; i < segments.Count; i++)
        {
            Destroy(segments[i].gameObject);
        }
        segments.Clear();

        if (numberOfApplesConsumed > 0) { averageMovesPerApple = numOfMovesWhenGreatestLengthReached / numberOfApplesConsumed; }
    }

    public float CalculateFitness()
    {
        float score = 0.0f;

        const float SCORE_PER_APPLE = 10.0f;
        const float SCORE_MOVES_MULTIPLIER = 2.0f;
        const float SCORE_DECAY_RATE = 0.01f; // Should improve this to be based on maximum number of moves possible in a scene.

        score += numberOfApplesConsumed * SCORE_PER_APPLE;
        score = score * Mathf.Exp(numOfMovesWhenGreatestLengthReached * -(SCORE_DECAY_RATE)) * SCORE_MOVES_MULTIPLIER;

        dna.fitness = score;

        return score;
    }
}

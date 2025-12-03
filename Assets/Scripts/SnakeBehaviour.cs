using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SnakeBehaviour : MonoBehaviour
{
    public GameManager gameManager;

    public Transform SnakeSegment;
    private List<Transform> segments;

    private Vector2 direction = Vector2.right;
    private Vector2 prevDirection = Vector2.right; // For stopping the snake from moving back into itself
    private int turnDirection = 0; // -1 to 1, 0 to move forward

    public TextMeshProUGUI distanceToObstacleText;
    public TextMeshProUGUI distanceToAppleText;
    public TextMeshProUGUI seeAppleText;

    /// GENETIC ALGORITHMS

    public DNA dna;
    int distanceToObstacleInFront;
    float distanceToApple; // Since I want to consider distance even if it isn't in line of sight, this is a float (To account for diagonals)
    bool seeApple;

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

        if (Input.GetKeyDown(KeyCode.W) && prevDirection != Vector2.down)
        {
            direction = Vector2.up;
        }
        else if (Input.GetKeyDown(KeyCode.S) && prevDirection != Vector2.up)
        {
            direction = Vector2.down;
        }
        else if (Input.GetKey(KeyCode.A) && prevDirection != Vector2.right)
        {
            direction = Vector2.left;
        }
        else if (Input.GetKeyDown(KeyCode.D) && prevDirection != Vector2.left)
        {
            direction = Vector2.right;
        }

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

        //if (turnDirection == -1) // Turn Left
        //{
        //    if (direction == Vector2.up)
        //    {
        //        direction = Vector2.left;
        //    }
        //    else if (direction == Vector2.left)
        //    {
        //        direction = Vector2.down;
        //    }
        //    else if (direction == Vector2.down)
        //    {
        //        direction = Vector2.right;
        //    }
        //    else
        //    {
        //        direction = Vector2.up;
        //    }
        //}
        //else if (turnDirection == 1) // Turn Right
        //{
        //    if (direction == Vector2.up)
        //    {
        //        direction = Vector2.right;
        //    }
        //    else if (direction == Vector2.right)
        //    {
        //        direction = Vector2.down;
        //    }
        //    else if (direction == Vector2.down)
        //    {
        //        direction = Vector2.left;
        //    }
        //    else
        //    {
        //        direction = Vector2.up;
        //    }
        //}
    }

    private void FixedUpdate()
    {    
        /// MOVE SNAKE

        for (int i = segments.Count - 1; i > 0; i--)
        {
            segments[i].position = segments[i - 1].position;
        }
        this.transform.position = new Vector3(Mathf.Round(this.transform.position.x) + direction.x, Mathf.Round(this.transform.position.y) + direction.y, 0.0f);
        prevDirection = direction;

        /// SNAKE SENSES ~ What it sees

        float rayLength = Mathf.Max(gameManager.SceneSize.x, gameManager.SceneSize.y);
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
        if (gameManager.GetApple() != null)
        {
            Vector2 difference = gameManager.GetApple().transform.position - transform.position;
            distanceToApple = difference.magnitude;
        }

        /// UPDATE UI
        // Later this should probably be moved to game controller, and game controller should be in charge of selecting the snake and updating the UI.

        distanceToObstacleText.text = "Distance to Obstacle: " + distanceToObstacleInFront;
        distanceToAppleText.text = "Distance to Apple: " + distanceToApple;
        seeAppleText.text = "Sees Apple: " + seeApple;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Food"))
        {
            Destroy(other.gameObject);
            Grow();
        }
        if (other.CompareTag("Wall"))
        {
            GameOver();
        }
        if (other.CompareTag("Player"))
        {
            GameOver();
        }
    }

    private void Grow()
    {
        for (int i = 0; i < gameManager.GrowthPerApple; i++)
        {
            Transform segment = Instantiate(SnakeSegment);
            segment.position = segments[segments.Count - 1].position;
            segments.Add(segment);
        }

        /// UPDATE COLOUR GRADIENT (If enabled)

        if (gameManager.SnakeColourGradient)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                // Gradient fromm head to tail
                Color color = Color.Lerp(gameManager.headColor, gameManager.tailColor, (float)i / (segments.Count - 1));
                SpriteRenderer sprite = segments[i].GetComponent<SpriteRenderer>();
                if (sprite != null) { sprite.color = color; }
            }
        }
    }

    private void GameOver()
    {
        Debug.Log("Snake Died");
        SceneManager.LoadScene(SceneManager.GetActiveScene().name); // Restart
    }
}

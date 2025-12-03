using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SnakeBehaviour : MonoBehaviour
{
    public GameManager gameManager;

    public Transform SnakeSegment;
    private List<Transform> segments;

    private Vector2 direction = Vector2.right;
    private Vector2 prevDirection = Vector2.right; // For stopping the snake from moving back into itself

    /// GENETIC ALGORITHMS

    int turnDirection = 0; // -1 to 1, 0 to move forward

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
        // Update snake segments
        for (int i = segments.Count - 1; i > 0; i--)
        {
            segments[i].position = segments[i - 1].position;
        }

        this.transform.position = new Vector3(Mathf.Round(this.transform.position.x) + direction.x, Mathf.Round(this.transform.position.y) + direction.y, 0.0f);
        prevDirection = direction;
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

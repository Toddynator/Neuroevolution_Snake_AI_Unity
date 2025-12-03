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

        if (Input.GetKeyDown(KeyCode.W) && direction != Vector2.down)
        {
            direction = Vector2.up;
        }
        else if (Input.GetKeyDown(KeyCode.S) && direction != Vector2.up)
        {
            direction = Vector2.down;
        }
        else if (Input.GetKey(KeyCode.A) && direction != Vector2.right)
        {
            direction = Vector2.left;
        }
        else if (Input.GetKeyDown(KeyCode.D) && direction != Vector2.left)
        {
            direction = Vector2.right;
        }
    }

    private void FixedUpdate()
    {
        // Update snake segments
        for (int i = segments.Count - 1; i > 0; i--)
        {
            segments[i].position = segments[i - 1].position;
        }

        this.transform.position = new Vector3(Mathf.Round(this.transform.position.x) + direction.x, Mathf.Round(this.transform.position.y) + direction.y, 0.0f);
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

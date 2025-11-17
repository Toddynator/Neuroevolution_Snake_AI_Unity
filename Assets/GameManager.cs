/*
Should Spawn new Apples each time one has been consumed
Should control the size of the scene.
 */

using Mono.Cecil;
using UnityEngine;
public class GameManager : MonoBehaviour
{
    public GameObject FoodPrefab;

    private Vector2 baseSceneSize = new Vector2(32, 16);
    private float baseCameraSize = 10.0f;
    public Vector2 SceneSize = new Vector2(32, 16);

    public int GrowthPerApple = 1;

    GameObject[] walls;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Ensure scene size is even so that camera is always centred.
        if (SceneSize.x % 2 != 0) { SceneSize.x += 1; }
        if (SceneSize.y % 2 != 0) { SceneSize.y += 1; }

        /// RESIZE CAMERA TO FIT SCENE INTO VIEW

        Camera camera = Camera.main;
        Vector2 cameraSizeIncrement = new Vector2(baseCameraSize / baseSceneSize.x, baseCameraSize / baseSceneSize.y);
        Vector2 sizeDifference = new Vector2(SceneSize.x - baseSceneSize.x, SceneSize.y - baseSceneSize.y);
        if (SceneSize.x <= baseSceneSize.x && SceneSize.y <= baseSceneSize.y)
        {
            camera.orthographicSize = 10.0f;
        }
        else if (sizeDifference.x * cameraSizeIncrement.x > sizeDifference.y * cameraSizeIncrement.y)
        {
            camera.orthographicSize = baseCameraSize + (cameraSizeIncrement.x * sizeDifference.x);
        }
        else
        {
            camera.orthographicSize = baseCameraSize + (cameraSizeIncrement.y * sizeDifference.y);
        }

        /// SETUP THE WALLS 

        walls = GameObject.FindGameObjectsWithTag("Wall");
        walls[0].transform.position = new Vector3(Mathf.Round(-SceneSize.x * 0.5f) - 1.0f, 0.0f, 0.0f);
        walls[1].transform.position = new Vector3(Mathf.Round(SceneSize.x * 0.5f) + 1.0f, 0.0f, 0.0f);
        walls[2].transform.position = new Vector3(0.0f, Mathf.Round(SceneSize.y * 0.5f) + 1.0f, 0.0f);
        walls[3].transform.position = new Vector3(0.0f, Mathf.Round(-SceneSize.y * 0.5f) - 1.0f, 0.0f);

        walls[0].transform.localScale = new Vector3(1.0f, SceneSize.y + 1.0f, 0.0f);
        walls[1].transform.localScale = new Vector3(1.0f, SceneSize.y + 1.0f, 0.0f);
        walls[2].transform.localScale = new Vector3(SceneSize.x + 3.0f, 1.0f, 0.0f);
        walls[3].transform.localScale = new Vector3(SceneSize.x + 3.0f, 1.0f, 0.0f);
    }

    // Update is called once per frame
    void Update()
    {
        // Check if apple should be respawned
        if (GameObject.FindGameObjectsWithTag("Food").Length == 0)
        {
            Vector3 randomizedPosition = Vector3.zero;
            randomizedPosition.x = Mathf.Round(Random.Range(Mathf.Round(-SceneSize.x * 0.5f), Mathf.Round(SceneSize.x * 0.5f)));
            randomizedPosition.y = Mathf.Round(Random.Range(Mathf.Round(-SceneSize.y * 0.5f), Mathf.Round(SceneSize.y * 0.5f)));
            Instantiate(FoodPrefab, randomizedPosition, Quaternion.identity);
        }
    }
}

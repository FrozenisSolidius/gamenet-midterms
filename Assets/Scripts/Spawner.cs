using Unity.Netcode;
using UnityEngine;


public class Spawner : MonoBehaviour
{
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private GameObject balloonPrefab;
    [SerializeField] private GameObject[] pickupPrefabs;
    [SerializeField] private float interval = 1.2f;
    [SerializeField] private float xRange = 2.5f;
    [SerializeField] private float spawnY = 6f;

    [Header("Balloon spacing")]
    [SerializeField] private float minBalloonGap = 2f;   
    [SerializeField] private float minBalloonDelay = 2.5f;  

    private float nextSpawn;
    private float lastBalloonX = 999f;
    private float lastBalloonTime = -999f;

    private void Update()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.state.Value != GameState.Flying || !gm.enemyActive.Value) return;

        if (Time.time < nextSpawn) return;
        nextSpawn = Time.time + interval;

        float roll = Random.value;
        GameObject prefab;
        bool isBalloon = false;

        if (roll < 0.55f)
        {
            prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        }
        else if (roll < 0.85f)
        {

            if (Time.time - lastBalloonTime < minBalloonDelay)
                prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            else
            {
                prefab = balloonPrefab;
                isBalloon = true;
            }
        }
        else
        {
            prefab = pickupPrefabs[Random.Range(0, pickupPrefabs.Length)];
        }

        float x = Random.Range(-xRange, xRange);

        if (isBalloon)
        {

            for (int i = 0; i < 10 && Mathf.Abs(x - lastBalloonX) < minBalloonGap; i++)
                x = Random.Range(-xRange, xRange);

            lastBalloonX = x;
            lastBalloonTime = Time.time;
        }

        GameObject go = Instantiate(prefab, new Vector3(x, spawnY, 0f), Quaternion.identity);
        go.GetComponent<NetworkObject>().Spawn(true);
    }
}
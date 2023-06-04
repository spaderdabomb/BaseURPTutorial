using System.Collections;
using System.Collections.Generic;
using Unity.Burst.CompilerServices;
using UnityEngine;

public class BulletHoleManager : MonoBehaviour
{
    [SerializeField] GameObject bulletHolePrefab;
    [SerializeField] GameObject bulletHoleContainer;
    [SerializeField] float destroyDelay;

    Ray currentRay;
    RaycastHit currentHit;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                SpawnBulletHole(hit, ray);
            }

            currentRay = ray;
            currentHit = hit;
        }

        Debug.DrawLine(currentRay.origin, currentHit.point);
    }

    void SpawnBulletHole(RaycastHit hit, Ray ray)
    {
        float positionMultiplier = 0.5f;
        float spawnX = hit.point.x - ray.direction.x * positionMultiplier;
        float spawnY = hit.point.y - ray.direction.y * positionMultiplier;
        float spawnZ = hit.point.z - ray.direction.z * positionMultiplier;
        Vector3 spawnPosition = new Vector3(spawnX, spawnY, spawnZ);

        GameObject spawnedObject = Instantiate(bulletHolePrefab, hit.point, Quaternion.identity);
        Quaternion targetRotation = Quaternion.LookRotation(ray.direction);

        spawnedObject.transform.rotation = targetRotation;
        spawnedObject.transform.SetParent(bulletHoleContainer.transform);
        spawnedObject.transform.Rotate(Vector3.forward, Random.Range(0f, 360f));
        Destroy(spawnedObject, destroyDelay);
    }
}

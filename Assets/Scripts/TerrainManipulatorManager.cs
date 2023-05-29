using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using Unity.Burst.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Rendering.DebugUI.Table;

public class TerrainManipulatorManager : MonoBehaviour
{
    public Terrain terrain;
    public float smoothingRadius = 2f;
    public GameObject playerGO;
    public float maxSmoothingDelta = 1f / 500f;
    public int postProcessingSmoothingRadius = 1;

    private Vector3 gizmosVector3 = Vector3.zero;
    private Vector3 gizmosVector3_2 = Vector3.zero;

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 playerPosition = playerGO.transform.position;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                SmoothTerrainAroundPosition(playerPosition, hit.point);
            }
        }
    }

    private void SmoothTerrainAroundPosition(Vector3 position, Vector3 clickPosition)
    {
        // Get terrain data and player look direction
        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 normDirection = Vector3.Normalize(clickPosition - position);
        Vector3 roundedDirection = new Vector3(Mathf.Round(normDirection.x), 0f, Mathf.Round(normDirection.z));

        // Get terrain index nearest to player
        int terrainXatPlayer = Mathf.FloorToInt((position.x - terrainPosition.x) / terrainData.size.x * terrainData.heightmapResolution);
        int terrainZatPlayer = Mathf.FloorToInt((position.z - terrainPosition.z) / terrainData.size.z * terrainData.heightmapResolution);

        int radiusInSamples = Mathf.RoundToInt(smoothingRadius / terrainData.size.x * terrainData.heightmapResolution);
        int gridSize = radiusInSamples * 2 + 1;

        print("Grid Height: " + gridSize.ToString());

        float playerTerrainVertexX = terrainXatPlayer * (terrainData.size.x / terrainData.heightmapResolution);
        float playerTerrainVertexY = playerGO.transform.position.y;
        float playerTerrainVertexZ = terrainZatPlayer * (terrainData.size.z / terrainData.heightmapResolution);

        gizmosVector3 = new Vector3(playerTerrainVertexX, terrainData.GetHeight(terrainXatPlayer, terrainZatPlayer), playerTerrainVertexZ);

        // Debug.DrawLine(playerGO.transform.position, Vector3.zero, Color.yellow);


        int terrainXOffset = (int)(roundedDirection.x * radiusInSamples);
        int terrainZOffset = (int)(roundedDirection.z * radiusInSamples);

        int heightCenterX = radiusInSamples - terrainXOffset;  // Index X in heights[,] array where player is at
        int heightCenterZ = radiusInSamples - terrainZOffset;  // Index Z in heights[,] array where player is at

        // Get heights from terrain
        int terrainStartIndexX = terrainXatPlayer + terrainXOffset - radiusInSamples;
        int terrainStartIndexZ = terrainZatPlayer + terrainZOffset - radiusInSamples;


        float[,] heights = terrainData.GetHeights(terrainStartIndexX, terrainStartIndexZ, gridSize, gridSize);
        float centerPointHeight = heights[heightCenterZ, heightCenterX];

        // DebugSquares(heights, terrainStartIndexX, terrainStartIndexZ, terrainXatPlayer, terrainZatPlayer, terrainData);

        for (int z = 0; z < gridSize; z++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                float distance = Mathf.Pow(heightCenterX - x, 2) + Mathf.Pow(heightCenterZ - z, 2);
                float weight = Mathf.Clamp01(1 / distance);

                weight = 1f;
                float smoothedHeightDelta = (centerPointHeight - heights[z, x]) * (weight/ 2f);

                // Clamps height delta to max
                float smoothingSign = Mathf.Sign(smoothedHeightDelta);
                if (Mathf.Abs(smoothedHeightDelta) > maxSmoothingDelta)
                {
                    smoothedHeightDelta = maxSmoothingDelta * smoothingSign;
                }
                    
                // if (maxVertexExceeded) continue;*/

                heights[z, x] += smoothedHeightDelta;
            }
        }

        float[,] postProcessedHeights = SmoothingPostProcessing(heights, terrainStartIndexX, terrainStartIndexZ);

        terrainData.SetHeights(terrainStartIndexX - postProcessingSmoothingRadius, terrainStartIndexZ - postProcessingSmoothingRadius, postProcessedHeights);
        // terrainData.SetHeights(terrainStartIndexX, terrainStartIndexZ, heights);
    }

    private float[,] SmoothingPostProcessing(float[,] array, int terrainStartIndexX, int terrainStartIndexZ)
    {
        int inputRows = array.GetLength(0);
        int inputCols = array.GetLength(1);

        float[,] newHeights = Terrain.activeTerrain.terrainData.GetHeights(terrainStartIndexX - postProcessingSmoothingRadius, 
                                                                           terrainStartIndexZ - postProcessingSmoothingRadius,
                                                                           inputRows + 2 * postProcessingSmoothingRadius, inputCols + 2 * postProcessingSmoothingRadius);
        int smoothedRows = newHeights.GetLength(0);
        int smoothedCols = newHeights.GetLength(1);
        float[,] smoothedArray = new float[smoothedRows, smoothedCols];

        // Replace the inner portion of the array
        for (int row = postProcessingSmoothingRadius; row < smoothedRows - postProcessingSmoothingRadius; row++)
        {
            for (int col = postProcessingSmoothingRadius; col < smoothedCols - postProcessingSmoothingRadius; col++)
            {
                newHeights[row, col] = array[row - postProcessingSmoothingRadius, col - postProcessingSmoothingRadius];
            }
        }

        // Iterate through each element in the array
        for (int row = 0; row < smoothedRows; row++)
        {
            for (int col = 0; col < smoothedCols; col++)
            {
                // Calculate the sum and count of neighboring elements
                float sum = 0;
                float count = 0;

                // Iterate through neighboring elements within the smooth radius
                for (int i = row - postProcessingSmoothingRadius; i <= row + postProcessingSmoothingRadius; i++)
                {
                    for (int j = col - postProcessingSmoothingRadius; j <= col + postProcessingSmoothingRadius; j++)
                    {
                        // Check if the neighboring element is within the array bounds
                        if (i >= 0 && i < smoothedRows && j >= 0 && j < smoothedCols)
                        {
                            // Calculate the weight based on the distance from the current element
                            float distance = Math.Abs(row - i) + Math.Abs(col - j);
                            float weight = postProcessingSmoothingRadius - distance + 1;

                            // Accumulate the weighted value and increase the count
                            sum += newHeights[i, j] * weight;
                            count += weight;
                        }
                    }
                }

                // Calculate the average and assign it to the corresponding element in the smoothed array
                float delta = newHeights[row, col] - sum / count;
                float smoothingSign = Mathf.Sign(delta);
                if (Mathf.Abs(delta) > maxSmoothingDelta)
                {
                    delta = smoothingSign * maxSmoothingDelta;
                }
                smoothedArray[row, col] = newHeights[row, col] - delta;
            }
        }

        return smoothedArray;
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(gizmosVector3, 0.5f);
        Gizmos.DrawWireSphere(gizmosVector3_2, 50f);
    }

    private void DebugSquares(float[,] heights, int terrainStartIndexX, int terrainStartIndexZ, int terrainXatPlayer, int terrainZatPlayer, TerrainData terrainData)
    {
        for (int i = 0; i < heights.GetLength(0); i++)
        {
            for (int j = 0; j < heights.GetLength(1); j++)
            {
                float vertexX = (terrainStartIndexX + i) * (terrainData.size.x / terrainData.heightmapResolution);
                float vertexY = terrainData.GetHeight(terrainXatPlayer, terrainZatPlayer);
                float vertexZ = (terrainStartIndexZ + j) * (terrainData.size.z / terrainData.heightmapResolution);

                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.position = new Vector3(vertexX, vertexY, vertexZ);
                cube.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            }
        }
    }
}

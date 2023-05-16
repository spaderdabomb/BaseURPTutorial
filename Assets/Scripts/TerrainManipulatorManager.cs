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
    public float maxVertexDelta = 1f / 10f;
    public int postProcessingSmoothingRadius = 1;

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

        print(gridSize);

        int terrainXOffset = (int)(roundedDirection.x * radiusInSamples);
        int terrainZOffset = (int)(roundedDirection.z * radiusInSamples);

        int heightCenterX = radiusInSamples - terrainXOffset;  // Index X in heights[,] array where player is at
        int heightCenterZ = radiusInSamples - terrainZOffset;  // Index Z in heights[,] array where player is at

        // Get heights from terrain
        int terrainStartIndexX = terrainXatPlayer + terrainXOffset - radiusInSamples;
        int terrainStartIndexZ = terrainZatPlayer + terrainZOffset - radiusInSamples;
        float[,] heights = terrainData.GetHeights(terrainStartIndexX, terrainStartIndexZ, gridSize, gridSize);
        float centerPointHeight = heights[heightCenterZ, heightCenterX];

        for (int z = 0; z < gridSize; z++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                float distance = Mathf.Pow(centerPointHeight - x, 2) + Mathf.Pow(centerPointHeight - z, 2);
                float weight = Mathf.Clamp01(1 / distance);
                float smoothedHeight = (centerPointHeight - heights[z, x]) * (weight/ 2f);

                // Clamps height delta to max
                // if (smoothedHeight > maxSmoothingDelta) smoothedHeight = maxSmoothingDelta;

                // Checks all neighboring vertices
                /*                bool maxVertexExceeded = CheckNeighboringVertices(heights, z, x);
                                if (maxVertexExceeded) continue;*/

                heights[z, x] += smoothedHeight;
            }
        }

        // float[,] postProcessedHeights = SmoothingPostProcessing(heights, terrainStartIndexX, terrainStartIndexZ);

        // terrainData.SetHeights(terrainStartIndexX - postProcessingSmoothingRadius, terrainStartIndexZ - postProcessingSmoothingRadius, postProcessedHeights);
        terrainData.SetHeights(terrainStartIndexX, terrainStartIndexZ, heights);
    }

    private bool CheckNeighboringVertices(float[,] array, int row, int col)
    {
        int rows = array.GetLength(0); // Number of rows in the array
        int cols = array.GetLength(1); // Number of columns in the array

        // Make neighbor indices
        int[,] neighbors = {
                    { row - 1, col }, // Top
                    { row + 1, col }, // Bottom
                    { row, col - 1 }, // Left
                    { row, col + 1 }  // Right
                };

        // Check if value exceeds max vertex value
        bool maxVertexExceeded = false;
        for (int i = 0; i < array.GetLength(0); i++)
        {
            for (int j = 0; j < array.GetLength(1); j++)
            {
                int neighborRow = row - i;
                int neighborCol = col - j;

                if (neighborRow >= 0 && neighborRow < rows && neighborCol >= 0 && neighborCol < cols)
                {
                    float neighborValue = array[neighborRow, neighborCol];
                    print(Mathf.Abs((neighborValue - array[row, col])));
                    if (Mathf.Abs((neighborValue - array[row, col])) > maxVertexDelta)
                    {
                        maxVertexExceeded = true;
                        print("failed");
                        break;
                    }
                }
            }
        }

        return maxVertexExceeded;
    }

    private float[,] SmoothingPostProcessing(float[,] array, int terrainStartIndexX, int terrainStartIndexZ)
    {
        print(postProcessingSmoothingRadius);
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
                smoothedArray[row, col] = sum / count;
            }
        }

        // Print the original and smoothed arrays
        Console.WriteLine("Original Array:");
        PrintArray(array);

        Console.WriteLine("Smoothed Array:");
        PrintArray(smoothedArray);

        return smoothedArray;
    }

    static void PrintArray(float[,] array)
    {
        int rows = array.GetLength(0);
        int cols = array.GetLength(1);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                Console.Write(array[row, col] + " ");
            }
            Console.WriteLine();
        }
        Console.WriteLine();
    }
}

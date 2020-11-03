﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MazeGenerator : MonoBehaviour
{
    private TilesSO[] _tiles;
    private GameObject _enemyPrefab;

    private Vector3 _spawnPoint;
    private Stack<Vector2Int> _cellStack;
    private Vector3 _startPoint;
    private Cell[,] _cells;
    private int[] _xDistance, _zDistance;
    private Vector2Int _firstCell;
    private int _maxNodeCount;
    private int _currentEmptyNode;
    private PathfindingNode[] _pathfindingNodes;
    private MazeSettingsSO _mazeSettings;

    private void Awake()
    {
        _enemyPrefab = Resources.Load<GameObject>("Maze/TempEnemy"); // TODO change?
        _tiles = Resources.LoadAll<TilesSO>("Maze");
        // TODO order by id?
    }

    // Start is called before the first frame update
    public PathfindingNode[] GenerateMaze(MazeSettingsSO mazeSettings, IWinCondition winCondition, out int count) // TODO IWIN
    {
        _mazeSettings = mazeSettings;

        // TODO move somwhere else or add initialization
        int generationCounter = 0;
        while (generationCounter < _mazeSettings.triesToGenerateMaze)
        {
            if ((float)GenerateCells() / (float)(_mazeSettings.length * _mazeSettings.width) >= _mazeSettings.minTilesPercentage)
            {
                break;
            }
            generationCounter++;
        }
        //Debug.Log("Tries: " + generationCounter);
        Debug.Log("Max Nodes: " + _maxNodeCount);

        CreateNodes();

        SpawnTiles();

        Debug.Log("Generation Done");

        // TODO remove or move?
        GameManager.Instance.Player.transform.position = _spawnPoint;

        // TODO make better & move?
        for (int i = 0; i < _currentEmptyNode; i++)
        {
            if (_pathfindingNodes[i] != null) {
                if (Random.Range(0f, 1f) <= _mazeSettings.spawnChance)
                {
                    Instantiate(_enemyPrefab, new Vector3(_pathfindingNodes[i].position.x , _startPoint.y + 1, _pathfindingNodes[i].position.y), Quaternion.identity);
                }
            }
        }

        Destroy(this); // TODO uncomment?
        count = _currentEmptyNode;
        return _pathfindingNodes;
    }

    // TODO remove
    #region Visualization in Editor

    private void OnDrawGizmos()
    {
        /*
        DrawCells();
        */

        DrawNodes();
    }

    private void DrawNodes()
    {
        if (_pathfindingNodes != null)
        {
            for (int i = 0; i < _currentEmptyNode; i++)
            {
                if (_pathfindingNodes[i] == null)
                {
                    continue;
                }

                Vector3 start = new Vector3(_pathfindingNodes[i].position.x, _startPoint.y - 0.1f, _pathfindingNodes[i].position.y);
                Vector3 end;

                Gizmos.color = Color.green;

                Gizmos.DrawSphere(start, 0.3f);

                Gizmos.color = Color.yellow;

                for (int j = 0; j < 8; j++)
                {
                    if (_pathfindingNodes[i].neighbours[j] != null)
                    {
                        end = new Vector3(_pathfindingNodes[i].neighbours[j].position.x, _startPoint.y - 0.1f, _pathfindingNodes[i].neighbours[j].position.y);
                        Gizmos.DrawLine(start, end);
                    }
                }
            }
        }
    }
    private void DrawCells()
    {
        if (_cells != null)
        {
            float xPos, zPos;

            for (int i = 0; i < _mazeSettings.length; i++)
            {
                for (int j = 0; j < _mazeSettings.width; j++)
                {
                    if (_cells[i, j] != null)
                    {
                        Gizmos.color = Color.cyan;
                        xPos = _xDistance[j] * _mazeSettings.distanceBetweenCells;
                        zPos = _zDistance[i] * _mazeSettings.distanceBetweenCells;

                        Gizmos.DrawSphere(new Vector3(_startPoint.x + xPos, _startPoint.y, _startPoint.z + zPos), 0.3f);

                        Gizmos.color = Color.red;
                        DrawPaths(i, j);
                    }
                }
            }
        }
    }
    private void DrawPaths(int x, int y)
    {
        float xPos = _xDistance[x] * _mazeSettings.distanceBetweenCells;
        float yPos = _zDistance[y] * _mazeSettings.distanceBetweenCells;

        Vector3 start = new Vector3(_startPoint.x + xPos, _startPoint.y, _startPoint.z + yPos);
        Vector3 end;

        // Top
        if (_cells[x, y].IsDoor(Side.Top))
        {
            xPos = _xDistance[x + 1] * _mazeSettings.distanceBetweenCells;
            yPos = _zDistance[y] * _mazeSettings.distanceBetweenCells;
            end = new Vector3(_startPoint.x + xPos, _startPoint.y, _startPoint.z + yPos);
            Gizmos.DrawLine(start, end);
        }
        // Right
        if (_cells[x, y].IsDoor(Side.Right))
        {
            xPos = _xDistance[x] * _mazeSettings.distanceBetweenCells;
            yPos = _zDistance[y + 1] * _mazeSettings.distanceBetweenCells;
            end = new Vector3(_startPoint.x + xPos, _startPoint.y, _startPoint.z + yPos);
            Gizmos.DrawLine(start, end);
        }
        // Bottom
        if (_cells[x, y].IsDoor(Side.Bottom))
        {
            xPos = _xDistance[x - 1] * _mazeSettings.distanceBetweenCells;
            yPos = _zDistance[y] * _mazeSettings.distanceBetweenCells;
            end = new Vector3(_startPoint.x + xPos, _startPoint.y, _startPoint.z + yPos);
            Gizmos.DrawLine(start, end);
        }
        // Left
        if (_cells[x, y].IsDoor(Side.Left))
        {
            xPos = _xDistance[x] * _mazeSettings.distanceBetweenCells;
            yPos = _zDistance[y - 1] * _mazeSettings.distanceBetweenCells;
            end = new Vector3(_startPoint.x + xPos, _startPoint.y, _startPoint.z + yPos);
            Gizmos.DrawLine(start, end);
        }
    }

    #endregion

    #region Cell Generation

    private int GenerateCells()
    {
        int tileCounter = 1;
        // Z, X
        _cells = new Cell[_mazeSettings.length, _mazeSettings.width];
        _startPoint = new Vector3(_mazeSettings.centerPoint.x - ((float)_mazeSettings.width / 2f) * _mazeSettings.distanceBetweenCells, _mazeSettings.centerPoint.y, _mazeSettings.centerPoint.z - ((float)_mazeSettings.length / 2f) * _mazeSettings.distanceBetweenCells); // RLpos
        _cellStack = new Stack<Vector2Int>();
        _xDistance = new int[_mazeSettings.width + 1];
        _zDistance = new int[_mazeSettings.length + 1];

        _zDistance[0] = _xDistance[0] = 0;
        for (int i = 1; i < _mazeSettings.length + 1; i++)
        {
            if (_mazeSettings.randomDistanceBetweenCells)
            {
                _zDistance[i] = _zDistance[i - 1] + Random.Range(_mazeSettings.minDistanceMultiplyer, _mazeSettings.maxDistanceMultiplyer);
            }
            else
            {
                _zDistance[i] = i;
            }
        }

        for (int i = 1; i < _mazeSettings.width + 1; i++)
        {
            if (_mazeSettings.randomDistanceBetweenCells)
            {
                _xDistance[i] = _xDistance[i - 1] + Random.Range(_mazeSettings.minDistanceMultiplyer, _mazeSettings.maxDistanceMultiplyer);
            }
            else
            {
                _xDistance[i] = i;
            }
        }

        // First Cell - add all possible neighbours, Z, X
        Vector2Int currentCellPositionInArray = _firstCell = new Vector2Int(Random.Range(1, _mazeSettings.length - 1), Random.Range(1, _mazeSettings.width - 1));
        _cells[currentCellPositionInArray.x, currentCellPositionInArray.y] = new Cell(true, true, true, true);
        PushNeighbouringCells(currentCellPositionInArray);
        _maxNodeCount = (_zDistance[currentCellPositionInArray.x + 1] - _zDistance[currentCellPositionInArray.x]) * (_xDistance[currentCellPositionInArray.y + 1] - _xDistance[currentCellPositionInArray.y]);

        // All other Cells
        while (_cellStack.Count > 0)
        {
            currentCellPositionInArray = _cellStack.Pop();
            if (_cells[currentCellPositionInArray.x, currentCellPositionInArray.y] != null)
            {
                continue;
            }
            CreateNewCell(currentCellPositionInArray);
            CreateNewDoors(currentCellPositionInArray);
            PushNeighbouringCells(currentCellPositionInArray);
            tileCounter++;
            _maxNodeCount += (_zDistance[currentCellPositionInArray.x + 1] - _zDistance[currentCellPositionInArray.x]) * (_xDistance[currentCellPositionInArray.y + 1] - _xDistance[currentCellPositionInArray.y]);
        }

        return tileCounter;
    }

    // TODO move somwhere else, cuz used from other region
    // Pushes neighbouring Cells into Stack
    private void PushNeighbouringCells(Vector2Int position)
    {
        Cell cell = _cells[position.x, position.y];

        if (cell.IsDoor(Side.Top))
        {
            _cellStack.Push(new Vector2Int(position.x + 1, position.y));
        }
        if (cell.IsDoor(Side.Right))
        {
            _cellStack.Push(new Vector2Int(position.x, position.y + 1));
        }
        if (cell.IsDoor(Side.Bottom))
        {
            _cellStack.Push(new Vector2Int(position.x - 1, position.y));
        }
        if (cell.IsDoor(Side.Left))
        {
            _cellStack.Push(new Vector2Int(position.x, position.y - 1));
        }
    }

    // Creates new Cell and copies the door positions of surrounding Cells
    private void CreateNewCell(Vector2Int position)
    {
        Cell cell = new Cell();

        // Top - Z
        if (position.x < _mazeSettings.length - 1)
        {
            if (_cells[position.x + 1, position.y] != null)
            {
                if (_cells[position.x + 1, position.y].IsDoor(Side.Bottom))
                {
                    cell.OpenWall(Side.Top);
                }
            }
        }
        // Right - X
        if (position.y < _mazeSettings.width - 1)
        {
            if (_cells[position.x, position.y + 1] != null)
            {
                if (_cells[position.x, position.y + 1].IsDoor(Side.Left))
                {
                    cell.OpenWall(Side.Right);
                }
            }
        }
        // Bottom - Z
        if (position.x > 0)
        {
            if (_cells[position.x - 1, position.y] != null)
            {
                if (_cells[position.x - 1, position.y].IsDoor(Side.Top))
                {
                    cell.OpenWall(Side.Bottom);
                }
            }
        }
        // Left - X
        if (position.y > 0)
        {
            if (_cells[position.x, position.y - 1] != null)
            {
                if (_cells[position.x, position.y - 1].IsDoor(Side.Right))
                {
                    cell.OpenWall(Side.Left);
                }
            }
        }

        _cells[position.x, position.y] = cell;
    }

    // Try to create new doors
    private void CreateNewDoors(Vector2Int position)
    {
        int doorCount = _cells[position.x, position.y].GetDoorCount();
        //Debug.Log(doorCount);
        int offset = Random.Range(0, 4);

        for (int i = 0; i < 4; i++)
        {
            // Offset - random starting direction
            switch ((i + offset) % 4)
            {
                case 0:
                    // Top
                    if (position.x < _mazeSettings.length - 1)
                    {
                        if (_cells[position.x + 1, position.y] == null)
                        {
                            if (Random.Range(0f, 1) <= _mazeSettings.doorDirectionChance[0] * _mazeSettings.doorChanceFallOff[doorCount])
                            {
                                doorCount++;
                                _cells[position.x, position.y].OpenWall(Side.Top);
                            }
                        }
                    }
                    break;
                case 1:
                    // Rigth
                    if (position.y < _mazeSettings.width - 1)
                    {
                        if (_cells[position.x, position.y + 1] == null)
                        {
                            if (Random.Range(0f, 1) <= _mazeSettings.doorDirectionChance[1] * _mazeSettings.doorChanceFallOff[doorCount])
                            {
                                doorCount++;
                                _cells[position.x, position.y].OpenWall(Side.Right);
                            }
                        }
                    }
                    break;
                case 2:
                    // bottom
                    if (position.x > 0)
                    {
                        if (_cells[position.x - 1, position.y] == null)
                        {
                            if (Random.Range(0f, 1) <= _mazeSettings.doorDirectionChance[2] * _mazeSettings.doorChanceFallOff[doorCount])
                            {
                                doorCount++;
                                _cells[position.x, position.y].OpenWall(Side.Bottom);
                            }
                        }
                    }
                    break;
                case 3:
                    // Left
                    if (position.y > 0)
                    {
                        if (_cells[position.x, position.y - 1] == null)
                        {
                            if (Random.Range(0f, 1) <= _mazeSettings.doorDirectionChance[3] * _mazeSettings.doorChanceFallOff[doorCount])
                            {
                                doorCount++;
                                _cells[position.x, position.y].OpenWall(Side.Left);
                            }
                        }
                    }
                    break;
            }
        }
    }

    #endregion

    #region Node Generation

    private void CreateNodes()
    {
        _currentEmptyNode = 0;
        _pathfindingNodes = new PathfindingNode[_maxNodeCount];

        // First Cell
        _spawnPoint = new Vector3(_startPoint.x + (_xDistance[_firstCell.y]) * _mazeSettings.distanceBetweenCells, _startPoint.y + 1f, _startPoint.z + (_zDistance[_firstCell.x]) * _mazeSettings.distanceBetweenCells); // TODO only points to the right cell; rework hardcoded?; TODO spawns somwhere else // RLpos
        CreateRoom(_firstCell);
        PushNeighbouringCells(_firstCell);
        _cells[_firstCell.x, _firstCell.y].generated = true;

        // Other Cells, Z, X
        Vector2Int currentCell;
        while (_cellStack.Count > 0)
        {
            currentCell = _cellStack.Pop();
            if (_cells[currentCell.x, currentCell.y].generated == true)
            {
                continue;
            }

            if (Random.Range(0f, 1f) <= _mazeSettings.roomChance[_cells[currentCell.x, currentCell.y].GetDoorCount() - 1])
            {
                CreateRoom(currentCell);
                //Debug.Log("Room");
            }
            else
            {
                CreateCorridor(currentCell);
                //Debug.Log("Corridor");
            }

            PushNeighbouringCells(currentCell);
            _cells[currentCell.x, currentCell.y].generated = true;
        }
    }

    private void CreateRoom(Vector2Int position)
    {
        int tileType = _mazeSettings.roomTileTypes[Random.Range(0, _mazeSettings.roomTileTypes.Length)];

        Vector2Int dimensions = GetDimensions(position);
        // X, Z
        Vector2 realPosition = new Vector2(_startPoint.x + _xDistance[position.y] * _mazeSettings.distanceBetweenCells, _startPoint.z + _zDistance[position.x] * _mazeSettings.distanceBetweenCells); // RLpos
        _cells[position.x, position.y].lowestPathfindingNodeID = _currentEmptyNode;

        // From bottom left corner 
        for (int i = 0; i < dimensions.x; i++) // Z
        {
            for (int j = 0; j < dimensions.y; j++) // X
            {
                _pathfindingNodes[_currentEmptyNode] = new PathfindingNode(_currentEmptyNode, realPosition.x + _mazeSettings.distanceBetweenCells * j, realPosition.y + _mazeSettings.distanceBetweenCells * i, tileType); // RLpos

                // 2nd column + (Y)
                if (j > 0)
                {
                    ConnectTwoNodes(_pathfindingNodes[_currentEmptyNode], _pathfindingNodes[_currentEmptyNode - 1], Side.Left);
                }

                // 2nd row + (X)
                if (i > 0)
                {
                    ConnectTwoNodes(_pathfindingNodes[_currentEmptyNode], _pathfindingNodes[_currentEmptyNode - dimensions.y], Side.Bottom);

                    // Diagonal ones
                    if (j > 0)
                    {
                        ConnectTwoNodes(_pathfindingNodes[_currentEmptyNode], _pathfindingNodes[_currentEmptyNode - dimensions.y - 1], Side.BottomLeft);
                    }

                    if (j < dimensions.y - 1)
                    {
                        ConnectTwoNodes(_pathfindingNodes[_currentEmptyNode], _pathfindingNodes[_currentEmptyNode - dimensions.y + 1], Side.BottomRight);
                    }
                }

                _currentEmptyNode++;

                // TODO prob move somwhere else
                //Instantiate(floor, new Vector3(startPoint.x + realPosition.x + i * distanceBetweenCells, startPoint.y, startPoint.z + realPosition.y + j * distanceBetweenCells), Quaternion.identity);
            }
        }

        // Connect nodes with surrounding Cells
        ConnectNodesFromDifferentCells(position, dimensions);

        // TODO walls (spawn different tiles), spawn items, spawn enemies; prob do later
    }

    // Creates corridor
    private void CreateCorridor(Vector2Int position)
    {
        int tileType = _mazeSettings.corridorTileTypes[Random.Range(0, _mazeSettings.corridorTileTypes.Length)];

        // X, Z
        Vector2 realPosition = new Vector2(_startPoint.x + _xDistance[position.y] * _mazeSettings.distanceBetweenCells, _startPoint.z + _zDistance[position.x] * _mazeSettings.distanceBetweenCells); // RLpos
        Vector2Int dimensions = GetDimensions(position);
        Vector2Int pathCoords = GetPath(position, dimensions);
        Vector2 centerPosition = new Vector2(realPosition.x + _mazeSettings.distanceBetweenCells * pathCoords.y, realPosition.y + _mazeSettings.distanceBetweenCells * pathCoords.x); // RLpos
        // Z, X
        int centerID = _currentEmptyNode + (dimensions.y * pathCoords.x) + pathCoords.y;
        _cells[position.x, position.y].lowestPathfindingNodeID = _currentEmptyNode;

        PathfindingNode centerNode = new PathfindingNode(centerID, centerPosition.x, centerPosition.y, tileType);
        _pathfindingNodes[centerID] = centerNode;

        // Top
        if (_cells[position.x, position.y].IsDoor(Side.Top))
        {
            if (pathCoords.x < (dimensions.x - 1))
            {
                int id = centerID + dimensions.y;
                _pathfindingNodes[id] = new PathfindingNode(centerID + dimensions.y, centerPosition.x, centerPosition.y + _mazeSettings.distanceBetweenCells, tileType); // RLpos
                ConnectTwoNodes(_pathfindingNodes[id], centerNode, Side.Bottom);
            }
        }
        // Right
        if (_cells[position.x, position.y].IsDoor(Side.Right))
        {
            if (pathCoords.y < (dimensions.y - 1))
            {
                int id = centerID + 1;
                _pathfindingNodes[id] = new PathfindingNode(centerID + 1, centerPosition.x + _mazeSettings.distanceBetweenCells, centerPosition.y, tileType); // RLpos
                ConnectTwoNodes(_pathfindingNodes[id], centerNode, Side.Left);
            }
        }
        // Bottom
        if (_cells[position.x, position.y].IsDoor(Side.Bottom))
        {
            if (pathCoords.x > 0)
            {
                int id = centerID - dimensions.y;
                _pathfindingNodes[id] = new PathfindingNode(centerID - dimensions.y, centerPosition.x, centerPosition.y - _mazeSettings.distanceBetweenCells, tileType); // RLpos
                ConnectTwoNodes(_pathfindingNodes[id], centerNode, Side.Top);
            }
        }
        // Left
        if (_cells[position.x, position.y].IsDoor(Side.Left))
        {
            if (pathCoords.y > 0)
            {
                int id = centerID - 1;
                _pathfindingNodes[id] = new PathfindingNode(centerID - 1, centerPosition.x - _mazeSettings.distanceBetweenCells, centerPosition.y, tileType); // RLpos
                ConnectTwoNodes(_pathfindingNodes[id], centerNode, Side.Right);
            }
        }

        _currentEmptyNode += dimensions.x * dimensions.y;

        // Connect nodes with surrounding Cells
        ConnectNodesFromDifferentCells(position, dimensions);

        // TODO walls (spawn different tiles), spawn items, spawn enemies; prob do later
    }

    private Vector2Int GetDimensions(Vector2Int position)
    {
        return new Vector2Int(_zDistance[position.x + 1] - _zDistance[position.x], _xDistance[position.y + 1] - _xDistance[position.y]);
    }

    private Vector2Int GetDimensions(int positionZ, int positionX)
    {
        return new Vector2Int(_zDistance[positionZ + 1] - _zDistance[positionZ], _xDistance[positionX + 1] - _xDistance[positionX]);
    }

    private Vector2Int GetPath(Vector2Int position, Vector2Int dimension)
    {
        Vector2Int result = new Vector2Int();

        switch (dimension.x)
        {
            case 1:
                result.x = 0;
                break;
            case 2:
                result.x = position.x % 2;
                break;
            case 3:
                result.x = 1;
                break;
            default:
                throw new System.Exception("Rework needed to support larger rooms");
        }

        switch (dimension.y)
        {
            case 1:
                result.y = 0;
                break;
            case 2:
                result.y = position.y % 2;
                break;
            case 3:
                result.y = 1;
                break;
            default:
                throw new System.Exception("Rework needed to support larger rooms");
        }

        return result;
    }

    private void ConnectTwoNodes(PathfindingNode first, PathfindingNode second, Side direction)
    {
        switch (direction)
        {
            case Side.Top:
                first.neighbours[0] = second;
                second.neighbours[4] = first;
                break;
            case Side.Right:
                first.neighbours[2] = second;
                second.neighbours[6] = first;
                break;
            case Side.Bottom:
                first.neighbours[4] = second;
                second.neighbours[0] = first;
                break;
            case Side.Left:
                first.neighbours[6] = second;
                second.neighbours[2] = first;
                break;
            case Side.TopRight:
                first.neighbours[1] = second;
                second.neighbours[5] = first;
                break;
            case Side.BottomRight:
                first.neighbours[3] = second;
                second.neighbours[7] = first;
                break;
            case Side.BottomLeft:
                first.neighbours[5] = second;
                second.neighbours[1] = first;
                break;
            case Side.TopLeft:
                first.neighbours[7] = second;
                second.neighbours[3] = first;
                break;
        }
    }

    // Connects nodes between cells
    private void ConnectNodesFromDifferentCells(Vector2Int position, Vector2Int firstDimensions)
    {
        Cell cell = _cells[position.x, position.y];
        Vector2Int paths = GetPath(position, firstDimensions);
        Vector2Int secondDimension;
        int firstBaseID = cell.lowestPathfindingNodeID;
        int secondBaseID;

        //Debug.Log("pos " + position.x + ", " + position.y);
        //Debug.Log(cell.ToString2());
        // Top
        if (cell.IsDoor(Side.Top))
        {
            if (_cells[position.x + 1, position.y].generated)
            {
                secondBaseID = _cells[position.x + 1, position.y].lowestPathfindingNodeID;
                ConnectTwoNodes(_pathfindingNodes[firstBaseID + (firstDimensions.y * (firstDimensions.x - 1)) + paths.y], _pathfindingNodes[secondBaseID + paths.y], Side.Top);
            }
        }
        // Right
        if (cell.IsDoor(Side.Right))
        {
            if (_cells[position.x, position.y + 1].generated)
            {
                secondBaseID = _cells[position.x, position.y + 1].lowestPathfindingNodeID;
                secondDimension = GetDimensions(position.x, position.y + 1);
                ConnectTwoNodes(_pathfindingNodes[firstBaseID + (firstDimensions.y * paths.x) + firstDimensions.y - 1], _pathfindingNodes[secondBaseID + (secondDimension.y * paths.x)], Side.Right);
            }
        }
        // Bottom
        if (cell.IsDoor(Side.Bottom))
        {
            if (_cells[position.x - 1, position.y].generated)
            {
                secondBaseID = _cells[position.x - 1, position.y].lowestPathfindingNodeID;
                secondDimension = GetDimensions(position.x - 1, position.y);
                ConnectTwoNodes(_pathfindingNodes[firstBaseID + paths.y], _pathfindingNodes[secondBaseID + (secondDimension.y * (secondDimension.x - 1)) + paths.y], Side.Bottom);
            }
        }
        // Left
        if (cell.IsDoor(Side.Left))
        {
            if (_cells[position.x, position.y - 1].generated)
            {
                secondBaseID = _cells[position.x, position.y - 1].lowestPathfindingNodeID;
                secondDimension = GetDimensions(position.x, position.y - 1);
                ConnectTwoNodes(_pathfindingNodes[firstBaseID + (firstDimensions.y * paths.x)], _pathfindingNodes[secondBaseID + (secondDimension.y * paths.x) + secondDimension.y - 1], Side.Left);
            }
        }
    }

    #endregion

    #region Tile Spawning

    private void SpawnTiles()
    {
        Vector2 dimensions;

        for (int i = 0; i < _mazeSettings.length; i++)
        {
            for (int j = 0; j < _mazeSettings.width; j++)
            {
                if (_cells[i, j] != null)
                {

                    dimensions = GetDimensions(i, j);
                    for (int k = 0; k < dimensions.x * dimensions.y; k++)
                    {
                        SpawnTilePrefab(_pathfindingNodes[_cells[i, j].lowestPathfindingNodeID + k]);
                    }
                }
            }
        }
    }

    // TODO add int with tileset type
    private void SpawnTilePrefab(PathfindingNode node)
    {
        if (node != null)
        {
            int firstDoor = node.GetFirstDoor();

            switch (node.GetDoorCount())
            {
                case 1:
                    Instantiate(_tiles[node.TileType].tiles[0], new Vector3(node.position.x, transform.position.y, node.position.y), Quaternion.Euler(new Vector3(0, firstDoor * 90, 0)), transform);
                    break;
                case 2:
                    if (node.neighbours[((firstDoor + 2) * 2) % 8] == null)
                    {
                        Instantiate(_tiles[node.TileType].tiles[1], new Vector3(node.position.x, transform.position.y, node.position.y), Quaternion.Euler(new Vector3(0, firstDoor * 90, 0)), transform);
                    }
                    else
                    {
                        Instantiate(_tiles[node.TileType].tiles[2], new Vector3(node.position.x, transform.position.y, node.position.y), Quaternion.Euler(new Vector3(0, firstDoor * 90, 0)), transform);
                    }
                    break;
                case 3:
                    Instantiate(_tiles[node.TileType].tiles[3], new Vector3(node.position.x, transform.position.y, node.position.y), Quaternion.Euler(new Vector3(0, (firstDoor - 1) * 90, 0)), transform);
                    break;
                case 4:
                    Instantiate(_tiles[node.TileType].tiles[4], new Vector3(node.position.x, transform.position.y, node.position.y), Quaternion.identity, transform);
                    break;
                default:
                    throw new System.Exception("Cell can't have more than four doors");
            }
        }
    }

    #endregion
}